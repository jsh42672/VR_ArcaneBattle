using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArcaneVR.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class GrimoireUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GrimoireData grimoireData;
        
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image symbolImage;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private CanvasGroup contentFadeGroup;

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.3f;

        private int _currentPageIndex = 0;
        private bool _isTransitioning = false;

        private void Start()
        {
            if (contentFadeGroup == null)
                contentFadeGroup = GetComponent<CanvasGroup>();

            if (grimoireData != null && grimoireData.pages.Count > 0)
            {
                UpdateUI(_currentPageIndex);
            }
        }

        public void NextPage()
        {
            if (_isTransitioning || grimoireData == null || grimoireData.pages.Count == 0) return;

            int nextIndex = (_currentPageIndex + 1) % grimoireData.pages.Count;
            StartCoroutine(TransitionToPage(nextIndex));
        }

        public void PreviousPage()
        {
            if (_isTransitioning || grimoireData == null || grimoireData.pages.Count == 0) return;

            int prevIndex = (_currentPageIndex - 1 + grimoireData.pages.Count) % grimoireData.pages.Count;
            StartCoroutine(TransitionToPage(prevIndex));
        }

        private IEnumerator TransitionToPage(int targetIndex)
        {
            _isTransitioning = true;

            // 1. Fade Out
            yield return StartCoroutine(Fade(contentFadeGroup, 1, 0, fadeDuration));

            // 2. Update Content
            UpdateUI(targetIndex);
            _currentPageIndex = targetIndex;

            // 3. Fade In
            yield return StartCoroutine(Fade(contentFadeGroup, 0, 1, fadeDuration));

            _isTransitioning = false;
        }

        private void UpdateUI(int index)
        {
            var data = grimoireData.pages[index];
            if (titleText) titleText.text = data.pageTitle;
            if (symbolImage)
            {
                symbolImage.sprite = data.magicSymbol;
                symbolImage.color = data.themeColor;
            }
            if (descriptionText) descriptionText.text = data.description;
        }

        private IEnumerator Fade(CanvasGroup group, float start, float end, float duration)
        {
            if (group == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            group.alpha = end;
        }
    }
}
