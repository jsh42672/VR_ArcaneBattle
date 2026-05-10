using System;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Tracks head Y/X-axis movement. Fires OnDodgeSuccess when movement exceeds 15cm threshold.
    /// </summary>
    public class DodgeDetector : MonoBehaviour
    {
        public event Action OnDodgeSuccess;

        [SerializeField] private Transform headTransform;
        [SerializeField] private float dodgeThreshold = 0.15f;

        private Vector3 baselineHeadPosition;
        private bool isWindowOpen;

        public bool IsWindowOpen => isWindowOpen;
        public string LastDebugMessage { get; private set; } = "Dodge: idle";

        private void Awake()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (!isWindowOpen)
                return;

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            if (headTransform == null)
                return;

            var delta = headTransform.position - baselineHeadPosition;
            if (Mathf.Abs(delta.x) < dodgeThreshold && Mathf.Abs(delta.y) < dodgeThreshold)
                return;

            isWindowOpen = false;
            LastDebugMessage = $"Dodge Success: {delta.x:0.00},{delta.y:0.00}";
            OnDodgeSuccess?.Invoke();
        }

        public void BeginDodgeWindow()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            baselineHeadPosition = headTransform != null ? headTransform.position : Vector3.zero;
            isWindowOpen = true;
            LastDebugMessage = "Dodge Window";
        }

        public void CancelDodgeWindow()
        {
            isWindowOpen = false;
            LastDebugMessage = "Dodge Cancelled";
        }
    }
}
