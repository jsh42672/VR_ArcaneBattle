using ArcaneVR.Boss;
using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(133)]
    public class BossElementStatusBridge : MonoBehaviour
    {
        [SerializeField] private GolemCombatTarget combatTarget;
        [SerializeField] private BossAI bossAI;
        [SerializeField] private bool driveBossAiState = true;

        private GolemCombatTarget subscribedCombatTarget;

        public string LastBridgeStatus { get; private set; } = "BossStatusBridge: idle";

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
        }

        private void ResolveReferences()
        {
            if (combatTarget == null)
                combatTarget = GetComponent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();

            if (bossAI == null)
                bossAI = GetComponent<BossAI>() ?? FindAnyObjectByType<BossAI>();
        }

        private void Subscribe()
        {
            if (subscribedCombatTarget == combatTarget)
                return;

            Unsubscribe();
            subscribedCombatTarget = combatTarget;
            if (subscribedCombatTarget == null)
                return;

            subscribedCombatTarget.OnElementStatusChanged += HandleStatusChanged;
            subscribedCombatTarget.OnDefeated += HandleDefeated;
            HandleStatusChanged(subscribedCombatTarget.GetStatusSnapshot());
        }

        private void Unsubscribe()
        {
            if (subscribedCombatTarget != null)
            {
                subscribedCombatTarget.OnElementStatusChanged -= HandleStatusChanged;
                subscribedCombatTarget.OnDefeated -= HandleDefeated;
            }

            subscribedCombatTarget = null;
        }

        private void HandleStatusChanged(BossElementStatusSnapshot snapshot)
        {
            LastBridgeStatus = $"BossStatusBridge: {snapshot.combatCue}";

            if (!driveBossAiState || bossAI == null)
                return;

            if (snapshot.currentHealth <= 0f)
            {
                bossAI.Die();
                return;
            }

            if (snapshot.isWeakExposed || snapshot.isStaggered)
            {
                bossAI.ExposeWeakness();
                return;
            }

            if (snapshot.isBarrierActive)
            {
                bossAI.EnterDefense();
                return;
            }

            if (snapshot.isChargeCounterWindowOpen)
            {
                bossAI.BeginCharge();
                return;
            }

            bossAI.ChangeState(BossState.Idle);
        }

        private void HandleDefeated()
        {
            bossAI?.Die();
            LastBridgeStatus = "BossStatusBridge: defeated";
        }
    }
}
