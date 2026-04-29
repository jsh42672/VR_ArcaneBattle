using UnityEngine;

namespace ArcaneVR.Core
{
    /// <summary>
    /// ScriptableObject. Stores which elemental attributes have been unlocked.
    /// Persists across scenes via GameManager.
    /// </summary>
    [CreateAssetMenu(fileName = "AttributeUnlockData", menuName = "ArcaneVR/AttributeUnlockData")]
    public class AttributeUnlockData : ScriptableObject
    {
        public bool fireUnlocked;
        public bool iceUnlocked;
        public bool thunderUnlocked;
    }
}
