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
        [SerializeField] private GestureEventRouter gestureRouter;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private float combinationWindow = 0.5f;
        [SerializeField] private float comboDeclarationWindow = 4f;
        [SerializeField] private bool enableLegacyTwoHandPoseCombos;
        [SerializeField] private bool emitFailEvents = true;
        [SerializeField] private bool allowCombosWithoutGameManager = true;
        [SerializeField] private bool allowLockedCombosInEditor = true;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private ArcaneActionModeController actionModeController;
        [Header("Palm Clash Combo Casting")]
        [SerializeField] private bool enablePalmClashComboCasting = true;
        [SerializeField] private float comboClashDistance = 0.22f;
        [SerializeField] private float comboChargeReleaseDistance = 0.36f;
        [SerializeField] private float comboPalmForwardVelocity = 0.55f;
        [SerializeField] private float comboChargeTimeout = 2.4f;
        [SerializeField] private float comboCastCooldown = 0.65f;
        [SerializeField] private bool requireOpenPalmsForComboClash = true;
        [SerializeField] private bool requirePalmsFacingForwardForComboCast = true;
        [SerializeField] private float comboPalmFacingDot = 0.18f;
        [SerializeField] private OVRHand leftOvrHand;
        [SerializeField] private OVRHand rightOvrHand;

        public event Action<SpellId> OnCombinationSuccess;
        public event Action OnCombinationFail;
        public event Action<SpellId, bool> OnComboReadyChanged;
        public event Action<bool, ElementType> OnElementDeclared;
        public event Action<SpellId, Vector3> OnComboChargeStarted;
        public event Action OnComboChargeCancelled;

        public ElementType CurrentElement { get; private set; } = ElementType.None;
        public PoseId CurrentAttackPose { get; private set; } = PoseId.None;
        public ElementType LeftDeclaredElement { get; private set; } = ElementType.None;
        public ElementType RightDeclaredElement { get; private set; } = ElementType.None;
        public bool IsComboReady { get; private set; }
        public SpellId CurrentComboCandidate { get; private set; } = SpellId.None;
        public bool IsLeftDeclarationSuppressedByPull { get; private set; }
        public bool IsLeftDeclarationSuppressedByMode { get; private set; }
        public bool IsComboCharged { get; private set; }
        public Vector3 ComboChargeOrigin { get; private set; }
        public float ComboChargeForwardSpeed { get; private set; }
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
        private SpellId chargedComboCandidate = SpellId.None;
        private Vector3 previousComboMidpoint;
        private bool hasPreviousComboMidpoint;
        private float comboChargedTime = -999f;
        private float lastPalmClashComboCastTime = -999f;
        private bool hasPendingComboCastPose;
        private SpellId pendingComboCastSpell = SpellId.None;
        private Vector3 pendingComboCastOrigin;
        private Vector3 pendingComboCastDirection;

        private void Awake()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (actionModeController == null)
                actionModeController = FindAnyObjectByType<ArcaneActionModeController>();

            comboDeclarationWindow = Mathf.Max(comboDeclarationWindow, 4f);
            comboClashDistance = Mathf.Clamp(comboClashDistance, 0.12f, 0.45f);
            comboChargeReleaseDistance = Mathf.Max(comboChargeReleaseDistance, comboClashDistance + 0.08f);
            comboPalmForwardVelocity = Mathf.Max(0.15f, comboPalmForwardVelocity);
            comboChargeTimeout = Mathf.Max(0.6f, comboChargeTimeout);
            comboCastCooldown = Mathf.Max(0.2f, comboCastCooldown);
            comboPalmFacingDot = Mathf.Clamp(comboPalmFacingDot, -0.2f, 0.75f);
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
            var now = Time.time;
            RefreshComboCandidate(now);
            UpdatePalmClashCombo(now);
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

            if (now - lastLeftPoseTime > combinationWindow || now - lastRightPoseTime > combinationWindow)
                return;

            var spellId = ResolveSpell(lastLeftPose, lastRightPose);
            if (spellId == SpellId.None)
            {
                if (emitFailEvents)
                    OnCombinationFail?.Invoke();
                return;
            }

            if (!IsComboSpell(spellId))
            {
                LastComboStatus = $"Combo: ignore legacy single {spellId}";
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
            ReportReadyComboCast(false);
        }

        public bool ReportCombinePushForTest()
        {
            return ReportReadyComboCast(true);
        }

        public bool ReportRightHandComboThrust()
        {
            if (enablePalmClashComboCasting)
            {
                LastComboStatus = "Combo: palm clash required";
                return false;
            }

            return ReportReadyComboCast(false);
        }

        private bool ReportReadyComboCast(bool ignoreCastMode)
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

            if (enablePalmClashComboCasting && !ignoreCastMode && !IsComboCharged)
            {
                LastComboStatus = "Combo: clash palms first";
                EmitFail();
                return false;
            }

            var spellId = CurrentComboCandidate;
            if (enablePalmClashComboCasting && !hasPendingComboCastPose)
            {
                pendingComboCastSpell = spellId;
                pendingComboCastOrigin = ComboChargeOrigin;
                pendingComboCastDirection = ResolveComboCastDirection(ComboChargeOrigin);
                hasPendingComboCastPose = true;
            }

            if (TryEmitSuccess(spellId, now) && IsComboSpell(spellId))
            {
                ClearComboDeclarations();
                ClearComboCharge(false);
                return true;
            }

            return false;
        }

        public bool TryConsumePendingComboCastPose(SpellId spellId, out Vector3 origin, out Vector3 direction)
        {
            if (hasPendingComboCastPose && pendingComboCastSpell == spellId)
            {
                origin = pendingComboCastOrigin;
                direction = pendingComboCastDirection.sqrMagnitude > 0.001f
                    ? pendingComboCastDirection.normalized
                    : ResolveComboCastDirection(pendingComboCastOrigin);
                hasPendingComboCastPose = false;
                pendingComboCastSpell = SpellId.None;
                return true;
            }

            origin = ComboChargeOrigin;
            direction = ResolveComboCastDirection(origin);
            return false;
        }

        private void UpdatePalmClashCombo(float now)
        {
            ComboChargeForwardSpeed = 0f;

            if (!enablePalmClashComboCasting)
                return;

            if (isGrimoireOpen || !IsCastModeActive() || !IsComboReady || CurrentComboCandidate == SpellId.None)
            {
                ClearComboCharge(false);
                return;
            }

            if (!TryGetPalmPosition(true, out var leftPalmPosition) ||
                !TryGetPalmPosition(false, out var rightPalmPosition))
            {
                ClearComboCharge(false);
                LastComboStatus = IsComboReady ? "Combo ready: hands not tracked" : LastComboStatus;
                return;
            }

            if (requireOpenPalmsForComboClash && !AreBothHandsOpenForCombo())
            {
                ClearComboCharge(false);
                LastComboStatus = $"Combo ready: open both palms";
                return;
            }

            var distance = Vector3.Distance(leftPalmPosition, rightPalmPosition);
            var midpoint = (leftPalmPosition + rightPalmPosition) * 0.5f;

            if (!IsComboCharged)
            {
                hasPreviousComboMidpoint = false;
                if (distance <= comboClashDistance)
                    BeginComboCharge(CurrentComboCandidate, midpoint, now);

                return;
            }

            if (chargedComboCandidate != CurrentComboCandidate ||
                now - comboChargedTime > comboChargeTimeout ||
                distance > comboChargeReleaseDistance)
            {
                ClearComboCharge(true);
                return;
            }

            var waitingForPalmsForward = false;
            if (hasPreviousComboMidpoint && Time.deltaTime > 0f)
            {
                var velocity = (midpoint - previousComboMidpoint) / Time.deltaTime;
                ComboChargeForwardSpeed = Vector3.Dot(velocity, ResolveViewForward());

                var palmsFacingForward = !requirePalmsFacingForwardForComboCast || ArePalmsFacingForward();
                waitingForPalmsForward = !palmsFacingForward;
                if (ComboChargeForwardSpeed >= comboPalmForwardVelocity &&
                    palmsFacingForward &&
                    now - lastPalmClashComboCastTime >= comboCastCooldown)
                {
                    lastPalmClashComboCastTime = now;
                    pendingComboCastSpell = CurrentComboCandidate;
                    pendingComboCastOrigin = midpoint;
                    pendingComboCastDirection = ResolveComboCastDirection(midpoint);
                    hasPendingComboCastPose = true;

                    if (TryEmitSuccess(CurrentComboCandidate, now))
                    {
                        ClearComboDeclarations();
                        ClearComboCharge(false);
                        return;
                    }
                }

            }

            previousComboMidpoint = midpoint;
            hasPreviousComboMidpoint = true;
            ComboChargeOrigin = midpoint;
            LastComboStatus = waitingForPalmsForward
                ? "Combo charged: face palms forward"
                : $"Combo charged: push {ComboChargeForwardSpeed:0.00}/{comboPalmForwardVelocity:0.00}";
        }

        private void BeginComboCharge(SpellId spellId, Vector3 midpoint, float now)
        {
            IsComboCharged = true;
            chargedComboCandidate = spellId;
            comboChargedTime = now;
            ComboChargeOrigin = midpoint;
            previousComboMidpoint = midpoint;
            hasPreviousComboMidpoint = true;
            LastComboStatus = $"Combo charged: {SpellHitData.GetDisplayName(spellId)}";
            OnComboChargeStarted?.Invoke(spellId, midpoint);
        }

        private void ClearComboCharge(bool emitCancelled)
        {
            if (!IsComboCharged)
            {
                hasPreviousComboMidpoint = false;
                chargedComboCandidate = SpellId.None;
                return;
            }

            IsComboCharged = false;
            chargedComboCandidate = SpellId.None;
            comboChargedTime = -999f;
            hasPreviousComboMidpoint = false;
            ComboChargeForwardSpeed = 0f;
            if (emitCancelled)
                OnComboChargeCancelled?.Invoke();
        }

        private bool TryGetPalmPosition(bool isLeft, out Vector3 position)
        {
            var hand = ResolveOvrHand(isLeft);
            if (hand != null && hand.IsTracked)
            {
                if (hand.IsPointerPoseValid && hand.PointerPose != null)
                {
                    position = hand.PointerPose.position;
                    return true;
                }

                position = hand.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private OVRHand ResolveOvrHand(bool isLeft)
        {
            var current = isLeft ? leftOvrHand : rightOvrHand;
            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            if (current != null &&
                current.GetHand() == expected &&
                current.enabled &&
                current.IsTracked)
            {
                return current;
            }

            OVRHand best = null;
            var bestScore = int.MinValue;
            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand == null || hand.GetHand() != expected)
                    continue;

                var score = 0;
                if (hand.gameObject.activeInHierarchy)
                    score += 10;
                if (hand.enabled)
                    score += 20;
                if (hand.IsTracked)
                    score += 40;
                if (hand.IsPointerPoseValid)
                    score += 10;

                if (score <= bestScore)
                    continue;

                best = hand;
                bestScore = score;
            }

            if (isLeft)
                leftOvrHand = best;
            else
                rightOvrHand = best;

            return best;
        }

        private bool AreBothHandsOpenForCombo()
        {
            return IsHandOpenForCombo(true) && IsHandOpenForCombo(false);
        }

        private bool ArePalmsFacingForward()
        {
            if (!TryGetPalmForward(true, out var leftForward) ||
                !TryGetPalmForward(false, out var rightForward))
            {
                return false;
            }

            var viewForward = ResolveViewForward();
            return Vector3.Dot(leftForward.normalized, viewForward) >= comboPalmFacingDot &&
                   Vector3.Dot(rightForward.normalized, viewForward) >= comboPalmFacingDot;
        }

        private bool TryGetPalmForward(bool isLeft, out Vector3 forward)
        {
            var hand = ResolveOvrHand(isLeft);
            if (hand != null && hand.IsTracked)
            {
                if (hand.IsPointerPoseValid && hand.PointerPose != null)
                {
                    forward = hand.PointerPose.forward;
                    return forward.sqrMagnitude > 0.001f;
                }

                forward = hand.transform.forward;
                return forward.sqrMagnitude > 0.001f;
            }

            forward = Vector3.zero;
            return false;
        }

        private bool IsHandOpenForCombo(bool isLeft)
        {
            if (gestureDetector != null)
            {
                var prototypePose = isLeft
                    ? gestureDetector.CurrentLeftPrototypePose
                    : gestureDetector.CurrentRightPrototypePose;
                if (prototypePose == PoseType.OpenPalm)
                    return true;

                var pose = isLeft
                    ? gestureDetector.CurrentLeftPose
                    : gestureDetector.CurrentRightPose;
                if (pose == PoseId.OpenPalm)
                    return true;
            }

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (gestureRouter == null || !gestureRouter.HasReceivedGestureEvent)
                return false;

            return isLeft
                ? gestureRouter.CurrentLeftPose == PoseType.OpenPalm
                : gestureRouter.CurrentRightPose == PoseType.OpenPalm;
        }

        private Vector3 ResolveComboCastDirection(Vector3 origin)
        {
            var camera = Camera.main;
            if (camera != null)
                return (camera.transform.position + camera.transform.forward * 18f - origin).normalized;

            return ResolveViewForward();
        }

        private Vector3 ResolveViewForward()
        {
            var camera = Camera.main;
            if (camera != null)
                return camera.transform.forward.normalized;

            var forward = transform.forward;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
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

            return SpellId.None;
        }

        public static ElementType PoseToElement(PoseId pose)
        {
            return pose switch
            {
                PoseId.OpenPalm => ElementType.Fire,
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
                PoseType.TwoFinger => ElementType.Ice,
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
            var existingElement = isLeft ? LeftDeclaredElement : RightDeclaredElement;
            var existingTime = isLeft ? lastLeftDeclarationTime : lastRightDeclarationTime;
            if (enablePalmClashComboCasting &&
                !ignoreLeftPull &&
                !ignoreCastMode &&
                element == ElementType.Fire &&
                existingElement != ElementType.None &&
                existingElement != ElementType.Fire &&
                now - existingTime <= comboDeclarationWindow)
            {
                LastComboStatus = $"Combo: {(isLeft ? "L" : "R")} locked {existingElement}";
                RefreshComboCandidate(now);
                return true;
            }

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
            OnElementDeclared?.Invoke(isLeft, element);
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
            return pose == PoseId.OpenPalm || pose == PoseId.Ok || pose == PoseId.Horn;
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
            ClearComboCharge(false);
        }

        private void HandleGrimoireClose()
        {
            isGrimoireOpen = false;
        }
    }
}
