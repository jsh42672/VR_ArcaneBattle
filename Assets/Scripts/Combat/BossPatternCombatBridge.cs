using UnityEngine;

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
                return;
            }

            dodgeDetector?.BeginDodgeWindow(attackType);
            LastBridgeStatus = $"Dodge window: {attackType}";
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
