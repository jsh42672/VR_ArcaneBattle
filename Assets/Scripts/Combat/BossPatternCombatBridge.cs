using UnityEngine;
using System;

namespace ArcaneVR.Combat
{
    public class BossPatternCombatBridge : MonoBehaviour
    {
        [SerializeField] private DodgeDetector dodgeDetector;
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private float defaultResponseWindowDuration = 1.2f;
        [SerializeField] private float defaultChargeCounterDuration = 3f;
        [SerializeField] private float defaultGolemBarrierDuration = 8f;

        public event Action<BossAttackType, float> OnAttackResponseWindowStarted;
        public event Action<float> OnChargeCounterWindowStarted;
        public event Action<float> OnGolemBarrierStarted;

        public string LastBridgeStatus { get; private set; } = "Bridge: idle";

        private void Awake()
        {
            ResolveReferences();
        }

        public void BeginAttackResponseWindow(BossAttackType attackType)
        {
            BeginAttackResponseWindow(attackType, defaultResponseWindowDuration);
        }

        public void BeginAttackResponseWindow(BossAttackType attackType, float duration)
        {
            ResolveReferences();

            if (attackType == BossAttackType.Low)
            {
                barrierController?.BeginResponseWindow(attackType, duration);
                LastBridgeStatus = $"Barrier window: {attackType}";
                OnAttackResponseWindowStarted?.Invoke(attackType, duration);
                return;
            }

            dodgeDetector?.BeginDodgeWindow(attackType);
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
                dodgeDetector = FindAnyObjectByType<DodgeDetector>();

            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();

            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();
        }
    }
}
