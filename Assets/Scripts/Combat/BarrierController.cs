using System;
using ArcaneVR.Input;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class BarrierController : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private BossAttackType currentAttackType = BossAttackType.Low;
        [SerializeField] private float defaultWindowDuration = 1.2f;
        [SerializeField] private float requiredHoldTime = 0.25f;
        [SerializeField] private float manaCost = 1f;
        [SerializeField] private float activeDuration = 0.8f;

        public event Action<BossAttackType> OnResponseWindowStarted;
        public event Action<bool, string> OnResponseWindowResolved;
        public event Action<bool> OnGuardPoseChanged;
        public event Action<bool> OnBarrierActiveChanged;

        private float windowEndTime;
        private float guardHoldTimer;
        private float activeEndTime;
        private bool isResponseWindowOpen;
        private bool isGuardPoseActive;
        private bool isBarrierActive;
        private bool isConstraintBarrierActive;
        private bool subscribedToBarrierGesture;

        public bool IsResponseWindowOpen => isResponseWindowOpen;
        public bool IsGuardPoseActive => isGuardPoseActive;
        public bool IsBarrierActive => isBarrierActive;
        public BossAttackType CurrentAttackType => currentAttackType;
        public float ResponseWindowRemaining => isResponseWindowOpen ? Mathf.Max(0f, windowEndTime - Time.time) : 0f;
        public float GuardHoldTime => guardHoldTimer;
        public float RequiredHoldTime => requiredHoldTime;
        public string LastResultText { get; private set; } = "Barrier: idle";

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (gestureDetector != null && subscribedToBarrierGesture)
                gestureDetector.OnBothHandsBarrierChanged -= OnBothHandsBarrierGestureChanged;
        }

        private void Update()
        {
            ResolveReferences();
            TrySubscribeBarrierGesture();
            UpdateGuardPoseState();
            UpdateResponseWindow();
            UpdateActiveBarrier();
        }

        public void BeginResponseWindow(BossAttackType attackType)
        {
            BeginResponseWindow(attackType, defaultWindowDuration);
        }

        public void BeginResponseWindow(BossAttackType attackType, float duration)
        {
            currentAttackType = attackType;
            isResponseWindowOpen = true;
            guardHoldTimer = 0f;
            windowEndTime = Time.time + Mathf.Max(0.1f, duration);
            LastResultText = $"Window: {attackType}";
            OnResponseWindowStarted?.Invoke(attackType);
        }

        public void CancelResponseWindow()
        {
            if (!isResponseWindowOpen)
                return;

            ResolveWindow(false, "Barrier Cancelled");
        }

        // 투사체 도달 시점에 외부에서 호출 — 막지 못 했으면 실패 처리
        public void ForceResolve()
        {
            if (!isResponseWindowOpen)
                return;

            ResolveWindow(false, "Barrier Fail: hit");
        }

        private void ResolveReferences()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private void TrySubscribeBarrierGesture()
        {
            if (subscribedToBarrierGesture || gestureDetector == null)
                return;
            gestureDetector.OnBothHandsBarrierChanged += OnBothHandsBarrierGestureChanged;
            subscribedToBarrierGesture = true;
        }

        private void OnBothHandsBarrierGestureChanged(bool active)
        {
            isConstraintBarrierActive = active;
            if (isBarrierActive == active)
                return;
            isBarrierActive = active;
            OnBarrierActiveChanged?.Invoke(isBarrierActive);
        }

        private void UpdateGuardPoseState()
        {
            var nextGuardPose = IsGuardPoseDetected();
            if (isGuardPoseActive == nextGuardPose)
                return;

            isGuardPoseActive = nextGuardPose;
            OnGuardPoseChanged?.Invoke(isGuardPoseActive);
        }

        private bool IsGuardPoseDetected()
        {
            if (gestureDetector == null)
                return false;

            return IsFistLike(gestureDetector.CurrentLeftPose, gestureDetector.CurrentLeftPrototypePose) &&
                   IsFistLike(gestureDetector.CurrentRightPose, gestureDetector.CurrentRightPrototypePose);
        }

        private static bool IsFistLike(PoseId pose, PoseType prototypePose)
        {
            return prototypePose == PoseType.Fist ||
                   pose == PoseId.Fist ||
                   pose == PoseId.FistPush;
        }

        private void UpdateResponseWindow()
        {
            if (!isResponseWindowOpen)
                return;

            if (Time.time > windowEndTime)
            {
                ResolveWindow(false, "Barrier Fail: timeout");
                return;
            }

            if (!isGuardPoseActive)
            {
                guardHoldTimer = 0f;
                return;
            }

            guardHoldTimer += Time.deltaTime;
            if (guardHoldTimer < requiredHoldTime)
                return;

            if (combatManager != null && !combatManager.TryConsumeMana(manaCost))
            {
                ResolveWindow(false, "Barrier Fail: no mana");
                return;
            }

            SetBarrierActive(true);
            ResolveWindow(true, $"Barrier Success: {currentAttackType}");
        }

        private void UpdateActiveBarrier()
        {
            // 속박 배리어가 활성 중이면 타이머로 끄지 않음 (제스처 해제 시 OnBothHandsBarrierGestureChanged가 처리)
            if (!isBarrierActive || isConstraintBarrierActive || Time.time <= activeEndTime)
                return;

            SetBarrierActive(false);
        }

        private void ResolveWindow(bool success, string result)
        {
            isResponseWindowOpen = false;
            guardHoldTimer = 0f;
            LastResultText = result;
            if (success)
                Debug.Log($"[Barrier] ✅ 베리어 성공 | 공격={currentAttackType} | {result}");
            else
                Debug.LogWarning($"[Barrier] ❌ 베리어 실패 | 공격={currentAttackType} | {result}");
            OnResponseWindowResolved?.Invoke(success, result);
        }

        private void SetBarrierActive(bool active)
        {
            if (isBarrierActive == active)
                return;

            isBarrierActive = active;
            if (active)
                activeEndTime = Time.time + activeDuration;

            OnBarrierActiveChanged?.Invoke(isBarrierActive);
        }
    }
}
