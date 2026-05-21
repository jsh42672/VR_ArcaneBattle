using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Bridges Meta Interaction SDK hand pose selector events into the Arcane gesture router.
    /// Attach this to a recorded hand pose selector prefab, then wire the selector's
    /// Selected/Unselected UnityEvents to HandleSelected/HandleUnselected.
    /// </summary>
    public class MetaHandPoseGestureBridge : MonoBehaviour
    {
        public enum Handedness
        {
            Right,
            Left
        }

        [SerializeField] private Handedness hand = Handedness.Right;
        [SerializeField] private PoseType pose = PoseType.OpenPalm;
        [SerializeField] private GestureEventRouter router;
        [SerializeField] private bool routerOverridesOvrPrototype = true;
        [SerializeField] private bool logEvents;

        public void Configure(Handedness targetHand, PoseType targetPose, bool overrideOvrPrototype = true)
        {
            hand = targetHand;
            pose = targetPose;
            routerOverridesOvrPrototype = overrideOvrPrototype;
        }

        private void Awake()
        {
            ResolveRouter();
            ApplyRouterPriority();
        }

        private void OnEnable()
        {
            ResolveRouter();
            ApplyRouterPriority();
        }

        public void HandleSelected()
        {
            ResolveRouter();
            if (router == null)
                return;

            ApplyRouterPriority();

            if (hand == Handedness.Right)
                SelectRightPose();
            else
                SelectLeftPose();

            if (logEvents)
                Debug.Log($"[MetaHandPoseGestureBridge] Selected {hand} {pose}", this);
        }

        public void HandleUnselected()
        {
            ResolveRouter();
            if (router == null)
                return;

            if (hand == Handedness.Right)
                ClearRightPose();
            else
                ClearLeftPose();

            if (logEvents)
                Debug.Log($"[MetaHandPoseGestureBridge] Unselected {hand} {pose}", this);
        }

        private void ResolveRouter()
        {
            if (router == null)
                router = FindAnyObjectByType<GestureEventRouter>();
        }

        private void ApplyRouterPriority()
        {
            if (!routerOverridesOvrPrototype)
                return;

            foreach (var detector in FindObjectsByType<GestureDetector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                detector.BindGestureEventRouter(router);
                detector.SetAllowOvrPrototypeOverrideRouter(false);
            }
        }

        private void SelectRightPose()
        {
            switch (pose)
            {
                case PoseType.OpenPalm:
                    router.OnOpenPalmStart();
                    break;
                case PoseType.Fist:
                    router.OnFistRightStart();
                    break;
                case PoseType.ThumbsUp:
                    router.OnThumbsUpStart();
                    break;
                case PoseType.TwoFinger:
                    router.OnTwoFingerStart();
                    break;
            }
        }

        private void SelectLeftPose()
        {
            switch (pose)
            {
                case PoseType.OpenPalm:
                    router.OnOpenPalmLeftStart();
                    break;
                case PoseType.Fist:
                    router.OnLeftFistDetected();
                    break;
                case PoseType.ThumbsUp:
                    router.OnThumbsUpLeftStart();
                    break;
                case PoseType.TwoFinger:
                    router.OnTwoFingerLeftStart();
                    break;
            }
        }

        private void ClearRightPose()
        {
            switch (pose)
            {
                case PoseType.OpenPalm:
                    router.OnOpenPalmEnd();
                    break;
                case PoseType.Fist:
                    router.OnFistRightEnd();
                    break;
                case PoseType.ThumbsUp:
                    router.OnThumbsUpEnd();
                    break;
                case PoseType.TwoFinger:
                    router.OnTwoFingerEnd();
                    break;
            }
        }

        private void ClearLeftPose()
        {
            switch (pose)
            {
                case PoseType.OpenPalm:
                    router.OnOpenPalmLeftEnd();
                    break;
                case PoseType.Fist:
                    router.OnLeftFistLost();
                    break;
                case PoseType.ThumbsUp:
                    router.OnThumbsUpLeftEnd();
                    break;
                case PoseType.TwoFinger:
                    router.OnTwoFingerLeftEnd();
                    break;
            }
        }
    }
}
