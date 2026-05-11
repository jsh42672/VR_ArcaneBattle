using System;
using ArcaneVR.Input;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class ConstraintController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private HandPullMovementController handPullMovementController;

        [Header("Constraint")]
        [SerializeField]
        private float defaultDuration = 5f;

        [SerializeField]
        private string lockReason = "Boss Charge Constraint";

        public event Action OnConstraintStarted;
        public event Action OnConstraintEnded;

        private bool isConstrained;
        private float remainingTime;

        public bool IsConstrained => isConstrained;
        public float RemainingTime => remainingTime;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (isConstrained)
            {
                EndConstraint();
            }
        }

        private void Update()
        {
            if (!isConstrained)
            {
                return;
            }

            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
            {
                EndConstraint();
            }
        }

        public void BeginConstraint()
        {
            BeginConstraint(defaultDuration);
        }

        public void BeginConstraint(float duration)
        {
            ResolveReferences();

            isConstrained = true;
            remainingTime = Mathf.Max(0.1f, duration);

            if (handPullMovementController != null)
            {
                handPullMovementController.SetMovementSuppressed(true, lockReason);
            }

            OnConstraintStarted?.Invoke();
        }

        public void EndConstraint()
        {
            if (!isConstrained)
            {
                return;
            }

            isConstrained = false;
            remainingTime = 0f;

            if (handPullMovementController != null)
            {
                handPullMovementController.SetMovementSuppressed(false, string.Empty);
            }

            OnConstraintEnded?.Invoke();
        }

        private void ResolveReferences()
        {
            if (handPullMovementController == null)
            {
                handPullMovementController = FindAnyObjectByType<HandPullMovementController>();
            }
        }
    }
}
