using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(130)]
    public class BarrierPlayerDamageBridge : MonoBehaviour
    {
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private float failedBarrierDamage = 10f;

        private BarrierController subscribedBarrierController;

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
            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private void Subscribe()
        {
            if (subscribedBarrierController == barrierController)
                return;

            Unsubscribe();
            subscribedBarrierController = barrierController;
            if (subscribedBarrierController != null)
                subscribedBarrierController.OnResponseWindowResolved += HandleResponseWindowResolved;
        }

        private void Unsubscribe()
        {
            if (subscribedBarrierController != null)
                subscribedBarrierController.OnResponseWindowResolved -= HandleResponseWindowResolved;

            subscribedBarrierController = null;
        }

        private void HandleResponseWindowResolved(bool success, string result)
        {
            if (success)
                return;

            if (combatManager == null)
                return;

            combatManager.ApplyPlayerHit(combatManager.FailedBarrierDamage, "BarrierFail");
        }
    }
}
