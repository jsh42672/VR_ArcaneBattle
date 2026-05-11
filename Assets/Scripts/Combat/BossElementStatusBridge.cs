using ArcaneVR.Boss;
using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Optional bridge for boss teammates: maps elemental combat status to the simple BossAI state contract.
    /// Attach this next to BossAI/GolemCombatTarget when the real boss object is ready.
    /// </summary>
    public class BossElementStatusBridge : MonoBehaviour
    {
        [SerializeField] private GolemCombatTarget combatTarget;
        [SerializeField] private BossAI bossAI;
        [SerializeField] private bool driveBossAiState = true;

        public string LastStatusText { get; private set; } = "BossStatus: idle";
        public BossElementStatusSnapshot LastSnapshot { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (combatTarget != null)
                combatTarget.OnElementStatusChanged += HandleElementStatusChanged;
        }

        private void OnDisable()
        {
            if (combatTarget != null)
                combatTarget.OnElementStatusChanged -= HandleElementStatusChanged;
        }

        public void RefreshNow()
        {
            ResolveReferences();
            if (combatTarget != null)
                HandleElementStatusChanged(combatTarget.GetStatusSnapshot());
        }

        private void HandleElementStatusChanged(BossElementStatusSnapshot snapshot)
        {
            LastSnapshot = snapshot;
            LastStatusText =
                $"BossStatus: {snapshot.combatCue} HP {snapshot.currentHealth:0}/{snapshot.maxHealth:0} " +
                $"Move {snapshot.movementSpeedMultiplier:0.00} Act {snapshot.actionSpeedMultiplier:0.00}";

            if (!driveBossAiState || bossAI == null)
                return;

            if (snapshot.currentHealth <= 0f)
            {
                bossAI.Die();
                return;
            }

            if (snapshot.isStaggered || snapshot.isWeakExposed)
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

        private void ResolveReferences()
        {
            if (combatTarget == null)
                combatTarget = GetComponent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();

            if (bossAI == null)
                bossAI = GetComponent<BossAI>() ?? FindAnyObjectByType<BossAI>();
        }
    }
}
