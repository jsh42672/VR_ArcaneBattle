using System;
using UnityEngine;

namespace ArcaneVR.Input
{
    public enum PoseId
    {
        Unknown
    }

    /// <summary>
    /// Detects individual hand poses via Meta XR Hand Tracking API and outputs Pose IDs for left and right hands.
    /// </summary>
    public class GestureDetector : MonoBehaviour
    {
        public event Action<PoseId, PoseId> OnPoseDetected;

        // TODO: Implement
    }
}
