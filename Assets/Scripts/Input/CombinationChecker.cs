using System;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Input
{
    public enum CombinationState
    {
        Idle,
        CombinationFocus,
        ElementDeclared,
        ComboReady,
        ComboCompleted,
        ComboFailed,
        ComboExpired
    }

    /// <summary>
    /// Receives two Pose IDs from GestureDetector and validates two-hand combination within a 0.5s window. Fires OnCombinationSuccess or OnCombinationFail events.
    /// </summary>
    public class CombinationChecker : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private float combinationWindow = 0.5f;
        [SerializeField] private float comboDeclarationWindow = 1.25f;
        [SerializeField] private bool enableLegacyTwoHandPoseCombos;
        [SerializeField] private bool emitFailEvents = true;
        [SerializeField] private bool allowCombosWithoutGameManager = true;
        [SerializeField] private bool allowLockedCombosInEditor = true;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private ArcaneActionModeController actionModeController;
        [SerializeField] private CombinationFocusModeController focusModeController;
        [SerializeField] private float failureFeedbackSeconds = 0.5f;
        [SerializeField] private bool enableComboDebugLogs = true;
        [SerializeField] private bool allowLeftElementDeclarationOutsideFocusForDebug = true;

        public event Action<SpellId> OnCombinationSuccess;
        public event Action OnCombinationFail;
        public event Action<SpellId, bool> OnComboReadyChanged;

        public CombinationState State { get; private set; } = CombinationState.Idle;
        public ElementType CurrentElement { get; private set; } = ElementType.None;
        public PoseId CurrentAttackPose { get; private set; } = PoseId.None;
        public ElementType LeftDeclaredElement { get; private set; } = ElementType.None;
        public ElementType RightDeclaredElement { get; private set; } = ElementType.None;
        public bool IsComboReady { get; private set; }
        public SpellId CurrentComboCandidate { get; private set; } = SpellId.None;
        public bool IsLeftDeclarationSuppressedByPull { get; private set; }
        public bool IsLeftDeclarationSuppressedByMode { get; private set; }
        public string DeclarationHand { get; private set; } = "-";
        public string LastComboStatus { get; private set; } = "Combo: idle";
        public float FailureFeedbackSeconds => failureFeedbackSeconds;

        private PoseId lastLeftPose = PoseId.None;
        private PoseId lastRightPose = PoseId.None;
        private float lastLeftPoseTime = -999f;
        private float lastRightPoseTime = -999f;
        private SpellId lastSpellId = SpellId.None;
        private float lastSuccessTime = -999f;
        private bool isGrimoireOpen;
        private float lastLeftDeclarationTime = -999f;
        private float lastRightDeclarationTime = -999f;
        private float inputLockedUntilTime = -999f;
        private float comboShootWindowUntilTime = -999f;
        private bool comboShootArmed;

        private void Awake()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (actionModeController == null)
                actionModeController = FindAnyObjectByType<ArcaneActionModeController>();

            if (focusModeController == null)
                focusModeController = FindAnyObjectByType<CombinationFocusModeController>();
        }

        private void OnEnable()
        {
            if (gestureDetector != null)
            {
                gestureDetector.OnPoseDetected += HandlePoseDetected;
                gestureDetector.OnGestureConfirmed += HandleGestureConfirmed;
                gestureDetector.OnHandPoseCleared += HandleHandPoseCleared;
                gestureDetector.OnCombinePushDetected += ReportCombinePush;
            }

            if (grimoireManager != null)
            {
                grimoireManager.OnGrimoireOpen += HandleGrimoireOpen;
                grimoireManager.OnGrimoireClose += HandleGrimoireClose;
                isGrimoireOpen = grimoireManager.IsOpen;
            }
        }

        private void OnDisable()
        {
            if (gestureDetector != null)
            {
                gestureDetector.OnPoseDetected -= HandlePoseDetected;
                gestureDetector.OnGestureConfirmed -= HandleGestureConfirmed;
                gestureDetector.OnHandPoseCleared -= HandleHandPoseCleared;
                gestureDetector.OnCombinePushDetected -= ReportCombinePush;
            }

            if (grimoireManager != null)
            {
                grimoireManager.OnGrimoireOpen -= HandleGrimoireOpen;
                grimoireManager.OnGrimoireClose -= HandleGrimoireClose;
            }
        }

        private void Update()
        {
            Tick(Time.time);
        }

        public void TickForTest(float now)
        {
            Tick(now);
        }

        private void Tick(float now)
        {
            if (IsInputLocked(now))
                return;

            if (State == CombinationState.ComboFailed ||
                State == CombinationState.ComboExpired ||
                State == CombinationState.ComboCompleted)
            {
                ClearComboDeclarations("Combo: idle");
                State = IsCombinationFocusActive() ? CombinationState.CombinationFocus : CombinationState.Idle;
                return;
            }

            RefreshComboCandidate(now);
        }

        private void HandlePoseDetected(PoseId left, PoseId right)
        {
            var now = Time.time;
            var leftElement = PoseToElement(left);
            if (leftElement != ElementType.None)
                CurrentElement = leftElement;

            if (left != PoseId.None)
            {
                lastLeftPose = left;
                lastLeftPoseTime = now;
            }

            if (right != PoseId.None)
            {
                lastRightPose = right;
                lastRightPoseTime = now;
            }

            if (isGrimoireOpen && now - lastRightPoseTime <= combinationWindow)
            {
                var rightOnlySpellId = ResolveRightHandSingleSpell(lastRightPose);
                if (rightOnlySpellId != SpellId.None && CurrentElement != ElementType.None)
                {
                    TryEmitSuccess(rightOnlySpellId, now);
                    return;
                }
            }

            if (now - lastLeftPoseTime > combinationWindow || now - lastRightPoseTime > combinationWindow)
                return;

            var spellId = ResolveSpell(lastLeftPose, lastRightPose);
            if (spellId == SpellId.None)
            {
                if (IsCombinationFocusActive())
                {
                    RefreshComboCandidate(now);
                    return;
                }

                if (emitFailEvents)
                    OnCombinationFail?.Invoke();
                return;
            }

            if (IsComboSpell(spellId) && !enableLegacyTwoHandPoseCombos)
            {
                RefreshComboCandidate(now);
                return;
            }

            if (IsComboSpell(spellId) && !IsCombinationFocusActive())
            {
                LastComboStatus = "Combo: focus required";
                RefreshComboCandidate(now);
                return;
            }

            if (isGrimoireOpen && IsComboSpell(spellId))
            {
                EmitFail();
                return;
            }

            if (IsComboSpell(spellId) && !IsComboUnlocked(spellId))
            {
                EmitFail();
                return;
            }

            TryEmitSuccess(spellId, now);
        }

        public void ReportCombinePush()
        {
            ReportCombinePushInternal(false);
        }

        public bool ReportCombinePushForTest()
        {
            return ReportCombinePushInternal(true);
        }

        public bool ReportFocusExpired()
        {
            if (IsInputLocked(Time.time))
                return false;

            State = CombinationState.ComboExpired;
            inputLockedUntilTime = Time.time + Mathf.Max(0f, failureFeedbackSeconds);
            SetComboCandidate(false, SpellId.None, "-", "Combo: expired");
            if (emitFailEvents)
                OnCombinationFail?.Invoke();
            return true;
        }

        public void ReleaseFocusLock(string status = "Combo: focus exit")
        {
            inputLockedUntilTime = -999f;
            comboShootWindowUntilTime = -999f;
            comboShootArmed = false;
            ClearComboDeclarations(status);
            State = CombinationState.Idle;
        }

        public void ArmComboShootWindow(float seconds)
        {
            if (CurrentComboCandidate == SpellId.None)
                return;

            comboShootWindowUntilTime = Time.time + Mathf.Max(0.05f, seconds);
            comboShootArmed = true;
            SetComboCandidate(true, CurrentComboCandidate, "Left+Right",
                $"Combo armed: {SpellHitData.GetDisplayName(CurrentComboCandidate)}");
            LogCombo($"Shoot window armed: candidate={CurrentComboCandidate}, seconds={seconds:0.00}.");
        }

        private bool ReportCombinePushInternal(bool ignoreCastMode)
        {
            var now = Time.time;
            RefreshComboCandidate(now, ignoreLeftPull: ignoreCastMode, ignoreCastMode: ignoreCastMode);

            if (!ignoreCastMode && !IsCombinationFocusActive())
            {
                LogCombo("Combine push ignored: focus or shoot window is not active.");
                EmitFail();
                return false;
            }

            if (CurrentComboCandidate == SpellId.None)
            {
                LogCombo("Combine push ignored: waiting for left and right element declarations.");
                return false;
            }

            if (!comboShootArmed)
            {
                LogCombo($"Combine push confirmed candidate {CurrentComboCandidate}; waiting for forward shoot.");
                ArmComboShootWindow(comboDeclarationWindow);
                return true;
            }

            if (!IsComboReady)
            {
                LogCombo($"Combine shoot failed: armed candidate {CurrentComboCandidate} is not ready.");
                EmitFail();
                return false;
            }

            var spellId = CurrentComboCandidate;
            if (TryEmitSuccess(spellId, now) && IsComboSpell(spellId))
            {
                LogCombo($"Combo cast success: {spellId}.");
                State = CombinationState.ComboCompleted;
                comboShootArmed = false;
                ClearComboDeclarations("Combo: completed");
                return true;
            }

            return false;
        }

        private void HandleGestureConfirmed(bool isLeft, string gestureName, PoseType pose)
        {
            var element = GestureNameToElement(gestureName);
            var sourceName = BuildGestureSourceName(isLeft, gestureName);
            LogCombo($"{sourceName} confirmed: gesture={gestureName}, element={element}.");
            if (element == ElementType.None)
                return;

            SubmitElementDeclaration(isLeft, element);
        }

        private void HandleHandPoseCleared(bool isLeft)
        {
            RefreshComboCandidate(Time.time);
        }

        private static SpellId ResolveSpell(PoseId left, PoseId right)
        {
            if (left == PoseId.Fist && right == PoseId.Ok)
                return SpellId.Combo_FireIce;

            if (left == PoseId.Ok && right == PoseId.Horn)
                return SpellId.Combo_IceThunder;

            if (left == PoseId.Horn && right == PoseId.Fist)
                return SpellId.Combo_ThunderFire;

            if (right == PoseId.IndexPoint && IsElementPose(left))
                return SpellId.Single_Pointer;

            if (right == PoseId.OpenPalm && IsElementPose(left))
                return SpellId.Single_Wave;

            if (right == PoseId.FistPush && IsElementPose(left))
                return SpellId.Single_Strike;

            return SpellId.None;
        }

        private static SpellId ResolveRightHandSingleSpell(PoseId right)
        {
            return right switch
            {
                PoseId.IndexPoint => SpellId.Single_Pointer,
                PoseId.OpenPalm => SpellId.Single_Wave,
                PoseId.FistPush => SpellId.Single_Strike,
                _ => SpellId.None
            };
        }

        public static ElementType PoseToElement(PoseId pose)
        {
            return pose switch
            {
                PoseId.Fist => ElementType.Fire,
                PoseId.Ok => ElementType.Ice,
                PoseId.Horn => ElementType.Thunder,
                _ => ElementType.None
            };
        }

        public static ElementType PrototypePoseToElement(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => ElementType.Fire,
                PoseType.Fist => ElementType.Ice,
                PoseType.ThumbsUp => ElementType.Thunder,
                _ => ElementType.None
            };
        }

        private static ElementType GestureNameToElement(string gestureName)
        {
            return gestureName switch
            {
                "Fire" => ElementType.Fire,
                "Ice" => ElementType.Ice,
                "Thunder" => ElementType.Thunder,
                "ThunderShoot" => ElementType.Thunder,
                _ => ElementType.None
            };
        }

        private static string BuildGestureSourceName(bool isLeft, string gestureName)
        {
            var side = isLeft ? "left" : "right";
            return gestureName switch
            {
                "Fire" => $"{side}_fire",
                "Ice" => $"{side}_ice",
                "Thunder" => $"{side}_thunder",
                "ThunderShoot" => $"{side}_thunder_shoot",
                _ => $"{side}_{gestureName}"
            };
        }

        public bool SubmitElementDeclaration(bool isLeft, ElementType element)
        {
            return RegisterElementDeclaration(isLeft, element, false, false);
        }

        public bool SubmitElementDeclarationForTest(bool isLeft, ElementType element)
        {
            return RegisterElementDeclaration(isLeft, element, true, true);
        }

        private bool RegisterElementDeclaration(bool isLeft, ElementType element, bool ignoreLeftPull, bool ignoreCastMode)
        {
            if (element == ElementType.None)
                return false;

            var now = Time.time;
            if (IsInputLocked(now))
                return false;

            CurrentElement = element;
            RefreshActionModeReference();

            if (isLeft)
            {
                if (LeftDeclaredElement != ElementType.None)
                {
                    if (LeftDeclaredElement == element)
                        return State != CombinationState.ComboFailed;

                    LogCombo($"Left element changed: {LeftDeclaredElement} -> {element}.");
                    LeftDeclaredElement = element;
                    lastLeftDeclarationTime = now;
                    return RefreshComboCandidate(now, ignoreLeftPull, ignoreCastMode);
                }

                var debugIgnoreFocus = allowLeftElementDeclarationOutsideFocusForDebug;
                IsLeftDeclarationSuppressedByPull = !ignoreLeftPull && IsLeftPullActive();
                IsLeftDeclarationSuppressedByMode = !ignoreCastMode && !debugIgnoreFocus && !IsCombinationFocusActive();
                if (IsLeftDeclarationSuppressedByPull || IsLeftDeclarationSuppressedByMode)
                {
                    LastComboStatus = IsLeftDeclarationSuppressedByPull
                        ? "Combo: left blocked by pull"
                        : "Combo: focus required";
                    LogCombo($"Left declaration blocked: element={element}, pull={IsLeftDeclarationSuppressedByPull}, focusRequired={IsLeftDeclarationSuppressedByMode}.");
                    RefreshComboCandidate(now, ignoreLeftPull, ignoreCastMode);
                    return false;
                }

                LeftDeclaredElement = element;
                lastLeftDeclarationTime = now;
                LastComboStatus = $"Combo: L {element}";
                LogCombo(debugIgnoreFocus && !IsCombinationFocusActive()
                    ? $"Left element declared outside focus for debug: {element}."
                    : $"Left element declared: {element}.");
            }
            else
            {
                if (RightDeclaredElement != ElementType.None)
                {
                    if (RightDeclaredElement == element)
                        return State != CombinationState.ComboFailed;

                    LogCombo($"Right element changed: {RightDeclaredElement} -> {element}.");
                    RightDeclaredElement = element;
                    lastRightDeclarationTime = now;
                    return RefreshComboCandidate(now, ignoreLeftPull, ignoreCastMode);
                }

                RightDeclaredElement = element;
                lastRightDeclarationTime = now;
                LastComboStatus = $"Combo: R {element}";
                LogCombo($"Right element declared: {element}.");
            }

            return RefreshComboCandidate(now, ignoreLeftPull, ignoreCastMode);
        }

        private bool RefreshComboCandidate(float now, bool ignoreLeftPull = false, bool ignoreCastMode = false)
        {
            if (IsInputLocked(now))
                return false;

            var debugIgnoreFocus = allowLeftElementDeclarationOutsideFocusForDebug;
            IsLeftDeclarationSuppressedByPull = !ignoreLeftPull && IsLeftPullActive();
            IsLeftDeclarationSuppressedByMode = !ignoreCastMode && !debugIgnoreFocus && !IsCombinationFocusActive();
            var modeActive = ignoreCastMode || debugIgnoreFocus || IsCombinationFocusActive();

            var leftValid = LeftDeclaredElement != ElementType.None &&
                            !IsLeftDeclarationSuppressedByPull &&
                            !IsLeftDeclarationSuppressedByMode;
            var rightValid = RightDeclaredElement != ElementType.None &&
                             modeActive;

            if (leftValid && rightValid && LeftDeclaredElement == RightDeclaredElement)
            {
                SetComboCandidate(false, SpellId.None, "Left+Right", "Combo: same element");
                State = CombinationState.ElementDeclared;
                LogCombo($"Same element pair ignored: {LeftDeclaredElement}. Declare a different element.");
                return true;
            }

            if (!leftValid || !rightValid)
            {
                var hand = BuildDeclarationHand(leftValid, rightValid);
                var status = !modeActive && (LeftDeclaredElement != ElementType.None || RightDeclaredElement != ElementType.None)
                    ? "Combo: focus required"
                    : "Combo: waiting";
                SetComboCandidate(false, SpellId.None, hand, status);
                State = leftValid || rightValid
                    ? CombinationState.ElementDeclared
                    : modeActive
                    ? CombinationState.CombinationFocus
                    : CombinationState.Idle;
                return true;
            }

            var candidate = ResolveComboSpell(LeftDeclaredElement, RightDeclaredElement);
            var armedCandidate = candidate != SpellId.None &&
                                 comboShootArmed &&
                                 CurrentComboCandidate == candidate &&
                                 now <= comboShootWindowUntilTime;
            SetComboCandidate(
                armedCandidate,
                candidate,
                "Left+Right",
                armedCandidate
                    ? $"Combo armed: {SpellHitData.GetDisplayName(candidate)}"
                    : candidate != SpellId.None
                    ? $"Combo selected: {SpellHitData.GetDisplayName(candidate)}"
                    : "Combo: invalid pair");
            State = armedCandidate ? CombinationState.ComboReady : CombinationState.ElementDeclared;
            return candidate != SpellId.None;
        }

        private static SpellId ResolveComboSpell(ElementType left, ElementType right)
        {
            if (HasPair(left, right, ElementType.Fire, ElementType.Ice))
                return SpellId.Combo_FireIce;

            if (HasPair(left, right, ElementType.Ice, ElementType.Thunder))
                return SpellId.Combo_IceThunder;

            if (HasPair(left, right, ElementType.Thunder, ElementType.Fire))
                return SpellId.Combo_ThunderFire;

            return SpellId.None;
        }

        private static bool HasPair(ElementType left, ElementType right, ElementType a, ElementType b)
        {
            return left == a && right == b || left == b && right == a;
        }

        private static string BuildDeclarationHand(bool leftValid, bool rightValid)
        {
            if (leftValid && rightValid)
                return "Left+Right";
            if (leftValid)
                return "Left";
            if (rightValid)
                return "Right";

            return "-";
        }

        private bool IsLeftPullActive()
        {
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            return handPullMovement != null &&
                   handPullMovement.IsPulling &&
                   handPullMovement.ActiveHandName == "Left";
        }

        private bool IsCastModeActive()
        {
            RefreshActionModeReference();
            return actionModeController != null && actionModeController.IsCastModeActive;
        }

        private bool IsCombinationFocusActive()
        {
            if (focusModeController == null)
                focusModeController = FindAnyObjectByType<CombinationFocusModeController>();

            var focusActive = focusModeController != null && focusModeController.IsFocusActive;
            var shootWindowActive = IsComboReady &&
                                    CurrentComboCandidate != SpellId.None &&
                                    Time.time <= comboShootWindowUntilTime;
            return focusActive || shootWindowActive;
        }

        private void RefreshActionModeReference()
        {
            if (actionModeController == null)
                actionModeController = FindAnyObjectByType<ArcaneActionModeController>();
        }

        private static bool IsElementPose(PoseId pose)
        {
            return pose == PoseId.Fist || pose == PoseId.Ok || pose == PoseId.Horn;
        }

        private static bool IsComboSpell(SpellId spellId)
        {
            return spellId == SpellId.Combo_FireIce ||
                   spellId == SpellId.Combo_IceThunder ||
                   spellId == SpellId.Combo_ThunderFire;
        }

        private bool IsComboUnlocked(SpellId spellId)
        {
            if (allowLockedCombosInEditor && Application.isEditor)
                return true;

            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.UnlockData == null)
                return allowCombosWithoutGameManager;

            return spellId switch
            {
                SpellId.Combo_FireIce => gameManager.fireUnlocked && gameManager.iceUnlocked,
                SpellId.Combo_IceThunder => gameManager.iceUnlocked && gameManager.thunderUnlocked,
                SpellId.Combo_ThunderFire => gameManager.thunderUnlocked && gameManager.fireUnlocked,
                _ => true
            };
        }

        private bool TryEmitSuccess(SpellId spellId, float now)
        {
            if (spellId == lastSpellId && now - lastSuccessTime < combinationWindow)
                return false;

            lastSpellId = spellId;
            lastSuccessTime = now;
            CurrentAttackPose = lastRightPose;
            LastComboStatus = $"Combo cast: {SpellHitData.GetDisplayName(spellId)}";
            OnCombinationSuccess?.Invoke(spellId);
            return true;
        }

        private void ClearComboDeclarations(string status)
        {
            LeftDeclaredElement = ElementType.None;
            RightDeclaredElement = ElementType.None;
            lastLeftDeclarationTime = -999f;
            lastRightDeclarationTime = -999f;
            comboShootArmed = false;
            SetComboCandidate(false, SpellId.None, "-", status);
        }

        private void EmitFail()
        {
            EmitFail("Combo: failed");
        }

        private void EmitFail(string status)
        {
            State = CombinationState.ComboFailed;
            inputLockedUntilTime = Time.time + Mathf.Max(0f, failureFeedbackSeconds);
            comboShootArmed = false;
            SetComboCandidate(false, SpellId.None, "-", status);
            LogCombo($"Combo failed: {status}.");
            if (emitFailEvents)
                OnCombinationFail?.Invoke();
        }

        private bool IsInputLocked(float now)
        {
            return now < inputLockedUntilTime;
        }

        private void SetComboCandidate(bool ready, SpellId candidate, string declarationHand, string status)
        {
            var changed = IsComboReady != ready || CurrentComboCandidate != candidate;
            IsComboReady = ready;
            CurrentComboCandidate = candidate;
            DeclarationHand = declarationHand;
            LastComboStatus = status;

            if (changed)
                OnComboReadyChanged?.Invoke(CurrentComboCandidate, IsComboReady);
        }

        private void LogCombo(string message)
        {
            if (enableComboDebugLogs)
                Debug.Log($"[ComboMagicTest] {message}", this);
        }

        private void HandleGrimoireOpen()
        {
            isGrimoireOpen = true;
        }

        private void HandleGrimoireClose()
        {
            isGrimoireOpen = false;
        }
    }
}
