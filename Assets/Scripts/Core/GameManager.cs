using UnityEngine;

namespace ArcaneVR.Core
{
    /// <summary>
    /// Singleton. DontDestroyOnLoad.
    /// Holds global game state: unlocked attributes, current scene, session data.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // TODO: Add unlocked attributes, scene state, session data
    }
}
