using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Plays boss animation hooks when a high/middle/low attack window starts.
    /// Works with future animator controllers via triggers, and falls back to known attack state names.
    /// </summary>
    public class BossAttackAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private Animator bossAnimator;
        [SerializeField] private string highAttackTrigger = "AttackHigh";
        [SerializeField] private string middleAttackTrigger = "AttackMiddle";
        [SerializeField] private string lowAttackTrigger = "AttackLow";
        [SerializeField] private string highAttackState = "AttackHigh";
        [SerializeField] private string middleAttackState = "AttackMiddle";
        [SerializeField] private string lowAttackState = "AttackLow";
        [SerializeField] private string fallbackAttackState = "Armature|Armature|Armature|Armature|Triple_Combo_Attack|baselayer";
        [SerializeField] private string resourcesFallbackControllerPath = "ArcaneVR/ThunderGolemAttackController";
        [SerializeField] private float fallbackControllerRestoreDelay = 1.2f;
        [SerializeField] private float crossFadeDuration = 0.05f;
        [SerializeField] private bool showDebugLog;

        private BossPatternCombatBridge subscribedBridge;
        private RuntimeAnimatorController fallbackAttackController;
        private RuntimeAnimatorController controllerBeforeFallback;
        private bool hasControllerBeforeFallback;
        private float restoreFallbackControllerAtTime;

        public string LastAnimatorStatus { get; private set; } = "BossAnim: idle";

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();
            RestoreFallbackControllerIfNeeded();
        }

        private void ResolveReferences()
        {
            if (patternBridge == null)
                patternBridge = FindAnyObjectByType<BossPatternCombatBridge>();

            if (bossAnimator == null)
                bossAnimator = ResolveBossAnimator();

            if (fallbackAttackController == null && !string.IsNullOrEmpty(resourcesFallbackControllerPath))
                fallbackAttackController = Resources.Load<RuntimeAnimatorController>(resourcesFallbackControllerPath);
        }

        private Animator ResolveBossAnimator()
        {
            var golemTarget = FindAnyObjectByType<GolemCombatTarget>();
            if (golemTarget != null)
            {
                var targetAnimator = golemTarget.GetComponentInChildren<Animator>();
                if (targetAnimator != null)
                    return targetAnimator;
            }

            foreach (var animator in FindObjectsByType<Animator>(FindObjectsInactive.Exclude))
            {
                if (animator == null)
                    continue;

                var name = animator.gameObject.name.ToLowerInvariant();
                if (name.Contains("golem") || name.Contains("boss") || name.Contains("attack"))
                    return animator;
            }

            return null;
        }

        private void Subscribe()
        {
            if (patternBridge == null || subscribedBridge == patternBridge)
                return;

            Unsubscribe();
            subscribedBridge = patternBridge;
            subscribedBridge.OnAttackResponseWindowStarted += HandleAttackResponseWindowStarted;
        }

        private void Unsubscribe()
        {
            if (subscribedBridge == null)
                return;

            subscribedBridge.OnAttackResponseWindowStarted -= HandleAttackResponseWindowStarted;
            subscribedBridge = null;
        }

        private void HandleAttackResponseWindowStarted(BossAttackType attackType, float duration)
        {
            PlayAttackAnimation(attackType);
        }

        private void PlayAttackAnimation(BossAttackType attackType)
        {
            ResolveReferences();

            if (bossAnimator == null || bossAnimator.runtimeAnimatorController == null)
            {
                if (!TryPlayFallbackController(attackType))
                {
                    LastAnimatorStatus = bossAnimator == null ? "BossAnim: no animator" : "BossAnim: no controller";
                    return;
                }

                return;
            }

            bossAnimator.speed = 1f;
            var triggerName = ResolveTriggerName(attackType);
            if (HasTriggerParameter(triggerName))
            {
                ResetTriggerIfExists(highAttackTrigger);
                ResetTriggerIfExists(middleAttackTrigger);
                ResetTriggerIfExists(lowAttackTrigger);
                bossAnimator.SetTrigger(triggerName);
                LastAnimatorStatus = $"BossAnim: trigger {triggerName}";
                return;
            }

            if (TryCrossFade(ResolveStateName(attackType), attackType) ||
                TryCrossFade(fallbackAttackState, attackType) ||
                TryCrossFade("Attack", attackType))
            {
                return;
            }

            if (TryPlayFallbackController(attackType))
                return;

            LastAnimatorStatus = $"BossAnim: no state for {attackType}";
            if (showDebugLog)
                Debug.Log($"[BossAttackAnimatorBridge] No animation state or trigger found for {attackType}.");
        }

        private bool TryPlayFallbackController(BossAttackType attackType)
        {
            ResolveReferences();

            if (bossAnimator == null || fallbackAttackController == null)
                return false;

            bossAnimator.speed = 1f;
            if (bossAnimator.runtimeAnimatorController != fallbackAttackController)
            {
                controllerBeforeFallback = bossAnimator.runtimeAnimatorController;
                hasControllerBeforeFallback = true;
                bossAnimator.runtimeAnimatorController = fallbackAttackController;
            }

            restoreFallbackControllerAtTime = Time.time + Mathf.Max(0.1f, fallbackControllerRestoreDelay);

            if (!TryCrossFade(fallbackAttackState, attackType))
                bossAnimator.Play(fallbackAttackState, 0, 0f);

            LastAnimatorStatus = $"BossAnim: fallback controller ({attackType})";
            return true;
        }

        private void RestoreFallbackControllerIfNeeded()
        {
            if (restoreFallbackControllerAtTime <= 0f || Time.time < restoreFallbackControllerAtTime)
                return;

            restoreFallbackControllerAtTime = 0f;

            if (bossAnimator == null ||
                bossAnimator.runtimeAnimatorController != fallbackAttackController ||
                !hasControllerBeforeFallback)
            {
                return;
            }

            bossAnimator.runtimeAnimatorController = controllerBeforeFallback;
            controllerBeforeFallback = null;
            hasControllerBeforeFallback = false;
            LastAnimatorStatus = "BossAnim: restored controller";
        }

        private bool HasTriggerParameter(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName) || bossAnimator == null)
                return false;

            foreach (var parameter in bossAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger &&
                    parameter.name == triggerName)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResetTriggerIfExists(string triggerName)
        {
            if (HasTriggerParameter(triggerName))
                bossAnimator.ResetTrigger(triggerName);
        }

        private bool TryCrossFade(string stateName, BossAttackType attackType)
        {
            if (string.IsNullOrEmpty(stateName) || bossAnimator == null)
                return false;

            var stateHash = Animator.StringToHash(stateName);
            for (var layer = 0; layer < bossAnimator.layerCount; layer++)
            {
                if (!bossAnimator.HasState(layer, stateHash))
                    continue;

                bossAnimator.CrossFadeInFixedTime(stateHash, crossFadeDuration, layer);
                LastAnimatorStatus = $"BossAnim: state {stateName} ({attackType})";
                return true;
            }

            return false;
        }

        private string ResolveTriggerName(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return highAttackTrigger;
                case BossAttackType.Middle:
                    return middleAttackTrigger;
                case BossAttackType.Low:
                    return lowAttackTrigger;
                default:
                    return string.Empty;
            }
        }

        private string ResolveStateName(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return highAttackState;
                case BossAttackType.Middle:
                    return middleAttackState;
                case BossAttackType.Low:
                    return lowAttackState;
                default:
                    return string.Empty;
            }
        }
    }
}
