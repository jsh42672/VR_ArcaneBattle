using UnityEngine;

namespace ArcaneVR.Training
{
    public class Training_DefenseLogic : MonoBehaviour
    {
        [SerializeField] private GameObject defenseVFXSlot; // Slot for user to attach VFX
        [SerializeField] private float defenseRadius = 1.5f;
        
        public System.Action OnDefenseSuccess;
        public System.Action OnDefenseFailure;

        private bool isShieldActive = false;

        // This would be called by the existing input/gesture system in the project.
        // For now, we provide the logic to be triggered.
        public void ToggleShield(bool active)
        {
            isShieldActive = active;
            if (defenseVFXSlot != null) defenseVFXSlot.SetActive(active);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.name.Contains("EnemyBolt") || other.CompareTag("Projectile"))
            {
                if (isShieldActive)
                {
                    OnDefenseSuccess?.Invoke();
                    Destroy(other.gameObject);
                }
                else
                {
                    OnDefenseFailure?.Invoke();
                    // We don't destroy here to simulate "getting hit" or let it pass
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, defenseRadius);
        }
    }
}
