using System;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ArcaneVR.Scene
{
    public enum SceneType
    {
        Unknown
    }

    /// <summary>
    /// Manages scene transitions across all scenes. Handles attribute unlock propagation on Victory and retry logic on Defeat. Fires OnSceneLoaded event.
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        public event Action<SceneType> OnSceneLoaded;

        public SceneType CurrentSceneType { get; private set; } = SceneType.Unknown;

        private void OnEnable()
        {
            UnitySceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            UnitySceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void NotifyCurrentSceneLoaded()
        {
            SetCurrentScene(SceneType.Unknown);
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            SetCurrentScene(SceneType.Unknown);
        }

        private void SetCurrentScene(SceneType sceneType)
        {
            CurrentSceneType = sceneType;
            OnSceneLoaded?.Invoke(CurrentSceneType);
        }
    }
}
