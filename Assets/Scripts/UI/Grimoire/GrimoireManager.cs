using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Handles Grimoire summoning and dismissal. Positions the grimoire in front of the player camera.
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

        private bool _isOpen = false;

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main?.transform;
            }

            // Start with grimoire hidden
            if (grimoireCanvas != null)
            {
                grimoireCanvas.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (toggleAction != null)
            {
                toggleAction.action.Enable();
                toggleAction.action.performed += OnTogglePressed;
            }
        }

        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.action.performed -= OnTogglePressed;
            }
        }

        private void OnTogglePressed(InputAction.CallbackContext context)
        {
            ToggleGrimoire();
        }

        public void ToggleGrimoire()
        {
            _isOpen = !_isOpen;

            if (_isOpen)
            {
                OpenGrimoire();
            }
            else
            {
                CloseGrimoire();
            }
        }

        private void OpenGrimoire()
        {
            if (grimoireCanvas == null) return;

            // Position in front of camera
            if (playerCamera != null)
            {
                Vector3 targetPos = playerCamera.position + (playerCamera.forward * spawnDistance);
                targetPos.y += spawnHeightOffset;
                
                grimoireCanvas.transform.position = targetPos;
                
                // Make it look at the player (but keep it upright)
                Vector3 lookAtPos = playerCamera.position;
                lookAtPos.y = grimoireCanvas.transform.position.y; 
                grimoireCanvas.transform.LookAt(lookAtPos);
                grimoireCanvas.transform.Rotate(0, 180, 0); // UI faces the camera
            }

            grimoireCanvas.SetActive(true);
            OnGrimoireOpen?.Invoke();
        }

        private void CloseGrimoire()
        {
            if (grimoireCanvas != null)
            {
                grimoireCanvas.SetActive(false);
            }
            OnGrimoireClose?.Invoke();
        }
    }
}
