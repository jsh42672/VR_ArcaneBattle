using UnityEngine;
using ArcaneVR.Spell;

namespace ArcaneVR.Core
{
    /// <summary>
    /// Singleton. DontDestroyOnLoad.
    /// Holds global game state: unlocked attributes, current scene, session data.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private AttributeUnlockData unlockData;

        public static GameManager Instance { get; private set; }
        public AttributeUnlockData UnlockData => unlockData;
        public bool fireUnlocked => unlockData != null && unlockData.fireUnlocked;
        public bool iceUnlocked => unlockData != null && unlockData.iceUnlocked;
        public bool thunderUnlocked => unlockData != null && unlockData.thunderUnlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null || FindAnyObjectByType<GameManager>() != null)
                return;

            new GameObject("GameManager").AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (unlockData == null)
                unlockData = ScriptableObject.CreateInstance<AttributeUnlockData>();

            DontDestroyOnLoad(gameObject);
        }

        public bool IsUnlocked(ElementType element)
        {
            if (unlockData == null)
                return element == ElementType.None;

            return element switch
            {
                ElementType.Fire => unlockData.fireUnlocked,
                ElementType.Ice => unlockData.iceUnlocked,
                ElementType.Thunder => unlockData.thunderUnlocked,
                _ => true
            };
        }

        public void UnlockElement(ElementType element)
        {
            if (unlockData == null)
                unlockData = ScriptableObject.CreateInstance<AttributeUnlockData>();

            switch (element)
            {
                case ElementType.Fire:
                    unlockData.fireUnlocked = true;
                    break;
                case ElementType.Ice:
                    unlockData.iceUnlocked = true;
                    break;
                case ElementType.Thunder:
                    unlockData.thunderUnlocked = true;
                    break;
            }
        }

        public void ResetUnlocks(bool keepFireUnlocked = false)
        {
            if (unlockData == null)
                unlockData = ScriptableObject.CreateInstance<AttributeUnlockData>();

            unlockData.fireUnlocked = keepFireUnlocked;
            unlockData.iceUnlocked = false;
            unlockData.thunderUnlocked = false;
        }
    }
}
