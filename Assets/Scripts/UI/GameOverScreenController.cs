using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    public class GameOverScreenController : MonoBehaviour
    {
        [SerializeField] private string worldSceneName = "OPenWorld2";
        [SerializeField] private bool pauseGameOnShow = true;

        public void Show()
        {
            gameObject.SetActive(true);

            if (pauseGameOnShow)
                Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (pauseGameOnShow)
                Time.timeScale = 1f;

            gameObject.SetActive(false);
        }

        public void RestartCurrentScene()
        {
            Time.timeScale = 1f;

            UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[GameOverScreen] Cannot restart because the active scene is invalid.");
                return;
            }

            SceneManager.LoadScene(activeScene.name);
        }

        public void ReturnToWorld()
        {
            Time.timeScale = 1f;

            if (string.IsNullOrWhiteSpace(worldSceneName))
            {
                Debug.LogError("[GameOverScreen] World scene name is empty.");
                return;
            }

            SceneManager.LoadScene(worldSceneName);
        }
    }
}
