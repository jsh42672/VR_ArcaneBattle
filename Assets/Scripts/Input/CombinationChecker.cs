using System;
using UnityEngine;

namespace ArcaneVR.Input
{
    public enum SpellId
    {
        Unknown
    }

    /// <summary>
    /// Receives two Pose IDs from GestureDetector and validates two-hand combination within a 0.5s window. Fires OnCombinationSuccess or OnCombinationFail events.
    /// </summary>
    public class CombinationChecker : MonoBehaviour
    {
        public event Action<SpellId> OnCombinationSuccess;
        public event Action OnCombinationFail;

        // TODO: Implement
    }
}
