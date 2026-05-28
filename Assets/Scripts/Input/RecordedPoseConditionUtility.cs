using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Input
{
    public enum RecordedPoseConditionKind
    {
        None,
        BothForward,
        PalmTogether
    }

    public readonly struct RecordedPoseHandSample
    {
        public readonly bool leftTracked;
        public readonly bool rightTracked;
        public readonly Vector3 leftPalmPosition;
        public readonly Vector3 rightPalmPosition;
        public readonly Vector3 headPosition;
        public readonly Vector3 headForward;

        public RecordedPoseHandSample(
            bool leftTracked,
            bool rightTracked,
            Vector3 leftPalmPosition,
            Vector3 rightPalmPosition,
            Vector3 headPosition,
            Vector3 headForward)
        {
            this.leftTracked = leftTracked;
            this.rightTracked = rightTracked;
            this.leftPalmPosition = leftPalmPosition;
            this.rightPalmPosition = rightPalmPosition;
            this.headPosition = headPosition;
            this.headForward = headForward.sqrMagnitude > 0.0001f ? headForward.normalized : Vector3.forward;
        }
    }

    public static class RecordedPoseConditionUtility
    {
        private const string LeftPrefix = "left";
        private const string RightPrefix = "right";
        private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();

        public static RecordedPoseConditionKind GetConditionKind(string poseName)
        {
            var normalized = NormalizeName(poseName);
            if (normalized.Contains("bothforward"))
                return RecordedPoseConditionKind.BothForward;

            if (normalized.Contains("palmtogether"))
                return RecordedPoseConditionKind.PalmTogether;

            return RecordedPoseConditionKind.None;
        }

        public static string GetShortPoseName(string poseName)
        {
            var normalized = NormalizeName(poseName);
            var side = normalized.StartsWith(LeftPrefix) ? "left" : normalized.StartsWith(RightPrefix) ? "right" : "pose";

            if (normalized.Contains("bothforward"))
                return $"{side}_both_fwd";

            if (normalized.Contains("palmtogether"))
                return $"{side}_palm_together";

            if (normalized.Contains("openpalm"))
                return $"{side}_open";

            if (normalized.Contains("thumbsup"))
                return $"{side}_thumb";

            if (normalized.Contains("fist"))
                return $"{side}_fist";

            if (normalized.Contains("gun"))
                return $"{side}_gun";

            return poseName;
        }

        public static bool AreBothHandsForward(RecordedPoseHandSample sample, float minForwardDistance)
        {
            if (!sample.leftTracked || !sample.rightTracked)
                return false;

            var leftForwardDistance = Vector3.Dot(sample.leftPalmPosition - sample.headPosition, sample.headForward);
            var rightForwardDistance = Vector3.Dot(sample.rightPalmPosition - sample.headPosition, sample.headForward);
            return leftForwardDistance >= minForwardDistance && rightForwardDistance >= minForwardDistance;
        }

        public static bool ArePalmsTogether(RecordedPoseHandSample sample, float maxPalmDistance)
        {
            if (!sample.leftTracked || !sample.rightTracked)
                return false;

            return Vector3.Distance(sample.leftPalmPosition, sample.rightPalmPosition) <= maxPalmDistance;
        }

        public static bool ShouldBlockSingleRightOpenPalm(
            string poseName,
            MetaHandPoseGestureBridge.Handedness hand,
            PoseType pose,
            bool bothHandsForward)
        {
            return bothHandsForward &&
                   hand == MetaHandPoseGestureBridge.Handedness.Right &&
                   pose == PoseType.OpenPalm &&
                   GetConditionKind(poseName) == RecordedPoseConditionKind.None;
        }

        public static bool IsConditionSatisfied(
            RecordedPoseConditionKind conditionKind,
            float minForwardDistance,
            float palmTogetherDistance)
        {
            if (conditionKind == RecordedPoseConditionKind.None)
                return true;

            if (!TryGetCurrentHandSample(out var sample))
                return false;

            return conditionKind switch
            {
                RecordedPoseConditionKind.BothForward => AreBothHandsForward(sample, minForwardDistance),
                RecordedPoseConditionKind.PalmTogether => ArePalmsTogether(sample, palmTogetherDistance),
                _ => true
            };
        }

        public static bool AreBothHandsForwardNow(float minForwardDistance)
        {
            return TryGetCurrentHandSample(out var sample) && AreBothHandsForward(sample, minForwardDistance);
        }

        private static bool TryGetCurrentHandSample(out RecordedPoseHandSample sample)
        {
            sample = default;
            var subsystem = GetRunningHandSubsystem();
            if (subsystem == null)
                return false;

            var leftTracked = TryGetPalmPosition(subsystem.leftHand, out var leftPalmPosition);
            var rightTracked = TryGetPalmPosition(subsystem.rightHand, out var rightPalmPosition);
            var headTransform = Camera.main != null ? Camera.main.transform : null;
            var headPosition = headTransform != null ? headTransform.position : Vector3.zero;
            var headForward = headTransform != null ? headTransform.forward : Vector3.forward;

            sample = new RecordedPoseHandSample(
                leftTracked,
                rightTracked,
                leftPalmPosition,
                rightPalmPosition,
                headPosition,
                headForward);
            return leftTracked || rightTracked;
        }

        private static XRHandSubsystem GetRunningHandSubsystem()
        {
            HandSubsystems.Clear();
            SubsystemManager.GetSubsystems(HandSubsystems);
            foreach (var subsystem in HandSubsystems)
            {
                if (subsystem != null && subsystem.running)
                    return subsystem;
            }

            return null;
        }

        private static bool TryGetPalmPosition(XRHand hand, out Vector3 position)
        {
            position = Vector3.zero;
            if (!hand.isTracked)
                return false;

            var palm = hand.GetJoint(XRHandJointID.Palm);
            if (palm.TryGetPose(out var palmPose))
            {
                position = palmPose.position;
                return true;
            }

            position = hand.rootPose.position;
            return true;
        }

        private static string NormalizeName(string poseName)
        {
            return string.IsNullOrWhiteSpace(poseName)
                ? string.Empty
                : poseName.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }
    }
}
