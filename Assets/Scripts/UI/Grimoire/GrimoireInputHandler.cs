using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneVR.UI
{
    public class GrimoireInputHandler : MonoBehaviour
    {
        [SerializeField] private GrimoireUI grimoireUI;
        
        [Header("Input Actions")]
        [SerializeField] private InputActionReference nextPageAction;
        [SerializeField] private InputActionReference prevPageAction;

        private void OnEnable()
        {
            if (nextPageAction != null)
            {
                nextPageAction.action.Enable();
                nextPageAction.action.performed += OnNextPagePressed;
            }

            if (prevPageAction != null)
            {
                prevPageAction.action.Enable();
                prevPageAction.action.performed += OnPrevPagePressed;
            }
        }

        private void OnDisable()
        {
            if (nextPageAction != null)
            {
                nextPageAction.action.performed -= OnNextPagePressed;
            }

            if (prevPageAction != null)
            {
                prevPageAction.action.performed -= OnPrevPagePressed;
            }
        }

        private void OnNextPagePressed(InputAction.CallbackContext context)
        {
            if (grimoireUI != null)
            {
                grimoireUI.NextPage();
            }
        }

        private void OnPrevPagePressed(InputAction.CallbackContext context)
        {
            if (grimoireUI != null)
            {
                grimoireUI.PreviousPage();
            }
        }
    }
}
