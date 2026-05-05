using System.Collections.Generic;
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

        [Header("Rig References")]
        [SerializeField] private Transform xrOriginRoot;
        [SerializeField] private Transform headTransform;

        [Header("XR Hands")]
        [SerializeField] private bool autoRefreshHandSubsystem = true;
        [SerializeField] private float subsystemRefreshInterval = 0.5f;

        [Header("Fist Detection")]
        [SerializeField] private float fingerTipToPalmThreshold = 0.085f;
        [SerializeField] private int requiredClosedFingerCount = 3;

        [Header("Pull Movement")]
        [SerializeField] private float pullMultiplier = 1.2f;
        [SerializeField] private float maxMoveSpeed = 3.0f;
        [SerializeField] private float handMoveDeadZone = 0.003f;
        [SerializeField] private bool horizontalOnly = true;
        [SerializeField] private bool requirePullTowardBody = false;
        [SerializeField] private float towardBodyDotThreshold = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLog = true;

        private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem handSubsystem;

        private PullHand activeHand = PullHand.None;
        private Vector3 previousHandWorldPosition;
        private Vector3 lastMoveDelta;
        private string lastDebugMessage = "Idle";
        private float nextSubsystemRefreshTime;

        public bool IsPulling => activeHand != PullHand.None;
        public string ActiveHandName => activeHand.ToString();
        public Vector3 LastMoveDelta => lastMoveDelta;
        public string LastDebugMessage => lastDebugMessage;

        public bool HasLeftHand => IsHandTracked(PullHand.Left);
        public bool HasRightHand => IsHandTracked(PullHand.Right);
        public bool IsLeftFist => IsFist(PullHand.Left);
        public bool IsRightFist => IsFist(PullHand.Right);
        public bool HasHandSubsystem => handSubsystem != null;
        public bool IsHandSubsystemRunning => handSubsystem != null && handSubsystem.running;

        private void Awake()
        {
            ResolveRigReferences();
            RefreshHandSubsystem();
        }

        private void Update()
        {
            if (xrOriginRoot == null || headTransform == null)
            {
                ResolveRigReferences();
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
                return;
            }

            UpdateHandPullMovement();
        }

        private void ResolveRigReferences()
        {
            if (xrOriginRoot == null)
            {
                GameObject origin = GameObject.Find("XR Origin");

                if (origin == null)
                {
                    origin = GameObject.Find("XROriginCameraRig");
                }

                xrOriginRoot = origin != null ? origin.transform : transform;
            }

            if (headTransform == null)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                {
                    headTransform = mainCamera.transform;
                }
                else if (xrOriginRoot != null)
                {
                    Transform centerEye = xrOriginRoot.Find("TrackingSpace/CenterEyeAnchor");
                    headTransform = centerEye != null ? centerEye : transform;
                }
            }
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
                if (rightFist)
                {
                    BeginPull(PullHand.Right);
                }
                else if (leftFist)
                {
                    BeginPull(PullHand.Left);
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
            if (!TryGetPalmWorldPose(hand, out Pose palmPose))
            {
                return;
            }

            activeHand = hand;
            previousHandWorldPosition = palmPose.position;
            lastMoveDelta = Vector3.zero;
            lastDebugMessage = $"Pull Start: {hand}";

            if (showDebugLog)
            {
                Debug.Log($"[HandPullMovement] {lastDebugMessage}");
            }
        }

        private void ContinuePull()
        {
            if (!TryGetPalmWorldPose(activeHand, out Pose palmPose))
            {
                EndPull();
                return;
            }

            Vector3 currentHandWorldPosition = palmPose.position;
            Vector3 handWorldDelta = currentHandWorldPosition - previousHandWorldPosition;

            if (horizontalOnly)
            {
                handWorldDelta = Vector3.ProjectOnPlane(handWorldDelta, Vector3.up);
            }

            if (handWorldDelta.magnitude < handMoveDeadZone)
            {
                lastMoveDelta = Vector3.zero;
                previousHandWorldPosition = currentHandWorldPosition;
                return;
            }

            if (requirePullTowardBody && !IsPullingTowardBody(currentHandWorldPosition, handWorldDelta))
            {
                lastMoveDelta = Vector3.zero;
                lastDebugMessage = $"Not pulling toward body: {activeHand}";
                previousHandWorldPosition = currentHandWorldPosition;
                return;
            }

            Vector3 moveDelta = -handWorldDelta * pullMultiplier;
            float maxDistanceThisFrame = maxMoveSpeed * Time.deltaTime;

            if (moveDelta.magnitude > maxDistanceThisFrame)
            {
                moveDelta = moveDelta.normalized * maxDistanceThisFrame;
            }

            if (xrOriginRoot != null)
            {
                xrOriginRoot.position += moveDelta;
            }

            lastMoveDelta = moveDelta;
            previousHandWorldPosition = currentHandWorldPosition;

            lastDebugMessage =
                $"Pulling: {activeHand}, Move=({moveDelta.x:F3}, {moveDelta.y:F3}, {moveDelta.z:F3})";
        }

        private void EndPull()
        {
            lastDebugMessage = $"Pull End: {activeHand}";
            lastMoveDelta = Vector3.zero;

            if (showDebugLog)
            {
                Debug.Log($"[HandPullMovement] {lastDebugMessage}");
            }

            activeHand = PullHand.None;
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

            if (!TryGetJointWorldPose(xrHand, XRHandJointID.Palm, out Pose palmPose))
            {
                if (!TryGetRootWorldPose(xrHand, out palmPose))
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

        private bool IsFingerClosed(XRHand hand, XRHandJointID fingerTipId, Vector3 palmWorldPosition)
        {
            if (!TryGetJointWorldPose(hand, fingerTipId, out Pose fingerTipPose))
            {
                return false;
            }

            float distance = Vector3.Distance(fingerTipPose.position, palmWorldPosition);
            return distance <= fingerTipToPalmThreshold;
        }

        private bool TryGetPalmWorldPose(PullHand hand, out Pose palmPose)
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

            if (TryGetJointWorldPose(xrHand, XRHandJointID.Palm, out palmPose))
            {
                return true;
            }

            return TryGetRootWorldPose(xrHand, out palmPose);
        }

        private bool TryGetJointWorldPose(XRHand hand, XRHandJointID jointId, out Pose worldPose)
        {
            XRHandJoint joint = hand.GetJoint(jointId);

            if (!joint.TryGetPose(out Pose localPose))
            {
                worldPose = Pose.identity;
                return false;
            }

            worldPose = ToWorldPose(localPose.position, localPose.rotation);
            return true;
        }

        private bool TryGetRootWorldPose(XRHand hand, out Pose worldPose)
        {
            if (!hand.isTracked)
            {
                worldPose = Pose.identity;
                return false;
            }

            worldPose = ToWorldPose(hand.rootPose.position, hand.rootPose.rotation);
            return true;
        }

        private Pose ToWorldPose(Vector3 localPosition, Quaternion localRotation)
        {
            Transform origin = xrOriginRoot != null ? xrOriginRoot : transform;
            return new Pose(origin.TransformPoint(localPosition), origin.rotation * localRotation);
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
