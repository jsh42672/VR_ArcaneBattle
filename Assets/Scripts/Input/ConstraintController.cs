using System;
using System.Collections;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// CenterFixed 페이즈: 플레이어를 경기장 중앙으로 끌어당기고 대부분의 행동을 차단.
    /// 허용: 마도서 펼치기, 배리어 제스처. 차단: 마법 시전, 조합 마법, 이동.
    /// </summary>
    public class ConstraintController : MonoBehaviour
    {
        [Header("── 속박 대상 ──")]
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private CombinationFocusModeController combinationFocus;

        [Header("── 경기장 중앙 고정 ──")]
        [Tooltip("플레이어 리그 루트 (ArcanePlayerRig).")]
        [SerializeField] private Transform playerRigRoot;
        [Tooltip("플레이어를 끌어당길 경기장 중앙 마커 (Circle).")]
        [SerializeField] private Transform playerCenterAnchor;
        [Tooltip("플레이어를 중앙으로 이동시키는 데 걸리는 시간 (초).")]
        [SerializeField] private float pullDuration = 3f;
        [Tooltip("도착 판정 거리 (m).")]
        [SerializeField] private float arrivalThreshold = 1.0f;

        public event Action OnConstraintStart;
        public event Action OnConstraintEnd;
        public event Action OnConstraintStarted;
        public event Action OnConstraintEnded;

        private const string ConstraintReason = "CenterFixed";
        private Coroutine pullRoutine;

        public bool IsConstrained { get; private set; }
        public bool HasArrived { get; private set; }
        public string LastDebugMessage { get; private set; } = "Constraint: idle";

        private void Awake()
        {
            ResolveReferences();
        }

        public void BeginConstraint()
        {
            if (IsConstrained)
                return;

            ResolveReferences();
            IsConstrained = true;
            HasArrived = false;

            // 이동 억제
            handPullMovement?.SetMovementSuppressed(true, ConstraintReason);

            // 마법 시전 차단
            spellCaster?.SetCastingSuppressed(true, ConstraintReason);

            // 조합 마법 차단 (컴포넌트 비활성화)
            if (combinationFocus != null)
                combinationFocus.enabled = false;

            // 경기장 중앙으로 끌어당김
            if (playerRigRoot != null && playerCenterAnchor != null)
            {
                if (pullRoutine != null)
                    StopCoroutine(pullRoutine);
                pullRoutine = StartCoroutine(PullToCenter());
            }

            LastDebugMessage = "Constraint: start";
            OnConstraintStart?.Invoke();
            OnConstraintStarted?.Invoke();
        }

        public void EndConstraint()
        {
            if (!IsConstrained)
                return;

            if (pullRoutine != null)
            {
                StopCoroutine(pullRoutine);
                pullRoutine = null;
            }

            IsConstrained = false;
            HasArrived = false;

            handPullMovement?.SetMovementSuppressed(false, ConstraintReason);
            spellCaster?.SetCastingSuppressed(false, ConstraintReason);

            if (combinationFocus != null)
                combinationFocus.enabled = true;

            LastDebugMessage = "Constraint: end";
            OnConstraintEnd?.Invoke();
            OnConstraintEnded?.Invoke();
        }

        public void SetConstraintActive(bool active)
        {
            if (active) BeginConstraint();
            else EndConstraint();
        }

        private void OnDisable()
        {
            if (IsConstrained)
                EndConstraint();
        }

        private IEnumerator PullToCenter()
        {
            var anchor = playerCenterAnchor;
            var head = ArcanePlayerRigResolver.FindHeadTransform();
            if (playerRigRoot == null || anchor == null || head == null)
                yield break;

            // 시작 시점의 head-rig 오프셋 기준으로 목표 rig 위치 계산
            var headOffsetAtStart = new Vector3(
                head.position.x - playerRigRoot.position.x,
                0f,
                head.position.z - playerRigRoot.position.z);
            var startRigPos = playerRigRoot.position;
            var targetRigPos = new Vector3(
                anchor.position.x - headOffsetAtStart.x,
                playerRigRoot.position.y,
                anchor.position.z - headOffsetAtStart.z);

            var elapsed = 0f;
            while (elapsed < pullDuration && playerRigRoot != null && anchor != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pullDuration));
                playerRigRoot.position = Vector3.Lerp(startRigPos, targetRigPos, t);

                var headXZ = new Vector3(head.position.x, 0f, head.position.z);
                var anchorXZ = new Vector3(anchor.position.x, 0f, anchor.position.z);
                var dist = Vector3.Distance(headXZ, anchorXZ);
                LastDebugMessage = $"Constraint: pulling {dist:0.1f}m";

                if (dist <= arrivalThreshold)
                {
                    HasArrived = true;
                    LastDebugMessage = "Constraint: arrived";
                    break;
                }

                yield return null;
            }

            // 3초 종료 후 정확한 위치로 스냅
            if (playerRigRoot != null && head != null && anchor != null)
            {
                var finalOffset = new Vector3(
                    head.position.x - playerRigRoot.position.x,
                    0f,
                    head.position.z - playerRigRoot.position.z);
                playerRigRoot.position = new Vector3(
                    anchor.position.x - finalOffset.x,
                    playerRigRoot.position.y,
                    anchor.position.z - finalOffset.z);
            }
            HasArrived = true;
            LastDebugMessage = "Constraint: arrived";
            pullRoutine = null;
        }

        private void ResolveReferences()
        {
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            if (combinationFocus == null)
                combinationFocus = FindAnyObjectByType<CombinationFocusModeController>();

            if (playerRigRoot == null)
            {
                var rig = GameObject.Find("ArcanePlayerRig");
                if (rig != null)
                    playerRigRoot = rig.transform;
            }
        }
    }
}
