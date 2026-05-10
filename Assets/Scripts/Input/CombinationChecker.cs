using System;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Input
{
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

        public event Action<SpellId> OnCombinationSuccess;
        public event Action OnCombinationFail;
        public event Action<SpellId, bool> OnComboReadyChanged;

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

        private PoseId lastLeftPose = PoseId.None;
        private PoseId lastRightPose = PoseId.None;
        private float lastLeftPoseTime = -999f;
        private float lastRightPoseTime = -999f;
        private SpellId lastSpellId = SpellId.None;
        private float lastSuccessTime = -999f;
        private bool isGrimoireOpen;
        private float lastLeftDeclarationTime = -999f;
        private float lastRightDeclarationTime = -999f;

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
        }

        private void OnEnable()
        {
            if (gestureDetector != null)
            {
                gestureDetector.OnPoseDetected += HandlePoseDetected;
                gestureDetector.OnHandPoseConfirmed += HandleHandPoseConfirmed;
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
                gestureDetector.OnHandPoseConfirmed -= HandleHandPoseConfirmed;
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
            RefreshComboCandidate(Time.time);
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
                if (emitFailEvents)
                    OnCombinationFail?.Invoke();
                return;
            }

            if (IsComboSpell(spellId) && !enableLegacyTwoHandPoseCombos)
            {
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

        private bool ReportCombinePushInternal(bool ignoreCastMode)
        {
            var now = Time.time;
            RefreshComboCandidate(now, ignoreLeftPull: ignoreCastMode, ignoreCastMode: ignoreCastMode);

            if (!ignoreCastMode && !IsCastModeActive())
            {
                EmitFail();
                return false;
            }

            if (!IsComboReady || CurrentComboCandidate == SpellId.None)
            {
                EmitFail();
                return false;
            }

            var spellId = CurrentComboCandidate;
            if (TryEmitSuccess(spellId, now) && IsComboSpell(spellId))
            {
                ClearComboDeclarations();
                return true;
            }

            return false;
        }

        private void HandleHandPoseConfirmed(bool isLeft, PoseType pose)
        {
            var element = PrototypePoseToElement(pose);
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
            CurrentElement = element;
            RefreshActionModeReference();

            if (isLeft)
            {
                IsLeftDeclarationSuppressedByPull = !ignoreLeftPull && IsLeftPullActive();
                IsLeftDeclarationSuppressedByMode = !ignoreCastMode && !IsCastModeActive();
                if (IsLeftDeclarationSuppressedByPull || IsLeftDeclarationSuppressedByMode)
                {
                    LastComboStatus = IsLeftDeclarationSuppressedByPull
                        ? "Combo: left blocked by pull"
                        : "Combo: left blocked by move mode";
                    RefreshComboCandidate(now, ignoreLeftPull, ignoreCastMode);
                    return false;
                }

                LeftDeclaredElement = element;
                lastLeftDeclarationTime = now;
                LastComboStatus = $"Combo: L {element}";
            }
            else
            {
                RightDeclaredElement = element;
                lastRightDeclarationTime = now;
                LastComboStatus = $"Combo: R {element}";
            }

            RefreshComboCandidate(now, ignoreLeftPull, ignoreCastMode);
            return true;
        }

        private void RefreshComboCandidate(float now, bool ignoreLeftPull = false, bool ignoreCastMode = false)
        {
            IsLeftDeclarationSuppressedByPull = !ignoreLeftPull && IsLeftPullActive();
            IsLeftDeclarationSuppressedByMode = !ignoreCastMode && !IsCastModeActive();

            var leftValid = LeftDeclaredElement != ElementType.None &&
                            !IsLeftDeclarationSuppressedByPull &&
                            !IsLeftDeclarationSuppressedByMode &&
                            now - lastLeftDeclarationTime <= comboDeclarationWindow;
            var rightValid = RightDeclaredElement != ElementType.None &&
                             now - lastRightDeclarationTime <= comboDeclarationWindow;

            if (!leftValid || !rightValid || LeftDeclaredElement == RightDeclaredElement)
            {
                var hand = BuildDeclarationHand(leftValid, rightValid);
                var status = LeftDeclaredElement == RightDeclaredElement && leftValid && rightValid
                    ? "Combo: same element"
                    : "Combo: waiting";
                SetComboCandidate(false, SpellId.None, hand, status);
                return;
            }

            var candidate = ResolveComboSpell(LeftDeclaredElement, RightDeclaredElement);
            SetComboCandidate(
                candidate != SpellId.None,
                candidate,
                "Left+Right",
                candidate != SpellId.None
                    ? $"Combo ready: {SpellHitData.GetDisplayName(candidate)}"
                    : "Combo: invalid pair");
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

        private void ClearComboDeclarations()
        {
            LeftDeclaredElement = ElementType.None;
            RightDeclaredElement = ElementType.None;
            lastLeftDeclarationTime = -999f;
            lastRightDeclarationTime = -999f;
            SetComboCandidate(false, SpellId.None, "-", "Combo: cleared");
        }

        private void EmitFail()
        {
            LastComboStatus = "Combo: failed";
            if (emitFailEvents)
                OnCombinationFail?.Invoke();
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
