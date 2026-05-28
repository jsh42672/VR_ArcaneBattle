using UnityEngine;
using Oculus.Interaction;

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
        [SerializeField] private bool autoApplyRecordedPoseConditions = true;
        [SerializeField] private float bothForwardMinDistance = 0.32f;
        [SerializeField] private float palmTogetherMaxDistance = 0.14f;

        private ActiveStateGroup activeStateGroup;
        private bool selectionForwardedToRouter;

        public void Configure(Handedness targetHand, PoseType targetPose, bool overrideOvrPrototype = true)
        {
            hand = targetHand;
            pose = targetPose;
            routerOverridesOvrPrototype = overrideOvrPrototype;
        }

        private void Awake()
        {
            ResolveRouter();
            ResolveActiveStateGroup();
            ApplyRouterPriority();
        }

        private void OnEnable()
        {
            ResolveRouter();
            ResolveActiveStateGroup();
            ApplyRouterPriority();
        }

        private void OnDisable()
        {
            ClearSelectionFromRouter();
        }

        private void Update()
        {
            if (!UsesRuntimeGate())
                return;

            SyncConditionalSelection();
        }

        public void HandleSelected()
        {
            ResolveRouter();
            if (router == null)
                return;

            ApplyRouterPriority();

            if (UsesRuntimeGate())
            {
                SyncConditionalSelection();
                return;
            }

            ForwardSelectionToRouter();
        }

        public void HandleUnselected()
        {
            ResolveRouter();
            if (router == null)
                return;

            if (UsesRuntimeGate())
            {
                SyncConditionalSelection();
                return;
            }

            ClearSelectionFromRouter();
        }

        private void ResolveRouter()
        {
            if (router == null)
                router = FindAnyObjectByType<GestureEventRouter>();
        }

        private void ResolveActiveStateGroup()
        {
            if (activeStateGroup == null)
                activeStateGroup = GetComponentInChildren<ActiveStateGroup>(true);
        }

        private bool UsesRuntimeGate()
        {
            if (!autoApplyRecordedPoseConditions)
                return false;

            return RecordedPoseConditionUtility.GetConditionKind(name) != RecordedPoseConditionKind.None ||
                   IsSingleRightOpenPalmCandidate();
        }

        private void SyncConditionalSelection()
        {
            ResolveRouter();
            ResolveActiveStateGroup();
            if (router == null || activeStateGroup == null)
                return;

            var shouldForward = activeStateGroup.Active && CanForwardSelection();
            if (shouldForward && !selectionForwardedToRouter)
                ForwardSelectionToRouter();
            else if (!shouldForward && selectionForwardedToRouter)
                ClearSelectionFromRouter();
        }

        private bool CanForwardSelection()
        {
            var conditionKind = RecordedPoseConditionUtility.GetConditionKind(name);
            if (!RecordedPoseConditionUtility.IsConditionSatisfied(
                    conditionKind,
                    bothForwardMinDistance,
                    palmTogetherMaxDistance))
            {
                return false;
            }

            var bothHandsForward = RecordedPoseConditionUtility.AreBothHandsForwardNow(bothForwardMinDistance);
            return !RecordedPoseConditionUtility.ShouldBlockSingleRightOpenPalm(name, hand, pose, bothHandsForward);
        }

        private bool IsSingleRightOpenPalmCandidate()
        {
            return hand == Handedness.Right &&
                   pose == PoseType.OpenPalm &&
                   RecordedPoseConditionUtility.GetConditionKind(name) == RecordedPoseConditionKind.None;
        }

        private void ForwardSelectionToRouter()
        {
            if (selectionForwardedToRouter)
                return;

            if (hand == Handedness.Right)
                SelectRightPose();
            else
                SelectLeftPose();

            selectionForwardedToRouter = true;

            if (logEvents)
                Debug.Log($"[MetaHandPoseGestureBridge] Selected {hand} {pose}", this);
        }

        private void ClearSelectionFromRouter()
        {
            if (!selectionForwardedToRouter)
                return;

            ResolveRouter();
            if (router == null)
            {
                selectionForwardedToRouter = false;
                return;
            }

            if (hand == Handedness.Right)
                ClearRightPose();
            else
                ClearLeftPose();

            selectionForwardedToRouter = false;

            if (logEvents)
                Debug.Log($"[MetaHandPoseGestureBridge] Unselected {hand} {pose}", this);
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
