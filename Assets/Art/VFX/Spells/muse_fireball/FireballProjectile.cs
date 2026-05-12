using UnityEngine;

public class FireballProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float lifetime = 5f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }
        
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hittable") || !other.isTrigger)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            // Simple placeholder explosion effect logic if prefab is missing
            Debug.Log("Fireball Exploded!");
        }
        
        Destroy(gameObject);
    }
}
