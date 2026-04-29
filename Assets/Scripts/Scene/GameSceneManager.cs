using System;
using UnityEngine;

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

        // TODO: Implement
    }
}
