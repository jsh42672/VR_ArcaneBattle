using UnityEngine;

namespace ArcaneVR.Input
{
    public class MetaHandPoseDebugLogger : MonoBehaviour
    {
        [SerializeField] private string poseName;

        public void LogSelected()
        {
            Debug.Log($"[MetaHandPose] SELECTED {GetPoseName()}", this);
        }

        public void LogUnselected()
        {
            Debug.Log($"[MetaHandPose] UNSELECTED {GetPoseName()}", this);
        }

        private string GetPoseName()
        {
            return string.IsNullOrWhiteSpace(poseName) ? gameObject.name : poseName;
        }
    }
}
