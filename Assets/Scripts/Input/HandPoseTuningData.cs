using UnityEngine;

namespace ArcaneVR.Input
{
    [CreateAssetMenu(fileName = "HandPoseTuningData", menuName = "ArcaneVR/HandPoseTuningData")]
    public class HandPoseTuningData : ScriptableObject
    {
        [Header("Timing")]
        public float stablePoseDuration = 0.1f;
        public float grimoireHoldDuration = 0.3f;

        [Header("OVR Pinch Shape")]
        [Range(0f, 1f)] public float pinchThreshold = 0.65f;
        [Range(0f, 1f)] public float openThreshold = 0.25f;
        [Range(0f, 1f)] public float okThumbIndexThreshold = 0.55f;

        [Header("XR Hands Shape")]
        public float okTipDistance = 0.04f;
        public float extendedDistance = 0.105f;
        public float indexExtendedDistance = 0.11f;
        public float curledDistance = 0.025f;
        public float relaxedDistance = 0.1f;

        [Header("Motion")]
        public float combineDistance = 0.16f;
        public float pushVelocity = 0.45f;
    }
}
