using UnityEngine;

namespace ArcaneVR.Training
{
    public class Training_MovingTarget : MonoBehaviour
    {
        public enum MovePattern { Horizontal, Vertical, Rotation }

        [SerializeField] private MovePattern pattern = MovePattern.Horizontal;
        [SerializeField] private float speed = 2f;
        [SerializeField] private float distance = 3f;
        [SerializeField] private int scoreValue = 10;
        [SerializeField] private float rotationRadius = 3f;

        private Vector3 startPos;
        private bool isHit = false;

        public System.Action<int> OnHit;

        void Start()
        {
            startPos = transform.position;
        }

        void Update()
        {
            if (pattern == MovePattern.Horizontal)
            {
                float offset = Mathf.Sin(Time.time * speed) * distance;
                transform.position = startPos + transform.right * offset;
            }
            else if (pattern == MovePattern.Vertical)
            {
                float offset = Mathf.Sin(Time.time * speed) * distance;
                transform.position = startPos + transform.up * offset;
            }
            else if (pattern == MovePattern.Rotation)
            {
                float x = Mathf.Cos(Time.time * speed) * rotationRadius;
                float y = Mathf.Sin(Time.time * speed) * rotationRadius;
                transform.position = startPos + new Vector3(x, y, 0);
                transform.Rotate(Vector3.forward, 90 * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isHit) return;

            // Simple check for projectile. In a real scenario, we might check tags or components.
            if (other.name.Contains("Bolt") || other.name.Contains("Projectile") || other.GetComponent<Rigidbody>() != null)
            {
                isHit = true;
                OnHit?.Invoke(scoreValue);
                
                // Visual feedback (simple)
                var renderer = GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = Color.red;

                Invoke(nameof(ResetTarget), 0.5f);
            }
        }

        private void ResetTarget()
        {
            isHit = false;
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = Color.white;
        }
    }
}
