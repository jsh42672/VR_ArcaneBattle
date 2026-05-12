using UnityEngine;
using System;

namespace ArcaneVR.Training
{
    public class Training_MovementGate : MonoBehaviour
    {
        public event Action OnGatePassed;
        [SerializeField] private Color activeColor = Color.yellow;
        [SerializeField] private Color passedColor = Color.gray;
        
        private bool isPassed = false;
        private Renderer gateRenderer;

        void Start()
        {
            gateRenderer = GetComponent<Renderer>();
            if (gateRenderer != null) gateRenderer.material.color = activeColor;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPassed) return;

            if (other.CompareTag("Player") || other.name.Contains("XR Origin"))
            {
                isPassed = true;
                if (gateRenderer != null) gateRenderer.material.color = passedColor;
                OnGatePassed?.Invoke();
            }
        }

        public void ResetGate()
        {
            isPassed = false;
            if (gateRenderer != null) gateRenderer.material.color = activeColor;
        }
    }
}
