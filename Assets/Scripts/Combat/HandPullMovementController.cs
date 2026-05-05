using UnityEngine;

namespace ArcaneVR.Player
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
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private Transform headTransform;

        [Header("Runtime Hand References")]
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRHand rightHand;
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;

        [Header("Auto Find")]
        [SerializeField] private bool autoFindRuntimeHands = true;
        [SerializeField] private float handFindRetryInterval = 0.5f;

        [Header("Fist Detection")]
        [SerializeField] private float fistFingerThreshold = 0.65f;
        [SerializeField] private int requiredClosedFingerCount = 3;

        [Header("Pull Movement")]
        [SerializeField] private float pullMultiplier = 1.2f;
        [SerializeField] private float maxMoveSpeed = 3.0f;
        [SerializeField] private float handMoveDeadZone = 0.003f;
        [SerializeField] private bool horizontalOnly = true;
        [SerializeField] private bool requirePullTowardBody = true;
        [SerializeField] private float towardBodyDotThreshold = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLog = true;

        private PullHand activeHand = PullHand.None;
        private Vector3 previousHandLocalPosition;
        private Vector3 lastMoveDelta;
        private string lastDebugMessage = "Idle";

        private float nextHandFindTime;

        public bool IsPulling => activeHand != PullHand.None;
        public string ActiveHandName => activeHand.ToString();
        public Vector3 LastMoveDelta => lastMoveDelta;
        public string LastDebugMessage => lastDebugMessage;

        public bool HasLeftHand => leftHand != null && leftHandTransform != null;
        public bool HasRightHand => rightHand != null && rightHandTransform != null;
        public bool IsLeftFist => IsFist(leftHand);
        public bool IsRightFist => IsFist(rightHand);

        private void Awake()
        {
            ResolveRigReferences();
        }

        private void Update()
        {
            if (autoFindRuntimeHands && Time.time >= nextHandFindTime)
            {
                nextHandFindTime = Time.time + handFindRetryInterval;
                TryFindRuntimeHands();
            }

            if (!HasRequiredReferences())
            {
                lastDebugMessage = "Waiting for runtime hand objects";
                lastMoveDelta = Vector3.zero;
                return;
            }

            UpdateHandPullMovement();
        }

        private void ResolveRigReferences()
        {
            if (xrOriginRoot == null)
            {
                xrOriginRoot = transform;
            }

            if (trackingSpace == null && xrOriginRoot != null)
            {
                Transform foundTrackingSpace = xrOriginRoot.Find("TrackingSpace");

                if (foundTrackingSpace != null)
                {
                    trackingSpace = foundTrackingSpace;
                }
            }

            if (headTransform == null && trackingSpace != null)
            {
                Transform foundHead = trackingSpace.Find("CenterEyeAnchor");

                if (foundHead != null)
                {
                    headTransform = foundHead;
                }
            }
        }

        private bool HasRequiredReferences()
        {
            return xrOriginRoot != null
                   && trackingSpace != null
                   && headTransform != null
                   && leftHand != null
                   && rightHand != null
                   && leftHandTransform != null
                   && rightHandTransform != null;
        }

        private void TryFindRuntimeHands()
        {
            if (trackingSpace == null)
            {
                ResolveRigReferences();

                if (trackingSpace == null)
                {
                    lastDebugMessage = "TrackingSpace not found";
                    return;
                }
            }

            OVRHand[] hands = trackingSpace.GetComponentsInChildren<OVRHand>(true);

            foreach (OVRHand hand in hands)
            {
                if (hand == null)
                    continue;

                if (hand.HandType == OVRHand.Hand.HandLeft)
                {
                    leftHand = hand;
                    leftHandTransform = hand.transform;
                }
                else if (hand.HandType == OVRHand.Hand.HandRight)
                {
                    rightHand = hand;
                    rightHandTransform = hand.transform;
                }
            }

            if (leftHand != null && rightHand != null)
            {
                lastDebugMessage = "Runtime hands found";

                if (showDebugLog)
                {
                    Debug.Log(
                        $"[HandPullMovement] Runtime hands found. " +
                        $"Left={leftHand.name}, Right={rightHand.name}"
                    );
                }
            }
        }

        private void UpdateHandPullMovement()
        {
            bool leftFist = IsFist(leftHand);
            bool rightFist = IsFist(rightHand);

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
            activeHand = hand;
            previousHandLocalPosition = GetHandLocalPosition(hand);
            lastMoveDelta = Vector3.zero;
            lastDebugMessage = $"Pull Start: {hand}";

            if (showDebugLog)
            {
                Debug.Log($"[HandPullMovement] {lastDebugMessage}");
            }
        }

        private void ContinuePull()
        {
            Vector3 currentHandLocalPosition = GetHandLocalPosition(activeHand);
            Vector3 handLocalDelta = currentHandLocalPosition - previousHandLocalPosition;

            if (handLocalDelta.magnitude < handMoveDeadZone)
            {
                lastMoveDelta = Vector3.zero;
                previousHandLocalPosition = currentHandLocalPosition;
                return;
            }

            Vector3 handWorldDelta = trackingSpace.TransformDirection(handLocalDelta);

            if (horizontalOnly)
            {
                handWorldDelta = Vector3.ProjectOnPlane(handWorldDelta, Vector3.up);
            }

            if (requirePullTowardBody && !IsPullingTowardBody(activeHand, handWorldDelta))
            {
                lastMoveDelta = Vector3.zero;
                lastDebugMessage = $"Not pulling toward body: {activeHand}";
                previousHandLocalPosition = currentHandLocalPosition;
                return;
            }

            Vector3 moveDelta = -handWorldDelta * pullMultiplier;

            float maxDistanceThisFrame = maxMoveSpeed * Time.deltaTime;

            if (moveDelta.magnitude > maxDistanceThisFrame)
            {
                moveDelta = moveDelta.normalized * maxDistanceThisFrame;
            }

            xrOriginRoot.position += moveDelta;

            lastMoveDelta = moveDelta;
            previousHandLocalPosition = currentHandLocalPosition;

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

        private Vector3 GetHandLocalPosition(PullHand hand)
        {
            Transform handTransform = GetHandTransform(hand);

            if (handTransform == null)
            {
                return Vector3.zero;
            }

            return trackingSpace.InverseTransformPoint(handTransform.position);
        }

        private Transform GetHandTransform(PullHand hand)
        {
            if (hand == PullHand.Left)
            {
                return leftHandTransform;
            }

            if (hand == PullHand.Right)
            {
                return rightHandTransform;
            }

            return null;
        }

        private bool IsPullingTowardBody(PullHand hand, Vector3 handWorldDelta)
        {
            Transform handTransform = GetHandTransform(hand);

            if (handTransform == null || headTransform == null)
            {
                return false;
            }

            Vector3 handToBody = headTransform.position - handTransform.position;

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

        private bool IsFist(OVRHand hand)
        {
            if (hand == null)
            {
                return false;
            }

            if (!hand.IsTracked || !hand.IsDataValid)
            {
                return false;
            }

            int closedFingerCount = 0;

            if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= fistFingerThreshold)
            {
                closedFingerCount++;
            }

            if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) >= fistFingerThreshold)
            {
                closedFingerCount++;
            }

            if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) >= fistFingerThreshold)
            {
                closedFingerCount++;
            }

            if (hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) >= fistFingerThreshold)
            {
                closedFingerCount++;
            }

            return closedFingerCount >= requiredClosedFingerCount;
        }
    }
}