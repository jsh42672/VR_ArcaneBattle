using ArcaneVR.Input;
using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(130)]
    public class DodgePlayerDamageBridge : MonoBehaviour
    {
        [SerializeField] private DodgeDetector dodgeDetector;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private float failedDodgeDamage = 10f;

        private DodgeDetector subscribedDodgeDetector;

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
                dodgeDetector = FindAnyObjectByType<DodgeDetector>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private void Subscribe()
        {
            if (subscribedDodgeDetector == dodgeDetector)
                return;

            Unsubscribe();
            subscribedDodgeDetector = dodgeDetector;
            if (subscribedDodgeDetector != null)
                subscribedDodgeDetector.OnDodgeFail += HandleDodgeFail;
        }

        private void Unsubscribe()
        {
            if (subscribedDodgeDetector != null)
                subscribedDodgeDetector.OnDodgeFail -= HandleDodgeFail;

            subscribedDodgeDetector = null;
        }

        private void HandleDodgeFail()
        {
            if (combatManager == null)
                return;

            combatManager.ApplyPlayerHit(combatManager.FailedDodgeDamage, "DodgeFail");
        }
    }
}
