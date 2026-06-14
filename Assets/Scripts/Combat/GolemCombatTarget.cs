using System;
using System.Collections;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public readonly struct BossElementStatusSnapshot
    {
        public readonly float currentHealth;
        public readonly float maxHealth;
        public readonly bool isBarrierActive;
        public readonly bool isWeakExposed;
        public readonly bool isChargeCounterWindowOpen;
        public readonly bool isSlowed;
        public readonly bool isBurning;
        public readonly bool isStaggered;
        public readonly float barrierRemaining;
        public readonly float weakRemaining;
        public readonly float chargeCounterRemaining;
        public readonly string combatCue;

        public BossElementStatusSnapshot(
            float currentHealth,
            float maxHealth,
            bool isBarrierActive,
            bool isWeakExposed,
            bool isChargeCounterWindowOpen,
            bool isSlowed,
            bool isBurning,
            bool isStaggered,
            float barrierRemaining,
            float weakRemaining,
            float chargeCounterRemaining,
            string combatCue)
        {
            this.currentHealth = currentHealth;
            this.maxHealth = maxHealth;
            this.isBarrierActive = isBarrierActive;
            this.isWeakExposed = isWeakExposed;
            this.isChargeCounterWindowOpen = isChargeCounterWindowOpen;
            this.isSlowed = isSlowed;
            this.isBurning = isBurning;
            this.isStaggered = isStaggered;
            this.barrierRemaining = barrierRemaining;
            this.weakRemaining = weakRemaining;
            this.chargeCounterRemaining = chargeCounterRemaining;
            this.combatCue = combatCue;
        }
    }

    public class GolemCombatTarget : MonoBehaviour, ISpellTarget
    {
        [SerializeField] private float maxHealth = 300f;
        [SerializeField] private float currentHealth = 300f;
        [SerializeField, Range(0f, 1f)] private float barrierDamageMultiplier = 0.2f;
        [SerializeField] private float weakDamageMultiplier = 1.5f;
        [SerializeField] private float weakDuration = 4f;
        [SerializeField] private float staggerDuration = 2.5f;
        [SerializeField] private float slowDuration = 3f;
        [SerializeField] private float burnDuration = 4f;
        [SerializeField] private float defaultChargeCounterDuration = 3f;
        [SerializeField] private float defaultBarrierDuration = 8f;
        [SerializeField] private float steamBurstPushDistance = 1.8f;
        [SerializeField] private float steamBurstPushDuration = 0.35f;
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool enableComboDebugLogs = true;

        public event Action<float, float> OnHealthChanged;
        public event Action<string> OnCombatCueChanged;
        public event Action<BossElementStatusSnapshot> OnElementStatusChanged;
        public event Action<SpellHitData, float, float> OnSpellDamageApplied;
        public event Action OnBarrierStarted;
        public event Action OnBarrierBroken;
        public event Action OnWeaknessExposed;
        public event Action OnChargeCounterSucceeded;
        public event Action OnDefeated;

        private Coroutine weakRoutine;
        private Coroutine staggerRoutine;
        private Coroutine slowRoutine;
        private Coroutine burnRoutine;
        private Coroutine pushRoutine;
        private Coroutine barrierRoutine;
        private Coroutine chargeRoutine;
        private float cueHoldUntilTime;
        private float barrierEndTime;
        private float weakEndTime;
        private float chargeEndTime;
        private bool defeated;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsBarrierActive { get; private set; }
        public bool IsWeakExposed { get; private set; }
        public bool IsSlowed { get; private set; }
        public bool IsBurning { get; private set; }
        public bool IsStaggered { get; private set; }
        public bool IsChargeCounterWindowOpen { get; private set; }
        public string CurrentCombatCue { get; private set; } = "IDLE";
        public bool CanAct => currentHealth > 0f && !IsStaggered;
        public float WeakRemaining => IsWeakExposed ? Mathf.Max(0f, weakEndTime - Time.time) : 0f;
        public float MovementSpeedMultiplier => IsStaggered ? 0f : IsSlowed ? 0.45f : 1f;
        public float ActionSpeedMultiplier => IsStaggered ? 0f : IsSlowed ? 0.65f : 1f;

        public BossElementStatusSnapshot GetStatusSnapshot()
        {
            return new BossElementStatusSnapshot(
                currentHealth,
                maxHealth,
                IsBarrierActive,
                IsWeakExposed,
                IsChargeCounterWindowOpen,
                IsSlowed,
                IsBurning,
                IsStaggered,
                IsBarrierActive ? Mathf.Max(0f, barrierEndTime - Time.time) : 0f,
                IsWeakExposed ? Mathf.Max(0f, weakEndTime - Time.time) : 0f,
                IsChargeCounterWindowOpen ? Mathf.Max(0f, chargeEndTime - Time.time) : 0f,
                CurrentCombatCue);
        }

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
            NotifyHealthChanged();
        }

        public void OnHit(SpellHitData hitData)
        {
            if (hitData == null)
                return;

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[{nameof(GolemCombatTarget)}] Hit spellId:{hitData.spellId} element:{hitData.element} " +
                    $"statusEffect:{hitData.statusEffect} damage:{hitData.damage} statusDuration:{hitData.statusDuration}",
                    this);
            }

            var resolvedChargeCounter = IsChargeCounterWindowOpen && hitData.IncludesElement(ElementType.Thunder);
            var brokeBarrier = IsBarrierActive &&
                               hitData.spellId == SpellId.Combo_IceThunder;
            var triggeredOverload = hitData.spellId == SpellId.Combo_ThunderFire && IsWeakExposed;
            var triggeredSteamBurst = hitData.spellId == SpellId.Combo_FireIce;

            if (resolvedChargeCounter)
                ResolveChargeCounterSuccess();

            if (brokeBarrier)
            {
                LogCombo($"Barrier break triggered by {hitData.spellId}.");
                BreakBarrier("BARRIER BREAK");
            }

            if (triggeredSteamBurst)
            {
                LogCombo($"Steam burst push triggered. distance={steamBurstPushDistance:0.00}, duration={steamBurstPushDuration:0.00}.");
                StartSteamBurstPush();
            }

            ApplyStatus(hitData);
            if (triggeredSteamBurst)
                SetCue("STEAM BURST", 1.1f, true);
            if (triggeredOverload)
            {
                LogCombo($"Overload triggered while weak. spell={hitData.spellId}.");
                SetCue("OVERLOAD", 1.1f, true);
            }

            var rawDamage = Mathf.Max(0f, hitData.damage);
            var finalDamage = CalculateDamage(hitData);
            ApplyDamage(
                finalDamage,
                hitData.element,
                resolvedChargeCounter || brokeBarrier || triggeredOverload);
            OnSpellDamageApplied?.Invoke(hitData, rawDamage, finalDamage);
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
            OnBarrierBroken?.Invoke();
            NotifyStatusChanged();
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
            {
                SetCue("DEAD", 0f, true);
                if (!defeated)
                {
                    defeated = true;
                    OnDefeated?.Invoke();
                }
            }
            else if (sourceElement != ElementType.None && !suppressHitCue)
            {
                defeated = false;
                SetCue($"HIT {sourceElement}");
            }
        }

        private float CalculateDamage(SpellHitData hitData)
        {
            var damage = Mathf.Max(0f, hitData.damage);

            if (IsBarrierActive)
                damage *= barrierDamageMultiplier;

            if (IsWeakExposed)
                damage *= weakDamageMultiplier;

            if (hitData.spellId == SpellId.Combo_ThunderFire && IsWeakExposed)
                damage *= 2.0f;

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
            OnChargeCounterSucceeded?.Invoke();
            NotifyStatusChanged();
        }

        private void ExposeWeakness(float duration)
        {
            if (weakRoutine != null)
                StopCoroutine(weakRoutine);

            weakRoutine = StartCoroutine(TimedWeakness(Mathf.Max(0.1f, duration)));
            OnWeaknessExposed?.Invoke();
        }

        private void StartStagger(float duration)
        {
            if (staggerRoutine != null)
                StopCoroutine(staggerRoutine);

            staggerRoutine = StartCoroutine(TimedStagger(Mathf.Max(0.1f, duration)));
        }

        private void StartSteamBurstPush()
        {
            if (steamBurstPushDistance <= 0f)
                return;

            if (pushRoutine != null)
                StopCoroutine(pushRoutine);

            pushRoutine = StartCoroutine(SteamBurstPushRoutine());
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
            barrierEndTime = Time.time + duration;
            SetCue("BARRIER", 0f, true);
            OnBarrierStarted?.Invoke();
            NotifyStatusChanged();
            yield return new WaitForSeconds(duration);
            IsBarrierActive = false;
            barrierRoutine = null;
            SetCue("IDLE");
            NotifyStatusChanged();
        }

        private IEnumerator TimedChargeCounterWindow(float duration)
        {
            IsChargeCounterWindowOpen = true;
            chargeEndTime = Time.time + duration;
            SetCue("CHARGE", 0f, true);
            NotifyStatusChanged();
            yield return new WaitForSeconds(duration);
            IsChargeCounterWindowOpen = false;
            chargeRoutine = null;
            if (!IsStaggered)
                SetCue("CHARGE MISSED", 0.8f, true);
            NotifyStatusChanged();
        }

        private IEnumerator TimedWeakness(float duration)
        {
            IsWeakExposed = true;
            weakEndTime = Time.time + duration;
            SetCue("WEAK");
            NotifyStatusChanged();
            yield return new WaitForSeconds(duration);
            IsWeakExposed = false;
            weakRoutine = null;
            if (!IsChargeCounterWindowOpen && !IsBarrierActive && !IsStaggered)
                SetCue("IDLE");
            NotifyStatusChanged();
        }

        private IEnumerator TimedStagger(float duration)
        {
            IsStaggered = true;
            SetCue("STAGGER");
            NotifyStatusChanged();
            yield return new WaitForSeconds(duration);
            IsStaggered = false;
            staggerRoutine = null;
            if (!IsWeakExposed && !IsBarrierActive && !IsChargeCounterWindowOpen)
                SetCue("IDLE");
            NotifyStatusChanged();
        }

        private IEnumerator SteamBurstPushRoutine()
        {
            var duration = Mathf.Max(0.05f, steamBurstPushDuration);
            var start = transform.position;
            var pushDirection = -transform.forward;
            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude < 0.001f)
                pushDirection = -Vector3.forward;

            var end = start + pushDirection.normalized * steamBurstPushDistance;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            transform.position = end;
            pushRoutine = null;
        }

        private IEnumerator TimedSlow(float duration)
        {
            IsSlowed = true;
            SetCue("SLOW");
            NotifyStatusChanged();
            yield return new WaitForSeconds(duration);
            IsSlowed = false;
            slowRoutine = null;
            NotifyStatusChanged();
        }

        private IEnumerator TimedBurn(SpellHitData hitData)
        {
            IsBurning = true;
            SetCue("BURN");
            NotifyStatusChanged();
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
            NotifyStatusChanged();
        }

        private void NotifyStatusChanged()
        {
            OnElementStatusChanged?.Invoke(GetStatusSnapshot());
        }

        private void LogCombo(string message)
        {
            if (enableComboDebugLogs)
                Debug.Log($"[ComboMagicTest] {message}", this);
        }
    }
}
