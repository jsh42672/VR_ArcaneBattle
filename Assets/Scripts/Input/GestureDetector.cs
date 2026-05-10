using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Input
{
    public enum PoseId
    {
        None,
        Fist,
        Ok,
        Horn,
        OpenPalm,
        IndexPoint,
        FistPush,
        Combine
    }

    public struct FingerPoseDebug
    {
        public bool hasData;
        public bool thumbExtended;
        public bool indexExtended;
        public bool middleExtended;
        public bool ringExtended;
        public bool pinkyExtended;
        public float thumbCurl;
        public float indexCurl;
        public float middleCurl;
        public float ringCurl;
        public float pinkyCurl;

        public string ToCompactString()
        {
            if (!hasData)
                return "T:- I:- M:- R:- P:-";

            return $"T:{ToMark(thumbExtended)}{thumbCurl:0.00} I:{ToMark(indexExtended)}{indexCurl:0.00} M:{ToMark(middleExtended)}{middleCurl:0.00} R:{ToMark(ringExtended)}{ringCurl:0.00} P:{ToMark(pinkyExtended)}{pinkyCurl:0.00}";
        }

        private static string ToMark(bool value)
        {
            return value ? "O" : "X";
        }
    }

    public struct GunFingerState
    {
        public bool thumbOpen;
        public bool indexOpen;
        public bool middleNotFist;
        public bool ringClosed;
        public bool pinkyClosed;
    }

    /// <summary>
    /// Detects individual hand poses via Meta XR Hand Tracking API and outputs Pose IDs for left and right hands.
    /// </summary>
    public class GestureDetector : MonoBehaviour
    {
        [Header("Optional Meta XR Hand Components")]
        [SerializeField] private OVRHand leftOvrHand;
        [SerializeField] private OVRHand rightOvrHand;

        [Header("XR Hands Static Gesture Router")]
        [SerializeField] private bool useXrHandsStaticGestureRouter = true;
        [SerializeField] private GestureEventRouter gestureEventRouter;

        [Header("Quest Tuning")]
        [SerializeField] private HandPoseTuningData tuningData;

        [Header("Pose Timing")]
        [SerializeField] private float stablePoseDuration = 0.1f;
        [SerializeField] private float grimoireHoldDuration = 0.3f;
        [SerializeField] private float combineDistance = 0.16f;
        [SerializeField] private float pushVelocity = 0.45f;
        [SerializeField] private float combinePushCooldown = 0.7f;

        [Header("Debug Detection")]
        [SerializeField] private bool openPalmDetectionEnabled = true;
        [SerializeField] private bool preferXrHandShapeDetection = false;
        [SerializeField] private bool requireThumbExtendedForGun = true;
        [SerializeField] private float ovrFingerExtensionMargin = 0.025f;
        [SerializeField] private float forwardGunDotThreshold = 0.45f;
        [SerializeField] private float gunOcclusionGraceDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float gunThumbMaxCurl = 0.45f;
        [SerializeField, Range(0f, 1f)] private float gunIndexMaxCurl = 0.40f;
        [SerializeField, Range(0f, 1f)] private float gunForwardIndexMaxCurl = 0.52f;
        [SerializeField, Range(0f, 1f)] private float gunMiddleFistRejectCurl = 0.75f;
        [SerializeField, Range(0f, 1f)] private float gunRingMinCurl = 0.70f;
        [SerializeField, Range(0f, 1f)] private float gunPinkyMinCurl = 0.70f;
        [SerializeField, Range(0f, 1f)] private float gunThumbOpenExitCurl = 0.55f;
        [SerializeField, Range(0f, 1f)] private float gunIndexOpenExitCurl = 0.52f;
        [SerializeField, Range(0f, 1f)] private float gunMiddleNotFistEnterCurl = 0.68f;
        [SerializeField, Range(0f, 1f)] private float gunRingClosedExitCurl = 0.48f;
        [SerializeField, Range(0f, 1f)] private float gunPinkyClosedExitCurl = 0.48f;
        [SerializeField] private float gunIndexOpenAngle = 25f;
        [SerializeField] private float gunForwardIndexOpenAngle = 35f;
        [SerializeField] private float gunIndexOpenExitAngle = 42f;
        [SerializeField] private float gunThumbOpenAngle = 20f;
        [SerializeField] private float gunThumbOpenExitAngle = 35f;
        [SerializeField] private float fistThumbClosedAngle = 20f;
        [SerializeField] private float gunMiddleNotFistEnterAngle = 55f;
        [SerializeField] private float gunMiddleFistRejectAngle = 65f;
        [SerializeField] private float gunRingClosedAngle = 50f;
        [SerializeField] private float gunPinkyClosedAngle = 50f;
        [SerializeField] private float gunRingClosedExitAngle = 35f;
        [SerializeField] private float gunPinkyClosedExitAngle = 35f;
        [SerializeField, Min(1)] private int gunRequiredStableFrames = 3;

        [Header("Temporary Bone Angle Debug")]
        [SerializeField] private bool showBoneAngleDebug = false;
        [SerializeField] private bool debugRightHandAngles = true;
        [SerializeField] private bool showPrototypeDebugLog;

        [Header("Gesture Spell Prototype")]
        [SerializeField] private float prototypePoseHoldTime = 0.3f;
        [SerializeField] private float prototypeOpenMaxAngle = 25f;
        [SerializeField] private float prototypeThumbOpenMaxAngle = 20f;
        [SerializeField] private float prototypeThumbClosedMinAngle = 20f;
        [SerializeField] private float prototypeFingerClosedMinAngle = 50f;
        [SerializeField] private float prototypeLeftFistHoldTime = 0.2f;
        [SerializeField] private float prototypeLeftFistReleaseBuffer = 0.15f;
        [SerializeField] private float prototypeLowConfidenceGraceDuration = 0.2f;
        [SerializeField] private bool usePrototypeDistanceFallback = true;
        [SerializeField, Range(0f, 1f)] private float prototypeOpenCurlMax = 0.45f;
        [SerializeField, Range(0f, 1f)] private float prototypeThumbOpenCurlMax = 0.55f;
        [SerializeField, Range(0f, 1f)] private float prototypeThumbClosedCurlMin = 0.45f;
        [SerializeField, Range(0f, 1f)] private float prototypeFingerClosedCurlMin = 0.55f;

        public event Action<PoseId, PoseId> OnPoseDetected;
        public event Action OnGrimTrigger;
        public event Action<PoseType> OnRightPoseConfirmed;
        public event Action OnRightPoseCleared;
        public event Action OnLeftFistStart;
        public event Action OnLeftFistEnd;
        public event Action<PoseType> OnPoseConfirmed;
        public event Action OnPoseCleared;
        public event Action<bool, PoseType> OnHandPoseConfirmed;
        public event Action<bool> OnHandPoseCleared;
        public event Action OnCombinePushDetected;

        private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();

        private XRHandSubsystem handSubsystem;
        private PoseId candidateLeftPose = PoseId.None;
        private PoseId candidateRightPose = PoseId.None;
        private PoseId stableLeftPose = PoseId.None;
        private PoseId stableRightPose = PoseId.None;
        private float leftCandidateStartTime;
        private float rightCandidateStartTime;
        private float leftOpenPalmStartTime = -1f;
        private bool grimoireTriggeredForHold;
        private Vector3 previousLeftPalmPosition;
        private Vector3 previousRightPalmPosition;
        private bool hasPreviousLeftPalm;
        private bool hasPreviousRightPalm;
        private FingerPoseDebug leftFingerDebug;
        private FingerPoseDebug rightFingerDebug;
        private float lastLeftGunTime = -999f;
        private float lastRightGunTime = -999f;
        private int leftGunStableFrames;
        private int rightGunStableFrames;
        private int debugBoneLogFrame;
        private GunFingerState leftGunFingerState;
        private GunFingerState rightGunFingerState;
        private OVRSkeleton debugSkeletonLeft;
        private OVRSkeleton debugSkeletonRight;
        private PoseType leftPrototypeCandidatePose = PoseType.None;
        private PoseType rightPrototypeCandidatePose = PoseType.None;
        private PoseType leftPrototypeConfirmedPose = PoseType.None;
        private PoseType rightPrototypeConfirmedPose = PoseType.None;
        private float leftPrototypePoseHoldTimer;
        private float rightPrototypePoseHoldTimer;
        private float leftPrototypeLowConfidenceTimer;
        private float rightPrototypeLowConfidenceTimer;
        private bool leftFistMovementConfirmed;
        private float leftFistMovementHoldTimer;
        private float leftFistMovementReleaseTimer;
        private string leftPrototypeDebug = "L: waiting";
        private string rightPrototypeDebug = "R: waiting";
        private int debugBindingLogFrame;
        private bool routerEventsSubscribed;
        private Vector3 previousCombineMidpoint;
        private bool hasPreviousCombineMidpoint;
        private float lastCombinePushTime = -999f;

        private struct PrototypeFingerCurls
        {
            public float thumb;
            public float index;
            public float middle;
            public float ring;
            public float pinky;
        }

        private float StablePoseDuration => tuningData != null ? tuningData.stablePoseDuration : stablePoseDuration;
        private float GrimoireHoldDuration => tuningData != null ? tuningData.grimoireHoldDuration : grimoireHoldDuration;
        private float PinchThreshold => tuningData != null ? tuningData.pinchThreshold : 0.65f;
        private float OpenThreshold => tuningData != null ? tuningData.openThreshold : 0.25f;
        private float OkThumbIndexThreshold => tuningData != null ? tuningData.okThumbIndexThreshold : 0.55f;
        private float OkTipDistance => tuningData != null ? tuningData.okTipDistance : 0.04f;
        private float ExtendedDistance => tuningData != null ? tuningData.extendedDistance : 0.105f;
        private float IndexExtendedDistance => tuningData != null ? tuningData.indexExtendedDistance : 0.11f;
        private float CurledDistance => tuningData != null ? tuningData.curledDistance : 0.025f;
        private float RelaxedDistance => tuningData != null ? tuningData.relaxedDistance : 0.1f;
        private float CombineDistance => tuningData != null ? tuningData.combineDistance : combineDistance;
        private float PushVelocity => tuningData != null ? tuningData.pushVelocity : pushVelocity;

        public PoseId CurrentLeftPose => stableLeftPose;
        public PoseId CurrentRightPose => stableRightPose;
        public FingerPoseDebug CurrentLeftFingerDebug => leftFingerDebug;
        public FingerPoseDebug CurrentRightFingerDebug => rightFingerDebug;
        public PoseType CurrentLeftPrototypePose => leftPrototypeConfirmedPose;
        public PoseType CurrentRightPrototypePose => rightPrototypeConfirmedPose;
        public string CurrentLeftHandStatus => IsRouterDrivingPoses
            ? $"[L] XRRouter pose:{gestureEventRouter.CurrentLeftPose} fist:{gestureEventRouter.LeftFistActive} events:{gestureEventRouter.ReceivedEventCount}"
            : BuildHandBindingLog("L", leftOvrHand, debugSkeletonLeft);
        public string CurrentRightHandStatus => IsRouterDrivingPoses
            ? $"[R] XRRouter pose:{gestureEventRouter.CurrentRightPose} events:{gestureEventRouter.ReceivedEventCount}"
            : BuildHandBindingLog("R", rightOvrHand, debugSkeletonRight);
        public string CurrentLeftPrototypeDebug => leftPrototypeDebug;
        public string CurrentRightPrototypeDebug => rightPrototypeDebug;
        public bool IsCombineCandidate { get; private set; }
        public float CurrentCombineForwardSpeed { get; private set; }

        private float WithOvrFingerMargin(float curlThreshold)
        {
            return Mathf.Clamp01(curlThreshold + Mathf.Max(0f, ovrFingerExtensionMargin));
        }

        private bool IsRouterDrivingPoses =>
            useXrHandsStaticGestureRouter &&
            gestureEventRouter != null &&
            gestureEventRouter.HasReceivedGestureEvent;

        private void Awake()
        {
            if (tuningData == null)
                tuningData = Resources.Load<HandPoseTuningData>("ArcaneVR/HandPoseTuningData");

            AutoBindOvrHands();
            ResolveGestureEventRouter();
        }

        private void OnEnable()
        {
            ResolveGestureEventRouter();
            SubscribeRouterEvents();
        }

        private void OnDisable()
        {
            UnsubscribeRouterEvents();
        }

        public void BindHands(OVRHand leftHand, OVRHand rightHand)
        {
            leftOvrHand = leftHand;
            rightOvrHand = rightHand;
        }

        public void BindGestureEventRouter(GestureEventRouter router)
        {
            if (gestureEventRouter == router)
                return;

            UnsubscribeRouterEvents();
            gestureEventRouter = router;
            SubscribeRouterEvents();
        }

        public void SetTuningData(HandPoseTuningData data)
        {
            tuningData = data;
        }

        public void InjectPose(PoseId left, PoseId right)
        {
            stableLeftPose = left;
            stableRightPose = right;
            candidateLeftPose = left;
            candidateRightPose = right;
            leftCandidateStartTime = Time.time;
            rightCandidateStartTime = Time.time;
            OnPoseDetected?.Invoke(left, right);
        }

        public void InjectGrimoireTrigger()
        {
            OnGrimTrigger?.Invoke();
        }

        private void Update()
        {
            if (leftOvrHand == null || rightOvrHand == null)
                AutoBindOvrHands();

            if (useXrHandsStaticGestureRouter)
            {
                ResolveGestureEventRouter();
                SubscribeRouterEvents();
                if (gestureEventRouter != null && gestureEventRouter.HasReceivedGestureEvent)
                {
                    SyncRouterStateForDebug();
                    LogHandBindingState();
                    return;
                }

                if (gestureEventRouter != null)
                {
                    rightPrototypeDebug = "XR Gesture Router: waiting for StaticHandGesture event; OVR fallback active";
                    leftPrototypeDebug = "XR Gesture Router: waiting for StaticHandGesture event; OVR fallback active";
                }
            }

            LogHandBindingState();
            RefreshHandSubsystem();

            var leftPose = DetectHandPose(true, out var leftPalmPosition);
            var rightPose = DetectHandPose(false, out var rightPalmPosition);

            if (UpdateCombineState(leftPalmPosition, rightPalmPosition))
            {
                leftPose = PoseId.Combine;
                rightPose = PoseId.Combine;
            }

            UpdateStablePose(true, leftPose);
            UpdateStablePose(false, rightPose);
            UpdateGrimoireTrigger(leftPose);

            if (stableLeftPose != PoseId.None || stableRightPose != PoseId.None)
                OnPoseDetected?.Invoke(stableLeftPose, stableRightPose);
        }

        private void ResolveGestureEventRouter()
        {
            if (!useXrHandsStaticGestureRouter || gestureEventRouter != null)
                return;

            gestureEventRouter = FindAnyObjectByType<GestureEventRouter>();
        }

        private void SubscribeRouterEvents()
        {
            if (!useXrHandsStaticGestureRouter || routerEventsSubscribed || gestureEventRouter == null)
                return;

            gestureEventRouter.OnRightPoseConfirmed += HandleRouterRightPoseConfirmed;
            gestureEventRouter.OnRightPoseCleared += HandleRouterRightPoseCleared;
            gestureEventRouter.OnLeftFistStart += HandleRouterLeftFistStart;
            gestureEventRouter.OnLeftFistEnd += HandleRouterLeftFistEnd;
            routerEventsSubscribed = true;
        }

        private void UnsubscribeRouterEvents()
        {
            if (!routerEventsSubscribed || gestureEventRouter == null)
                return;

            gestureEventRouter.OnRightPoseConfirmed -= HandleRouterRightPoseConfirmed;
            gestureEventRouter.OnRightPoseCleared -= HandleRouterRightPoseCleared;
            gestureEventRouter.OnLeftFistStart -= HandleRouterLeftFistStart;
            gestureEventRouter.OnLeftFistEnd -= HandleRouterLeftFistEnd;
            routerEventsSubscribed = false;
        }

        private void SyncRouterStateForDebug()
        {
            rightPrototypeCandidatePose = gestureEventRouter.CurrentRightPose;
            rightPrototypeConfirmedPose = gestureEventRouter.CurrentRightPose;
            leftPrototypeCandidatePose = gestureEventRouter.CurrentLeftPose;
            leftPrototypeConfirmedPose = gestureEventRouter.CurrentLeftPose;
            leftFistMovementConfirmed = gestureEventRouter.LeftFistActive;
            stableRightPose = ToPoseId(gestureEventRouter.CurrentRightPose);
            stableLeftPose = ToPoseId(gestureEventRouter.CurrentLeftPose);
            rightPrototypeDebug = gestureEventRouter.DebugStatus;
            leftPrototypeDebug = gestureEventRouter.LeftFistActive
                ? "XR Gesture Router: Left Fist active"
                : "XR Gesture Router: Left Fist none";
        }

        private void HandleRouterRightPoseConfirmed(PoseType pose)
        {
            rightPrototypeCandidatePose = pose;
            rightPrototypeConfirmedPose = pose;
            rightPrototypePoseHoldTimer = 0f;
            stableRightPose = ToPoseId(pose);
            candidateRightPose = stableRightPose;
            rightPrototypeDebug = $"XR Gesture Router: Right {pose}";

            OnRightPoseConfirmed?.Invoke(pose);
            OnHandPoseConfirmed?.Invoke(false, pose);
            OnPoseConfirmed?.Invoke(pose);
            OnPoseDetected?.Invoke(stableLeftPose, stableRightPose);
        }

        private void HandleRouterRightPoseCleared()
        {
            var hadPose = rightPrototypeConfirmedPose != PoseType.None || stableRightPose != PoseId.None;
            rightPrototypeCandidatePose = PoseType.None;
            rightPrototypeConfirmedPose = PoseType.None;
            rightPrototypePoseHoldTimer = 0f;
            stableRightPose = PoseId.None;
            candidateRightPose = PoseId.None;
            rightPrototypeDebug = "XR Gesture Router: Right none";

            if (!hadPose)
                return;

            OnRightPoseCleared?.Invoke();
            OnHandPoseCleared?.Invoke(false);
            OnPoseCleared?.Invoke();
            OnPoseDetected?.Invoke(stableLeftPose, stableRightPose);
        }

        private void HandleRouterLeftFistStart()
        {
            leftFistMovementConfirmed = true;
            leftPrototypeCandidatePose = PoseType.Fist;
            leftPrototypeConfirmedPose = PoseType.Fist;
            leftPrototypePoseHoldTimer = 0f;
            stableLeftPose = PoseId.Fist;
            candidateLeftPose = PoseId.Fist;
            leftPrototypeDebug = "XR Gesture Router: Left Fist active";

            OnLeftFistStart?.Invoke();
            OnHandPoseConfirmed?.Invoke(true, PoseType.Fist);
            OnPoseDetected?.Invoke(stableLeftPose, stableRightPose);
        }

        private void HandleRouterLeftFistEnd()
        {
            var hadFist = leftFistMovementConfirmed || stableLeftPose == PoseId.Fist;
            leftFistMovementConfirmed = false;
            leftPrototypeCandidatePose = PoseType.None;
            leftPrototypeConfirmedPose = PoseType.None;
            leftPrototypePoseHoldTimer = 0f;
            stableLeftPose = PoseId.None;
            candidateLeftPose = PoseId.None;
            leftPrototypeDebug = "XR Gesture Router: Left Fist none";

            if (!hadFist)
                return;

            OnLeftFistEnd?.Invoke();
            OnHandPoseCleared?.Invoke(true);
            OnPoseDetected?.Invoke(stableLeftPose, stableRightPose);
        }

        private static PoseId ToPoseId(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => PoseId.OpenPalm,
                PoseType.Fist => PoseId.Fist,
                PoseType.ThumbsUp => PoseId.Horn,
                _ => PoseId.None
            };
        }

        private void RefreshHandSubsystem()
        {
            if (handSubsystem != null && handSubsystem.running)
                return;

            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);

            foreach (var subsystem in handSubsystems)
            {
                if (subsystem.running)
                {
                    handSubsystem = subsystem;
                    return;
                }
            }
        }

        private PoseId DetectHandPose(bool isLeft, out Vector3 palmPosition)
        {
            palmPosition = Vector3.zero;

            if (preferXrHandShapeDetection)
            {
                var xrPose = DetectXrHandPose(isLeft, out palmPosition);
                if (xrPose != PoseId.None)
                    return xrPose;
            }

            var ovrPose = DetectOvrHandPose(isLeft, out palmPosition);
            if (ovrPose != PoseId.None)
                return ovrPose;

            return preferXrHandShapeDetection ? PoseId.None : DetectXrHandPose(isLeft, out palmPosition);
        }

        private PoseId DetectOvrHandPose(bool isLeft, out Vector3 palmPosition)
        {
            palmPosition = Vector3.zero;

            var hand = isLeft ? leftOvrHand : rightOvrHand;
            if (hand == null)
            {
                SetFingerDebug(isLeft, default);
                SetDebugSkeleton(isLeft, null);
                ClearPrototypePose(isLeft);
                UpdateLeftFistMovement(isLeft, false);
                return PoseId.None;
            }

            palmPosition = hand.transform.position;

            if (!hand.IsTracked || !hand.IsDataValid || hand.IsSystemGestureInProgress)
            {
                SetFingerDebug(isLeft, default);
                SetDebugSkeleton(isLeft, null);
                ClearPrototypePose(isLeft);
                UpdateLeftFistMovement(isLeft, false);
                ResetGunStableFrames(isLeft);
                return PoseId.None;
            }

            if (hand.HandConfidence != OVRHand.TrackingConfidence.High)
            {
                ClearPrototypePose(isLeft);
                UpdateLeftFistMovement(isLeft, false);
                ResetGunStableFrames(isLeft);
                return HasRecentGunPose(isLeft) ? PoseId.IndexPoint : PoseId.None;
            }

            var skeletonPose = DetectOvrSkeletonPose(hand, isLeft, palmPosition);
            if (skeletonPose != PoseId.None)
                return skeletonPose;

            var pinchStrengths = new[]
            {
                hand.GetFingerPinchStrength(OVRHand.HandFinger.Thumb),
                hand.GetFingerPinchStrength(OVRHand.HandFinger.Index),
                hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle),
                hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring),
                hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky)
            };

            return ResolvePoseFromPinches(isLeft, palmPosition, pinchStrengths);
        }

        private PoseId DetectOvrSkeletonPose(OVRHand hand, bool isLeft, Vector3 palmPosition)
        {
            var skeleton = hand.GetComponentInChildren<OVRSkeleton>(true);
            if (skeleton == null || !skeleton.IsInitialized || !skeleton.IsDataValid || skeleton.Bones == null)
            {
                SetFingerDebug(isLeft, default);
                SetDebugSkeleton(isLeft, null);
                ClearPrototypePose(isLeft);
                UpdateLeftFistMovement(isLeft, false);
                return PoseId.None;
            }

            SetDebugSkeleton(isLeft, skeleton);
            LogBoneAngles(skeleton, isLeft ? "L" : "R");
            UpdatePrototypePoseDetection(hand, skeleton, isLeft);

            if (!TryGetOvrBonePosition(skeleton, out var thumbBase, OVRSkeleton.BoneId.Hand_Thumb2, OVRSkeleton.BoneId.XRHand_ThumbProximal) ||
                !TryGetOvrBonePosition(skeleton, out var thumbDistal, OVRSkeleton.BoneId.Hand_Thumb3, OVRSkeleton.BoneId.XRHand_ThumbDistal) ||
                !TryGetOvrBonePosition(skeleton, out var thumbTip, OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.XRHand_ThumbTip) ||
                !TryGetOvrBonePosition(skeleton, out var indexBase, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal) ||
                !TryGetOvrBonePosition(skeleton, out var indexMiddle, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.XRHand_IndexIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var indexDistal, OVRSkeleton.BoneId.Hand_Index3, OVRSkeleton.BoneId.XRHand_IndexDistal) ||
                !TryGetOvrBonePosition(skeleton, out var indexTip, OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.XRHand_IndexTip) ||
                !TryGetOvrBonePosition(skeleton, out var middleBase, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal) ||
                !TryGetOvrBonePosition(skeleton, out var middleMiddle, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.XRHand_MiddleIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var middleDistal, OVRSkeleton.BoneId.Hand_Middle3, OVRSkeleton.BoneId.XRHand_MiddleDistal) ||
                !TryGetOvrBonePosition(skeleton, out var middleTip, OVRSkeleton.BoneId.Hand_MiddleTip, OVRSkeleton.BoneId.XRHand_MiddleTip) ||
                !TryGetOvrBonePosition(skeleton, out var ringBase, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal) ||
                !TryGetOvrBonePosition(skeleton, out var ringMiddle, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.XRHand_RingIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var ringDistal, OVRSkeleton.BoneId.Hand_Ring3, OVRSkeleton.BoneId.XRHand_RingDistal) ||
                !TryGetOvrBonePosition(skeleton, out var ringTip, OVRSkeleton.BoneId.Hand_RingTip, OVRSkeleton.BoneId.XRHand_RingTip) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyBase, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyMiddle, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.XRHand_LittleIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyDistal, OVRSkeleton.BoneId.Hand_Pinky3, OVRSkeleton.BoneId.XRHand_LittleDistal) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyTip, OVRSkeleton.BoneId.Hand_PinkyTip, OVRSkeleton.BoneId.XRHand_LittleTip))
            {
                SetFingerDebug(isLeft, default);
                return PoseId.None;
            }

            palmPosition = (indexBase + middleBase + ringBase + pinkyBase) * 0.25f;
            if (TryGetOvrBonePosition(skeleton, out var skeletonPalm, OVRSkeleton.BoneId.XRHand_Palm))
                palmPosition = skeletonPalm;

            var thumbCurl = CalculateThumbCurl(palmPosition, thumbBase, thumbDistal, thumbTip);
            var thumbAngle = GetBoneAngle(skeleton, OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.XRHand_ThumbMetacarpal);
            var indexAngle = GetBoneAngle(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal);
            var middleAngle = GetBoneAngle(skeleton, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal);
            var ringAngle = GetBoneAngle(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal);
            var pinkyAngle = GetBoneAngle(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal);
            var indexPositionCurl = CalculateFingerCurl(indexBase, indexMiddle, indexDistal, indexTip);
            var middlePositionCurl = CalculateFingerCurl(middleBase, middleMiddle, middleDistal, middleTip);
            var ringPositionCurl = CalculateFingerCurl(ringBase, ringMiddle, ringDistal, ringTip);
            var pinkyPositionCurl = CalculateFingerCurl(pinkyBase, pinkyMiddle, pinkyDistal, pinkyTip);
            var indexExtended = indexPositionCurl < WithOvrFingerMargin(gunIndexMaxCurl);
            var middleExtended = middlePositionCurl < WithOvrFingerMargin(gunMiddleFistRejectCurl);
            var ringExtended = ringPositionCurl < WithOvrFingerMargin(gunRingMinCurl);
            var pinkyExtended = pinkyPositionCurl < WithOvrFingerMargin(gunPinkyMinCurl);
            var pointerAimDirection = hand.IsPointerPoseValid && hand.PointerPose != null ? hand.PointerPose.forward : Vector3.zero;
            var indexDebugMaxCurl = IsGunPointingForward(indexBase, indexTip, middleBase, middleTip, pointerAimDirection)
                ? gunForwardIndexOpenAngle
                : gunIndexOpenAngle;

            SetFingerDebug(isLeft, BuildFingerAngleDebug(
                thumbAngle,
                indexAngle,
                middleAngle,
                ringAngle,
                pinkyAngle,
                indexDebugMaxCurl));

            if (TryResolveGunPoseFromAngles(
                    isLeft,
                    thumbAngle,
                    indexAngle,
                    middleAngle,
                    ringAngle,
                    pinkyAngle,
                    indexBase,
                    indexMiddle,
                    indexDistal,
                    indexTip,
                    middleBase,
                    middleMiddle,
                    middleDistal,
                    middleTip,
                    pointerAimDirection))
            {
                return PoseId.IndexPoint;
            }

            var thumbCurledForFist = thumbAngle > fistThumbClosedAngle;
            if (thumbCurledForFist && !indexExtended && !middleExtended && !ringExtended && !pinkyExtended)
                return ResolveFistOrPush(isLeft, palmPosition);

            if (openPalmDetectionEnabled && indexExtended && middleExtended && ringExtended && pinkyExtended)
                return PoseId.OpenPalm;

            return PoseId.None;
        }

        private static bool TryGetOvrBonePosition(OVRSkeleton skeleton, out Vector3 position, params OVRSkeleton.BoneId[] boneIds)
        {
            position = Vector3.zero;

            if (skeleton.Bones == null)
                return false;

            foreach (var bone in skeleton.Bones)
            {
                if (bone == null || bone.Transform == null)
                    continue;

                for (var i = 0; i < boneIds.Length; i++)
                {
                    if (bone.Id != boneIds[i])
                        continue;

                    position = bone.Transform.position;
                    return true;
                }
            }

            return false;
        }

        private void SetFingerDebug(bool isLeft, FingerPoseDebug debug)
        {
            if (isLeft)
                leftFingerDebug = debug;
            else
                rightFingerDebug = debug;
        }

        private void SetDebugSkeleton(bool isLeft, OVRSkeleton skeleton)
        {
            if (isLeft)
                debugSkeletonLeft = skeleton;
            else
                debugSkeletonRight = skeleton;
        }

        private FingerPoseDebug BuildFingerDebug(float thumbCurl, float indexCurl, float middleCurl, float ringCurl, float pinkyCurl, float indexMaxCurl)
        {
            return new FingerPoseDebug
            {
                hasData = true,
                thumbExtended = thumbCurl < WithOvrFingerMargin(gunThumbMaxCurl),
                indexExtended = indexCurl < WithOvrFingerMargin(indexMaxCurl),
                middleExtended = middleCurl < WithOvrFingerMargin(gunMiddleFistRejectCurl),
                ringExtended = ringCurl < WithOvrFingerMargin(gunRingMinCurl),
                pinkyExtended = pinkyCurl < WithOvrFingerMargin(gunPinkyMinCurl),
                thumbCurl = thumbCurl,
                indexCurl = indexCurl,
                middleCurl = middleCurl,
                ringCurl = ringCurl,
                pinkyCurl = pinkyCurl
            };
        }

        private FingerPoseDebug BuildFingerAngleDebug(float thumbAngle, float indexAngle, float middleAngle, float ringAngle, float pinkyAngle, float indexMaxAngle)
        {
            return new FingerPoseDebug
            {
                hasData = true,
                thumbExtended = thumbAngle < gunThumbOpenAngle,
                indexExtended = indexAngle < indexMaxAngle,
                middleExtended = middleAngle < gunMiddleFistRejectAngle,
                ringExtended = ringAngle < gunRingClosedAngle,
                pinkyExtended = pinkyAngle < gunPinkyClosedAngle,
                thumbCurl = thumbAngle,
                indexCurl = indexAngle,
                middleCurl = middleAngle,
                ringCurl = ringAngle,
                pinkyCurl = pinkyAngle
            };
        }

        private static float CalculateFingerCurl(Vector3 basePosition, Vector3 middlePosition, Vector3 distalPosition, Vector3 tipPosition)
        {
            var chainLength = Vector3.Distance(basePosition, middlePosition) +
                              Vector3.Distance(middlePosition, distalPosition) +
                              Vector3.Distance(distalPosition, tipPosition);
            if (chainLength <= 0.001f)
                return 1f;

            var straightness = Vector3.Distance(basePosition, tipPosition) / chainLength;
            return Mathf.Clamp01(Mathf.InverseLerp(0.92f, 0.45f, straightness));
        }

        private static float CalculateThumbCurl(Vector3 palm, Vector3 basePosition, Vector3 distalPosition, Vector3 tipPosition)
        {
            var chainLength = Vector3.Distance(basePosition, distalPosition) +
                              Vector3.Distance(distalPosition, tipPosition);
            if (chainLength <= 0.001f)
                return 1f;

            var straightness = Vector3.Distance(basePosition, tipPosition) / chainLength;
            var straightnessCurl = Mathf.InverseLerp(0.9f, 0.45f, straightness);
            var palmCurl = Mathf.InverseLerp(0.035f, 0.004f, Vector3.Distance(tipPosition, palm) - Vector3.Distance(basePosition, palm));
            return Mathf.Clamp01(Mathf.Max(straightnessCurl, palmCurl));
        }

        private void OnGUI()
        {
            if (!showBoneAngleDebug)
                return;

            var skeleton = debugRightHandAngles ? debugSkeletonRight : debugSkeletonLeft;
            var handLabel = debugRightHandAngles ? "Right" : "Left";
            var angles = new StringBuilder();
            angles.AppendLine($"=== HAND DEBUG ({handLabel} Hand) ===");
            AppendHandStatusDebug(angles, true, rightOvrHand, debugSkeletonRight);
            AppendHandStatusDebug(angles, false, leftOvrHand, debugSkeletonLeft);
            angles.AppendLine($"[R] Candidate:{rightPrototypeCandidatePose} Confirmed:{rightPrototypeConfirmedPose}");
            angles.AppendLine($"[L] Fist:{leftFistMovementConfirmed}");
            angles.AppendLine(rightPrototypeDebug);
            angles.AppendLine(leftPrototypeDebug);

            if (skeleton == null || skeleton.Bones == null)
            {
                angles.AppendLine("Selected skeleton: missing");
            }
            else
            {
                AppendBoneAngleDebug(angles, skeleton, OVRSkeleton.BoneId.Hand_Thumb1);
                AppendBoneAngleDebug(angles, skeleton, OVRSkeleton.BoneId.Hand_Index1);
                AppendBoneAngleDebug(angles, skeleton, OVRSkeleton.BoneId.Hand_Middle1);
                AppendBoneAngleDebug(angles, skeleton, OVRSkeleton.BoneId.Hand_Ring1);
                AppendBoneAngleDebug(angles, skeleton, OVRSkeleton.BoneId.Hand_Pinky1);
            }

            GUI.skin.label.fontSize = 20;
            GUI.Label(new Rect(10, 10, 980, 520), angles.ToString());
        }

        private void LogHandBindingState()
        {
            if (!showBoneAngleDebug)
                return;

            debugBindingLogFrame++;
            if (debugBindingLogFrame % 120 != 0)
                return;

            var leftState = BuildHandBindingLog("L", leftOvrHand, debugSkeletonLeft);
            var rightState = BuildHandBindingLog("R", rightOvrHand, debugSkeletonRight);
            Debug.Log($"[HAND] {leftState} | {rightState}");
        }

        private static string BuildHandBindingLog(string label, OVRHand hand, OVRSkeleton skeleton)
        {
            if (hand == null)
                return $"[{label}] hand:null";

            var skeletonState = skeleton != null && skeleton.IsInitialized && skeleton.IsDataValid && skeleton.Bones != null
                ? "ok"
                : "null";

            return $"[{label}] tracked:{hand.IsTracked} valid:{hand.IsDataValid} conf:{hand.HandConfidence} sk:{skeletonState}";
        }

        private static void AppendHandStatusDebug(StringBuilder builder, bool isRight, OVRHand hand, OVRSkeleton skeleton)
        {
            var label = isRight ? "R" : "L";
            if (hand == null)
            {
                builder.AppendLine($"[{label}] OVRHand: null");
                return;
            }

            builder.AppendLine(
                $"[{label}] tracked:{hand.IsTracked} valid:{hand.IsDataValid} conf:{hand.HandConfidence} sk:{(skeleton != null ? "ok" : "null")}");
        }

        private static void AppendBoneAngleDebug(StringBuilder angles, OVRSkeleton skeleton, OVRSkeleton.BoneId boneId)
        {
            foreach (var bone in skeleton.Bones)
            {
                if (bone == null || bone.Transform == null || bone.Id != boneId)
                    continue;

                var euler = bone.Transform.localRotation.eulerAngles;
                angles.AppendLine($"{boneId}: X={NormalizeEulerAngle(euler.x):F1} Z={NormalizeEulerAngle(euler.z):F1}");
                return;
            }

            angles.AppendLine($"{boneId}: missing");
        }

        private void LogBoneAngles(OVRSkeleton skeleton, string tag)
        {
            debugBoneLogFrame++;
            if (debugBoneLogFrame % 90 != 0)
                return;

            if (skeleton == null || skeleton.Bones == null)
                return;

            var thumb = 0f;
            var index = 0f;
            var middle = 0f;
            var ring = 0f;
            var pinky = 0f;

            foreach (var bone in skeleton.Bones)
            {
                if (bone == null || bone.Transform == null)
                    continue;

                var angle = NormalizeEulerAngle(bone.Transform.localRotation.eulerAngles.x);
                switch (bone.Id)
                {
                    case OVRSkeleton.BoneId.Hand_Thumb1:
                        thumb = angle;
                        break;
                    case OVRSkeleton.BoneId.Hand_Index1:
                        index = angle;
                        break;
                    case OVRSkeleton.BoneId.Hand_Middle1:
                        middle = angle;
                        break;
                    case OVRSkeleton.BoneId.Hand_Ring1:
                        ring = angle;
                        break;
                    case OVRSkeleton.BoneId.Hand_Pinky1:
                        pinky = angle;
                        break;
                }
            }

            Debug.Log($"[BONE] {tag} T={thumb:F0} I={index:F0} M={middle:F0} R={ring:F0} P={pinky:F0}");
        }

        private void UpdatePrototypePoseDetection(OVRHand hand, OVRSkeleton skeleton, bool isLeft)
        {
            if (hand == null || skeleton == null)
            {
                if (isLeft)
                    leftPrototypeDebug = "L: hand/skeleton not ready";
                else
                    rightPrototypeDebug = "R: hand/skeleton not ready";

                ClearPrototypePose(isLeft);
                UpdateLeftFistMovement(isLeft, false);
                return;
            }

            ref var lowConfidenceTimer = ref (isLeft ? ref leftPrototypeLowConfidenceTimer : ref rightPrototypeLowConfidenceTimer);
            if (hand.HandConfidence != OVRHand.TrackingConfidence.High)
            {
                lowConfidenceTimer += Time.deltaTime;
                var debugText = $"{(isLeft ? "L" : "R")}: low confidence {lowConfidenceTimer:0.00}/{prototypeLowConfidenceGraceDuration:0.00}s";
                if (isLeft)
                    leftPrototypeDebug = debugText;
                else
                    rightPrototypeDebug = debugText;

                if (lowConfidenceTimer >= prototypeLowConfidenceGraceDuration)
                    ClearPrototypePose(isLeft);

                UpdateLeftFistMovement(isLeft, false);
                return;
            }

            lowConfidenceTimer = 0f;

            var detected = DetectPrototypePose(skeleton, out var prototypeDebug);
            if (isLeft)
                leftPrototypeDebug = prototypeDebug;
            else
                rightPrototypeDebug = prototypeDebug;

            if (isLeft)
                UpdateLeftFistMovement(true, detected == PoseType.Fist);

            ref var candidatePose = ref (isLeft ? ref leftPrototypeCandidatePose : ref rightPrototypeCandidatePose);
            ref var confirmedPose = ref (isLeft ? ref leftPrototypeConfirmedPose : ref rightPrototypeConfirmedPose);
            ref var holdTimer = ref (isLeft ? ref leftPrototypePoseHoldTimer : ref rightPrototypePoseHoldTimer);

            if (detected == candidatePose && detected != PoseType.None)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= prototypePoseHoldTime && confirmedPose != detected)
                {
                    confirmedPose = detected;
                    if (!isLeft)
                        OnRightPoseConfirmed?.Invoke(detected);

                    OnHandPoseConfirmed?.Invoke(isLeft, detected);
                    OnPoseConfirmed?.Invoke(detected);
                    LogPrototypeDebug($"[POSE CONFIRMED] {(isLeft ? "L" : "R")} {detected}");
                }
            }
            else
            {
                candidatePose = detected;
                holdTimer = 0f;
                if (detected == PoseType.None && confirmedPose != PoseType.None)
                    ClearPrototypePose(isLeft);
            }

            if (showPrototypeDebugLog && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[POSE] {(isLeft ? "L" : "R")} Candidate:{candidatePose} Confirmed:{confirmedPose} Timer:{holdTimer:F2}s");
                Debug.Log($"[POSEDBG] {prototypeDebug}");
            }
        }

        private PoseType DetectPrototypePose(OVRSkeleton skeleton, out string debug)
        {
            var angleOpen = IsOpenPalmByAngle(skeleton);
            var angleFist = IsFistByAngle(skeleton);
            var angleThumbsUp = IsThumbsUpByAngle(skeleton);
            var curlDebug = "curl unavailable";
            var curlOpen = false;
            var curlFist = false;
            var curlThumbsUp = false;

            if (usePrototypeDistanceFallback && TryGetPrototypeFingerCurls(skeleton, out var curls))
            {
                curlOpen = IsOpenPalmByCurl(curls);
                curlFist = IsFistByCurl(curls);
                curlThumbsUp = IsThumbsUpByCurl(curls);
                curlDebug = $"curl T:{curls.thumb:0.00} I:{curls.index:0.00} M:{curls.middle:0.00} R:{curls.ring:0.00} P:{curls.pinky:0.00}";
            }

            var thumb = GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.XRHand_ThumbMetacarpal);
            var index = GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal);
            var middle = GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal);
            var ring = GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal);
            var pinky = GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal);
            debug = $"angle |T:{thumb:0} I:{index:0} M:{middle:0} R:{ring:0} P:{pinky:0}| {curlDebug} | open:{angleOpen || curlOpen} thumbs:{angleThumbsUp || curlThumbsUp} fist:{angleFist || curlFist}";

            if (angleOpen || curlOpen)
                return PoseType.OpenPalm;

            if (angleThumbsUp || curlThumbsUp)
                return PoseType.ThumbsUp;

            if (angleFist || curlFist)
                return PoseType.Fist;

            return PoseType.None;
        }

        private bool TryGetPrototypeFingerCurls(OVRSkeleton skeleton, out PrototypeFingerCurls curls)
        {
            curls = default;

            if (!TryGetOvrBonePosition(skeleton, out var thumbBase, OVRSkeleton.BoneId.Hand_Thumb2, OVRSkeleton.BoneId.XRHand_ThumbProximal) ||
                !TryGetOvrBonePosition(skeleton, out var thumbDistal, OVRSkeleton.BoneId.Hand_Thumb3, OVRSkeleton.BoneId.XRHand_ThumbDistal) ||
                !TryGetOvrBonePosition(skeleton, out var thumbTip, OVRSkeleton.BoneId.Hand_ThumbTip, OVRSkeleton.BoneId.XRHand_ThumbTip) ||
                !TryGetOvrBonePosition(skeleton, out var indexBase, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal) ||
                !TryGetOvrBonePosition(skeleton, out var indexMiddle, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.XRHand_IndexIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var indexDistal, OVRSkeleton.BoneId.Hand_Index3, OVRSkeleton.BoneId.XRHand_IndexDistal) ||
                !TryGetOvrBonePosition(skeleton, out var indexTip, OVRSkeleton.BoneId.Hand_IndexTip, OVRSkeleton.BoneId.XRHand_IndexTip) ||
                !TryGetOvrBonePosition(skeleton, out var middleBase, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal) ||
                !TryGetOvrBonePosition(skeleton, out var middleMiddle, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.XRHand_MiddleIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var middleDistal, OVRSkeleton.BoneId.Hand_Middle3, OVRSkeleton.BoneId.XRHand_MiddleDistal) ||
                !TryGetOvrBonePosition(skeleton, out var middleTip, OVRSkeleton.BoneId.Hand_MiddleTip, OVRSkeleton.BoneId.XRHand_MiddleTip) ||
                !TryGetOvrBonePosition(skeleton, out var ringBase, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal) ||
                !TryGetOvrBonePosition(skeleton, out var ringMiddle, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.XRHand_RingIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var ringDistal, OVRSkeleton.BoneId.Hand_Ring3, OVRSkeleton.BoneId.XRHand_RingDistal) ||
                !TryGetOvrBonePosition(skeleton, out var ringTip, OVRSkeleton.BoneId.Hand_RingTip, OVRSkeleton.BoneId.XRHand_RingTip) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyBase, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyMiddle, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.XRHand_LittleIntermediate) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyDistal, OVRSkeleton.BoneId.Hand_Pinky3, OVRSkeleton.BoneId.XRHand_LittleDistal) ||
                !TryGetOvrBonePosition(skeleton, out var pinkyTip, OVRSkeleton.BoneId.Hand_PinkyTip, OVRSkeleton.BoneId.XRHand_LittleTip))
            {
                return false;
            }

            var palm = (indexBase + middleBase + ringBase + pinkyBase) * 0.25f;
            if (TryGetOvrBonePosition(skeleton, out var skeletonPalm, OVRSkeleton.BoneId.XRHand_Palm))
                palm = skeletonPalm;

            curls = new PrototypeFingerCurls
            {
                thumb = CalculateThumbCurl(palm, thumbBase, thumbDistal, thumbTip),
                index = CalculateFingerCurl(indexBase, indexMiddle, indexDistal, indexTip),
                middle = CalculateFingerCurl(middleBase, middleMiddle, middleDistal, middleTip),
                ring = CalculateFingerCurl(ringBase, ringMiddle, ringDistal, ringTip),
                pinky = CalculateFingerCurl(pinkyBase, pinkyMiddle, pinkyDistal, pinkyTip)
            };

            return true;
        }

        private bool IsOpenPalmByAngle(OVRSkeleton skeleton)
        {
            return GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.XRHand_ThumbMetacarpal) <= prototypeOpenMaxAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal) <= prototypeOpenMaxAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal) <= prototypeOpenMaxAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal) <= prototypeOpenMaxAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal) <= prototypeOpenMaxAngle;
        }

        private bool IsFistByAngle(OVRSkeleton skeleton)
        {
            return GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.XRHand_ThumbMetacarpal) >= prototypeThumbClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal) >= prototypeFingerClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal) >= prototypeFingerClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal) >= prototypeFingerClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal) >= prototypeFingerClosedMinAngle;
        }

        private bool IsThumbsUpByAngle(OVRSkeleton skeleton)
        {
            return GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Thumb1, OVRSkeleton.BoneId.XRHand_ThumbMetacarpal) <= prototypeThumbOpenMaxAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.XRHand_IndexProximal) >= prototypeFingerClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.XRHand_MiddleProximal) >= prototypeFingerClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.XRHand_RingProximal) >= prototypeFingerClosedMinAngle &&
                   GetBoneCurlMagnitude(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.XRHand_LittleProximal) >= prototypeFingerClosedMinAngle;
        }

        private bool IsOpenPalmByCurl(PrototypeFingerCurls curls)
        {
            return curls.thumb <= prototypeOpenCurlMax &&
                   curls.index <= prototypeOpenCurlMax &&
                   curls.middle <= prototypeOpenCurlMax &&
                   curls.ring <= prototypeOpenCurlMax &&
                   curls.pinky <= prototypeOpenCurlMax;
        }

        private bool IsFistByCurl(PrototypeFingerCurls curls)
        {
            return curls.thumb >= prototypeThumbClosedCurlMin &&
                   curls.index >= prototypeFingerClosedCurlMin &&
                   curls.middle >= prototypeFingerClosedCurlMin &&
                   curls.ring >= prototypeFingerClosedCurlMin &&
                   curls.pinky >= prototypeFingerClosedCurlMin;
        }

        private bool IsThumbsUpByCurl(PrototypeFingerCurls curls)
        {
            return curls.thumb <= prototypeThumbOpenCurlMax &&
                   curls.index >= prototypeFingerClosedCurlMin &&
                   curls.middle >= prototypeFingerClosedCurlMin &&
                   curls.ring >= prototypeFingerClosedCurlMin &&
                   curls.pinky >= prototypeFingerClosedCurlMin;
        }

        private float GetBoneCurlMagnitude(OVRSkeleton skeleton, params OVRSkeleton.BoneId[] boneIds)
        {
            return Mathf.Abs(GetBoneAngle(skeleton, boneIds));
        }

        private void ClearPrototypePose(bool isLeft)
        {
            ref var candidatePose = ref (isLeft ? ref leftPrototypeCandidatePose : ref rightPrototypeCandidatePose);
            ref var confirmedPose = ref (isLeft ? ref leftPrototypeConfirmedPose : ref rightPrototypeConfirmedPose);
            ref var holdTimer = ref (isLeft ? ref leftPrototypePoseHoldTimer : ref rightPrototypePoseHoldTimer);

            var hadConfirmedPose = confirmedPose != PoseType.None;
            candidatePose = PoseType.None;
            confirmedPose = PoseType.None;
            holdTimer = 0f;

            if (!hadConfirmedPose)
                return;

            if (!isLeft)
                OnRightPoseCleared?.Invoke();

            OnHandPoseCleared?.Invoke(isLeft);
            OnPoseCleared?.Invoke();
            LogPrototypeDebug($"[POSE CLEARED] {(isLeft ? "L" : "R")}");
        }

        private void UpdateLeftFistMovement(bool isLeft, bool fistDetected)
        {
            if (!isLeft)
                return;

            if (!leftFistMovementConfirmed)
            {
                if (fistDetected)
                {
                    leftFistMovementHoldTimer += Time.deltaTime;
                    if (leftFistMovementHoldTimer >= prototypeLeftFistHoldTime)
                    {
                        leftFistMovementConfirmed = true;
                        leftFistMovementReleaseTimer = 0f;
                        OnLeftFistStart?.Invoke();
                        LogPrototypeDebug("[MOVE] Left fist grabbed");
                    }
                }
                else
                {
                    leftFistMovementHoldTimer = 0f;
                }

                return;
            }

            if (fistDetected)
            {
                leftFistMovementReleaseTimer = 0f;
                return;
            }

            leftFistMovementReleaseTimer += Time.deltaTime;
            if (leftFistMovementReleaseTimer < prototypeLeftFistReleaseBuffer)
                return;

            ReleaseLeftFistMovement();
        }

        private void ReleaseLeftFistMovement()
        {
            if (!leftFistMovementConfirmed)
                return;

            leftFistMovementConfirmed = false;
            leftFistMovementHoldTimer = 0f;
            leftFistMovementReleaseTimer = 0f;
            OnLeftFistEnd?.Invoke();
            LogPrototypeDebug("[MOVE] Left fist released");
        }

        private void LogPrototypeDebug(string message)
        {
            if (showPrototypeDebugLog)
                Debug.Log(message);
        }

        private static float GetBoneAngle(OVRSkeleton skeleton, params OVRSkeleton.BoneId[] boneIds)
        {
            if (skeleton.Bones == null)
                return 0f;

            foreach (var bone in skeleton.Bones)
            {
                if (bone == null || bone.Transform == null)
                    continue;

                for (var i = 0; i < boneIds.Length; i++)
                {
                    if (bone.Id != boneIds[i])
                        continue;

                    return NormalizeEulerAngle(bone.Transform.localRotation.eulerAngles.x);
                }
            }

            return 0f;
        }

        private static float NormalizeEulerAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private bool TryResolveGunPose(
            bool isLeft,
            float thumbCurl,
            float indexCurl,
            float middleCurl,
            float ringCurl,
            float pinkyCurl,
            Vector3 indexBase,
            Vector3 indexMiddle,
            Vector3 indexDistal,
            Vector3 indexTip,
            Vector3 middleBase,
            Vector3 middleMiddle,
            Vector3 middleDistal,
            Vector3 middleTip,
            Vector3 alternateAimDirection)
        {
            var pointingForward = IsGunPointingForward(indexBase, indexTip, middleBase, middleTip, alternateAimDirection);
            var indexMaxCurl = pointingForward ? gunForwardIndexMaxCurl : gunIndexMaxCurl;
            var state = GetGunFingerState(isLeft);
            state.thumbOpen = UpdateLowCurlState(thumbCurl, state.thumbOpen, gunThumbMaxCurl, gunThumbOpenExitCurl);
            state.indexOpen = UpdateLowCurlState(indexCurl, state.indexOpen, indexMaxCurl, gunIndexOpenExitCurl);
            state.middleNotFist = UpdateLowCurlState(middleCurl, state.middleNotFist, gunMiddleNotFistEnterCurl, gunMiddleFistRejectCurl);
            state.ringClosed = UpdateHighCurlState(ringCurl, state.ringClosed, gunRingMinCurl, gunRingClosedExitCurl);
            state.pinkyClosed = UpdateHighCurlState(pinkyCurl, state.pinkyClosed, gunPinkyMinCurl, gunPinkyClosedExitCurl);
            SetGunFingerState(isLeft, state);

            var thumbOk = !requireThumbExtendedForGun || state.thumbOpen;
            var notFist = middleCurl < gunMiddleFistRejectCurl && state.middleNotFist;
            var rawGun = thumbOk && state.indexOpen && state.middleNotFist && state.ringClosed && state.pinkyClosed && notFist;
            var occlusionGraceGun = thumbOk && state.ringClosed && state.pinkyClosed && notFist && pointingForward && HasRecentGunPose(isLeft);

            if (!rawGun && !occlusionGraceGun)
            {
                ResetGunStableFrames(isLeft);
                return false;
            }

            if (occlusionGraceGun && !rawGun)
                return true;

            var stableFrames = IncrementGunStableFrames(isLeft);
            if (stableFrames < gunRequiredStableFrames && !HasRecentGunPose(isLeft))
                return false;

            MarkGunPose(isLeft);
            return true;
        }

        private bool TryResolveGunPoseFromAngles(
            bool isLeft,
            float thumbAngle,
            float indexAngle,
            float middleAngle,
            float ringAngle,
            float pinkyAngle,
            Vector3 indexBase,
            Vector3 indexMiddle,
            Vector3 indexDistal,
            Vector3 indexTip,
            Vector3 middleBase,
            Vector3 middleMiddle,
            Vector3 middleDistal,
            Vector3 middleTip,
            Vector3 alternateAimDirection)
        {
            var pointingForward = IsGunPointingForward(indexBase, indexTip, middleBase, middleTip, alternateAimDirection);
            var indexOpenAngle = pointingForward ? gunForwardIndexOpenAngle : gunIndexOpenAngle;
            var state = GetGunFingerState(isLeft);
            state.thumbOpen = UpdateLowCurlState(thumbAngle, state.thumbOpen, gunThumbOpenAngle, gunThumbOpenExitAngle);
            state.indexOpen = UpdateLowCurlState(indexAngle, state.indexOpen, indexOpenAngle, gunIndexOpenExitAngle);
            state.middleNotFist = UpdateLowCurlState(middleAngle, state.middleNotFist, gunMiddleNotFistEnterAngle, gunMiddleFistRejectAngle);
            state.ringClosed = UpdateHighCurlState(ringAngle, state.ringClosed, gunRingClosedAngle, gunRingClosedExitAngle);
            state.pinkyClosed = UpdateHighCurlState(pinkyAngle, state.pinkyClosed, gunPinkyClosedAngle, gunPinkyClosedExitAngle);
            SetGunFingerState(isLeft, state);

            var thumbOk = !requireThumbExtendedForGun || state.thumbOpen;
            var notFist = middleAngle < gunMiddleFistRejectAngle && state.middleNotFist;
            var rawGun = thumbOk && state.indexOpen && state.middleNotFist && state.ringClosed && state.pinkyClosed && notFist;
            var occlusionGraceGun = thumbOk && state.ringClosed && state.pinkyClosed && notFist && pointingForward && HasRecentGunPose(isLeft);

            if (!rawGun && !occlusionGraceGun)
            {
                ResetGunStableFrames(isLeft);
                return false;
            }

            if (occlusionGraceGun && !rawGun)
                return true;

            var stableFrames = IncrementGunStableFrames(isLeft);
            if (stableFrames < gunRequiredStableFrames && !HasRecentGunPose(isLeft))
                return false;

            MarkGunPose(isLeft);
            return true;
        }

        private GunFingerState GetGunFingerState(bool isLeft)
        {
            return isLeft ? leftGunFingerState : rightGunFingerState;
        }

        private void SetGunFingerState(bool isLeft, GunFingerState state)
        {
            if (isLeft)
                leftGunFingerState = state;
            else
                rightGunFingerState = state;
        }

        private static bool UpdateLowCurlState(float curl, bool previousState, float enterThreshold, float exitThreshold)
        {
            if (previousState)
                return curl < exitThreshold;

            return curl < enterThreshold;
        }

        private static bool UpdateHighCurlState(float curl, bool previousState, float enterThreshold, float exitThreshold)
        {
            if (previousState)
                return curl > exitThreshold;

            return curl > enterThreshold;
        }

        private static bool IsOvrFingerStraight(Vector3 basePosition, Vector3 middlePosition, Vector3 distalPosition, Vector3 tipPosition, float threshold)
        {
            var chainLength = Vector3.Distance(basePosition, middlePosition) +
                              Vector3.Distance(middlePosition, distalPosition) +
                              Vector3.Distance(distalPosition, tipPosition);
            if (chainLength <= 0.001f)
                return false;

            return Vector3.Distance(basePosition, tipPosition) / chainLength >= threshold;
        }

        private bool IsGunPointingForward(Vector3 indexBase, Vector3 indexTip, Vector3 middleBase, Vector3 middleTip, Vector3 alternateAimDirection)
        {
            var indexDirection = indexTip - indexBase;
            var middleDirection = middleTip - middleBase;
            var aimDirection = indexDirection + middleDirection;

            var headForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            var normalizedHeadForward = headForward.normalized;
            var fingerPointingForward = aimDirection.sqrMagnitude > 0.0001f &&
                                        Vector3.Dot(aimDirection.normalized, normalizedHeadForward) >= forwardGunDotThreshold;
            var pointerPointingForward = alternateAimDirection.sqrMagnitude > 0.0001f &&
                                         Vector3.Dot(alternateAimDirection.normalized, normalizedHeadForward) >= forwardGunDotThreshold;
            return fingerPointingForward || pointerPointingForward;
        }

        private bool HasRecentGunPose(bool isLeft)
        {
            var lastGunTime = isLeft ? lastLeftGunTime : lastRightGunTime;
            return Time.time - lastGunTime <= gunOcclusionGraceDuration;
        }

        private int IncrementGunStableFrames(bool isLeft)
        {
            if (isLeft)
                return ++leftGunStableFrames;

            return ++rightGunStableFrames;
        }

        private void ResetGunStableFrames(bool isLeft)
        {
            if (isLeft)
                leftGunStableFrames = 0;
            else
                rightGunStableFrames = 0;
        }

        private void MarkGunPose(bool isLeft)
        {
            if (isLeft)
                lastLeftGunTime = Time.time;
            else
                lastRightGunTime = Time.time;
        }

        private PoseId DetectXrHandPose(bool isLeft, out Vector3 palmPosition)
        {
            palmPosition = Vector3.zero;

            if (handSubsystem == null || !handSubsystem.running)
                return PoseId.None;

            var hand = isLeft ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked)
                return PoseId.None;

            if (!TryGetJointPosition(hand, XRHandJointID.Palm, out palmPosition))
                TryGetJointPosition(hand, XRHandJointID.Wrist, out palmPosition);

            if (palmPosition == Vector3.zero)
                return PoseId.None;

            if (!TryGetJointPosition(hand, XRHandJointID.ThumbProximal, out var thumbBase) ||
                !TryGetJointPosition(hand, XRHandJointID.ThumbDistal, out var thumbDistal) ||
                !TryGetJointPosition(hand, XRHandJointID.ThumbTip, out var thumbTip) ||
                !TryGetJointPosition(hand, XRHandJointID.IndexProximal, out var indexBase) ||
                !TryGetJointPosition(hand, XRHandJointID.IndexIntermediate, out var indexMiddle) ||
                !TryGetJointPosition(hand, XRHandJointID.IndexDistal, out var indexDistal) ||
                !TryGetJointPosition(hand, XRHandJointID.IndexTip, out var indexTip) ||
                !TryGetJointPosition(hand, XRHandJointID.MiddleProximal, out var middleBase) ||
                !TryGetJointPosition(hand, XRHandJointID.MiddleIntermediate, out var middleMiddle) ||
                !TryGetJointPosition(hand, XRHandJointID.MiddleDistal, out var middleDistal) ||
                !TryGetJointPosition(hand, XRHandJointID.MiddleTip, out var middleTip) ||
                !TryGetJointPosition(hand, XRHandJointID.RingProximal, out var ringBase) ||
                !TryGetJointPosition(hand, XRHandJointID.RingIntermediate, out var ringMiddle) ||
                !TryGetJointPosition(hand, XRHandJointID.RingDistal, out var ringDistal) ||
                !TryGetJointPosition(hand, XRHandJointID.RingTip, out var ringTip) ||
                !TryGetJointPosition(hand, XRHandJointID.LittleProximal, out var littleBase) ||
                !TryGetJointPosition(hand, XRHandJointID.LittleIntermediate, out var littleMiddle) ||
                !TryGetJointPosition(hand, XRHandJointID.LittleDistal, out var littleDistal) ||
                !TryGetJointPosition(hand, XRHandJointID.LittleTip, out var littleTip))
            {
                SetFingerDebug(isLeft, default);
                return PoseId.None;
            }

            palmPosition = (indexBase + middleBase + ringBase + littleBase) * 0.25f;

            var thumbCurl = CalculateThumbCurl(palmPosition, thumbBase, thumbDistal, thumbTip);
            var indexCurl = CalculateFingerCurl(indexBase, indexMiddle, indexDistal, indexTip);
            var middleCurl = CalculateFingerCurl(middleBase, middleMiddle, middleDistal, middleTip);
            var ringCurl = CalculateFingerCurl(ringBase, ringMiddle, ringDistal, ringTip);
            var littleCurl = CalculateFingerCurl(littleBase, littleMiddle, littleDistal, littleTip);
            var indexExtended = indexCurl < WithOvrFingerMargin(gunIndexMaxCurl);
            var middleExtended = middleCurl < WithOvrFingerMargin(gunMiddleFistRejectCurl);
            var ringExtended = ringCurl < WithOvrFingerMargin(gunRingMinCurl);
            var littleExtended = littleCurl < WithOvrFingerMargin(gunPinkyMinCurl);
            var indexDebugMaxCurl = IsGunPointingForward(indexBase, indexTip, middleBase, middleTip, Vector3.zero)
                ? gunForwardIndexMaxCurl
                : gunIndexMaxCurl;

            SetFingerDebug(isLeft, BuildFingerDebug(
                thumbCurl,
                indexCurl,
                middleCurl,
                ringCurl,
                littleCurl,
                indexDebugMaxCurl));

            var okPinch = Vector3.Distance(thumbTip, indexTip) <= OkTipDistance;

            if (TryResolveGunPose(
                    isLeft,
                    thumbCurl,
                    indexCurl,
                    middleCurl,
                    ringCurl,
                    littleCurl,
                    indexBase,
                    indexMiddle,
                    indexDistal,
                    indexTip,
                    middleBase,
                    middleMiddle,
                    middleDistal,
                    middleTip,
                    Vector3.zero))
            {
                return PoseId.IndexPoint;
            }

            if (!indexExtended && !middleExtended && !ringExtended && !littleExtended)
                return ResolveFistOrPush(isLeft, palmPosition);

            if (okPinch && middleExtended && ringExtended && littleExtended)
                return PoseId.Ok;

            if (indexExtended && littleExtended && !middleExtended && !ringExtended)
                return PoseId.Horn;

            if (openPalmDetectionEnabled && indexExtended && middleExtended && ringExtended && littleExtended)
                return PoseId.OpenPalm;

            return PoseId.None;
        }

        private static bool TryGetJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position)
        {
            position = Vector3.zero;

            var joint = hand.GetJoint(jointId);
            if (!joint.TryGetPose(out var pose))
                return false;

            position = pose.position;
            return true;
        }

        private PoseId ResolvePoseFromPinches(bool isLeft, Vector3 palmPosition, float[] pinchStrengths)
        {
            var thumb = pinchStrengths[0] > OkThumbIndexThreshold;
            var index = pinchStrengths[1] > PinchThreshold;
            var middle = pinchStrengths[2] > PinchThreshold;
            var ring = pinchStrengths[3] > PinchThreshold;
            var pinky = pinchStrengths[4] > PinchThreshold;
            var relaxed = pinchStrengths[1] < OpenThreshold && pinchStrengths[2] < OpenThreshold && pinchStrengths[3] < OpenThreshold && pinchStrengths[4] < OpenThreshold;

            if (index && middle && ring && pinky)
                return ResolveFistOrPush(isLeft, palmPosition);

            if ((thumb || index) && !middle && !ring && !pinky)
                return PoseId.Ok;

            if (!index && middle && ring && !pinky)
                return PoseId.Horn;

            if (openPalmDetectionEnabled && relaxed)
                return PoseId.OpenPalm;

            return PoseId.None;
        }

        private PoseId ResolveFistOrPush(bool isLeft, Vector3 palmPosition)
        {
            var previousPosition = isLeft ? previousLeftPalmPosition : previousRightPalmPosition;
            var hasPrevious = isLeft ? hasPreviousLeftPalm : hasPreviousRightPalm;

            if (isLeft)
            {
                previousLeftPalmPosition = palmPosition;
                hasPreviousLeftPalm = true;
            }
            else
            {
                previousRightPalmPosition = palmPosition;
                hasPreviousRightPalm = true;
            }

            if (!hasPrevious || Time.deltaTime <= 0f)
                return PoseId.Fist;

            var velocity = (palmPosition - previousPosition) / Time.deltaTime;
            var headForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
            return Vector3.Dot(velocity, headForward) >= PushVelocity ? PoseId.FistPush : PoseId.Fist;
        }

        private bool UpdateCombineState(Vector3 leftPalmPosition, Vector3 rightPalmPosition)
        {
            CurrentCombineForwardSpeed = 0f;

            if (leftPalmPosition == Vector3.zero || rightPalmPosition == Vector3.zero)
            {
                IsCombineCandidate = false;
                hasPreviousCombineMidpoint = false;
                return false;
            }

            IsCombineCandidate = Vector3.Distance(leftPalmPosition, rightPalmPosition) <= CombineDistance;
            if (!IsCombineCandidate)
            {
                hasPreviousCombineMidpoint = false;
                return false;
            }

            var midpoint = (leftPalmPosition + rightPalmPosition) * 0.5f;
            if (hasPreviousCombineMidpoint && Time.deltaTime > 0f)
            {
                var velocity = (midpoint - previousCombineMidpoint) / Time.deltaTime;
                var headForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                CurrentCombineForwardSpeed = Vector3.Dot(velocity, headForward);

                if (CurrentCombineForwardSpeed >= PushVelocity &&
                    Time.time - lastCombinePushTime >= combinePushCooldown)
                {
                    lastCombinePushTime = Time.time;
                    OnCombinePushDetected?.Invoke();
                }
            }

            previousCombineMidpoint = midpoint;
            hasPreviousCombineMidpoint = true;
            return true;
        }

        private void UpdateStablePose(bool isLeft, PoseId detectedPose)
        {
            ref var candidatePose = ref (isLeft ? ref candidateLeftPose : ref candidateRightPose);
            ref var stablePose = ref (isLeft ? ref stableLeftPose : ref stableRightPose);
            ref var candidateStartTime = ref (isLeft ? ref leftCandidateStartTime : ref rightCandidateStartTime);

            if (candidatePose != detectedPose)
            {
                candidatePose = detectedPose;
                candidateStartTime = Time.time;
                return;
            }

            if (Time.time - candidateStartTime >= StablePoseDuration)
                stablePose = candidatePose;
        }

        private void UpdateGrimoireTrigger(PoseId leftPose)
        {
            if (leftPose == PoseId.OpenPalm)
            {
                if (leftOpenPalmStartTime < 0f)
                    leftOpenPalmStartTime = Time.time;

                if (!grimoireTriggeredForHold && Time.time - leftOpenPalmStartTime >= GrimoireHoldDuration)
                {
                    grimoireTriggeredForHold = true;
                    OnGrimTrigger?.Invoke();
                }

                return;
            }

            leftOpenPalmStartTime = -1f;
            grimoireTriggeredForHold = false;
        }

        private void AutoBindOvrHands()
        {
            if (leftOvrHand != null && leftOvrHand.GetHand() != OVRPlugin.Hand.HandLeft)
                leftOvrHand = null;

            if (rightOvrHand != null && rightOvrHand.GetHand() != OVRPlugin.Hand.HandRight)
                rightOvrHand = null;

            var bestLeft = FindBestOvrHand(true);
            var bestRight = FindBestOvrHand(false);

            if (bestLeft != null && (leftOvrHand == null || ScoreOvrHand(bestLeft, true) > ScoreOvrHand(leftOvrHand, true) + 20))
                leftOvrHand = bestLeft;

            if (bestRight != null && (rightOvrHand == null || ScoreOvrHand(bestRight, false) > ScoreOvrHand(rightOvrHand, false) + 20))
                rightOvrHand = bestRight;
        }

        private static OVRHand FindBestOvrHand(bool isLeft)
        {
            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            OVRHand bestHand = null;
            var bestScore = int.MinValue;

            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand.GetHand() != expected)
                    continue;

                var score = ScoreOvrHand(hand, isLeft);
                if (score <= bestScore)
                    continue;

                bestHand = hand;
                bestScore = score;
            }

            return bestHand;
        }

        private static int ScoreOvrHand(OVRHand hand, bool isLeft)
        {
            if (hand == null)
                return int.MinValue;

            var score = 0;
            if (hand.gameObject.activeInHierarchy)
                score += 100;
            if (hand.enabled)
                score += 20;
            if (hand.IsTracked)
                score += 30;
            if (hand.HandConfidence == OVRHand.TrackingConfidence.High)
                score += 30;
            if (hand.IsPointerPoseValid)
                score += 10;
            if (hand.GetComponentInChildren<OVRSkeleton>(true) != null)
                score += 10;
            if (hand.GetComponentInChildren<OVRMeshRenderer>(true) != null)
                score += 10;

            var path = GetHierarchyPath(hand.transform);
            if (path.Contains("Detached"))
                score -= 80;
            if (path.Contains("Controller"))
                score -= 25;
            if (path.Contains(isLeft ? "LeftHandAnchor/" : "RightHandAnchor/"))
                score += 30;
            if (path.Contains(isLeft ? "RightHand" : "LeftHand"))
                score -= 120;

            return score;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }
    }
}
