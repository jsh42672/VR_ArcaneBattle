using System;
using System.Collections;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class GolemCombatTarget : MonoBehaviour, ISpellTarget
    {
        [SerializeField] private float maxHealth = 300f;
        [SerializeField] private float currentHealth = 300f;
        [SerializeField, Range(0f, 1f)] private float barrierDamageMultiplier = 0.2f;
        [SerializeField] private float weakDamageMultiplier = 1.5f;
        [SerializeField] private float weakDuration = 5f;
        [SerializeField] private float staggerDuration = 2.5f;
        [SerializeField] private float slowDuration = 3f;
        [SerializeField, Range(0.1f, 1f)] private float defaultSlowMultiplier = 0.45f;
        [SerializeField] private float burnDuration = 4f;
        [SerializeField] private float defaultChargeCounterDuration = 3f;
        [SerializeField] private float defaultBarrierDuration = 8f;

        public event Action<float, float> OnHealthChanged;
        public event Action<string> OnCombatCueChanged;
        public event Action<SpellHitData> OnSpellHitReceived;
        public event Action<SpellHitData, float, float> OnDamageApplied;
        public event Action<BossElementStatusSnapshot> OnElementStatusChanged;
        public event Action<bool, float> OnBarrierChanged;
        public event Action<bool, float> OnWeaknessChanged;
        public event Action<bool, float> OnSlowChanged;
        public event Action<bool, float> OnBurnChanged;
        public event Action<bool, float> OnStaggerChanged;
        public event Action<bool, float> OnChargeCounterWindowChanged;

        private Coroutine weakRoutine;
        private Coroutine staggerRoutine;
        private Coroutine slowRoutine;
        private Coroutine burnRoutine;
        private Coroutine barrierRoutine;
        private Coroutine chargeRoutine;
        private float cueHoldUntilTime;
        private float barrierUntilTime;
        private float weakUntilTime;
        private float slowUntilTime;
        private float burnUntilTime;
        private float staggerUntilTime;
        private float chargeUntilTime;
        private float currentSlowMultiplier = 1f;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsBarrierActive { get; private set; }
        public bool IsWeakExposed { get; private set; }
        public bool IsSlowed { get; private set; }
        public bool IsBurning { get; private set; }
        public bool IsStaggered { get; private set; }
        public bool IsChargeCounterWindowOpen { get; private set; }
        public string CurrentCombatCue { get; private set; } = "IDLE";
        public float ReceivedDamageMultiplier => (IsBarrierActive ? barrierDamageMultiplier : 1f) *
                                                 (IsWeakExposed ? weakDamageMultiplier : 1f);
        public float MovementSpeedMultiplier => IsStaggered ? 0f : IsSlowed ? currentSlowMultiplier : 1f;
        public float ActionSpeedMultiplier => IsStaggered ? 0f : IsSlowed ? Mathf.Lerp(1f, currentSlowMultiplier, 0.65f) : 1f;
        public bool CanAct => currentHealth > 0f && !IsStaggered;
        public float BarrierRemaining => RemainingTime(barrierUntilTime);
        public float WeakRemaining => RemainingTime(weakUntilTime);
        public float SlowRemaining => RemainingTime(slowUntilTime);
        public float BurnRemaining => RemainingTime(burnUntilTime);
        public float StaggerRemaining => RemainingTime(staggerUntilTime);
        public float ChargeCounterRemaining => RemainingTime(chargeUntilTime);

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
            NotifyHealthChanged();
            NotifyStatusChanged();
        }

        public void OnHit(SpellHitData hitData)
        {
            if (hitData == null)
                return;

            OnSpellHitReceived?.Invoke(hitData.Clone());
            var resolvedChargeCounter = IsChargeCounterWindowOpen && hitData.IncludesElement(ElementType.Thunder);
            var brokeBarrier = IsBarrierActive &&
                               hitData.IncludesElement(ElementType.Ice) &&
                               hitData.IncludesElement(ElementType.Thunder);
            var triggeredOverload = hitData.spellId == SpellId.Combo_ThunderFire && IsWeakExposed;

            if (resolvedChargeCounter)
                ResolveChargeCounterSuccess();

            if (brokeBarrier)
                BreakBarrier("BARRIER BREAK");

            ApplyStatus(hitData);
            if (triggeredOverload)
                SetCue("OVERLOAD", 1.1f, true);

            var rawDamage = Mathf.Max(0f, hitData.damage);
            var finalDamage = CalculateDamage(hitData);
            ApplyDamage(
                finalDamage,
                hitData.element,
                resolvedChargeCounter || brokeBarrier || triggeredOverload,
                hitData,
                rawDamage);
        }

        public BossElementStatusSnapshot GetStatusSnapshot()
        {
            return new BossElementStatusSnapshot
            {
                currentHealth = currentHealth,
                maxHealth = maxHealth,
                isBarrierActive = IsBarrierActive,
                isWeakExposed = IsWeakExposed,
                isSlowed = IsSlowed,
                isBurning = IsBurning,
                isStaggered = IsStaggered,
                isChargeCounterWindowOpen = IsChargeCounterWindowOpen,
                receivedDamageMultiplier = ReceivedDamageMultiplier,
                movementSpeedMultiplier = MovementSpeedMultiplier,
                actionSpeedMultiplier = ActionSpeedMultiplier,
                canAct = CanAct,
                barrierRemaining = BarrierRemaining,
                weakRemaining = WeakRemaining,
                slowRemaining = SlowRemaining,
                burnRemaining = BurnRemaining,
                staggerRemaining = StaggerRemaining,
                chargeCounterRemaining = ChargeCounterRemaining,
                combatCue = CurrentCombatCue
            };
        }

        public void BeginBarrier()
        {
            BeginBarrier(defaultBarrierDuration);
        }

        public void BeginBarrier(float duration)
        {
            if (barrierRoutine != null)
                StopCoroutine(barrierRoutine);

            barrierRoutine = StartCoroutine(TimedBarrier(Mathf.Max(0.1f, duration)));
        }

        public void BreakBarrier(string cue = "BARRIER BREAK")
        {
            if (barrierRoutine != null)
            {
                StopCoroutine(barrierRoutine);
                barrierRoutine = null;
            }

            IsBarrierActive = false;
            barrierUntilTime = 0f;
            OnBarrierChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
            ExposeWeakness(weakDuration);
            SetCue(cue, 1.1f, true);
        }

        public void BeginChargeCounterWindow()
        {
            BeginChargeCounterWindow(defaultChargeCounterDuration);
        }

        public void BeginChargeCounterWindow(float duration)
        {
            if (chargeRoutine != null)
                StopCoroutine(chargeRoutine);

            chargeRoutine = StartCoroutine(TimedChargeCounterWindow(Mathf.Max(0.1f, duration)));
        }

        public void ApplyDamage(float damage, ElementType sourceElement = ElementType.None)
        {
            ApplyDamage(damage, sourceElement, false, null, damage);
        }

        private void ApplyDamage(
            float damage,
            ElementType sourceElement,
            bool suppressHitCue,
            SpellHitData hitData,
            float rawDamage)
        {
            if (damage <= 0f || currentHealth <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            NotifyHealthChanged();
            OnDamageApplied?.Invoke(hitData?.Clone(), Mathf.Max(0f, rawDamage), damage);
            if (currentHealth <= 0f)
                SetCue("DEAD", 0f, true);
            else if (sourceElement != ElementType.None && !suppressHitCue)
                SetCue($"HIT {sourceElement}");
            else
                NotifyStatusChanged();
        }

        private float CalculateDamage(SpellHitData hitData)
        {
            var damage = Mathf.Max(0f, hitData.damage);

            if (IsBarrierActive)
                damage *= barrierDamageMultiplier;

            if (IsWeakExposed)
                damage *= weakDamageMultiplier;

            if (hitData.spellId == SpellId.Combo_ThunderFire && IsWeakExposed)
                damage *= 1.35f;

            return damage;
        }

        private void ApplyStatus(SpellHitData hitData)
        {
            switch (hitData.statusEffect)
            {
                case StatusEffect.Burn:
                    StartBurn(hitData);
                    break;
                case StatusEffect.Slow:
                    StartSlow(hitData);
                    break;
                case StatusEffect.Stagger:
                    StartStagger(Mathf.Max(staggerDuration, hitData.statusDuration));
                    break;
            }

            if (hitData.spellId == SpellId.Combo_ThunderFire && IsWeakExposed)
                StartStagger(staggerDuration);
        }

        private void ResolveChargeCounterSuccess()
        {
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }

            IsChargeCounterWindowOpen = false;
            chargeUntilTime = 0f;
            OnChargeCounterWindowChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
            StartStagger(staggerDuration);
            ExposeWeakness(weakDuration);
            SetCue("STAGGER", 1.15f, true);
        }

        private void ExposeWeakness(float duration)
        {
            if (weakRoutine != null)
                StopCoroutine(weakRoutine);

            weakRoutine = StartCoroutine(TimedWeakness(Mathf.Max(0.1f, duration)));
        }

        private void StartStagger(float duration)
        {
            if (staggerRoutine != null)
                StopCoroutine(staggerRoutine);

            staggerRoutine = StartCoroutine(TimedStagger(Mathf.Max(0.1f, duration)));
        }

        private void StartSlow(SpellHitData hitData)
        {
            if (slowRoutine != null)
                StopCoroutine(slowRoutine);

            var duration = Mathf.Max(slowDuration, hitData.statusDuration);
            currentSlowMultiplier = Mathf.Clamp(
                hitData.statusMagnitude > 0f ? hitData.statusMagnitude : defaultSlowMultiplier,
                0.1f,
                1f);
            slowRoutine = StartCoroutine(TimedSlow(Mathf.Max(0.1f, duration)));
        }

        private void StartBurn(SpellHitData hitData)
        {
            if (burnRoutine != null)
                StopCoroutine(burnRoutine);

            burnRoutine = StartCoroutine(TimedBurn(hitData.Clone()));
        }

        private IEnumerator TimedBarrier(float duration)
        {
            barrierUntilTime = Time.time + duration;
            IsBarrierActive = true;
            OnBarrierChanged?.Invoke(true, duration);
            NotifyStatusChanged();
            SetCue("BARRIER", 0f, true);
            yield return new WaitForSeconds(duration);
            IsBarrierActive = false;
            barrierUntilTime = 0f;
            barrierRoutine = null;
            OnBarrierChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
            SetCue("IDLE");
        }

        private IEnumerator TimedChargeCounterWindow(float duration)
        {
            chargeUntilTime = Time.time + duration;
            IsChargeCounterWindowOpen = true;
            OnChargeCounterWindowChanged?.Invoke(true, duration);
            NotifyStatusChanged();
            SetCue("CHARGE", 0f, true);
            yield return new WaitForSeconds(duration);
            IsChargeCounterWindowOpen = false;
            chargeUntilTime = 0f;
            chargeRoutine = null;
            OnChargeCounterWindowChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
            if (!IsStaggered)
                SetCue("CHARGE MISSED", 0.8f, true);
        }

        private IEnumerator TimedWeakness(float duration)
        {
            weakUntilTime = Time.time + duration;
            IsWeakExposed = true;
            OnWeaknessChanged?.Invoke(true, duration);
            NotifyStatusChanged();
            SetCue("WEAK");
            yield return new WaitForSeconds(duration);
            IsWeakExposed = false;
            weakUntilTime = 0f;
            weakRoutine = null;
            OnWeaknessChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
            if (!IsChargeCounterWindowOpen && !IsBarrierActive && !IsStaggered)
                SetCue("IDLE");
        }

        private IEnumerator TimedStagger(float duration)
        {
            staggerUntilTime = Time.time + duration;
            IsStaggered = true;
            OnStaggerChanged?.Invoke(true, duration);
            NotifyStatusChanged();
            SetCue("STAGGER");
            yield return new WaitForSeconds(duration);
            IsStaggered = false;
            staggerUntilTime = 0f;
            staggerRoutine = null;
            OnStaggerChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
            if (!IsWeakExposed && !IsBarrierActive && !IsChargeCounterWindowOpen)
                SetCue("IDLE");
        }

        private IEnumerator TimedSlow(float duration)
        {
            slowUntilTime = Time.time + duration;
            IsSlowed = true;
            OnSlowChanged?.Invoke(true, duration);
            NotifyStatusChanged();
            SetCue("SLOW");
            yield return new WaitForSeconds(duration);
            IsSlowed = false;
            slowUntilTime = 0f;
            currentSlowMultiplier = 1f;
            slowRoutine = null;
            OnSlowChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
        }

        private IEnumerator TimedBurn(SpellHitData hitData)
        {
            var duration = Mathf.Max(burnDuration, hitData.statusDuration);
            var interval = Mathf.Max(0.2f, hitData.statusTickInterval);
            var tickDamage = Mathf.Max(0f, hitData.statusMagnitude);
            var endTime = Time.time + duration;
            burnUntilTime = endTime;
            IsBurning = true;
            OnBurnChanged?.Invoke(true, duration);
            NotifyStatusChanged();
            SetCue("BURN");

            while (Time.time < endTime)
            {
                yield return new WaitForSeconds(interval);
                ApplyDamage(tickDamage, ElementType.Fire);
            }

            IsBurning = false;
            burnUntilTime = 0f;
            burnRoutine = null;
            OnBurnChanged?.Invoke(false, 0f);
            NotifyStatusChanged();
        }

        private void SetCue(string cue, float minimumVisibleDuration = 0f, bool force = false)
        {
            if (currentHealth <= 0f && cue != "DEAD")
                return;

            if (!force && Time.time < cueHoldUntilTime && CurrentCombatCue != cue)
                return;

            if (CurrentCombatCue == cue)
            {
                if (minimumVisibleDuration > 0f)
                    cueHoldUntilTime = Mathf.Max(cueHoldUntilTime, Time.time + minimumVisibleDuration);
                NotifyStatusChanged();
                return;
            }

            CurrentCombatCue = cue;
            if (minimumVisibleDuration > 0f)
                cueHoldUntilTime = Time.time + minimumVisibleDuration;
            OnCombatCueChanged?.Invoke(CurrentCombatCue);
            NotifyStatusChanged();
        }

        private void NotifyHealthChanged()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void NotifyStatusChanged()
        {
            OnElementStatusChanged?.Invoke(GetStatusSnapshot());
        }

        private static float RemainingTime(float untilTime)
        {
            return untilTime <= 0f ? 0f : Mathf.Max(0f, untilTime - Time.time);
        }
    }
}
