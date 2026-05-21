using UnityEngine;

namespace ArcaneVR.UI
{
    public class GrimoireUI : MonoBehaviour
    {
        [SerializeField] private GameObject[] pages;
        [SerializeField] private int currentPageIndex;

        public int CurrentPageIndex => currentPageIndex;
        public int PageCount => pages != null ? pages.Length : 0;

        private void OnEnable()
        {
            RefreshPages();
        }

        public void NextPage()
        {
            if (PageCount == 0)
                return;

            currentPageIndex = (currentPageIndex + 1) % PageCount;
            RefreshPages();
        }

        public void PreviousPage()
        {
            if (PageCount == 0)
                return;

            currentPageIndex = (currentPageIndex + PageCount - 1) % PageCount;
            RefreshPages();
        }

        public void SetPage(int pageIndex)
        {
            if (PageCount == 0)
                return;

            currentPageIndex = Mathf.Clamp(pageIndex, 0, PageCount - 1);
            RefreshPages();
        }

        private void RefreshPages()
        {
            if (pages == null || pages.Length == 0)
                return;

            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, pages.Length - 1);
            for (var i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                    pages[i].SetActive(i == currentPageIndex);
            }
        }
    }
}
