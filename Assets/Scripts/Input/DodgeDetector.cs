using System;
using UnityEngine;
using ArcaneVR.Core;
using ArcaneVR.Combat;

namespace ArcaneVR.Input
{
    /// <summary>
    /// HMD 이동 기반 회피 감지. High(숙이기)/Mid(후퇴)/Low(배리어 포즈) 3단계 + 하위 호환 OnDodgeSuccess.
    /// </summary>
    public class DodgeDetector : MonoBehaviour
    {
        public event Action OnDodgeSuccess;
        public event Action OnDodgeHigh;
        public event Action OnDodgeMid;
        public event Action OnDodgeLow;
        public event Action OnDodgeFail;

        [Header("── 머리 추적 ──")]
        [SerializeField] private Transform headTransform;

        [Header("── High 회피 (숙이기) ──")]
        [Tooltip("HMD Y 하강 최솟값 (m)")]
        [SerializeField] private float highDodgeYDrop = 0.30f;
        [Tooltip("HMD Y 하강 최댓값 (m) — 이 이상이면 High 불발, 폴백으로 처리")]
        [SerializeField] private float highDodgeYMax = 0.40f;

        [Header("── Mid 회피 (뒤로 물러남) ──")]
        [Tooltip("HMD Z 후퇴 최솟값 (m)")]
        [SerializeField] private float midDodgeZRetreat = 0.25f;
        [Tooltip("HMD Z 후퇴 최댓값 (m) — 이 이상이면 Mid 불발, 폴백으로 처리")]
        [SerializeField] private float midDodgeZMax = 0.35f;

        [Header("── 폴백 (하위 호환) ──")]
        [Tooltip("High/Mid 불발 시 X/Y/Z 중 하나라도 이 이상이면 OnDodgeSuccess만 발동")]
        [SerializeField] private float dodgeThreshold = 0.15f;

        [Header("── Low 회피 (배리어 포즈) ──")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;
        [Tooltip("양손 배리어 포즈 유지 시간 (초)")]
        [SerializeField] private float lowPoseHoldTime = 0.2f;
        [Tooltip("양손 간격 최솟값 (m)")]
        [SerializeField] private float lowPoseHandMinSpread = 0.15f;
        [Tooltip("양손 간격 최댓값 (m)")]
        [SerializeField] private float lowPoseHandMaxSpread = 0.45f;
        [Tooltip("흉부 높이: 머리 기준 아래 최솟값 (음수, m)")]
        [SerializeField] private float lowPoseChestMinOffset = -0.45f;
        [Tooltip("흉부 높이: 머리 기준 아래 최댓값 (음수, m)")]
        [SerializeField] private float lowPoseChestMaxOffset = -0.15f;
        [Tooltip("Low 회피 재발동 쿨타임 (초)")]
        [SerializeField] private float lowDodgeCooldown = 1.5f;
        [Tooltip("Low 회피 마나 소모량 (슬롯)")]
        [SerializeField] private int lowDodgeManaCost = 1;
        [SerializeField] private CombatManager combatManager;

        private Vector3 baselineHeadPosition;
        private bool isWindowOpen;
        private float lowPoseHeldTime;
        private float lastLowDodgeTime = -999f;

        public bool IsWindowOpen => isWindowOpen;
        public string LastDebugMessage { get; private set; } = "Dodge: idle";

        private void Awake()
        {
            ResolveTransforms();
        }

        private void Update()
        {
            ResolveTransforms();
            UpdateLowPose();

            if (!isWindowOpen || headTransform == null)
                return;

            var delta = headTransform.position - baselineHeadPosition;

            var yDrop = -delta.y;
            if (yDrop >= highDodgeYDrop && yDrop <= highDodgeYMax)
            {
                FireDodge("High", delta, () => OnDodgeHigh?.Invoke());
                return;
            }

            var zRetreat = -delta.z;
            if (zRetreat >= midDodgeZRetreat && zRetreat <= midDodgeZMax)
            {
                FireDodge("Mid", delta, () => OnDodgeMid?.Invoke());
                return;
            }

            if (Mathf.Abs(delta.x) >= dodgeThreshold ||
                Mathf.Abs(delta.y) >= dodgeThreshold ||
                Mathf.Abs(delta.z) >= dodgeThreshold)
            {
                isWindowOpen = false;
                LastDebugMessage = $"Dodge Fallback: Δ=({delta.x:0.00},{delta.y:0.00},{delta.z:0.00})";
                OnDodgeSuccess?.Invoke();
            }
        }

        private void FireDodge(string type, Vector3 delta, Action specificEvent)
        {
            isWindowOpen = false;
            LastDebugMessage = $"Dodge {type}: Δ=({delta.x:0.00},{delta.y:0.00},{delta.z:0.00})";
            specificEvent?.Invoke();
            OnDodgeSuccess?.Invoke();
        }

        private void UpdateLowPose()
        {
            if (headTransform == null || leftHandTransform == null || rightHandTransform == null)
                return;

            if (Time.time - lastLowDodgeTime < lowDodgeCooldown)
                return;

            var headY = headTransform.position.y;
            var minChestY = headY + lowPoseChestMinOffset;
            var maxChestY = headY + lowPoseChestMaxOffset;

            var leftY = leftHandTransform.position.y;
            var rightY = rightHandTransform.position.y;
            var handSpread = Vector3.Distance(leftHandTransform.position, rightHandTransform.position);

            var inPose = leftY >= minChestY && leftY <= maxChestY &&
                         rightY >= minChestY && rightY <= maxChestY &&
                         handSpread >= lowPoseHandMinSpread && handSpread <= lowPoseHandMaxSpread;

            if (inPose)
            {
                lowPoseHeldTime += Time.unscaledDeltaTime;
                if (lowPoseHeldTime >= lowPoseHoldTime)
                {
                    lowPoseHeldTime = 0f;
                    lastLowDodgeTime = Time.time;
                    FireLowDodge();
                }
            }
            else
            {
                lowPoseHeldTime = 0f;
            }
        }

        private void FireLowDodge()
        {
            if (combatManager != null && !combatManager.TryConsumeMana(lowDodgeManaCost))
            {
                LastDebugMessage = "Dodge Low: 마나 부족";
                return;
            }
            LastDebugMessage = "Dodge Low: 배리어 포즈";
            OnDodgeLow?.Invoke();
            OnDodgeSuccess?.Invoke();
        }

        private void ResolveTransforms()
        {
            if (headTransform == null)
                headTransform = ArcanePlayerRigResolver.FindHeadTransform();
            if (leftHandTransform == null)
                leftHandTransform = ArcanePlayerRigResolver.FindHandTransform(true);
            if (rightHandTransform == null)
                rightHandTransform = ArcanePlayerRigResolver.FindHandTransform(false);
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        public void BeginDodgeWindow()
        {
            ResolveTransforms();
            baselineHeadPosition = headTransform != null ? headTransform.position : Vector3.zero;
            isWindowOpen = true;
            LastDebugMessage = "Dodge Window";
        }

        public void CancelDodgeWindow()
        {
            if (isWindowOpen)
                OnDodgeFail?.Invoke();
            isWindowOpen = false;
            LastDebugMessage = "Dodge Cancelled";
        }
    }
}
