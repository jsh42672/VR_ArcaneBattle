using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Applies player damage when high/middle dodge windows fail.
    /// Low attacks are handled by BarrierPlayerDamageBridge.
    /// </summary>
    public class DodgePlayerDamageBridge : MonoBehaviour
    {
        [SerializeField] private DodgeDetector dodgeDetector;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private bool applyDamageOnDodgeFail = true;
        [SerializeField] private float highAttackDamage = 14f;
        [SerializeField] private float middleAttackDamage = 12f;

        private DodgeDetector subscribedDodgeDetector;

        public string LastDamageStatus { get; private set; } = "DodgeHit: idle";
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

        private void Update()
        {
            ResolveReferences();
            Subscribe();
        }

        private void ResolveReferences()
        {
            if (dodgeDetector == null)
                dodgeDetector = GetComponent<DodgeDetector>() ?? FindAnyObjectByType<DodgeDetector>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private void Subscribe()
        {
            if (dodgeDetector == null || subscribedDodgeDetector == dodgeDetector)
                return;

            Unsubscribe();
            subscribedDodgeDetector = dodgeDetector;
            subscribedDodgeDetector.OnDodgeSuccess += HandleDodgeSuccess;
            subscribedDodgeDetector.OnDodgeFail += HandleDodgeFail;
        }

        private void Unsubscribe()
        {
            if (subscribedDodgeDetector == null)
                return;

            subscribedDodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
            subscribedDodgeDetector.OnDodgeFail -= HandleDodgeFail;
            subscribedDodgeDetector = null;
        }

        private void HandleDodgeSuccess()
        {
            LastDamageApplied = 0f;
            LastDamageStatus = $"DodgeHit: dodged {subscribedDodgeDetector.CurrentAttackType}";
        }

        private void HandleDodgeFail()
        {
            if (subscribedDodgeDetector == null)
                return;

            var attackType = subscribedDodgeDetector.CurrentAttackType;
            if (attackType == BossAttackType.Low)
            {
                LastDamageApplied = 0f;
                LastDamageStatus = "DodgeHit: low attack uses barrier";
                return;
            }

            if (!applyDamageOnDodgeFail)
            {
                LastDamageApplied = 0f;
                LastDamageStatus = $"DodgeHit: disabled ({attackType})";
                return;
            }

            ResolveReferences();
            var damage = ResolveDamage(attackType);
            if (combatManager != null)
                combatManager.ApplyPlayerHit(damage);

            LastDamageApplied = damage;
            LastDamageStatus = $"DodgeHit: -{damage:0.#} ({attackType})";
        }

        private float ResolveDamage(BossAttackType attackType)
        {
            return attackType == BossAttackType.High
                ? Mathf.Max(0f, highAttackDamage)
                : Mathf.Max(0f, middleAttackDamage);
        }
    }
}
