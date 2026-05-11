using UnityEngine;

namespace ArcaneVR.Training
{
    public class Training_ProjectileSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float fireRate = 2f;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private Transform target;

        private float nextFireTime;

        void Update()
        {
            if (target == null) return;

            if (Time.time >= nextFireTime)
            {
                Fire();
                nextFireTime = Time.time + fireRate;
            }
        }

        void Fire()
        {
            if (projectilePrefab == null)
            {
                // Fallback: Create a simple sphere if no prefab
                GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bolt.name = "Training_EnemyBolt";
                bolt.transform.position = transform.position;
                bolt.transform.localScale = Vector3.one * 0.2f;
                var rb = bolt.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = false;
                bolt.GetComponent<Collider>().isTrigger = true;
                
                Vector3 dir = (target.position - transform.position).normalized;
                rb.linearVelocity = dir * projectileSpeed;
                Destroy(bolt, 3f);
            }
            else
            {
                GameObject bolt = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
                Vector3 dir = (target.position - transform.position).normalized;
                var rb = bolt.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = dir * projectileSpeed;
                Destroy(bolt, 5f);
            }
        }

        public void SetTarget(Transform newTarget) => target = newTarget;
    }
}
