using System;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Receives Unity XR Hands StaticHandGesture UnityEvents and exposes the same
    /// prototype gesture events used by the ArcaneVR input pipeline.
    /// </summary>
    public class GestureEventRouter : MonoBehaviour
    {
        [SerializeField] private bool showDebugLog;

        public event Action<PoseType> OnRightPoseConfirmed;
        public event Action OnRightPoseCleared;
        public event Action<PoseType> OnLeftPoseConfirmed;
        public event Action OnLeftPoseCleared;
        public event Action OnLeftFistStart;
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
            OnLeftFistStart?.Invoke();
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
            OnLeftFistEnd?.Invoke();
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

            if (currentRightPose != PoseType.None && GetPosePriority(pose) < GetPosePriority(currentRightPose))
            {
                DebugStatus = $"XR Gesture Router: Right ignored {pose}, keeping {currentRightPose}";
                LogDebug($"[GESTURE] Right ignored lower-priority pose: {pose}, current: {currentRightPose}");
                return;
            }

            currentRightPose = pose;
            DebugStatus = $"XR Gesture Router: Right {pose}";
            OnRightPoseConfirmed?.Invoke(pose);
            LogDebug($"[GESTURE] Right pose: {pose}");
        }

        private void SetLeftPose(PoseType pose)
        {
            if (currentLeftPose == pose)
                return;

            if (currentLeftPose != PoseType.None && GetPosePriority(pose) < GetPosePriority(currentLeftPose))
            {
                DebugStatus = $"XR Gesture Router: Left ignored {pose}, keeping {currentLeftPose}";
                LogDebug($"[GESTURE] Left ignored lower-priority pose: {pose}, current: {currentLeftPose}");
                return;
            }

            currentLeftPose = pose;
            DebugStatus = $"XR Gesture Router: Left {pose}";
            OnLeftPoseConfirmed?.Invoke(pose);
            LogDebug($"[GESTURE] Left pose: {pose}");
        }

        private void ClearRightPoseIf(PoseType pose)
        {
            if (currentRightPose != pose)
                return;

            currentRightPose = PoseType.None;
            DebugStatus = $"XR Gesture Router: Right cleared {pose}";
            OnRightPoseCleared?.Invoke();
            LogDebug($"[GESTURE] Right pose cleared: {pose}");
        }

        private void ClearLeftPoseIf(PoseType pose)
        {
            if (currentLeftPose != pose)
                return;

            currentLeftPose = PoseType.None;
            DebugStatus = $"XR Gesture Router: Left cleared {pose}";
            OnLeftPoseCleared?.Invoke();
            LogDebug($"[GESTURE] Left pose cleared: {pose}");
        }

        private void LogDebug(string message)
        {
            if (showDebugLog)
                Debug.Log(message);
        }

        private static int GetPosePriority(PoseType pose)
        {
            return pose switch
            {
                PoseType.ThumbsUp => 3,
                PoseType.TwoFinger => 2,
                PoseType.Fist => 2,
                PoseType.OpenPalm => 1,
                _ => 0
            };
        }
    }
}
