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

        [Header("Debug")]
        [SerializeField] private bool showDebugLog = true;

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
            if (headTransform == null)
            {
                Debug.LogWarning("[DodgeDetector] Cannot begin dodge window. Head Transform is missing.");
                return;
            }

            LastDebugMessage = $"Dodge Window Start: {attackType}";

            currentAttackType = attackType;

            baselineHeadWorldPosition = headTransform.position;
            baselineRightDirection = Vector3.ProjectOnPlane(headTransform.right, Vector3.up).normalized;

            windowEndTime = Time.time + dodgeWindowDuration;
            isWindowOpen = true;
            hasResolved = false;

            if (showDebugLog)
            {
                Debug.Log($"[DodgeDetector] Dodge window opened. Attack={attackType}, Baseline={baselineHeadWorldPosition}");
            }
        }

        public void CancelDodgeWindow()
        {
            isWindowOpen = false;
            hasResolved = true;

            if (showDebugLog)
            {
                Debug.Log("[DodgeDetector] Dodge window cancelled.");
            }
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

            // 상단 공격: 고개를 아래로 숙이면 성공
            bool duckedEnough = delta.y <= -duckThreshold;

            if (showDebugLog)
            {
                Debug.Log($"[DodgeDetector] High Check | DeltaY={delta.y:F3}, Need=-{duckThreshold:F3}");
            }

            return duckedEnough;
        }

        private bool CheckMiddleAttackDodge()
        {
            Vector3 delta = headTransform.position - baselineHeadWorldPosition;

            // 공격 시작 순간의 오른쪽 방향 기준으로 좌우 이동량 계산
            float sideMove = Vector3.Dot(delta, baselineRightDirection);

            // 숙여서 피하는 것을 중단 회피로 오인하지 않기 위한 필터
            float verticalMove = Mathf.Abs(delta.y);

            bool movedSideEnough = Mathf.Abs(sideMove) >= bodySideThreshold;
            bool notJustDucking = verticalMove <= maxVerticalMovementForBodyDodge;

            if (showDebugLog)
            {
                string direction = sideMove >= 0f ? "Right" : "Left";
                Debug.Log(
                    $"[DodgeDetector] Middle Check | Side={sideMove:F3}({direction}), " +
                    $"Vertical={verticalMove:F3}, NeedSide={bodySideThreshold:F3}"
                );
            }

            return movedSideEnough && notJustDucking;
        }

        private void ResolveSuccess()
        {
            hasResolved = true;
            isWindowOpen = false;

            LastDebugMessage = $"Dodge Success: {currentAttackType}";
            if (showDebugLog)
            {
                
                Debug.Log($"[DodgeDetector] Dodge Success. Attack={currentAttackType}");
            }

            OnDodgeSuccess?.Invoke();
        }

        private void ResolveFail()
        {
            hasResolved = true;
            isWindowOpen = false;

            LastDebugMessage = $"Dodge Fail: {currentAttackType}";

            if (showDebugLog)
            {
                
                Debug.Log($"[DodgeDetector] Dodge Fail. Attack={currentAttackType}");
            }

            OnDodgeFail?.Invoke();
        }
    }
}