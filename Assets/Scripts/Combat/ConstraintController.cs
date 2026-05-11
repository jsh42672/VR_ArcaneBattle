using System;
using ArcaneVR.Input;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class ConstraintController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private HandPullMovementController handPullMovementController;

        [SerializeField]
        private SpellCaster spellCaster;

        [SerializeField]
        private Transform movementRoot;

        [SerializeField]
        private Transform headTransform;

        [SerializeField]
        private Transform arenaCenter;

        [Header("Constraint")]
        [SerializeField]
        private float defaultDuration = 5f;

        [SerializeField]
        private string lockReason = "Boss Charge Constraint";

        [SerializeField]
        private string responseLockReason = "Boss Attack Response Constraint";

        [SerializeField]
        private bool centerPlayerForResponse = true;

        [SerializeField]
        private float centerMoveDuration = 0.25f;

        [SerializeField]
        private bool centerOnHorizontalPlaneOnly = true;

        [SerializeField]
        private string arenaCenterMarkerName = "CombatZone_Marker";

        public event Action OnConstraintStarted;
        public event Action OnConstraintEnded;

        private bool isConstrained;
        private float remainingTime;
        private bool suppressesCasting;
        private string activeLockReason;
        private Coroutine centerRoutine;

        public bool IsConstrained => isConstrained;
        public float RemainingTime => remainingTime;
        public bool SuppressesCasting => suppressesCasting;
        public string ActiveLockReason => activeLockReason;

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
            BeginConstraint(duration, false, false, lockReason);
        }

        public void BeginResponseConstraint(float duration)
        {
            BeginConstraint(duration, true, centerPlayerForResponse, responseLockReason);
        }

        public void BeginConstraint(float duration, bool suppressCasting, bool movePlayerToCenter, string reason)
        {
            ResolveReferences();

            if (isConstrained && !string.IsNullOrWhiteSpace(activeLockReason))
            {
                if (handPullMovementController != null)
                    handPullMovementController.SetMovementSuppressed(false, activeLockReason);

                if (spellCaster != null && suppressesCasting)
                    spellCaster.SetCastingSuppressed(false, activeLockReason);
            }

            isConstrained = true;
            remainingTime = Mathf.Max(0.1f, duration);
            suppressesCasting = suppressCasting;
            activeLockReason = string.IsNullOrWhiteSpace(reason) ? lockReason : reason;

            if (handPullMovementController != null)
            {
                handPullMovementController.SetMovementSuppressed(true, activeLockReason);
            }

            if (spellCaster != null)
                spellCaster.SetCastingSuppressed(suppressesCasting, activeLockReason);

            if (movePlayerToCenter)
                MovePlayerToArenaCenter();

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

            if (centerRoutine != null)
            {
                StopCoroutine(centerRoutine);
                centerRoutine = null;
            }

            if (handPullMovementController != null)
            {
                handPullMovementController.SetMovementSuppressed(false, activeLockReason);
            }

            if (spellCaster != null)
                spellCaster.SetCastingSuppressed(false, activeLockReason);

            suppressesCasting = false;
            activeLockReason = string.Empty;

            OnConstraintEnded?.Invoke();
        }

        private void ResolveReferences()
        {
            if (handPullMovementController == null)
            {
                handPullMovementController = FindAnyObjectByType<HandPullMovementController>();
            }

            if (spellCaster == null)
            {
                spellCaster = FindAnyObjectByType<SpellCaster>();
            }

            if (headTransform == null)
            {
                headTransform = ArcanePlayerRigResolver.FindHeadTransform();
            }

            if (movementRoot == null)
            {
                movementRoot = ResolveMovementRoot();
            }

            if (arenaCenter == null)
            {
                var marker = GameObject.Find(arenaCenterMarkerName);
                if (marker != null)
                    arenaCenter = marker.transform;
            }
        }

        private Transform ResolveMovementRoot()
        {
            var rigRoot = ArcanePlayerRigResolver.FindPlayerRigTransform();
            if (rigRoot == null)
                return null;

            var cameraRig = rigRoot.GetComponent<OVRCameraRig>() ?? rigRoot.GetComponentInChildren<OVRCameraRig>(true);
            if (cameraRig != null)
            {
                cameraRig.EnsureGameObjectIntegrity();
                if (cameraRig.trackingSpace != null)
                    return cameraRig.trackingSpace;
            }

            var trackingSpace = rigRoot.Find("TrackingSpace");
            return trackingSpace != null ? trackingSpace : rigRoot;
        }

        private void MovePlayerToArenaCenter()
        {
            if (movementRoot == null || headTransform == null || arenaCenter == null)
                return;

            var delta = arenaCenter.position - headTransform.position;
            if (centerOnHorizontalPlaneOnly)
                delta = Vector3.ProjectOnPlane(delta, Vector3.up);

            if (delta.sqrMagnitude < 0.0001f)
                return;

            if (centerRoutine != null)
                StopCoroutine(centerRoutine);

            if (centerMoveDuration <= 0.01f)
            {
                movementRoot.position += delta;
                return;
            }

            centerRoutine = StartCoroutine(MoveRootByDelta(delta, centerMoveDuration));
        }

        private System.Collections.IEnumerator MoveRootByDelta(Vector3 delta, float duration)
        {
            var start = movementRoot.position;
            var target = start + delta;
            var elapsed = 0f;

            while (elapsed < duration && movementRoot != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                movementRoot.position = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }

            if (movementRoot != null)
                movementRoot.position = target;

            centerRoutine = null;
        }
    }
}
