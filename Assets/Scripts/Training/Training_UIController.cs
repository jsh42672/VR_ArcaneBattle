using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArcaneVR.Training
{
    public class Training_UIController : MonoBehaviour
    {
        [Header("Zone 1: Shooting")]
        [SerializeField] private TextMeshProUGUI scoreText;
        
        [Header("Zone 2: Defense")]
        [SerializeField] private TextMeshProUGUI defenseStatusText;

        [Header("Zone 3: Movement")]
        [SerializeField] private Image dashCooldownImage;
        [SerializeField] private Image pullCooldownImage;

        private int currentScore = 0;

        public void UpdateScore(int delta)
        {
            currentScore += delta;
            if (scoreText != null) scoreText.text = $"Score: {currentScore}";
        }

        public void SetDefenseStatus(string status, Color color)
        {
            if (defenseStatusText != null)
            {
                defenseStatusText.text = status;
                defenseStatusText.color = color;
                CancelInvoke(nameof(ClearDefenseStatus));
                Invoke(nameof(ClearDefenseStatus), 2f);
            }
        }

        private void ClearDefenseStatus()
        {
            if (defenseStatusText != null) defenseStatusText.text = "";
        }

        public void UpdateDashCooldown(float fillAmount)
        {
            if (dashCooldownImage != null) dashCooldownImage.fillAmount = fillAmount;
        }

        public void UpdatePullCooldown(float fillAmount)
        {
            if (pullCooldownImage != null) pullCooldownImage.fillAmount = fillAmount;
        }
    }
}
