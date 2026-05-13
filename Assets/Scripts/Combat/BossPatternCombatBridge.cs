using System;
using System.Collections;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class BossPatternCombatBridge : MonoBehaviour
    {
        public event Action<BossAttackType, float> OnAttackResponseWindowStarted;
        public event Action<float> OnChargeCounterWindowStarted;
        public event Action<float> OnGolemBarrierStarted;

        [SerializeField]
        private DodgeDetector dodgeDetector;

        [SerializeField]
        private BarrierController barrierController;

        [SerializeField]
        private GolemCombatTarget golemTarget;

        [SerializeField]
        private ConstraintController constraintController;

        [SerializeField]
        private bool enablePlayerConstraints;

        [SerializeField]
        private bool enableAttackResponseWindows;

        [SerializeField]
        private float defaultResponseWindowDuration = 1.2f;

        [SerializeField]
        private float defaultChargeCounterDuration = 3f;

        [SerializeField]
        private float defaultGolemBarrierDuration = 8f;

        [SerializeField]
        private float attackResponseConstraintLeadIn = 0.35f;

        [SerializeField]
        private float attackResponseConstraintExtraDuration = 0.2f;

        private Coroutine pendingAttackResponseRoutine;

        public string LastBridgeStatus { get; private set; } = "Bridge: idle";

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (pendingAttackResponseRoutine != null)
            {
                StopCoroutine(pendingAttackResponseRoutine);
                pendingAttackResponseRoutine = null;
            }
        }

        public void BeginAttackResponseWindow(BossAttackType attackType)
        {
            BeginAttackResponseWindow(attackType, defaultResponseWindowDuration);
        }

        public void BeginAttackResponseWindow(BossAttackType attackType, float duration)
        {
            if (!enableAttackResponseWindows)
            {
                LastBridgeStatus = $"Attack response disabled: {attackType}";
                return;
            }

            ResolveReferences();

            if (pendingAttackResponseRoutine != null)
            {
                StopCoroutine(pendingAttackResponseRoutine);
                pendingAttackResponseRoutine = null;
            }

            var responseDuration = Mathf.Max(0.1f, duration);
            var leadIn = Mathf.Max(0f, attackResponseConstraintLeadIn);
            var constraintDuration = leadIn + responseDuration + Mathf.Max(0f, attackResponseConstraintExtraDuration);
            if (enablePlayerConstraints)
                constraintController?.BeginResponseConstraint(constraintDuration);

            if (leadIn > 0f)
            {
                LastBridgeStatus = $"Constraint before {attackType}";
                pendingAttackResponseRoutine = StartCoroutine(BeginAttackResponseAfterLeadIn(attackType, responseDuration, leadIn));
                return;
            }

            StartAttackResponseWindow(attackType, responseDuration);
        }

        private IEnumerator BeginAttackResponseAfterLeadIn(BossAttackType attackType, float duration, float leadIn)
        {
            yield return new WaitForSeconds(leadIn);
            pendingAttackResponseRoutine = null;
            StartAttackResponseWindow(attackType, duration);
        }

        private void StartAttackResponseWindow(BossAttackType attackType, float duration)
        {
            ResolveReferences();

            if (attackType == BossAttackType.Low)
            {
                barrierController?.BeginResponseWindow(attackType, duration);
                LastBridgeStatus = $"Barrier window: {attackType}";
                OnAttackResponseWindowStarted?.Invoke(attackType, duration);
                return;
            }

            dodgeDetector?.BeginDodgeWindow(attackType, duration);
            LastBridgeStatus = $"Dodge window: {attackType}";
            OnAttackResponseWindowStarted?.Invoke(attackType, duration);
        }

        public void BeginChargeCounterWindow()
        {
            BeginChargeCounterWindow(defaultChargeCounterDuration);
        }

        public void BeginChargeCounterWindow(float duration)
        {
            ResolveReferences();
            golemTarget?.BeginChargeCounterWindow(duration);
            if (enablePlayerConstraints)
                constraintController?.BeginConstraint(duration);
            LastBridgeStatus = "Charge counter window";
            OnChargeCounterWindowStarted?.Invoke(duration);
        }

        public void BeginGolemBarrier()
        {
            BeginGolemBarrier(defaultGolemBarrierDuration);
        }

        public void BeginGolemBarrier(float duration)
        {
            ResolveReferences();
            golemTarget?.BeginBarrier(duration);
            LastBridgeStatus = "Golem barrier";
            OnGolemBarrierStarted?.Invoke(duration);
        }

        private void ResolveReferences()
        {
            if (dodgeDetector == null)
                dodgeDetector = FindAnyObjectByType<DodgeDetector>() ?? gameObject.AddComponent<DodgeDetector>();

            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>() ?? gameObject.AddComponent<BarrierController>();

            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();

            if (constraintController == null && enablePlayerConstraints)
                constraintController = FindAnyObjectByType<ConstraintController>();

            if (constraintController == null && enablePlayerConstraints)
                constraintController = gameObject.AddComponent<ConstraintController>();

            if (!enablePlayerConstraints && constraintController != null && constraintController.IsConstrained)
                constraintController.EndConstraint();
        }
    }
}
