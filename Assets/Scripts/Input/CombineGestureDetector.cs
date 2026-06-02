using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace ArcaneVR.Input
{
    /// <summary>
    /// XR Hands only detector for the two-hand combine stance.
    /// Keeps combine setup out of the per-element right-hand gesture scripts.
    /// </summary>
    public class CombineGestureDetector : MonoBehaviour
    {
        [Header("XR Hands")]
        [SerializeField] private XRHandTrackingEvents leftHandTrackingEvents;
        [SerializeField] private XRHandTrackingEvents rightHandTrackingEvents;
        [SerializeField] private XRHandShape leftCombineShape;
        [SerializeField] private XRHandShape rightCombineShape;

        [Header("Timing")]
        [SerializeField] private float poseHoldDuration = 0.15f;
        [SerializeField] private float poseLostGracePeriod = 0.25f;

        public event Action<bool> OnCombinePoseChanged;

        public bool IsLeftCombineActive { get; private set; }
        public bool IsRightCombineActive { get; private set; }
        public bool AreBothCombineActive => IsLeftCombineActive && IsRightCombineActive;
        public Vector3 LeftPalmPosition { get; private set; }
        public Vector3 RightPalmPosition { get; private set; }
        public bool HasLeftPalm { get; private set; }
        public bool HasRightPalm { get; private set; }
        public string LastStatus { get; private set; } = "Combine: idle";

        float leftHoldTime;
        float rightHoldTime;
        float leftLostTime = -1f;
        float rightLostTime = -1f;

        void OnEnable()
        {
            if (leftHandTrackingEvents != null)
                leftHandTrackingEvents.jointsUpdated.AddListener(HandleLeftJointsUpdated);

            if (rightHandTrackingEvents != null)
                rightHandTrackingEvents.jointsUpdated.AddListener(HandleRightJointsUpdated);
        }

        void OnDisable()
        {
            if (leftHandTrackingEvents != null)
                leftHandTrackingEvents.jointsUpdated.RemoveListener(HandleLeftJointsUpdated);

            if (rightHandTrackingEvents != null)
                rightHandTrackingEvents.jointsUpdated.RemoveListener(HandleRightJointsUpdated);

            SetLeftActive(false);
            SetRightActive(false);
            HasLeftPalm = false;
            HasRightPalm = false;
        }

        public bool TryGetPalmPositions(out Vector3 leftPalm, out Vector3 rightPalm)
        {
            leftPalm = LeftPalmPosition;
            rightPalm = RightPalmPosition;
            return HasLeftPalm && HasRightPalm;
        }

        void HandleLeftJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            UpdateHand(true, args, leftCombineShape);
        }

        void HandleRightJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            UpdateHand(false, args, rightCombineShape);
        }

        void UpdateHand(bool isLeft, XRHandJointsUpdatedEventArgs args, XRHandShape shape)
        {
            var tracked = args.hand.isTracked;
            var shapeAccepted = tracked && shape != null && shape.CheckConditions(args);

            if (TryGetPalmPosition(args.hand, out var palm))
            {
                if (isLeft)
                {
                    LeftPalmPosition = palm;
                    HasLeftPalm = true;
                }
                else
                {
                    RightPalmPosition = palm;
                    HasRightPalm = true;
                }
            }
            else if (isLeft)
            {
                HasLeftPalm = false;
            }
            else
            {
                HasRightPalm = false;
            }

            UpdatePoseState(isLeft, shapeAccepted);
        }

        void UpdatePoseState(bool isLeft, bool shapeAccepted)
        {
            var wasBothActive = AreBothCombineActive;
            if (shapeAccepted)
            {
                if (isLeft)
                {
                    leftLostTime = -1f;
                    leftHoldTime += Time.unscaledDeltaTime;
                    if (leftHoldTime >= poseHoldDuration)
                        SetLeftActive(true);
                }
                else
                {
                    rightLostTime = -1f;
                    rightHoldTime += Time.unscaledDeltaTime;
                    if (rightHoldTime >= poseHoldDuration)
                        SetRightActive(true);
                }
            }
            else
            {
                UpdateLostState(isLeft);
            }

            LastStatus = $"Combine: L {(IsLeftCombineActive ? "on" : "off")} R {(IsRightCombineActive ? "on" : "off")}";
            if (wasBothActive != AreBothCombineActive)
                OnCombinePoseChanged?.Invoke(AreBothCombineActive);
        }

        void UpdateLostState(bool isLeft)
        {
            var now = Time.unscaledTime;
            if (isLeft)
            {
                leftHoldTime = 0f;
                if (leftLostTime < 0f)
                    leftLostTime = now;
                if (now - leftLostTime >= poseLostGracePeriod)
                    SetLeftActive(false);
            }
            else
            {
                rightHoldTime = 0f;
                if (rightLostTime < 0f)
                    rightLostTime = now;
                if (now - rightLostTime >= poseLostGracePeriod)
                    SetRightActive(false);
            }
        }

        void SetLeftActive(bool active)
        {
            IsLeftCombineActive = active;
        }

        void SetRightActive(bool active)
        {
            IsRightCombineActive = active;
        }

        static bool TryGetPalmPosition(XRHand hand, out Vector3 palmPosition)
        {
            palmPosition = Vector3.zero;
            if (!hand.isTracked)
                return false;

            if (TryGetJointPosition(hand, XRHandJointID.Palm, out palmPosition))
                return true;

            return TryGetJointPosition(hand, XRHandJointID.Wrist, out palmPosition);
        }

        static bool TryGetJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position)
        {
            position = Vector3.zero;
            var joint = hand.GetJoint(jointId);
            if (!joint.TryGetPose(out var pose))
                return false;

            position = pose.position;
            return true;
        }
    }
}
