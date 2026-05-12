using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Debug/demo bridge: failed low-attack guard windows damage player HP through CombatManager.
    /// Remove or disable when real boss hitboxes take over.
    /// </summary>
    public class BarrierPlayerDamageBridge : MonoBehaviour
    {
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private bool applyDamageOnFailedWindow = true;
        [SerializeField] private float lowAttackDamage = 18f;
        [SerializeField] private float defaultAttackDamage = 12f;

        private BossAttackType activeAttackType = BossAttackType.Low;

        public string LastDamageStatus { get; private set; } = "PlayerHit: idle";
        public float LastDamageApplied { get; private set; }

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

        private void Subscribe()
        {
            if (barrierController == null)
                return;

            barrierController.OnResponseWindowStarted -= HandleResponseWindowStarted;
            barrierController.OnResponseWindowResolved -= HandleResponseWindowResolved;
            barrierController.OnResponseWindowStarted += HandleResponseWindowStarted;
            barrierController.OnResponseWindowResolved += HandleResponseWindowResolved;
        }

        private void Unsubscribe()
        {
            if (barrierController == null)
                return;

            barrierController.OnResponseWindowStarted -= HandleResponseWindowStarted;
            barrierController.OnResponseWindowResolved -= HandleResponseWindowResolved;
        }

        private void HandleResponseWindowStarted(BossAttackType attackType)
        {
            activeAttackType = attackType;
            LastDamageApplied = 0f;
            LastDamageStatus = $"PlayerHit: incoming {attackType}";
        }

        private void HandleResponseWindowResolved(bool success, string result)
        {
            ResolveReferences();

            if (success)
            {
                LastDamageApplied = 0f;
                LastDamageStatus = "PlayerHit: blocked";
                return;
            }

            if (!applyDamageOnFailedWindow || !IsFailureResult(result))
            {
                LastDamageApplied = 0f;
                LastDamageStatus = $"PlayerHit: no damage ({result})";
                return;
            }

            var damage = ResolveDamage(activeAttackType);
            if (combatManager != null)
                combatManager.ApplyPlayerHit(damage);

            LastDamageApplied = damage;
            LastDamageStatus = $"PlayerHit: -{damage:0.#} ({activeAttackType})";
        }

        private void ResolveReferences()
        {
            if (barrierController == null)
            {
                barrierController = GetComponent<BarrierController>() ??
                                    FindAnyObjectByType<BarrierController>();
                Subscribe();
            }

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private float ResolveDamage(BossAttackType attackType)
        {
            return attackType == BossAttackType.Low
                ? Mathf.Max(0f, lowAttackDamage)
                : Mathf.Max(0f, defaultAttackDamage);
        }

        private static bool IsFailureResult(string result)
        {
            return string.IsNullOrEmpty(result) ||
                   result.Contains("Fail") ||
                   result.Contains("timeout") ||
                   result.Contains("no mana");
        }
    }
}
