using UnityEngine;
using TMPro;

namespace ArcaneVR.Combat
{
    public class DodgeDebugDisplay : MonoBehaviour
    {
        [SerializeField] private DodgeDetector dodgeDetector;
        [SerializeField] private TextMeshProUGUI debugText;

        private void Update()
        {
            if (dodgeDetector == null || debugText == null)
                return;

            debugText.text =
                $"Dodge Test\n" +
                $"Window Open: {dodgeDetector.IsWindowOpen}\n" +
                $"Attack: {dodgeDetector.CurrentAttackType}\n" +
                $"Message: {dodgeDetector.LastDebugMessage}";
        }
    }
}