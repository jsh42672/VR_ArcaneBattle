using System;
using ArcaneVR.Input;
using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Manages the barrier response window. Checks both hands are in Barrier pose,
    /// held for required duration with sufficient mana. Fires OnBarrierSuccess or OnBarrierFail.
    /// </summary>
    public class BarrierController : MonoBehaviour
    {
        [SerializeField] GestureDetector gestureDetector;
        [SerializeField] CombatManager combatManager;

        public event Action OnBarrierSuccess;
        public event Action OnBarrierFail;

        // TODO: Implement barrier window logic
    }
}
