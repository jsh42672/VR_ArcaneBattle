using System;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Debug observer for GestureDetector events. This component does not drive
    /// gameplay; it keeps a compact log/status view of detected gestures.
    /// </summary>
    public class GestureEventRouter : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private bool showDebugLog;

        [Obsolete("GestureEventRouter is log-only. Subscribe to GestureDetector instead.")]
        public event Action<PoseType> OnRightPoseConfirmed;
        [Obsolete("GestureEventRouter is log-only. Subscribe to GestureDetector instead.")]
        public event Action OnRightPoseCleared;
        [Obsolete("GestureEventRouter is log-only. Subscribe to GestureDetector instead.")]
        public event Action<PoseType> OnLeftPoseConfirmed;
        [Obsolete("GestureEventRouter is log-only. Subscribe to GestureDetector instead.")]
        public event Action OnLeftPoseCleared;
        [Obsolete("GestureEventRouter is log-only. Subscribe to GestureDetector instead.")]
        public event Action OnLeftFistStart;
        [Obsolete("GestureEventRouter is log-only. Subscribe to GestureDetector instead.")]
        public event Action OnLeftFistEnd;

        private PoseType currentRightPose = PoseType.None;
        private PoseType currentLeftPose = PoseType.None;
        private bool leftFistActive;
        private int receivedEventCount;

        public PoseType CurrentRightPose => currentRightPose;
        public PoseType CurrentLeftPose => currentLeftPose;
        public bool LeftFistActive => leftFistActive;
        public bool HasReceivedGestureEvent => receivedEventCount > 0;
        public bool IsAnyGestureActive => currentRightPose != PoseType.None || currentLeftPose != PoseType.None || leftFistActive;
        public int ReceivedEventCount => receivedEventCount;
        public string DebugStatus { get; private set; } = "XR Gesture Router: waiting";

        private bool subscribed;

        private void Awake()
        {
            ResolveDetector();
        }

        private void OnEnable()
        {
            ResolveDetector();
            SubscribeDetector();
        }

        private void OnDisable()
        {
            UnsubscribeDetector();
        }

        public void OnOpenPalmStart()
        {
            MarkEvent();
            SetRightPose(PoseType.OpenPalm);
        }

        public void OnFistRightStart()
        {
            MarkEvent();
            SetRightPose(PoseType.Fist);
        }

        public void OnThumbsUpStart()
        {
            MarkEvent();
            SetRightPose(PoseType.ThumbsUp);
        }

        public void OnTwoFingerStart()
        {
            MarkEvent();
            SetRightPose(PoseType.TwoFinger);
        }

        public void OnLeftFistDetected()
        {
            MarkEvent();
            SetLeftPose(PoseType.Fist);
            if (leftFistActive)
                return;

            leftFistActive = true;
            DebugStatus = "XR Gesture Router: Left Fist start";
            LogDebug("[GESTURE] Left Fist start");
        }

        public void OnOpenPalmEnd()
        {
            MarkEvent();
            ClearRightPoseIf(PoseType.OpenPalm);
        }

        public void OnOpenPalmLeftStart()
        {
            MarkEvent();
            SetLeftPose(PoseType.OpenPalm);
        }

        public void OnOpenPalmLeftEnd()
        {
            MarkEvent();
            ClearLeftPoseIf(PoseType.OpenPalm);
        }

        public void OnFistRightEnd()
        {
            MarkEvent();
            ClearRightPoseIf(PoseType.Fist);
        }

        public void OnThumbsUpEnd()
        {
            MarkEvent();
            ClearRightPoseIf(PoseType.ThumbsUp);
        }

        public void OnTwoFingerEnd()
        {
            MarkEvent();
            ClearRightPoseIf(PoseType.TwoFinger);
        }

        public void OnThumbsUpLeftStart()
        {
            MarkEvent();
            SetLeftPose(PoseType.ThumbsUp);
        }

        public void OnTwoFingerLeftStart()
        {
            MarkEvent();
            SetLeftPose(PoseType.TwoFinger);
        }

        public void OnThumbsUpLeftEnd()
        {
            MarkEvent();
            ClearLeftPoseIf(PoseType.ThumbsUp);
        }

        public void OnTwoFingerLeftEnd()
        {
            MarkEvent();
            ClearLeftPoseIf(PoseType.TwoFinger);
        }

        public void OnLeftFistLost()
        {
            MarkEvent();
            ClearLeftPoseIf(PoseType.Fist);
            if (!leftFistActive)
                return;

            leftFistActive = false;
            DebugStatus = "XR Gesture Router: Left Fist end";
            LogDebug("[GESTURE] Left Fist end");
        }

        private void MarkEvent()
        {
            receivedEventCount++;
        }

        private void SetRightPose(PoseType pose)
        {
            if (currentRightPose == pose)
                return;

            currentRightPose = pose;
            DebugStatus = $"XR Gesture Router: Right {pose}";
            LogDebug($"[GESTURE] Right pose: {pose}");
        }

        private void SetLeftPose(PoseType pose)
        {
            if (currentLeftPose == pose)
                return;

            currentLeftPose = pose;
            DebugStatus = $"XR Gesture Router: Left {pose}";
            LogDebug($"[GESTURE] Left pose: {pose}");
        }

        private void ClearRightPoseIf(PoseType pose)
        {
            if (currentRightPose != pose)
                return;

            currentRightPose = PoseType.None;
            DebugStatus = $"XR Gesture Router: Right cleared {pose}";
            LogDebug($"[GESTURE] Right pose cleared: {pose}");
        }

        private void ClearLeftPoseIf(PoseType pose)
        {
            if (currentLeftPose != pose)
                return;

            currentLeftPose = PoseType.None;
            DebugStatus = $"XR Gesture Router: Left cleared {pose}";
            LogDebug($"[GESTURE] Left pose cleared: {pose}");
        }

        private void ResolveDetector()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();
        }

        private void SubscribeDetector()
        {
            if (subscribed || gestureDetector == null)
                return;

            gestureDetector.OnGestureConfirmed += HandleDetectorGestureConfirmed;
            gestureDetector.OnGestureCleared += HandleDetectorGestureCleared;
            gestureDetector.OnLeftFistStart += HandleDetectorLeftFistStart;
            gestureDetector.OnLeftFistEnd += HandleDetectorLeftFistEnd;
            subscribed = true;
        }

        private void UnsubscribeDetector()
        {
            if (!subscribed || gestureDetector == null)
                return;

            gestureDetector.OnGestureConfirmed -= HandleDetectorGestureConfirmed;
            gestureDetector.OnGestureCleared -= HandleDetectorGestureCleared;
            gestureDetector.OnLeftFistStart -= HandleDetectorLeftFistStart;
            gestureDetector.OnLeftFistEnd -= HandleDetectorLeftFistEnd;
            subscribed = false;
        }

        private void HandleDetectorGestureConfirmed(bool isLeft, string gestureName, PoseType pose)
        {
            MarkEvent();
            if (isLeft)
                SetLeftPose(pose);
            else
                SetRightPose(pose);

            DebugStatus = $"XR Gesture Router: {(isLeft ? "Left" : "Right")} {gestureName}";
            LogDebug($"[GESTURE] {(isLeft ? "Left" : "Right")} gesture: {gestureName} ({pose})");
        }

        private void HandleDetectorGestureCleared(bool isLeft, string gestureName)
        {
            MarkEvent();
            if (isLeft)
            {
                var pose = currentLeftPose;
                currentLeftPose = PoseType.None;
                DebugStatus = $"XR Gesture Router: Left cleared {gestureName}";
                LogDebug($"[GESTURE] Left gesture cleared: {gestureName} ({pose})");
                return;
            }

            var rightPose = currentRightPose;
            currentRightPose = PoseType.None;
            DebugStatus = $"XR Gesture Router: Right cleared {gestureName}";
            LogDebug($"[GESTURE] Right gesture cleared: {gestureName} ({rightPose})");
        }

        private void HandleDetectorLeftFistStart()
        {
            leftFistActive = true;
            DebugStatus = "XR Gesture Router: Left Fist start";
            LogDebug("[GESTURE] Left Fist start");
        }

        private void HandleDetectorLeftFistEnd()
        {
            leftFistActive = false;
            DebugStatus = "XR Gesture Router: Left Fist end";
            LogDebug("[GESTURE] Left Fist end");
        }

        private void LogDebug(string message)
        {
            if (showDebugLog)
                Debug.Log(message);
        }
    }
}
