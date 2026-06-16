using System;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class DodgeDetector : MonoBehaviour
    {
        public event Action OnDodgeSuccess;
        public event Action OnDodgeFail;
        public string LastDebugMessage { get; private set; }

        [Header("References")]
        [SerializeField] private Transform headTransform;

        [Header("Dodge Window")]
        [SerializeField] private float dodgeWindowDuration = 1.0f;

        [Header("High Attack - Duck Dodge")]
        [SerializeField] private float duckThreshold = 0.15f;

        [Header("Middle Attack - Body Side Dodge")]
        [SerializeField] private float bodySideThreshold = 0.25f;
        [SerializeField] private float maxVerticalMovementForBodyDodge = 0.20f;

        private BossAttackType currentAttackType;

        private Vector3 baselineHeadWorldPosition;
        private Vector3 baselineRightDirection;

        private float windowEndTime;
        private bool isWindowOpen;
        private bool hasResolved;

        public bool IsWindowOpen => isWindowOpen;
        public BossAttackType CurrentAttackType => currentAttackType;

        private void Awake()
        {
            if (headTransform == null && Camera.main != null)
            {
                headTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (!isWindowOpen || hasResolved)
                return;

            if (headTransform == null)
            {
                Debug.LogWarning("[DodgeDetector] Head Transform is missing.");
                ResolveFail();
                return;
            }

            if (Time.time > windowEndTime)
            {
                ResolveFail();
                return;
            }

            if (CheckDodgeSuccess())
            {
                ResolveSuccess();
            }
        }

        public void BeginDodgeWindow(BossAttackType attackType)
        {
            BeginDodgeWindow(attackType, dodgeWindowDuration);
        }

        public void BeginDodgeWindow(BossAttackType attackType, float duration)
        {
            if (headTransform == null)
            {
                Debug.LogWarning("[DodgeDetector] Cannot begin dodge window. Head Transform is missing.");
                return;
            }

            LastDebugMessage = $"Dodge Window Start: {attackType}";

            currentAttackType = attackType;

            baselineHeadWorldPosition = headTransform.position;
            baselineRightDirection = Vector3.ProjectOnPlane(headTransform.right, Vector3.up).normalized;

            windowEndTime = Time.time + Mathf.Max(0.1f, duration);
            isWindowOpen = true;
            hasResolved = false;
            Debug.Log($"[Dodge] 회피 창 열림 | 공격={attackType} | 시간={duration:0.0}s | High: 고개 숙이기 / Mid: 좌우 이동");
        }

        public void CancelDodgeWindow()
        {
            isWindowOpen = false;
            hasResolved = true;
        }

        // 투사체 도달 시점에 외부에서 호출 — 회피 못 했으면 실패 처리
        public void ForceResolve()
        {
            if (!isWindowOpen)
                return;

            ResolveFail();
        }

        private bool CheckDodgeSuccess()
        {
            switch (currentAttackType)
            {
                case BossAttackType.High:
                    return CheckHighAttackDodge();

                case BossAttackType.Middle:
                    return CheckMiddleAttackDodge();

                case BossAttackType.Low:
                    return false;

                default:
                    return false;
            }
        }

        private bool CheckHighAttackDodge()
        {
            Vector3 delta = headTransform.position - baselineHeadWorldPosition;
            return delta.y <= -duckThreshold;
        }

        private bool CheckMiddleAttackDodge()
        {
            Vector3 delta = headTransform.position - baselineHeadWorldPosition;
            float sideMove = Vector3.Dot(delta, baselineRightDirection);
            float verticalMove = Mathf.Abs(delta.y);
            bool movedSideEnough = Mathf.Abs(sideMove) >= bodySideThreshold;
            bool notJustDucking = verticalMove <= maxVerticalMovementForBodyDodge;
            return movedSideEnough && notJustDucking;
        }

        private void ResolveSuccess()
        {
            hasResolved = true;
            isWindowOpen = false;
            LastDebugMessage = $"Dodge Success: {currentAttackType}";
            Debug.Log($"[Dodge] ✅ 회피 성공 | 공격={currentAttackType}");
            OnDodgeSuccess?.Invoke();
        }

        private void ResolveFail()
        {
            hasResolved = true;
            isWindowOpen = false;
            LastDebugMessage = $"Dodge Fail: {currentAttackType}";
            Debug.LogWarning($"[Dodge] ❌ 회피 실패 | 공격={currentAttackType}");
            OnDodgeFail?.Invoke();
        }
    }
}
