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
        public event Action OnConstraintStarted;
        public event Action OnConstraintEnded;

        public bool IsConstrained { get; private set; }
        public string LastDebugMessage { get; private set; } = "Constraint: idle";

        public void BeginConstraint()
        {
            if (IsConstrained)
                return;

            IsConstrained = true;
            LastDebugMessage = "Constraint: start";
            OnConstraintStart?.Invoke();
            OnConstraintStarted?.Invoke();
        }

        public void EndConstraint()
        {
            if (!IsConstrained)
                return;

            IsConstrained = false;
            LastDebugMessage = "Constraint: end";
            OnConstraintEnd?.Invoke();
            OnConstraintEnded?.Invoke();
        }

        public void SetConstraintActive(bool active)
        {
            if (active)
                BeginConstraint();
            else
                EndConstraint();
        }
    }
}
