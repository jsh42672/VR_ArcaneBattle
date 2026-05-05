using UnityEngine;
using TMPro;
using ArcaneVR.Input;

namespace ArcaneVR.Player
{
    public class HandPullMovementDebugDisplay : MonoBehaviour
    {
        [SerializeField] private HandPullMovementController movementController;
        [SerializeField] private TextMeshProUGUI debugText;

        private void Update()
        {
            if (movementController == null || debugText == null)
            {
                return;
            }

            Vector3 move = movementController.LastMoveDelta;

            debugText.text =
                $"Hand Pull Movement\n\n" +
                $"Has Left Hand: {movementController.HasLeftHand}\n" +
                $"Has Right Hand: {movementController.HasRightHand}\n" +
                $"Left Fist: {movementController.IsLeftFist}\n" +
                $"Right Fist: {movementController.IsRightFist}\n" +
                $"Is Pulling: {movementController.IsPulling}\n" +
                $"Active Hand: {movementController.ActiveHandName}\n" +
                $"Move Delta: {move.x:F3}, {move.y:F3}, {move.z:F3}\n" +
                $"Message: {movementController.LastDebugMessage}";
        }
    }
}