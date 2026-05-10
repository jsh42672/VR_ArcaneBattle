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
        [SerializeField] private float burnDuration = 4f;
        [SerializeField] private float defaultChargeCounterDuration = 3f;
        [SerializeField] private float defaultBarrierDuration = 8f;

        public event Action<float, float> OnHealthChanged;
        public event Action<string> OnCombatCueChanged;

        private Coroutine weakRoutine;
        private Coroutine staggerRoutine;
        private Coroutine slowRoutine;
        private Coroutine burnRoutine;
        private Coroutine barrierRoutine;
        private Coroutine chargeRoutine;
        private float cueHoldUntilTime;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsBarrierActive { get; private set; }
        public bool IsWeakExposed { get; private set; }
        public bool IsSlowed { get; private set; }
        public bool IsBurning { get; private set; }
        public bool IsStaggered { get; private set; }
        public bool IsChargeCounterWindowOpen { get; private set; }
        public string CurrentCombatCue { get; private set; } = "IDLE";

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
            NotifyHealthChanged();
        }

        public void OnHit(SpellHitData hitData)
        {
            if (hitData == null)
                return;

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

            ApplyDamage(
                CalculateDamage(hitData),
                hitData.element,
                resolvedChargeCounter || brokeBarrier || triggeredOverload);
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
            ApplyDamage(damage, sourceElement, false);
        }

        private void ApplyDamage(float damage, ElementType sourceElement, bool suppressHitCue)
        {
            if (damage <= 0f || currentHealth <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            NotifyHealthChanged();
            if (currentHealth <= 0f)
                SetCue("DEAD", 0f, true);
            else if (sourceElement != ElementType.None && !suppressHitCue)
                SetCue($"HIT {sourceElement}");
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
                    StartSlow(Mathf.Max(slowDuration, hitData.statusDuration));
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

        private void StartSlow(float duration)
        {
            if (slowRoutine != null)
                StopCoroutine(slowRoutine);

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
            IsBarrierActive = true;
            SetCue("BARRIER", 0f, true);
            yield return new WaitForSeconds(duration);
            IsBarrierActive = false;
            barrierRoutine = null;
            SetCue("IDLE");
        }

        private IEnumerator TimedChargeCounterWindow(float duration)
        {
            IsChargeCounterWindowOpen = true;
            SetCue("CHARGE", 0f, true);
            yield return new WaitForSeconds(duration);
            IsChargeCounterWindowOpen = false;
            chargeRoutine = null;
            if (!IsStaggered)
                SetCue("CHARGE MISSED", 0.8f, true);
        }

        private IEnumerator TimedWeakness(float duration)
        {
            IsWeakExposed = true;
            SetCue("WEAK");
            yield return new WaitForSeconds(duration);
            IsWeakExposed = false;
            weakRoutine = null;
            if (!IsChargeCounterWindowOpen && !IsBarrierActive && !IsStaggered)
                SetCue("IDLE");
        }

        private IEnumerator TimedStagger(float duration)
        {
            IsStaggered = true;
            SetCue("STAGGER");
            yield return new WaitForSeconds(duration);
            IsStaggered = false;
            staggerRoutine = null;
            if (!IsWeakExposed && !IsBarrierActive && !IsChargeCounterWindowOpen)
                SetCue("IDLE");
        }

        private IEnumerator TimedSlow(float duration)
        {
            IsSlowed = true;
            SetCue("SLOW");
            yield return new WaitForSeconds(duration);
            IsSlowed = false;
            slowRoutine = null;
        }

        private IEnumerator TimedBurn(SpellHitData hitData)
        {
            IsBurning = true;
            SetCue("BURN");
            var duration = Mathf.Max(burnDuration, hitData.statusDuration);
            var interval = Mathf.Max(0.2f, hitData.statusTickInterval);
            var tickDamage = Mathf.Max(0f, hitData.statusMagnitude);
            var endTime = Time.time + duration;

            while (Time.time < endTime)
            {
                yield return new WaitForSeconds(interval);
                ApplyDamage(tickDamage, ElementType.Fire);
            }

            IsBurning = false;
            burnRoutine = null;
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
                return;
            }

            CurrentCombatCue = cue;
            if (minimumVisibleDuration > 0f)
                cueHoldUntilTime = Time.time + minimumVisibleDuration;
            OnCombatCueChanged?.Invoke(CurrentCombatCue);
        }

        private void NotifyHealthChanged()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
