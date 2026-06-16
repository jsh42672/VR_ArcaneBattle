using ArcaneVR.Boss;
using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(129)]
    public class BossAttackAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private Animator bossAnimator;
        [SerializeField] private BossChaseController chaseController;
        [SerializeField] private string highAttackTrigger = "AttackHigh";
        [SerializeField] private string middleAttackTrigger = "AttackMiddle";
        [SerializeField] private string lowAttackTrigger = "AttackLow";
        [SerializeField] private string chargeTrigger = "Charge";
        [SerializeField] private string barrierTrigger = "Barrier";
        [SerializeField] private string highAttackState = "Slah_Cast";
        [SerializeField] private string middleAttackState = "Spell Cast";
        [SerializeField] private string lowAttackState = "Golemn_attack";
        [SerializeField] private string fallbackAttackState = "OneHand_Up_Attack_B_1";
        [SerializeField] private string resourcesFallbackControllerPath = "ArcaneVR/ThunderGolemAttackController";
        [SerializeField] private float fallbackControllerRestoreDelay = 1.2f;
        [SerializeField] private float crossFadeDuration = 0.05f;
        [SerializeField] private bool showDebugLog;

        private BossPatternCombatBridge subscribedPatternBridge;
        private RuntimeAnimatorController fallbackController;
        private RuntimeAnimatorController controllerBeforeFallback;
        private float restoreControllerAtTime;
        private bool usingFallbackController;

        private void OnEnable()
        {
            MigrateLegacyStateNames();
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreFallbackController(force: true);
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();
            RestoreFallbackController(force: false);
        }

        private void ResolveReferences()
        {
            if (patternBridge == null)
                patternBridge = FindAnyObjectByType<BossPatternCombatBridge>();

            if (chaseController == null)
                chaseController = FindAnyObjectByType<BossChaseController>();

            if (bossAnimator == null)
            {
                if (chaseController != null && chaseController.BossAnimator != null)
                    bossAnimator = chaseController.BossAnimator;
                else
                {
                    var target = FindAnyObjectByType<GolemCombatTarget>();
                    bossAnimator = target != null ? target.GetComponentInChildren<Animator>(true) : GetComponentInChildren<Animator>(true);
                }
            }

            if (fallbackController == null && !string.IsNullOrEmpty(resourcesFallbackControllerPath))
                fallbackController = Resources.Load<RuntimeAnimatorController>(resourcesFallbackControllerPath);
        }

        private void Subscribe()
        {
            if (subscribedPatternBridge == patternBridge)
                return;

            Unsubscribe();
            subscribedPatternBridge = patternBridge;
            if (subscribedPatternBridge == null)
                return;

            subscribedPatternBridge.OnAttackResponseWindowStarted += HandleAttackStarted;
            subscribedPatternBridge.OnChargeCounterWindowStarted += HandleChargeStarted;
            subscribedPatternBridge.OnGolemBarrierStarted += HandleBarrierStarted;
        }

        private void Unsubscribe()
        {
            if (subscribedPatternBridge != null)
            {
                subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;
                subscribedPatternBridge.OnChargeCounterWindowStarted -= HandleChargeStarted;
                subscribedPatternBridge.OnGolemBarrierStarted -= HandleBarrierStarted;
            }

            subscribedPatternBridge = null;
        }

        private void HandleAttackStarted(BossAttackType attackType, float duration)
        {
            var trigger = attackType switch
            {
                BossAttackType.High => highAttackTrigger,
                BossAttackType.Middle => middleAttackTrigger,
                BossAttackType.Low => lowAttackTrigger,
                _ => string.Empty
            };

            var stateName = attackType switch
            {
                BossAttackType.High => highAttackState,
                BossAttackType.Middle => middleAttackState,
                BossAttackType.Low => lowAttackState,
                _ => fallbackAttackState
            };

            PlayAnimation(trigger, stateName);
        }

        private void HandleChargeStarted(float duration)
        {
            PlayAnimation(chargeTrigger, fallbackAttackState);
        }

        private void HandleBarrierStarted(float duration)
        {
            MaybeLog($"Barrier started without boss animation | duration={duration:0.00}s");
        }

        private void PlayAnimation(string triggerName, string preferredState)
        {
            if (bossAnimator == null)
                return;

            bossAnimator.applyRootMotion = false;

            EnsurePatternAttackController();
            if (TryCrossFade(preferredState))
            {
                MaybeLog($"Animator state: {preferredState}");
                return;
            }

            if (TrySetTrigger(triggerName))
            {
                MaybeLog($"Animator trigger: {triggerName}");
                return;
            }

            EnsureFallbackController();
            if (TryCrossFade(fallbackAttackState))
            {
                MaybeLog($"Animator fallback state: {fallbackAttackState}");
                return;
            }

            if (!string.IsNullOrEmpty(fallbackAttackState) && bossAnimator.runtimeAnimatorController != null)
                bossAnimator.Play(fallbackAttackState, 0, 0f);
        }

        private void EnsurePatternAttackController()
        {
            if (bossAnimator == null ||
                chaseController == null ||
                chaseController.PatternAttackController == null ||
                bossAnimator.runtimeAnimatorController == chaseController.PatternAttackController)
            {
                return;
            }

            bossAnimator.runtimeAnimatorController = chaseController.PatternAttackController;
        }

        private void EnsureFallbackController()
        {
            if (bossAnimator == null || fallbackController == null)
                return;

            if (bossAnimator.runtimeAnimatorController == fallbackController)
            {
                usingFallbackController = true;
                restoreControllerAtTime = Time.time + Mathf.Max(0.1f, fallbackControllerRestoreDelay);
                return;
            }

            controllerBeforeFallback = bossAnimator.runtimeAnimatorController;
            bossAnimator.runtimeAnimatorController = fallbackController;
            usingFallbackController = true;
            restoreControllerAtTime = Time.time + Mathf.Max(0.1f, fallbackControllerRestoreDelay);
        }

        private void RestoreFallbackController(bool force)
        {
            if (!usingFallbackController || bossAnimator == null)
                return;

            if (!force && (restoreControllerAtTime <= 0f || Time.time < restoreControllerAtTime))
                return;

            if (bossAnimator.runtimeAnimatorController == fallbackController)
                bossAnimator.runtimeAnimatorController = controllerBeforeFallback;

            controllerBeforeFallback = null;
            restoreControllerAtTime = 0f;
            usingFallbackController = false;
        }

        private bool TrySetTrigger(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName) || bossAnimator == null || bossAnimator.runtimeAnimatorController == null)
                return false;

            foreach (var parameter in bossAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                {
                    bossAnimator.SetTrigger(triggerName);
                    return true;
                }
            }

            return false;
        }

        private bool TryCrossFade(string stateName)
        {
            if (string.IsNullOrEmpty(stateName) || bossAnimator == null || bossAnimator.runtimeAnimatorController == null)
                return false;

            var stateHash = Animator.StringToHash(stateName);
            for (var layer = 0; layer < bossAnimator.layerCount; layer++)
            {
                if (!bossAnimator.HasState(layer, stateHash))
                    continue;

                bossAnimator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, crossFadeDuration), layer);
                return true;
            }

            return false;
        }

        private void MigrateLegacyStateNames()
        {
            if (string.IsNullOrEmpty(highAttackState) || highAttackState == "AttackHigh")
                highAttackState = "Slah_Cast";

            if (string.IsNullOrEmpty(middleAttackState) || middleAttackState == "AttackMiddle")
                middleAttackState = "Spell Cast";

            if (string.IsNullOrEmpty(lowAttackState) || lowAttackState == "AttackLow")
                lowAttackState = "Golemn_attack";
        }

        private void MaybeLog(string message)
        {
            if (!showDebugLog)
                return;

            Debug.Log($"[BossAttackAnimatorBridge] {message}", this);
        }
    }
}
