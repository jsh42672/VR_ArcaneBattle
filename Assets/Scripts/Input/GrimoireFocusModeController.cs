using ArcaneVR.UI;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Bridges an open grimoire into slow-time focus and locks right-hand element gestures.
    /// </summary>
    public class GrimoireFocusModeController : MonoBehaviour
    {
        private const string FocusReason = "Grimoire";

        [Header("── 핵심 참조 ──")]
        [SerializeField] private ArcaneTimeFocusController timeFocusController;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private LeftGrimoireGesture leftGrimoireGesture;

        [Header("── 오른손 재스처 잠금 ──")]
        [SerializeField] private bool disableRightElementGestures = true;
        [SerializeField] private MonoBehaviour[] rightElementGestures;

        private readonly Dictionary<MonoBehaviour, bool> previousGestureStates = new Dictionary<MonoBehaviour, bool>();
        private bool focusApplied;

        public bool IsFocusApplied => focusApplied;

        private void Awake()
        {
            ResolveReferences();
            AutoPopulateRightElementGestures();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeGrimoireManager();
            SyncFocusState();
        }

        private void OnDisable()
        {
            UnsubscribeGrimoireManager();
            SetFocusApplied(false);
        }

        private void Update()
        {
            ResolveReferences();
            SyncFocusState();
        }

        public void SyncFocusState()
        {
            var grimoireOpen = IsAnyGrimoireOpen();
            SetFocusApplied(grimoireOpen);
        }

        private bool IsAnyGrimoireOpen()
        {
            return (grimoireManager != null && grimoireManager.IsOpen) ||
                   (leftGrimoireGesture != null && leftGrimoireGesture.IsActive);
        }

        private void SetFocusApplied(bool active)
        {
            if (focusApplied == active)
                return;

            focusApplied = active;

            if (timeFocusController != null)
            {
                if (focusApplied)
                    timeFocusController.RequestFocus(FocusReason);
                else
                    timeFocusController.ReleaseFocus(FocusReason);
            }

            SetRightElementGesturesEnabled(!focusApplied);
        }

        private void SetRightElementGesturesEnabled(bool enabled)
        {
            if (!disableRightElementGestures)
                return;

            if (rightElementGestures == null || rightElementGestures.Length == 0)
                AutoPopulateRightElementGestures();

            if (rightElementGestures == null)
                return;

            foreach (var gesture in rightElementGestures)
            {
                if (gesture == null || gesture is RightPageTurnGesture)
                    continue;

                if (!enabled)
                {
                    if (!previousGestureStates.ContainsKey(gesture))
                        previousGestureStates.Add(gesture, gesture.enabled);

                    gesture.enabled = false;
                    continue;
                }

                if (previousGestureStates.TryGetValue(gesture, out var wasEnabled))
                    gesture.enabled = wasEnabled;
            }

            if (enabled)
                previousGestureStates.Clear();
        }

        private void ResolveReferences()
        {
            if (timeFocusController == null)
                timeFocusController = FindAnyObjectByType<ArcaneTimeFocusController>();

            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();

            if (leftGrimoireGesture == null)
                leftGrimoireGesture = FindAnyObjectByType<LeftGrimoireGesture>();
        }

        private void SubscribeGrimoireManager()
        {
            if (grimoireManager == null)
                return;

            grimoireManager.OnGrimoireOpen -= HandleGrimoireChanged;
            grimoireManager.OnGrimoireClose -= HandleGrimoireChanged;
            grimoireManager.OnGrimoireOpen += HandleGrimoireChanged;
            grimoireManager.OnGrimoireClose += HandleGrimoireChanged;
        }

        private void UnsubscribeGrimoireManager()
        {
            if (grimoireManager == null)
                return;

            grimoireManager.OnGrimoireOpen -= HandleGrimoireChanged;
            grimoireManager.OnGrimoireClose -= HandleGrimoireChanged;
        }

        private void HandleGrimoireChanged()
        {
            SyncFocusState();
        }

        private void AutoPopulateRightElementGestures()
        {
            var fireGesture = FindFirstObjectByTypeIncludingInactive<RightFireGesture>();
            var iceGesture = FindFirstObjectByTypeIncludingInactive<RightIceGesture>();
            var thunderGesture = FindFirstObjectByTypeIncludingInactive<RightThunderGesture>();

            rightElementGestures = new MonoBehaviour[]
            {
                fireGesture,
                iceGesture,
                thunderGesture
            };
        }

        private static T FindFirstObjectByTypeIncludingInactive<T>() where T : Object
        {
            var objects = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return objects.Length > 0 ? objects[0] : null;
        }
    }
}
