using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

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
            return hasData
                ? $"T:{Mark(thumbExtended)}{thumbCurl:0.00} I:{Mark(indexExtended)}{indexCurl:0.00} M:{Mark(middleExtended)}{middleCurl:0.00} R:{Mark(ringExtended)}{ringCurl:0.00} P:{Mark(pinkyExtended)}{pinkyCurl:0.00}"
                : "T:- I:- M:- R:- P:-";
        }

        private static string Mark(bool value)
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
    /// XR Hands based gesture interpreter. It owns gesture detection only and
    /// publishes pose events for systems such as combination, spell casting, and UI.
    /// </summary>
    public class GestureDetector : MonoBehaviour
    {
        private enum GestureKind
        {
            None,
            Fire,
            Ice,
            Thunder,
            ThunderShoot,
            PageTurn,
            Combine,
            CombineShoot,
            Barrier,
            Grimoire
        }

        [Header("XR Hands")]
        [SerializeField] private XRHandTrackingEvents leftHandTrackingEvents;
        [SerializeField] private XRHandTrackingEvents rightHandTrackingEvents;

        [Header("Right Hand Gestures")]
        [SerializeField] private XRHandShape rightFireGesture;
        [SerializeField] private XRHandShape rightIceGesture;
        [SerializeField] private XRHandShape rightThunderGesture;
        [SerializeField] private XRHandShape rightThunderShootGesture;
        [SerializeField] private XRHandShape rightPageTurnGesture;
        [SerializeField] private XRHandShape rightCombine;
        [SerializeField] private XRHandShape rightCombineShoot;
        [SerializeField] private XRHandShape rightBarrier;

        [Header("Left Hand Gestures")]
        [SerializeField] private XRHandShape leftGrimoireGesture;
        [SerializeField] private XRHandShape leftFire;
        [SerializeField] private XRHandShape leftIce;
        [SerializeField] private XRHandShape leftThunder;
        [SerializeField] private XRHandShape leftCombine;
        [SerializeField] private XRHandShape leftCombineShoot;
        [SerializeField] private XRHandShape leftBarrier;

        [Header("Pose Timing")]
        [SerializeField] private float poseHoldDuration = 0.1f;
        [SerializeField] private float poseLostGracePeriod = 0.2f;
        [SerializeField] private float rightThunderShootArmWindowSeconds = 0.8f;

        [Header("Combine Push")]
        [SerializeField] private float combineDistance = 0.16f;
        [SerializeField] private float pushVelocity = 0.45f;
        [SerializeField] private float combinePushCooldown = 0.7f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLog;
        [SerializeField] private bool showPlayModeDebugOverlay;
        [SerializeField] private KeyCode debugOverlayToggleKey = KeyCode.BackQuote;

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
        public event Action<bool, string, PoseType> OnGestureConfirmed;
        public event Action<bool, string> OnGestureCleared;

        private PoseType leftConfirmedPose = PoseType.None;
        private PoseType rightConfirmedPose = PoseType.None;
        private GestureKind leftCandidateKind = GestureKind.None;
        private GestureKind rightCandidateKind = GestureKind.None;
        private GestureKind leftConfirmedKind = GestureKind.None;
        private GestureKind rightConfirmedKind = GestureKind.None;
        private PoseId leftPoseId = PoseId.None;
        private PoseId rightPoseId = PoseId.None;
        private float leftCandidateStartTime = -999f;
        private float rightCandidateStartTime = -999f;
        private float leftLostStartTime = -1f;
        private float rightLostStartTime = -1f;
        private Vector3 leftPalmPosition;
        private Vector3 rightPalmPosition;
        private bool hasLeftPalm;
        private bool hasRightPalm;
        private Vector3 previousCombineMidpoint;
        private bool hasPreviousCombineMidpoint;
        private float lastCombinePushTime = -999f;
        private float rightThunderShootArmedUntilTime = -999f;
        private bool leftFistActive;
        private GUIStyle playModeDebugStyle;

        public PoseId CurrentLeftPose => leftPoseId;
        public PoseId CurrentRightPose => rightPoseId;
        public FingerPoseDebug CurrentLeftFingerDebug { get; private set; }
        public FingerPoseDebug CurrentRightFingerDebug { get; private set; }
        public PoseType CurrentLeftPrototypePose => leftConfirmedPose;
        public PoseType CurrentRightPrototypePose => rightConfirmedPose;
        public string CurrentLeftHandStatus { get; private set; } = "[L] XRHands waiting";
        public string CurrentRightHandStatus { get; private set; } = "[R] XRHands waiting";
        public string CurrentLeftPrototypeDebug { get; private set; } = "L: waiting";
        public string CurrentRightPrototypeDebug { get; private set; } = "R: waiting";
        public bool IsCombineCandidate { get; private set; }
        public float CurrentCombineForwardSpeed { get; private set; }

        private void Awake()
        {
            ResolveHandTrackingEvents();
        }

        private void OnEnable()
        {
            ResolveHandTrackingEvents();
            SubscribeHandEvents();
        }

        private void OnDisable()
        {
            UnsubscribeHandEvents();
            ClearHand(true, true);
            ClearHand(false, true);
            ResetCombineState();
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(debugOverlayToggleKey))
                showPlayModeDebugOverlay = !showPlayModeDebugOverlay;
#endif
            UpdateCombinePush();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !showPlayModeDebugOverlay)
                return;

            playModeDebugStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white },
                padding = new RectOffset(8, 8, 6, 6)
            };

            GUILayout.BeginArea(new Rect(16f, 16f, 520f, 160f), GUI.skin.box);
            GUILayout.Label("XR HAND GESTURE DEBUG", playModeDebugStyle);
            GUILayout.Label($"{CurrentLeftHandStatus} {CurrentLeftPrototypeDebug}", playModeDebugStyle);
            GUILayout.Label($"{CurrentRightHandStatus} {CurrentRightPrototypeDebug}", playModeDebugStyle);
            GUILayout.Label($"Combine candidate:{IsCombineCandidate} push:{CurrentCombineForwardSpeed:0.00}", playModeDebugStyle);
            GUILayout.EndArea();
        }

        public void BindHands(OVRHand leftHand, OVRHand rightHand)
        {
            // Kept for legacy callers. GestureDetector no longer reads OVR hands.
        }

        public void BindGestureEventRouter(GestureEventRouter router)
        {
            // Kept for legacy setup scripts. Router observes this detector now.
        }

        public void SetAllowOvrPrototypeOverrideRouter(bool allowOverride)
        {
            // Kept for legacy setup scripts.
        }

        public void SetTuningData(HandPoseTuningData data)
        {
            // Kept for legacy setup scripts.
        }

        public void ConfigureXrHands(
            XRHandTrackingEvents leftEvents,
            XRHandTrackingEvents rightEvents,
            XRHandShape leftOpenPalm,
            XRHandShape leftFist,
            XRHandShape leftThumbsUp,
            XRHandShape leftTwoFinger,
            XRHandShape rightOpenPalm,
            XRHandShape rightFist,
            XRHandShape rightThumbsUp,
            XRHandShape rightTwoFinger)
        {
            UnsubscribeHandEvents();
            leftHandTrackingEvents = leftEvents;
            rightHandTrackingEvents = rightEvents;
            leftGrimoireGesture = leftOpenPalm;
            leftIce = leftFist;
            leftThunder = leftThumbsUp;
            leftCombine = leftTwoFinger;
            rightFireGesture = rightOpenPalm;
            rightIceGesture = rightFist;
            rightThunderGesture = rightThumbsUp;
            rightCombine = rightTwoFinger;
            SubscribeHandEvents();
        }

        public void InjectPose(PoseId left, PoseId right)
        {
            ApplyPoseIdForTest(true, left);
            ApplyPoseIdForTest(false, right);
            OnPoseDetected?.Invoke(leftPoseId, rightPoseId);
        }

        public void InjectGrimoireTrigger()
        {
            OnGrimTrigger?.Invoke();
        }

        private void ResolveHandTrackingEvents()
        {
            if (leftHandTrackingEvents != null && rightHandTrackingEvents != null)
                return;

            foreach (var events in FindObjectsByType<XRHandTrackingEvents>(FindObjectsInactive.Include))
            {
                if (events.handedness == Handedness.Left && leftHandTrackingEvents == null)
                    leftHandTrackingEvents = events;
                else if (events.handedness == Handedness.Right && rightHandTrackingEvents == null)
                    rightHandTrackingEvents = events;
            }
        }

        private void SubscribeHandEvents()
        {
            if (leftHandTrackingEvents != null)
                leftHandTrackingEvents.jointsUpdated.AddListener(HandleLeftJointsUpdated);

            if (rightHandTrackingEvents != null)
                rightHandTrackingEvents.jointsUpdated.AddListener(HandleRightJointsUpdated);
        }

        private void UnsubscribeHandEvents()
        {
            if (leftHandTrackingEvents != null)
                leftHandTrackingEvents.jointsUpdated.RemoveListener(HandleLeftJointsUpdated);

            if (rightHandTrackingEvents != null)
                rightHandTrackingEvents.jointsUpdated.RemoveListener(HandleRightJointsUpdated);
        }

        private void HandleLeftJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            UpdateHand(true, args);
        }

        private void HandleRightJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            UpdateHand(false, args);
        }

        private void UpdateHand(bool isLeft, XRHandJointsUpdatedEventArgs args)
        {
            var tracked = args.hand.isTracked;
            if (tracked && TryGetPalmPosition(args.hand, out var palm))
            {
                if (isLeft)
                {
                    leftPalmPosition = palm;
                    hasLeftPalm = true;
                }
                else
                {
                    rightPalmPosition = palm;
                    hasRightPalm = true;
                }
            }
            else if (isLeft)
            {
                hasLeftPalm = false;
            }
            else
            {
                hasRightPalm = false;
            }

            SetFingerDebug(isLeft, BuildFingerDebug(args.hand));

            var detected = tracked ? DetectGesture(isLeft, args) : GestureKind.None;
            UpdatePoseState(isLeft, detected, tracked);
        }

        private GestureKind DetectGesture(bool isLeft, XRHandJointsUpdatedEventArgs args)
        {
            if (isLeft)
                return DetectLeftGesture(args);

            return DetectRightGesture(args);
        }

        private GestureKind DetectRightGesture(XRHandJointsUpdatedEventArgs args)
        {
            if (Matches(rightBarrier, args))
                return GestureKind.Barrier;
            if (Matches(rightCombineShoot, args))
                return GestureKind.CombineShoot;
            if (Matches(rightCombine, args))
                return GestureKind.Combine;

            var thunderChargeMatched = Matches(rightThunderGesture, args);
            if (Time.unscaledTime <= rightThunderShootArmedUntilTime && Matches(rightThunderShootGesture, args))
                return GestureKind.ThunderShoot;
            if (Matches(rightPageTurnGesture, args))
                return GestureKind.PageTurn;
            if (thunderChargeMatched)
                return GestureKind.Thunder;
            if (Matches(rightIceGesture, args))
                return GestureKind.Ice;
            if (Matches(rightFireGesture, args))
                return GestureKind.Fire;

            return GestureKind.None;
        }

        private GestureKind DetectLeftGesture(XRHandJointsUpdatedEventArgs args)
        {
            if (Matches(leftBarrier, args))
                return GestureKind.Barrier;
            if (Matches(leftCombineShoot, args))
                return GestureKind.CombineShoot;
            if (Matches(leftCombine, args))
                return GestureKind.Combine;
            if (Matches(leftThunder, args))
                return GestureKind.Thunder;
            if (Matches(leftIce, args))
                return GestureKind.Ice;
            if (Matches(leftFire, args))
                return GestureKind.Fire;
            if (Matches(leftGrimoireGesture, args))
                return GestureKind.Grimoire;

            return GestureKind.None;
        }

        private static bool Matches(XRHandShape shape, XRHandJointsUpdatedEventArgs args)
        {
            return shape != null && shape.CheckConditions(args);
        }

        private void UpdatePoseState(bool isLeft, GestureKind detected, bool tracked)
        {
            ref var candidate = ref (isLeft ? ref leftCandidateKind : ref rightCandidateKind);
            ref var candidateStart = ref (isLeft ? ref leftCandidateStartTime : ref rightCandidateStartTime);
            ref var lostStart = ref (isLeft ? ref leftLostStartTime : ref rightLostStartTime);

            if (detected == GestureKind.None)
            {
                if (!tracked)
                    SetStatus(isLeft, "not tracked");

                if (GetConfirmedKind(isLeft) == GestureKind.None)
                {
                    candidate = GestureKind.None;
                    lostStart = -1f;
                    return;
                }

                if (lostStart < 0f)
                    lostStart = Time.unscaledTime;

                if (Time.unscaledTime - lostStart >= Mathf.Max(0f, poseLostGracePeriod))
                    ClearHand(isLeft, false);

                return;
            }

            lostStart = -1f;
            SetStatus(isLeft, detected.ToString());

            if (candidate != detected)
            {
                candidate = detected;
                candidateStart = Time.unscaledTime;
                return;
            }

            if (GetConfirmedKind(isLeft) == detected)
                return;

            if (Time.unscaledTime - candidateStart >= Mathf.Max(0f, poseHoldDuration))
                ConfirmGesture(isLeft, detected);
        }

        private void ConfirmGesture(bool isLeft, GestureKind kind)
        {
            var previousKind = GetConfirmedKind(isLeft);
            var previousWasElement = IsElementGesture(previousKind);
            var newIsElement = IsElementGesture(kind);
            var pose = ToPoseType(kind);

            // When a non-element gesture (e.g. Grimoire) is replaced by a newly confirmed gesture
            // without going through None, ClearHand is never called and OnGestureCleared is never fired.
            // Fire it here before the new gesture state is applied.
            if (previousKind != GestureKind.None && previousKind != kind && !previousWasElement)
                OnGestureCleared?.Invoke(isLeft, previousKind.ToString());

            if (isLeft)
            {
                leftConfirmedKind = kind;
                leftConfirmedPose = pose;
                leftPoseId = ToPoseId(kind);
                CurrentLeftPrototypeDebug = $"L: {pose}";

                if (pose == PoseType.Fist && !leftFistActive)
                {
                    leftFistActive = true;
                    OnLeftFistStart?.Invoke();
                }
            }
            else
            {
                rightConfirmedKind = kind;
                rightConfirmedPose = pose;
                rightPoseId = ToPoseId(kind);
                CurrentRightPrototypeDebug = $"R: {pose}";
            }

            if (previousWasElement && !newIsElement)
            {
                if (!isLeft)
                    OnRightPoseCleared?.Invoke();

                OnHandPoseCleared?.Invoke(isLeft);
                OnPoseCleared?.Invoke();
            }

            if (kind == GestureKind.Grimoire)
                OnGrimTrigger?.Invoke();
            else if (!isLeft && kind == GestureKind.Thunder)
                rightThunderShootArmedUntilTime = Time.unscaledTime + Mathf.Max(0f, rightThunderShootArmWindowSeconds);

            OnGestureConfirmed?.Invoke(isLeft, kind.ToString(), pose);

            if (newIsElement)
            {
                if (!isLeft)
                    OnRightPoseConfirmed?.Invoke(pose);

                OnHandPoseConfirmed?.Invoke(isLeft, pose);
                OnPoseConfirmed?.Invoke(pose);
            }

            OnPoseDetected?.Invoke(leftPoseId, rightPoseId);
            LogDebug($"{(isLeft ? "Left" : "Right")} gesture confirmed: {kind}");
        }

        private void ClearHand(bool isLeft, bool force)
        {
            var hadPose = GetConfirmedPose(isLeft) != PoseType.None || force;
            var clearedKind = GetConfirmedKind(isLeft);
            if (isLeft)
            {
                if (leftFistActive)
                {
                    leftFistActive = false;
                    OnLeftFistEnd?.Invoke();
                }

                leftCandidateKind = GestureKind.None;
                leftConfirmedPose = PoseType.None;
                leftConfirmedKind = GestureKind.None;
                leftPoseId = PoseId.None;
                leftLostStartTime = -1f;
                CurrentLeftPrototypeDebug = "L: none";
            }
            else
            {
                rightCandidateKind = GestureKind.None;
                rightConfirmedPose = PoseType.None;
                rightConfirmedKind = GestureKind.None;
                rightPoseId = PoseId.None;
                rightLostStartTime = -1f;
                CurrentRightPrototypeDebug = "R: none";
                if (IsElementGesture(clearedKind))
                    OnRightPoseCleared?.Invoke();
            }

            if (!hadPose)
                return;

            if (IsElementGesture(clearedKind))
                OnHandPoseCleared?.Invoke(isLeft);

            OnGestureCleared?.Invoke(isLeft, clearedKind.ToString());
            OnPoseCleared?.Invoke();
            OnPoseDetected?.Invoke(leftPoseId, rightPoseId);
            LogDebug($"{(isLeft ? "Left" : "Right")} pose cleared");
        }

        private GestureKind GetConfirmedKind(bool isLeft)
        {
            return isLeft ? leftConfirmedKind : rightConfirmedKind;
        }

        private PoseType GetConfirmedPose(bool isLeft)
        {
            return isLeft ? leftConfirmedPose : rightConfirmedPose;
        }

        private void ApplyPoseIdForTest(bool isLeft, PoseId pose)
        {
            var prototypePose = ToPoseType(pose);
            if (isLeft)
            {
                leftPoseId = pose;
                leftConfirmedPose = prototypePose;
                leftConfirmedKind = ToGestureKind(prototypePose);
                leftFistActive = pose == PoseId.Fist || pose == PoseId.FistPush;
            }
            else
            {
                rightPoseId = pose;
                rightConfirmedPose = prototypePose;
                rightConfirmedKind = ToGestureKind(prototypePose);
            }
        }

        private void UpdateCombinePush()
        {
            CurrentCombineForwardSpeed = 0f;
            if (!hasLeftPalm || !hasRightPalm)
            {
                ResetCombineState();
                return;
            }

            IsCombineCandidate = Vector3.Distance(leftPalmPosition, rightPalmPosition) <= Mathf.Max(0f, combineDistance);
            if (!IsCombineCandidate)
            {
                hasPreviousCombineMidpoint = false;
                return;
            }

            var midpoint = (leftPalmPosition + rightPalmPosition) * 0.5f;
            if (hasPreviousCombineMidpoint && Time.deltaTime > 0f)
            {
                var velocity = (midpoint - previousCombineMidpoint) / Time.deltaTime;
                var forward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                CurrentCombineForwardSpeed = Vector3.Dot(velocity, forward);

                if (CurrentCombineForwardSpeed >= pushVelocity &&
                    Time.time - lastCombinePushTime >= combinePushCooldown)
                {
                    lastCombinePushTime = Time.time;
                    OnCombinePushDetected?.Invoke();
                }
            }

            previousCombineMidpoint = midpoint;
            hasPreviousCombineMidpoint = true;
        }

        private void ResetCombineState()
        {
            IsCombineCandidate = false;
            CurrentCombineForwardSpeed = 0f;
            hasPreviousCombineMidpoint = false;
        }

        private void SetStatus(bool isLeft, string status)
        {
            if (isLeft)
                CurrentLeftHandStatus = $"[L] XRHands {status}";
            else
                CurrentRightHandStatus = $"[R] XRHands {status}";
        }

        private void SetFingerDebug(bool isLeft, FingerPoseDebug debug)
        {
            if (isLeft)
                CurrentLeftFingerDebug = debug;
            else
                CurrentRightFingerDebug = debug;
        }

        private static FingerPoseDebug BuildFingerDebug(XRHand hand)
        {
            var debug = new FingerPoseDebug { hasData = hand.isTracked };
            if (!hand.isTracked)
                return debug;

            var palmOk = TryGetJointPosition(hand, XRHandJointID.Palm, out var palm);
            if (!palmOk)
                palmOk = TryGetJointPosition(hand, XRHandJointID.Wrist, out palm);

            if (!palmOk)
                return debug;

            debug.thumbCurl = EstimateCurl(hand, XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbTip, palm);
            debug.indexCurl = EstimateCurl(hand, XRHandJointID.IndexProximal, XRHandJointID.IndexTip, palm);
            debug.middleCurl = EstimateCurl(hand, XRHandJointID.MiddleProximal, XRHandJointID.MiddleTip, palm);
            debug.ringCurl = EstimateCurl(hand, XRHandJointID.RingProximal, XRHandJointID.RingTip, palm);
            debug.pinkyCurl = EstimateCurl(hand, XRHandJointID.LittleProximal, XRHandJointID.LittleTip, palm);
            debug.thumbExtended = debug.thumbCurl < 0.5f;
            debug.indexExtended = debug.indexCurl < 0.5f;
            debug.middleExtended = debug.middleCurl < 0.5f;
            debug.ringExtended = debug.ringCurl < 0.5f;
            debug.pinkyExtended = debug.pinkyCurl < 0.5f;
            return debug;
        }

        private static float EstimateCurl(XRHand hand, XRHandJointID rootJoint, XRHandJointID tipJoint, Vector3 palm)
        {
            if (!TryGetJointPosition(hand, rootJoint, out var root) ||
                !TryGetJointPosition(hand, tipJoint, out var tip))
            {
                return 1f;
            }

            var rootDistance = Mathf.Max(0.001f, Vector3.Distance(root, palm));
            var tipDistance = Vector3.Distance(tip, palm);
            return Mathf.Clamp01(1f - tipDistance / (rootDistance * 2.1f));
        }

        private static bool TryGetPalmPosition(XRHand hand, out Vector3 palmPosition)
        {
            palmPosition = Vector3.zero;
            if (!hand.isTracked)
                return false;

            return TryGetJointPosition(hand, XRHandJointID.Palm, out palmPosition) ||
                   TryGetJointPosition(hand, XRHandJointID.Wrist, out palmPosition);
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

        private static PoseId ToPoseId(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => PoseId.OpenPalm,
                PoseType.Fist => PoseId.Fist,
                PoseType.ThumbsUp => PoseId.Horn,
                PoseType.TwoFinger => PoseId.IndexPoint,
                _ => PoseId.None
            };
        }

        private static PoseId ToPoseId(GestureKind kind)
        {
            return kind switch
            {
                GestureKind.Fire => PoseId.Fist,
                GestureKind.Ice => PoseId.Ok,
                GestureKind.Thunder => PoseId.Horn,
                GestureKind.ThunderShoot => PoseId.FistPush,
                GestureKind.Combine => PoseId.Combine,
                GestureKind.CombineShoot => PoseId.FistPush,
                _ => PoseId.None
            };
        }

        private static PoseType ToPoseType(GestureKind kind)
        {
            return kind switch
            {
                GestureKind.Fire => PoseType.OpenPalm,
                GestureKind.Ice => PoseType.Fist,
                GestureKind.Thunder => PoseType.ThumbsUp,
                GestureKind.ThunderShoot => PoseType.TwoFinger,
                GestureKind.Combine => PoseType.TwoFinger,
                GestureKind.CombineShoot => PoseType.TwoFinger,
                GestureKind.Barrier => PoseType.Fist,
                GestureKind.Grimoire => PoseType.OpenPalm,
                GestureKind.PageTurn => PoseType.OpenPalm,
                _ => PoseType.None
            };
        }

        private static GestureKind ToGestureKind(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => GestureKind.Fire,
                PoseType.Fist => GestureKind.Ice,
                PoseType.ThumbsUp => GestureKind.Thunder,
                PoseType.TwoFinger => GestureKind.Combine,
                _ => GestureKind.None
            };
        }

        private static bool IsElementGesture(GestureKind kind)
        {
            return kind == GestureKind.Fire ||
                   kind == GestureKind.Ice ||
                   kind == GestureKind.Thunder ||
                   kind == GestureKind.ThunderShoot; // ThunderShoot is part of Thunder family; must not fire OnGestureCleared on Thunder↔ThunderShoot transitions
        }

        private static PoseType ToPoseType(PoseId pose)
        {
            return pose switch
            {
                PoseId.OpenPalm => PoseType.OpenPalm,
                PoseId.Fist => PoseType.Fist,
                PoseId.FistPush => PoseType.Fist,
                PoseId.Horn => PoseType.ThumbsUp,
                PoseId.IndexPoint => PoseType.TwoFinger,
                _ => PoseType.None
            };
        }

        private void LogDebug(string message)
        {
            if (showDebugLog)
                Debug.Log($"[GestureDetector] {message}", this);
        }
    }
}
