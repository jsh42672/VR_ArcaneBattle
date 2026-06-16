using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Central gate for slow-time modes and action locks.
    /// Recognition stays in gesture scripts; spell and UI systems keep their own behavior.
    /// </summary>
    public class TimeStopSystems : MonoBehaviour
    {
        [Header("── 시간 정지 핵심 참조 ──")]
        [SerializeField] private ArcaneTimeFocusController timeFocusController;
        [SerializeField] private float comboShootWindowSeconds = 6f;

        [Header("── 마도서 시간 정지 ──")]
        [SerializeField] private LeftGrimoireGesture leftGrimoireGesture;
        [SerializeField] private RightPageTurnGesture rightPageTurnGesture;
        [SerializeField] private GrimoireManager grimoireManager;

        [Header("── 양손 조합 시간 정지 ──")]
        [SerializeField] private CombinationFocusModeController combinationFocusController;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private bool enableComboDebugLogs = true;

        [Header("── 잠금 / 해제 대상 ──")]
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private SpellCaster spellCaster;

        private const string GrimoireReason = "Grimoire Time Stop";
        private const string CombinationReason = "Combination Time Stop";
        private const string CombinationFocusReason = "Combination Focus";
        private const string CombinationShootReason = "Combination Shoot";

        private bool previousPageTurnEnabled;

        public bool IsGrimoireTimeStopActive { get; private set; }
        public bool IsCombinationTimeStopActive { get; private set; }
        public bool IsCombinationShootPending { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            if (rightPageTurnGesture != null)
                previousPageTurnEnabled = rightPageTurnGesture.enabled;
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearAllLocks();
        }

        private void Update()
        {
            ResolveReferences();
            RefreshLocks();
        }

        private void Subscribe()
        {
            if (leftGrimoireGesture != null)
            {
                leftGrimoireGesture.onGrimoireAppear.RemoveListener(EnterGrimoireTimeStop);
                leftGrimoireGesture.onGrimoireAppear.AddListener(EnterGrimoireTimeStop);
                leftGrimoireGesture.onGrimoireDisappear.RemoveListener(ExitGrimoireTimeStop);
                leftGrimoireGesture.onGrimoireDisappear.AddListener(ExitGrimoireTimeStop);
            }

            if (combinationFocusController != null)
            {
                combinationFocusController.OnFocusChanged -= HandleCombinationFocusChanged;
                combinationFocusController.OnFocusChanged += HandleCombinationFocusChanged;
            }

            if (combinationChecker != null)
            {
                combinationChecker.OnComboReadyChanged -= HandleComboReadyChanged;
                combinationChecker.OnComboReadyChanged += HandleComboReadyChanged;
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
                combinationChecker.OnCombinationFail -= HandleCombinationFail;
                combinationChecker.OnCombinationFail += HandleCombinationFail;
            }
        }

        private void Unsubscribe()
        {
            if (leftGrimoireGesture != null)
            {
                leftGrimoireGesture.onGrimoireAppear.RemoveListener(EnterGrimoireTimeStop);
                leftGrimoireGesture.onGrimoireDisappear.RemoveListener(ExitGrimoireTimeStop);
            }

            if (combinationFocusController != null)
                combinationFocusController.OnFocusChanged -= HandleCombinationFocusChanged;

            if (combinationChecker != null)
            {
                combinationChecker.OnComboReadyChanged -= HandleComboReadyChanged;
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
                combinationChecker.OnCombinationFail -= HandleCombinationFail;
            }
        }

        private void EnterGrimoireTimeStop()
        {
            if (IsCombinationTimeStopActive || IsCombinationShootPending)
                return;

            IsGrimoireTimeStopActive = true;
            timeFocusController?.RequestFocus(GrimoireReason);
            RefreshLocks();
        }

        private void ExitGrimoireTimeStop()
        {
            IsGrimoireTimeStopActive = false;
            timeFocusController?.ReleaseFocus(GrimoireReason);
            RefreshLocks();
        }

        private void HandleCombinationFocusChanged(bool active)
        {
            if (active)
            {
                IsCombinationTimeStopActive = true;
                IsCombinationShootPending = false;
                timeFocusController?.RequestFocus(CombinationReason);
                LogCombo("Combination focus entered; time focus requested.");
            }
            else if (!IsCombinationShootPending)
            {
                IsCombinationTimeStopActive = false;
                timeFocusController?.ReleaseFocus(CombinationReason);
                LogCombo("Combination focus exited; time focus released.");
            }

            RefreshLocks();
        }

        private void HandleComboReadyChanged(SpellId spellId, bool ready)
        {
            if (!ready || !SpellHitData.IsComboSpellId(spellId))
                return;

            IsCombinationTimeStopActive = false;
            IsCombinationShootPending = true;
            combinationChecker?.ArmComboShootWindow(comboShootWindowSeconds);
            timeFocusController?.ReleaseFocus(CombinationReason);
            timeFocusController?.ReleaseFocus(CombinationFocusReason);
            LogCombo($"Combo ready: {spellId}. Shoot window pending for {comboShootWindowSeconds:0.00}s.");
            RefreshLocks();
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            if (!SpellHitData.IsComboSpellId(spellId))
                return;

            IsCombinationTimeStopActive = false;
            IsCombinationShootPending = false;
            timeFocusController?.ReleaseFocus(CombinationReason);
            timeFocusController?.ReleaseFocus(CombinationFocusReason);
            LogCombo($"Combo success: {spellId}. Locks released.");
            RefreshLocks();
        }

        private void HandleCombinationFail()
        {
            IsCombinationTimeStopActive = false;
            IsCombinationShootPending = false;
            timeFocusController?.ReleaseFocus(CombinationReason);
            timeFocusController?.ReleaseFocus(CombinationFocusReason);
            LogCombo("Combo failed. Locks released.");
            RefreshLocks();
        }

        private void RefreshLocks()
        {
            var spellLocked = IsGrimoireTimeStopActive || IsCombinationTimeStopActive || IsCombinationShootPending;
            var pullLocked = spellLocked;
            var grimoireLocked = IsCombinationTimeStopActive || IsCombinationShootPending;

            SetSpellSuppressed(spellLocked, ResolveLockReason());
            SetPullSuppressed(pullLocked, ResolveLockReason());

            if (leftGrimoireGesture != null)
                leftGrimoireGesture.SetExternalSuppressed(grimoireLocked, ResolveLockReason());
            if (grimoireManager != null)
                grimoireManager.SetExternalSuppressed(grimoireLocked, ResolveLockReason());

            if (rightPageTurnGesture != null)
                rightPageTurnGesture.enabled = IsGrimoireTimeStopActive || previousPageTurnEnabled;
        }

        private string ResolveLockReason()
        {
            if (IsGrimoireTimeStopActive)
                return GrimoireReason;
            if (IsCombinationTimeStopActive)
                return CombinationReason;
            if (IsCombinationShootPending)
                return CombinationShootReason;
            return string.Empty;
        }

        private void SetSpellSuppressed(bool suppressed, string reason)
        {
            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            spellCaster?.SetCastingSuppressed(suppressed, reason);
        }

        private void LogCombo(string message)
        {
            if (enableComboDebugLogs)
                Debug.Log($"[ComboMagicTest] {message}", this);
        }

        private void SetPullSuppressed(bool suppressed, string reason)
        {
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (handPullMovement == null)
                return;

            if (!suppressed &&
                handPullMovement.MovementSuppressionReason != GrimoireReason &&
                handPullMovement.MovementSuppressionReason != CombinationReason &&
                handPullMovement.MovementSuppressionReason != CombinationShootReason)
            {
                return;
            }

            handPullMovement.SetMovementSuppressed(suppressed, reason);
        }

        private void ClearAllLocks()
        {
            IsGrimoireTimeStopActive = false;
            IsCombinationTimeStopActive = false;
            IsCombinationShootPending = false;
            timeFocusController?.ReleaseFocus(GrimoireReason);
            timeFocusController?.ReleaseFocus(CombinationReason);
            SetSpellSuppressed(false, string.Empty);
            SetPullSuppressed(false, string.Empty);

            if (leftGrimoireGesture != null)
                leftGrimoireGesture.SetExternalSuppressed(false, "TimeStopSystems disabled");
            if (grimoireManager != null)
                grimoireManager.SetExternalSuppressed(false, "TimeStopSystems disabled");
            if (rightPageTurnGesture != null)
                rightPageTurnGesture.enabled = previousPageTurnEnabled;
        }

        private void ResolveReferences()
        {
            if (timeFocusController == null)
                timeFocusController = FindAnyObjectByType<ArcaneTimeFocusController>();
            if (leftGrimoireGesture == null)
                leftGrimoireGesture = FindAnyObjectByType<LeftGrimoireGesture>();
            if (rightPageTurnGesture == null)
                rightPageTurnGesture = FindAnyObjectByType<RightPageTurnGesture>(FindObjectsInactive.Include);
            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>(FindObjectsInactive.Include);
            if (combinationFocusController == null)
                combinationFocusController = FindAnyObjectByType<CombinationFocusModeController>();
            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();
            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();
        }
    }
}
