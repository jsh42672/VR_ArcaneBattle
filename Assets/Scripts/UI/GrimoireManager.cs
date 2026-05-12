using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Handles grimoire summoning and dismissal. Keeps the original script GUID while supporting the richer grimoire scene setup.
    /// </summary>
    public class GrimoireManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject grimoireCanvas;
        [SerializeField] private Transform playerCamera;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleAction;

        [Header("Settings")]
        [SerializeField] private float spawnDistance = 0.5f;
        [SerializeField] private float spawnHeightOffset = -0.2f;

        public event Action OnGrimoireOpen;
        public event Action OnGrimoireClose;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main?.transform;

            if (grimoireCanvas != null)
                grimoireCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            if (toggleAction == null)
                return;

            toggleAction.action.Enable();
            toggleAction.action.performed += OnTogglePressed;
        }

        private void OnDisable()
        {
            if (toggleAction == null)
                return;

            toggleAction.action.performed -= OnTogglePressed;
        }

        public void ToggleGrimoire()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            PositionGrimoire();

            if (grimoireCanvas != null)
                grimoireCanvas.SetActive(true);

            OnGrimoireOpen?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;

            if (grimoireCanvas != null)
                grimoireCanvas.SetActive(false);

            OnGrimoireClose?.Invoke();
        }

        private void OnTogglePressed(InputAction.CallbackContext context)
        {
            ToggleGrimoire();
        }

        private void PositionGrimoire()
        {
            if (grimoireCanvas == null || playerCamera == null)
                return;

            var targetPosition = playerCamera.position + playerCamera.forward * spawnDistance;
            targetPosition.y += spawnHeightOffset;
            grimoireCanvas.transform.position = targetPosition;

            var lookAtPosition = playerCamera.position;
            lookAtPosition.y = grimoireCanvas.transform.position.y;
            grimoireCanvas.transform.LookAt(lookAtPosition);
            grimoireCanvas.transform.Rotate(0f, 180f, 0f);
        }
    }
}
