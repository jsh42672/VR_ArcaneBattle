using System.Collections.Generic;
using ArcaneVR.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Input
{
    public class HandPullMovementController : MonoBehaviour
    {
        private enum PullHand
        {
            None,
            Left,
            Right
        }

        private enum PullHandMode
        {
            LeftOnly,
            RightOnly,
            Either
        }

        [Header("Rig References")]
        [SerializeField] private Transform xrOriginRoot;
        [SerializeField] private Transform movementRoot;
        [SerializeField] private Transform headTransform;

        [Header("XR Hands")]
        [SerializeField] private bool autoRefreshHandSubsystem = true;
        [SerializeField] private float subsystemRefreshInterval = 0.5f;

        [Header("Fist Detection")]
        [SerializeField] private float fingerTipToPalmThreshold = 0.085f;
        [SerializeField] private int requiredClosedFingerCount = 3;

        [Header("Pull Movement")]
        [SerializeField] private PullHandMode pullHandMode = PullHandMode.LeftOnly;
        [SerializeField] private float pullMultiplier = 9.6f;
        [SerializeField] private float maxMoveSpeed = 24.0f;
        [SerializeField] private float handMoveDeadZone = 0.003f;
        [SerializeField] private bool horizontalOnly = true;
        [SerializeField] private bool applyMovementInLateUpdate = true;
        [SerializeField] private bool requirePullTowardBody = false;
        [SerializeField] private float towardBodyDotThreshold = 0.25f;

        [Header("Collision Movement")]
        [SerializeField] private bool useCharacterController = true;
        [SerializeField] private bool autoCreateCharacterController = true;
        [SerializeField] private bool allowTransformFallback = false;
        [SerializeField] private CharacterController characterController;

        [Header("Character Controller Shape")]
        [SerializeField] private bool updateControllerShapeFromHead = true;
        [SerializeField] private float controllerRadius = 0.28f;
        [SerializeField] private float controllerMinHeight = 1.0f;
        [SerializeField] private float controllerMaxHeight = 2.2f;
        [SerializeField] private float controllerSkinWidth = 0.05f;
        [SerializeField] private float controllerStepOffset = 0.35f;
        [SerializeField] private float controllerSlopeLimit = 55f;

        [Header("Grounding")]
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float groundedStickVelocity = -2.0f;
        [SerializeField] private float terminalFallSpeed = -20.0f;

        [Header("Player Height")]
        [SerializeField] private bool autoCorrectLowHeadHeight = true;
        [SerializeField] private float heightCorrectionDelay = 0.75f;
        [SerializeField] private float minimumHeadHeight = 0.85f;
        [SerializeField] private float fallbackHeadHeight = 1.45f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLog;

        private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();

        private XRHandSubsystem handSubsystem;
        private PullHand activeHand = PullHand.None;

        private Vector3 previousHandTrackingPosition;
        private Vector3 lastMoveDelta;
        private Vector3 pendingMoveDelta;

        private string lastDebugMessage = "Idle";
        private float nextSubsystemRefreshTime;
        private bool heightCorrectionApplied;

        private float verticalVelocity;
        private bool movementAppliedThisFrame;
        private bool warnedNoCharacterController;

        public bool IsPulling => activeHand != PullHand.None;
        public string ActiveHandName => activeHand.ToString();
        public Vector3 LastMoveDelta => lastMoveDelta;
        public string LastDebugMessage => lastDebugMessage;

        public bool IsMovementSuppressed { get; private set; }
        public string MovementSuppressionReason { get; private set; } = string.Empty;

        public bool HasLeftHand => IsHandTracked(PullHand.Left);
        public bool HasRightHand => IsHandTracked(PullHand.Right);
        public bool IsLeftFist => IsFist(PullHand.Left);
        public bool IsRightFist => IsFist(PullHand.Right);

        public bool HasHandSubsystem => handSubsystem != null;
        public bool IsHandSubsystemRunning => handSubsystem != null && handSubsystem.running;

        public void ConfigurePrototype(Transform rigRoot, Transform head)
        {
            xrOriginRoot = rigRoot != null ? rigRoot : xrOriginRoot;
            headTransform = head != null ? head : headTransform;

            ResolveRigReferences();
            ResolveCharacterController();
            RefreshHandSubsystem();
        }

        public void SetMovementSuppressed(bool suppressed, string reason)
        {
            if (IsMovementSuppressed == suppressed &&
                MovementSuppressionReason == (reason ?? string.Empty))
            {
                return;
            }

            IsMovementSuppressed = suppressed;
            MovementSuppressionReason = suppressed
                ? string.IsNullOrWhiteSpace(reason) ? "Suppressed" : reason
                : string.Empty;

            if (!suppressed)
            {
                lastDebugMessage = "Pull enabled";
                return;
            }

            if (activeHand != PullHand.None)
            {
                EndPull();
            }

            pendingMoveDelta = Vector3.zero;
            lastMoveDelta = Vector3.zero;
            verticalVelocity = 0f;
            lastDebugMessage = $"Pull locked: {MovementSuppressionReason}";
        }

        private void Awake()
        {
            ResolveRigReferences();
            ResolveCharacterController();
            RefreshHandSubsystem();
        }

        private void OnEnable()
        {
            movementAppliedThisFrame = false;
            pendingMoveDelta = Vector3.zero;
        }

        private void OnDisable()
        {
            activeHand = PullHand.None;
            pendingMoveDelta = Vector3.zero;
            lastMoveDelta = Vector3.zero;
            verticalVelocity = 0f;
        }

        private void Update()
        {
            movementAppliedThisFrame = false;

            if (xrOriginRoot == null || movementRoot == null || headTransform == null)
            {
                ResolveRigReferences();
                ResolveCharacterController();
            }

            if (useCharacterController && characterController == null)
            {
                ResolveCharacterController();
            }

            if (autoRefreshHandSubsystem && Time.time >= nextSubsystemRefreshTime)
            {
                nextSubsystemRefreshTime = Time.time + subsystemRefreshInterval;
                RefreshHandSubsystem();
            }

            if (handSubsystem == null || !handSubsystem.running)
            {
                lastDebugMessage = "Waiting for XRHandSubsystem";
                lastMoveDelta = Vector3.zero;

                ApplyPassiveGravityIfNeeded();
                return;
            }

            if (IsMovementSuppressed)
            {
                if (activeHand != PullHand.None)
                {
                    EndPull();
                }

                pendingMoveDelta = Vector3.zero;
                lastMoveDelta = Vector3.zero;
                verticalVelocity = 0f;
                lastDebugMessage = $"Pull locked: {MovementSuppressionReason}";
                return;
            }

            ApplyHeightCorrectionIfNeeded();
            UpdateHandPullMovement();

            if (!applyMovementInLateUpdate)
            {
                ApplyPassiveGravityIfNeeded();
            }
        }

        private void LateUpdate()
        {
            if (!applyMovementInLateUpdate)
            {
                return;
            }

            if (ShouldUseCharacterController())
            {
                ApplyMoveDelta(pendingMoveDelta);
                pendingMoveDelta = Vector3.zero;
                return;
            }

            if (pendingMoveDelta.sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            ApplyMoveDelta(pendingMoveDelta);
            pendingMoveDelta = Vector3.zero;
        }

        private void ResolveRigReferences()
        {
            if (xrOriginRoot == null)
            {
                xrOriginRoot = ArcanePlayerRigResolver.FindPlayerRigTransform() ?? transform;
            }

            movementRoot = ResolveMovementRoot(xrOriginRoot);

            if (headTransform == null)
            {
                headTransform = ArcanePlayerRigResolver.FindHeadTransform();

                if (headTransform == null && xrOriginRoot != null)
                {
                    Transform centerEye = xrOriginRoot.Find("TrackingSpace/CenterEyeAnchor");
                    headTransform = centerEye != null ? centerEye : transform;
                }
            }
        }

        private Transform ResolveMovementRoot(Transform rigRoot)
        {
            if (movementRoot != null)
            {
                return movementRoot;
            }

            if (rigRoot == null)
            {
                return transform;
            }

            OVRCameraRig ovrCameraRig = rigRoot.GetComponent<OVRCameraRig>();
            if (ovrCameraRig == null)
            {
                ovrCameraRig = rigRoot.GetComponentInChildren<OVRCameraRig>(true);
            }

            if (ovrCameraRig != null)
            {
                ovrCameraRig.EnsureGameObjectIntegrity();

                if (ovrCameraRig.trackingSpace != null)
                {
                    return ovrCameraRig.trackingSpace;
                }
            }

            Transform trackingSpace = rigRoot.Find("TrackingSpace");
            return trackingSpace != null ? trackingSpace : rigRoot;
        }

        private void ResolveCharacterController()
        {
            if (!useCharacterController)
            {
                return;
            }

            if (characterController != null)
            {
                ConfigureCharacterControllerDefaults();
                return;
            }

            Transform root = movementRoot != null ? movementRoot : xrOriginRoot;
            if (root == null)
            {
                root = transform;
            }

            characterController = root.GetComponent<CharacterController>();

            if (characterController == null && xrOriginRoot != null)
            {
                characterController = xrOriginRoot.GetComponent<CharacterController>();
            }

            if (characterController == null && root.parent != null)
            {
                characterController = root.GetComponentInParent<CharacterController>();
            }

            if (characterController == null)
            {
                characterController = root.GetComponentInChildren<CharacterController>(true);
            }

            if (characterController == null && autoCreateCharacterController)
            {
                characterController = root.gameObject.AddComponent<CharacterController>();
                lastDebugMessage = $"CharacterController created on {root.name}";
            }

            ConfigureCharacterControllerDefaults();
        }

        private void ConfigureCharacterControllerDefaults()
        {
            if (characterController == null)
            {
                return;
            }

            characterController.radius = Mathf.Max(0.05f, controllerRadius);
            characterController.skinWidth = Mathf.Max(0.01f, controllerSkinWidth);
            characterController.stepOffset = Mathf.Max(0f, controllerStepOffset);
            characterController.slopeLimit = Mathf.Clamp(controllerSlopeLimit, 0f, 89f);
            characterController.minMoveDistance = 0f;
            characterController.detectCollisions = true;
            characterController.enableOverlapRecovery = true;

            UpdateCharacterControllerShape();
        }

        private void UpdateCharacterControllerShape()
        {
            if (!updateControllerShapeFromHead || characterController == null)
            {
                return;
            }

            Transform controllerTransform = characterController.transform;

            float headLocalY = fallbackHeadHeight;
            float headLocalX = 0f;
            float headLocalZ = 0f;

            if (headTransform != null)
            {
                Vector3 headLocal = controllerTransform.InverseTransformPoint(headTransform.position);
                headLocalY = headLocal.y;
                headLocalX = headLocal.x;
                headLocalZ = headLocal.z;
            }

            float height = Mathf.Clamp(
                headLocalY,
                Mathf.Max(controllerMinHeight, controllerRadius * 2f),
                Mathf.Max(controllerMaxHeight, controllerMinHeight)
            );

            characterController.height = height;
            characterController.center = new Vector3(
                headLocalX,
                height * 0.5f,
                headLocalZ
            );
        }

        private void RefreshHandSubsystem()
        {
            if (handSubsystem != null && handSubsystem.running)
            {
                return;
            }

            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);

            handSubsystem = null;

            foreach (XRHandSubsystem subsystem in handSubsystems)
            {
                if (subsystem != null && subsystem.running)
                {
                    handSubsystem = subsystem;
                    lastDebugMessage = "XRHandSubsystem running";
                    return;
                }
            }

            if (handSubsystems.Count > 0)
            {
                handSubsystem = handSubsystems[0];
                lastDebugMessage = "XRHandSubsystem found but not running";
            }
            else
            {
                lastDebugMessage = "XRHandSubsystem not found";
            }
        }

        private void UpdateHandPullMovement()
        {
            bool leftFist = IsFist(PullHand.Left);
            bool rightFist = IsFist(PullHand.Right);

            if (activeHand == PullHand.None)
            {
                if (leftFist && IsPullHandAllowed(PullHand.Left))
                {
                    BeginPull(PullHand.Left);
                }
                else if (rightFist && IsPullHandAllowed(PullHand.Right))
                {
                    BeginPull(PullHand.Right);
                }

                return;
            }

            if (activeHand == PullHand.Left && !leftFist)
            {
                EndPull();
                return;
            }

            if (activeHand == PullHand.Right && !rightFist)
            {
                EndPull();
                return;
            }

            ContinuePull();
        }

        private void BeginPull(PullHand hand)
        {
            if (!TryGetPalmTrackingPose(hand, out Pose palmPose))
            {
                return;
            }

            activeHand = hand;
            previousHandTrackingPosition = palmPose.position;
            lastMoveDelta = Vector3.zero;
            lastDebugMessage = $"Pull Start: {hand}";

            if (showDebugLog)
            {
                Debug.Log($"[HandPullMovement] {lastDebugMessage}");
            }
        }

        private void ContinuePull()
        {
            if (!TryGetPalmTrackingPose(activeHand, out Pose palmPose))
            {
                EndPull();
                return;
            }

            Vector3 currentHandTrackingPosition = palmPose.position;
            Vector3 handTrackingDelta = currentHandTrackingPosition - previousHandTrackingPosition;
            Vector3 handWorldDelta = ToWorldVector(handTrackingDelta);

            if (horizontalOnly)
            {
                handWorldDelta = Vector3.ProjectOnPlane(handWorldDelta, Vector3.up);
            }

            if (handWorldDelta.magnitude < handMoveDeadZone)
            {
                lastMoveDelta = Vector3.zero;
                previousHandTrackingPosition = currentHandTrackingPosition;
                return;
            }

            Vector3 currentHandWorldPosition = ToWorldPoint(currentHandTrackingPosition);

            if (requirePullTowardBody && !IsPullingTowardBody(currentHandWorldPosition, handWorldDelta))
            {
                lastMoveDelta = Vector3.zero;
                lastDebugMessage = $"Not pulling toward body: {activeHand}";
                previousHandTrackingPosition = currentHandTrackingPosition;
                return;
            }

            Vector3 moveDelta = -handWorldDelta * pullMultiplier;

            if (horizontalOnly)
            {
                moveDelta = Vector3.ProjectOnPlane(moveDelta, Vector3.up);
            }

            float maxDistanceThisFrame = maxMoveSpeed * Time.deltaTime;
            if (moveDelta.magnitude > maxDistanceThisFrame)
            {
                moveDelta = moveDelta.normalized * maxDistanceThisFrame;
            }

            if (applyMovementInLateUpdate)
            {
                pendingMoveDelta += moveDelta;
            }
            else
            {
                ApplyMoveDelta(moveDelta);
            }

            lastMoveDelta = moveDelta;
            previousHandTrackingPosition = currentHandTrackingPosition;

            lastDebugMessage =
                $"Pulling: {activeHand}, Move=({moveDelta.x:F3}, {moveDelta.y:F3}, {moveDelta.z:F3})";
        }

        private void ApplyMoveDelta(Vector3 moveDelta)
        {
            if (xrOriginRoot == null)
            {
                return;
            }

            if (ShouldUseCharacterController())
            {
                ApplyCharacterControllerMove(moveDelta);
                return;
            }

            ApplyTransformFallback(moveDelta);
        }

        private bool ShouldUseCharacterController()
        {
            return useCharacterController &&
                   characterController != null &&
                   characterController.enabled &&
                   characterController.gameObject.activeInHierarchy;
        }

        private void ApplyCharacterControllerMove(Vector3 horizontalMoveDelta)
        {
            if (characterController == null)
            {
                return;
            }

            UpdateCharacterControllerShape();

            if (horizontalOnly)
            {
                horizontalMoveDelta = Vector3.ProjectOnPlane(horizontalMoveDelta, Vector3.up);
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedStickVelocity;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, terminalFallSpeed);
            }

            Vector3 verticalMoveDelta = Vector3.up * (verticalVelocity * Time.deltaTime);
            Vector3 finalMoveDelta = horizontalMoveDelta + verticalMoveDelta;

            CollisionFlags collisionFlags = characterController.Move(finalMoveDelta);
            movementAppliedThisFrame = true;

            if ((collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                verticalVelocity = groundedStickVelocity;
            }

            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }
        }

        private void ApplyPassiveGravityIfNeeded()
        {
            if (movementAppliedThisFrame)
            {
                return;
            }

            if (!ShouldUseCharacterController())
            {
                return;
            }

            ApplyCharacterControllerMove(Vector3.zero);
        }

        private void ApplyTransformFallback(Vector3 moveDelta)
        {
            if (!allowTransformFallback)
            {
                if (!warnedNoCharacterController)
                {
                    warnedNoCharacterController = true;
                    Debug.LogWarning(
                        "[HandPullMovement] CharacterController is missing or disabled. " +
                        "Transform fallback is disabled to prevent floating through uneven ground. " +
                        "Enable autoCreateCharacterController or assign a CharacterController."
                    );
                }

                lastDebugMessage = "Movement blocked: CharacterController missing";
                return;
            }

            Transform root = movementRoot != null ? movementRoot : xrOriginRoot;
            if (root == null)
            {
                return;
            }

            root.position += moveDelta;
            movementAppliedThisFrame = true;
        }

        private void EndPull()
        {
            lastDebugMessage = $"Pull End: {activeHand}";
            lastMoveDelta = Vector3.zero;
            pendingMoveDelta = Vector3.zero;

            if (showDebugLog)
            {
                Debug.Log($"[HandPullMovement] {lastDebugMessage}");
            }

            activeHand = PullHand.None;
        }

        private void ApplyHeightCorrectionIfNeeded()
        {
            if (!autoCorrectLowHeadHeight || heightCorrectionApplied)
            {
                return;
            }

            if (Time.timeSinceLevelLoad < heightCorrectionDelay)
            {
                return;
            }

            if (movementRoot == null || headTransform == null)
            {
                return;
            }

            float headY = headTransform.position.y;
            if (headY >= minimumHeadHeight)
            {
                heightCorrectionApplied = true;
                return;
            }

            float lift = fallbackHeadHeight - headY;
            if (lift <= 0f)
            {
                heightCorrectionApplied = true;
                return;
            }

            Transform target = characterController != null
                ? characterController.transform
                : movementRoot;

            target.position += Vector3.up * lift;
            heightCorrectionApplied = true;

            lastDebugMessage = $"Height corrected +{lift:F2}m";

            if (showDebugLog)
            {
                Debug.Log($"[HandPullMovement] {lastDebugMessage}");
            }
        }

        private bool IsPullHandAllowed(PullHand hand)
        {
            if (pullHandMode == PullHandMode.Either)
            {
                return true;
            }

            return (pullHandMode == PullHandMode.LeftOnly && hand == PullHand.Left) ||
                   (pullHandMode == PullHandMode.RightOnly && hand == PullHand.Right);
        }

        private bool IsPullingTowardBody(Vector3 handWorldPosition, Vector3 handWorldDelta)
        {
            if (headTransform == null)
            {
                return false;
            }

            Vector3 handToBody = headTransform.position - handWorldPosition;

            if (horizontalOnly)
            {
                handToBody = Vector3.ProjectOnPlane(handToBody, Vector3.up);
            }

            if (handToBody.sqrMagnitude < 0.0001f || handWorldDelta.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float dot = Vector3.Dot(handWorldDelta.normalized, handToBody.normalized);
            return dot >= towardBodyDotThreshold;
        }

        private bool IsHandTracked(PullHand hand)
        {
            if (handSubsystem == null || !handSubsystem.running)
            {
                return false;
            }

            XRHand xrHand = GetXRHand(hand);
            return xrHand.isTracked;
        }

        private bool IsFist(PullHand hand)
        {
            if (handSubsystem == null || !handSubsystem.running)
            {
                return false;
            }

            XRHand xrHand = GetXRHand(hand);

            if (!xrHand.isTracked)
            {
                return false;
            }

            if (!TryGetJointTrackingPose(xrHand, XRHandJointID.Palm, out Pose palmPose))
            {
                if (!TryGetRootTrackingPose(xrHand, out palmPose))
                {
                    return false;
                }
            }

            int closedFingerCount = 0;

            if (IsFingerClosed(xrHand, XRHandJointID.IndexTip, palmPose.position))
            {
                closedFingerCount++;
            }

            if (IsFingerClosed(xrHand, XRHandJointID.MiddleTip, palmPose.position))
            {
                closedFingerCount++;
            }

            if (IsFingerClosed(xrHand, XRHandJointID.RingTip, palmPose.position))
            {
                closedFingerCount++;
            }

            if (IsFingerClosed(xrHand, XRHandJointID.LittleTip, palmPose.position))
            {
                closedFingerCount++;
            }

            return closedFingerCount >= requiredClosedFingerCount;
        }

        private bool IsFingerClosed(XRHand hand, XRHandJointID fingerTipId, Vector3 palmTrackingPosition)
        {
            if (!TryGetJointTrackingPose(hand, fingerTipId, out Pose fingerTipPose))
            {
                return false;
            }

            float distance = Vector3.Distance(fingerTipPose.position, palmTrackingPosition);
            return distance <= fingerTipToPalmThreshold;
        }

        private bool TryGetPalmTrackingPose(PullHand hand, out Pose palmPose)
        {
            palmPose = Pose.identity;

            if (handSubsystem == null || !handSubsystem.running)
            {
                return false;
            }

            XRHand xrHand = GetXRHand(hand);

            if (!xrHand.isTracked)
            {
                return false;
            }

            if (TryGetJointTrackingPose(xrHand, XRHandJointID.Palm, out palmPose))
            {
                return true;
            }

            return TryGetRootTrackingPose(xrHand, out palmPose);
        }

        private bool TryGetJointTrackingPose(XRHand hand, XRHandJointID jointId, out Pose trackingPose)
        {
            XRHandJoint joint = hand.GetJoint(jointId);

            if (!joint.TryGetPose(out Pose localPose))
            {
                trackingPose = Pose.identity;
                return false;
            }

            trackingPose = localPose;
            return true;
        }

        private bool TryGetRootTrackingPose(XRHand hand, out Pose trackingPose)
        {
            if (!hand.isTracked)
            {
                trackingPose = Pose.identity;
                return false;
            }

            trackingPose = hand.rootPose;
            return true;
        }

        private Vector3 ToWorldPoint(Vector3 localPosition)
        {
            Transform origin = movementRoot != null
                ? movementRoot
                : xrOriginRoot != null
                    ? xrOriginRoot
                    : transform;

            return origin.TransformPoint(localPosition);
        }

        private Vector3 ToWorldVector(Vector3 localVector)
        {
            Transform origin = movementRoot != null
                ? movementRoot
                : xrOriginRoot != null
                    ? xrOriginRoot
                    : transform;

            return origin.TransformVector(localVector);
        }

        private XRHand GetXRHand(PullHand hand)
        {
            if (handSubsystem == null)
            {
                return default;
            }

            if (hand == PullHand.Left)
            {
                return handSubsystem.leftHand;
            }

            if (hand == PullHand.Right)
            {
                return handSubsystem.rightHand;
            }

            return default;
        }
    }
}