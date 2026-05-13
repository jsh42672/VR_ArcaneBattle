using System;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ArcaneVR.Scene
{
    public enum SceneType
    {
        Unknown,
        Main,
        Tutorial,
        World,
        WorldMain,
        Battle,
        FireColoseum,
        IceColoseum,
        ElectricColoseum,
        Result
    }

    /// <summary>
    /// Central scene transition API for prototype and presentation flow.
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }

        public event Action<SceneType> OnSceneLoaded;

        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private SceneType defaultWorldScene = SceneType.WorldMain;
        [SerializeField] private SceneType defaultCombatScene = SceneType.FireColoseum;

        public SceneType CurrentSceneType { get; private set; } = SceneType.Unknown;
        public SceneType LastWorldScene { get; private set; } = SceneType.WorldMain;
        public SceneType LastCombatScene { get; private set; } = SceneType.FireColoseum;
        public string LastTransitionStatus { get; private set; } = "Scene: idle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateSingleton()
        {
            if (Instance != null || FindAnyObjectByType<GameSceneManager>() != null)
                return;

            var host = new GameObject("GameSceneManager");
            host.AddComponent<GameSceneManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            UnitySceneManager.sceneLoaded += HandleSceneLoaded;
            NotifyCurrentSceneLoaded();
        }

        private void OnDisable()
        {
            UnitySceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void NotifyCurrentSceneLoaded()
        {
            SetCurrentScene(ResolveSceneType(UnitySceneManager.GetActiveScene().name));
        }

        public bool LoadMain()
        {
            return LoadScene(SceneType.Main);
        }

        public bool LoadTutorial()
        {
            return LoadScene(SceneType.Tutorial);
        }

        public bool LoadWorld()
        {
            return LoadScene(defaultWorldScene);
        }

        public bool LoadBattle()
        {
            return LoadScene(defaultCombatScene);
        }

        public bool LoadFireColoseum()
        {
            return LoadScene(SceneType.FireColoseum);
        }

        public bool LoadIceColoseum()
        {
            return LoadScene(SceneType.IceColoseum);
        }

        public bool LoadElectricColoseum()
        {
            return LoadScene(SceneType.ElectricColoseum);
        }

        public bool RetryCombat()
        {
            return LoadScene(LastCombatScene == SceneType.Unknown ? defaultCombatScene : LastCombatScene);
        }

        public bool ReturnToWorld()
        {
            return LoadScene(LastWorldScene == SceneType.Unknown ? defaultWorldScene : LastWorldScene);
        }

        public bool CompleteCombat(ElementType unlockedElement)
        {
            if (unlockedElement != ElementType.None && GameManager.Instance != null)
                GameManager.Instance.UnlockElement(unlockedElement);

            return ReturnToWorld();
        }

        public bool LoadScene(SceneType sceneType)
        {
            var sceneName = GetSceneName(sceneType);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                LastTransitionStatus = $"Scene: no mapping for {sceneType}";
                Debug.LogWarning($"[GameSceneManager] No scene mapping for {sceneType}.");
                return false;
            }

            return LoadSceneByName(sceneName);
        }

        public bool LoadSceneByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LastTransitionStatus = $"Scene missing: {sceneName}";
                Debug.LogWarning($"[GameSceneManager] Scene is not in Build Settings: {sceneName}");
                return false;
            }

            LastTransitionStatus = $"Loading: {sceneName}";
            UnitySceneManager.LoadScene(sceneName);
            return true;
        }

        public static SceneType ResolveSceneType(string sceneName)
        {
            return sceneName switch
            {
                "Main" => SceneType.Main,
                "Tutorial" => SceneType.Tutorial,
                "World" => SceneType.World,
                "World_main" => SceneType.WorldMain,
                "FireColoseum" => SceneType.FireColoseum,
                "IceColoseum" => SceneType.IceColoseum,
                "ElectricColoseum" => SceneType.ElectricColoseum,
                "Result" => SceneType.Result,
                _ => SceneType.Unknown
            };
        }

        public static string GetSceneName(SceneType sceneType)
        {
            return sceneType switch
            {
                SceneType.Main => "Main",
                SceneType.Tutorial => "Tutorial",
                SceneType.World => "World",
                SceneType.WorldMain => "World_main",
                SceneType.Battle => "FireColoseum",
                SceneType.FireColoseum => "FireColoseum",
                SceneType.IceColoseum => "IceColoseum",
                SceneType.ElectricColoseum => "ElectricColoseum",
                SceneType.Result => "Result",
                _ => string.Empty
            };
        }

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            SetCurrentScene(ResolveSceneType(scene.name));
        }

        private void SetCurrentScene(SceneType sceneType)
        {
            CurrentSceneType = sceneType;

            if (sceneType == SceneType.World || sceneType == SceneType.WorldMain)
                LastWorldScene = sceneType;
            else if (sceneType == SceneType.FireColoseum ||
                     sceneType == SceneType.IceColoseum ||
                     sceneType == SceneType.ElectricColoseum)
                LastCombatScene = sceneType;

            LastTransitionStatus = $"Loaded: {UnitySceneManager.GetActiveScene().name}";
            OnSceneLoaded?.Invoke(CurrentSceneType);
        }
    }
}
