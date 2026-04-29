using System;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Activates during boss charging pattern. Lerps player to optimal distance then locks MovementController. Fires OnConstraintStart and OnConstraintEnd events.
    /// </summary>
    public class ConstraintController : MonoBehaviour
    {
        public event Action OnConstraintStart;
        public event Action OnConstraintEnd;

        // TODO: Implement
    }
}
