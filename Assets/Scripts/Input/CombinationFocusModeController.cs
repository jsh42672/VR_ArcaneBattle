using System;
using System.Collections.Generic;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Opens a short slow-motion window for deliberate two-hand combination spell casting.
    /// </summary>
    public class CombinationFocusModeController : MonoBehaviour
    {
        [Header("── 핵심 참조 ──")]
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombineGestureDetector combineGestureDetector;
        [SerializeField] private ArcaneTimeFocusController timeFocusController;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private LeftGrimoireGesture leftGrimoireGesture;
        [SerializeField] private Transform headTransform;

        [Header("── 포커스 유지 시간 ──")]
        [SerializeField, Range(0.05f, 1f)] private float slowTimeScale = 0.2f;
        [SerializeField] private float maxDuration = 10f;
        [SerializeField] private bool autoExitAfterDuration = true;

        [Header("── 양손 맞닿음 진입 조건 ──")]
        [SerializeField] private bool enableHandsTogetherEntry = true;
        [SerializeField] private bool requireCombineGestureForFocus = true;
        [SerializeField] private float handsTogetherDistance = 0.12f;
        [SerializeField] private float handsReleaseDistance = 0.22f;
        [SerializeField] private float handsTogetherHoldTime = 0.35f;

        [Header("── 조합 발사 밀기 판정 ──")]
        [SerializeField] private float combinePushDistance = 0.18f;
        [SerializeField] private float combinePushVelocity = 0.45f;
        [SerializeField] private float combinePushCooldown = 0.7f;

        [Header("── 포커스 중 억제 대상 ──")]
        [SerializeField] private bool suppressLegacyGesturesDuringFocus = true;
        [SerializeField] private MonoBehaviour[] legacyGesturesToSuppress;

        [Header("── 디버그 ──")]
        [SerializeField] private bool allowKeyboardToggleInEditor = true;
        [SerializeField] private KeyCode keyboardToggleKey = KeyCode.F8;

        public event Action<bool> OnFocusChanged;

        private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem handSubsystem;
        private float focusStartUnscaledTime = -999f;
        private float handsTogetherStartUnscaledTime = -999f;
        private float previousTimeScale = 1f;
        private float previousFixedDeltaTime = 0.02f;
        private Vector3 previousCombineMidpoint;
        private bool hasPreviousCombineMidpoint;
        private float lastCombinePushUnscaledTime = -999f;
        private bool waitingForHandsRelease;
        private float failureExitUnscaledTime = -999f;
        private bool focusSuppressionApplied;
        private readonly Dictionary<MonoBehaviour, bool> previousLegacyGestureStates = new Dictionary<MonoBehaviour, bool>();

        public bool IsFocusActive { get; private set; }
        public bool HandsTogether { get; private set; }
        public bool IsCombinePushCandidate { get; private set; }
        public float CurrentHandDistance { get; private set; } = -1f;
        public float CurrentCombineForwardSpeed { get; private set; }
        public string LastStatus { get; private set; } = "Focus: idle";
        public float RemainingSeconds => IsFocusActive && autoExitAfterDuration
            ? Mathf.Max(0f, maxDuration - (Time.unscaledTime - focusStartUnscaledTime))
            : 0f;

        private const string SuppressionReason = "Combination Focus";
        private const string TimeFocusReason = "Combination Focus";

        private void Awake()
        {
            ResolveReferences();
            RefreshHandSubsystem();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
                combinationChecker.OnCombinationFail += HandleCombinationFail;
            }
        }

        private void OnDisable()
        {
            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
                combinationChecker.OnCombinationFail -= HandleCombinationFail;
            }

            if (IsFocusActive)
                ExitFocus("Focus: disabled");
        }

        private void Update()
        {
            ResolveReferences();
            RefreshHandSubsystem();

            if (!IsFocusActive && focusSuppressionApplied)
                SetFocusSuppression(false);

#if ENABLE_LEGACY_INPUT_MANAGER
            if (allowKeyboardToggleInEditor && Application.isEditor && UnityEngine.Input.GetKeyDown(keyboardToggleKey))
            {
                if (IsFocusActive)
                    ExitFocus("Focus: keyboard exit");
                else
                    EnterFocus("Focus: keyboard enter");
            }
#endif

            if (!TryGetPalmPositions(out var leftPalm, out var rightPalm))
            {
                HandsTogether = false;
                CurrentHandDistance = -1f;
                handsTogetherStartUnscaledTime = -999f;
                hasPreviousCombineMidpoint = false;

                if (!IsFocusActive)
                    LastStatus = "Focus: waiting for hands";

                return;
            }

            CurrentHandDistance = Vector3.Distance(leftPalm, rightPalm);
            HandsTogether = CurrentHandDistance <= handsTogetherDistance;

            if (IsFocusActive)
            {
                UpdateActiveFocus(leftPalm, rightPalm);
                return;
            }

            UpdateFocusEntry();
        }

        public void EnterFocus(string reason = "Focus: enter")
        {
            if (IsFocusActive)
                return;

            previousTimeScale = Time.timeScale;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            if (timeFocusController != null)
            {
                timeFocusController.RequestFocus(TimeFocusReason);
            }
            else
            {
                Time.timeScale = Mathf.Clamp(slowTimeScale, 0.05f, 1f);
                Time.fixedDeltaTime = previousFixedDeltaTime * Time.timeScale;
            }

            IsFocusActive = true;
            focusStartUnscaledTime = Time.unscaledTime;
            handsTogetherStartUnscaledTime = -999f;
            hasPreviousCombineMidpoint = false;
            failureExitUnscaledTime = -999f;
            LastStatus = reason;
            SetPullSuppressed(true);
            SetFocusSuppression(true);
            OnFocusChanged?.Invoke(true);
        }

        public void ExitFocus(string reason = "Focus: exit")
        {
            if (!IsFocusActive)
                return;

            if (timeFocusController != null)
            {
                timeFocusController.ReleaseFocus(TimeFocusReason);
            }
            else
            {
                Time.timeScale = previousTimeScale;
                Time.fixedDeltaTime = previousFixedDeltaTime;
            }

            IsFocusActive = false;
            waitingForHandsRelease = true;
            hasPreviousCombineMidpoint = false;
            IsCombinePushCandidate = false;
            CurrentCombineForwardSpeed = 0f;
            failureExitUnscaledTime = -999f;
            LastStatus = reason;
            SetFocusSuppression(false);
            SetPullSuppressed(false);
            combinationChecker?.ReleaseFocusLock("Combo: focus exit");
            OnFocusChanged?.Invoke(false);
        }

        private void UpdateFocusEntry()
        {
            IsCombinePushCandidate = false;
            CurrentCombineForwardSpeed = 0f;

            if (!enableHandsTogetherEntry)
            {
                LastStatus = "Focus: entry disabled";
                return;
            }

            if (requireCombineGestureForFocus && !IsCombinePoseActive())
            {
                handsTogetherStartUnscaledTime = -999f;
                LastStatus = "Focus: waiting combine gesture";
                return;
            }

            if (waitingForHandsRelease)
            {
                if (CurrentHandDistance >= handsReleaseDistance)
                    waitingForHandsRelease = false;

                LastStatus = "Focus: release hands";
                return;
            }

            if (!HandsTogether)
            {
                handsTogetherStartUnscaledTime = -999f;
                LastStatus = "Focus: hands apart";
                return;
            }

            if (handsTogetherStartUnscaledTime < 0f)
                handsTogetherStartUnscaledTime = Time.unscaledTime;

            var held = Time.unscaledTime - handsTogetherStartUnscaledTime;
            LastStatus = $"Focus: praying {held:0.00}/{handsTogetherHoldTime:0.00}";

            if (held >= handsTogetherHoldTime)
                EnterFocus("Focus: hands together");
        }

        private void UpdateActiveFocus(Vector3 leftPalm, Vector3 rightPalm)
        {
            SetPullSuppressed(true);

            if (failureExitUnscaledTime > 0f)
            {
                IsCombinePushCandidate = false;
                CurrentCombineForwardSpeed = 0f;
                if (Time.unscaledTime >= failureExitUnscaledTime)
                {
                    ExitFocus("Focus: combo failed");
                    return;
                }

                LastStatus = "Focus: combo failed";
                return;
            }

            if (autoExitAfterDuration && Time.unscaledTime - focusStartUnscaledTime >= maxDuration)
            {
                if (combinationChecker != null && combinationChecker.ReportFocusExpired())
                {
                    LastStatus = "Focus: combo expired";
                    return;
                }

                ExitFocus("Focus: timeout");
                return;
            }

            UpdateCombinePush(leftPalm, rightPalm);
            LastStatus = IsCombinePushCandidate
                ? $"Focus: active push {CurrentCombineForwardSpeed:0.00}/{combinePushVelocity:0.00}"
                : $"Focus: active {RemainingSeconds:0.0}s";
        }

        private void UpdateCombinePush(Vector3 leftPalm, Vector3 rightPalm)
        {
            CurrentCombineForwardSpeed = 0f;
            IsCombinePushCandidate = CurrentHandDistance <= combinePushDistance;
            if (requireCombineGestureForFocus && !IsCombinePoseActive())
            {
                IsCombinePushCandidate = false;
                hasPreviousCombineMidpoint = false;
                return;
            }

            if (!IsCombinePushCandidate)
            {
                hasPreviousCombineMidpoint = false;
                return;
            }

            var midpoint = (leftPalm + rightPalm) * 0.5f;
            if (hasPreviousCombineMidpoint && Time.unscaledDeltaTime > 0f)
            {
                var velocity = (midpoint - previousCombineMidpoint) / Time.unscaledDeltaTime;
                var forward = ResolveHeadForward();
                CurrentCombineForwardSpeed = Vector3.Dot(velocity, forward);

                if (CurrentCombineForwardSpeed >= combinePushVelocity &&
                    Time.unscaledTime - lastCombinePushUnscaledTime >= combinePushCooldown)
                {
                    lastCombinePushUnscaledTime = Time.unscaledTime;
                    LastStatus = "Focus: combine push";
                    combinationChecker?.ReportCombinePush();
                }
            }

            previousCombineMidpoint = midpoint;
            hasPreviousCombineMidpoint = true;
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            if (SpellHitData.IsComboSpellId(spellId))
                ExitFocus($"Focus: cast {SpellHitData.GetDisplayName(spellId)}");
        }

        private void HandleCombinationFail()
        {
            if (!IsFocusActive)
                return;

            var delay = combinationChecker != null ? combinationChecker.FailureFeedbackSeconds : 0.5f;
            failureExitUnscaledTime = Time.unscaledTime + Mathf.Max(0f, delay);
            IsCombinePushCandidate = false;
            hasPreviousCombineMidpoint = false;
            LastStatus = "Focus: combo failed";
        }

        private void SetPullSuppressed(bool suppressed)
        {
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (handPullMovement == null)
                return;

            if (!suppressed && handPullMovement.MovementSuppressionReason != SuppressionReason)
                return;

            handPullMovement.SetMovementSuppressed(suppressed, SuppressionReason);
        }

        private void SetFocusSuppression(bool suppressed)
        {
            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();
            if (leftGrimoireGesture == null)
                leftGrimoireGesture = FindAnyObjectByType<LeftGrimoireGesture>();

            grimoireManager?.SetExternalSuppressed(suppressed, SuppressionReason);
            leftGrimoireGesture?.SetExternalSuppressed(suppressed, SuppressionReason);
            SetLegacyGesturesSuppressed(suppressed);
            focusSuppressionApplied = suppressed;
        }

        private void SetLegacyGesturesSuppressed(bool suppressed)
        {
            if (!suppressLegacyGesturesDuringFocus)
                return;

            if (legacyGesturesToSuppress == null || legacyGesturesToSuppress.Length == 0)
                AutoPopulateLegacyGestures();

            if (legacyGesturesToSuppress == null)
                return;

            foreach (var gesture in legacyGesturesToSuppress)
            {
                if (gesture == null || gesture == this)
                    continue;

                if (suppressed)
                {
                    if (!previousLegacyGestureStates.ContainsKey(gesture))
                        previousLegacyGestureStates.Add(gesture, gesture.enabled);

                    gesture.enabled = false;
                    continue;
                }

                if (previousLegacyGestureStates.TryGetValue(gesture, out var wasEnabled))
                    gesture.enabled = wasEnabled;
            }

            if (!suppressed)
                previousLegacyGestureStates.Clear();
        }

        private void AutoPopulateLegacyGestures()
        {
            legacyGesturesToSuppress = new MonoBehaviour[]
            {
                FindAnyObjectByType<RightFireGesture>(),
                FindAnyObjectByType<RightIceGesture>(),
                FindAnyObjectByType<RightThunderGesture>(),
                FindAnyObjectByType<LeftGrimoireGesture>(),
                FindAnyObjectByType<RightPageTurnGesture>()
            };
        }

        private bool TryGetPalmPositions(out Vector3 leftPalm, out Vector3 rightPalm)
        {
            leftPalm = Vector3.zero;
            rightPalm = Vector3.zero;

            if (combineGestureDetector != null &&
                (!requireCombineGestureForFocus || IsFocusActive || combineGestureDetector.AreBothCombineActive) &&
                combineGestureDetector.TryGetPalmPositions(out leftPalm, out rightPalm))
            {
                return true;
            }

            if (requireCombineGestureForFocus)
                return false;

            if (handSubsystem == null || !handSubsystem.running)
                return false;

            if (!TryGetPalmPosition(handSubsystem.leftHand, out leftPalm))
                return false;

            return TryGetPalmPosition(handSubsystem.rightHand, out rightPalm);
        }

        private bool IsCombinePoseActive()
        {
            return !requireCombineGestureForFocus ||
                   (combineGestureDetector != null && combineGestureDetector.AreBothCombineActive);
        }

        private static bool TryGetPalmPosition(XRHand hand, out Vector3 palmPosition)
        {
            palmPosition = Vector3.zero;
            if (!hand.isTracked)
                return false;

            if (TryGetJointPosition(hand, XRHandJointID.Palm, out palmPosition))
                return true;

            return TryGetJointPosition(hand, XRHandJointID.Wrist, out palmPosition);
        }

        private static bool TryGetJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position)
        {
            position = Vector3.zero;
            var joint = hand.GetJoint(jointId);
            if (!joint.TryGetPose(out var pose))
                return false;

            position = pose.position;
            return true;
        }

        private Vector3 ResolveHeadForward()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            var forward = headTransform != null ? headTransform.forward : transform.forward;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void ResolveReferences()
        {
            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();

            if (combineGestureDetector == null)
                combineGestureDetector = FindAnyObjectByType<CombineGestureDetector>();

            if (timeFocusController == null)
                timeFocusController = FindAnyObjectByType<ArcaneTimeFocusController>();

            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();

            if (leftGrimoireGesture == null)
                leftGrimoireGesture = FindAnyObjectByType<LeftGrimoireGesture>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
        }

        private void RefreshHandSubsystem()
        {
            if (handSubsystem != null && handSubsystem.running)
                return;

            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            handSubsystem = null;

            foreach (var subsystem in handSubsystems)
            {
                if (subsystem == null)
                    continue;

                handSubsystem = subsystem;
                if (subsystem.running)
                    break;
            }
        }
    }
}
