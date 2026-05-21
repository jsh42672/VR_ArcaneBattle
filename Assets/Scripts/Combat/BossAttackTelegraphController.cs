using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(128)]
    public class BossAttackTelegraphController : MonoBehaviour
    {
        [SerializeField] private BossPatternCombatBridge patternBridge;

        private BossPatternCombatBridge subscribedPatternBridge;

        public BossAttackType LastAttackType { get; private set; }
        public float TelegraphRemaining { get; private set; }
        public string LastTelegraphStatus { get; private set; } = "Telegraph: idle";

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

            if (TelegraphRemaining > 0f)
            {
                TelegraphRemaining = Mathf.Max(0f, TelegraphRemaining - Time.deltaTime);
                if (TelegraphRemaining <= 0f)
                    LastTelegraphStatus = "Telegraph: idle";
            }
        }

        private void ResolveReferences()
        {
            if (patternBridge == null)
                patternBridge = FindAnyObjectByType<BossPatternCombatBridge>();
        }

        private void Subscribe()
        {
            if (subscribedPatternBridge == patternBridge)
                return;

            Unsubscribe();
            subscribedPatternBridge = patternBridge;
            if (subscribedPatternBridge != null)
                subscribedPatternBridge.OnAttackResponseWindowStarted += HandleAttackStarted;
        }

        private void Unsubscribe()
        {
            if (subscribedPatternBridge != null)
                subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;

            subscribedPatternBridge = null;
        }

        private void HandleAttackStarted(BossAttackType attackType, float duration)
        {
            LastAttackType = attackType;
            TelegraphRemaining = Mathf.Max(0f, duration);
            LastTelegraphStatus = $"Telegraph: {attackType}";
        }
    }
}
