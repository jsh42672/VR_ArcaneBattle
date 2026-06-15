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
        [Tooltip("끌어당기는 속도 (m/s).")]
        [SerializeField] private float pullSpeed = 3f;
        [Tooltip("도착 판정 거리 (m).")]
        [SerializeField] private float arrivalThreshold = 0.3f;

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
            var target = playerCenterAnchor;
            var head = ArcanePlayerRigResolver.FindHeadTransform();

            while (playerRigRoot != null && target != null && head != null)
            {
                var headXZ = new Vector3(head.position.x, 0f, head.position.z);
                var targetXZ = new Vector3(target.position.x, 0f, target.position.z);
                var dist = Vector3.Distance(headXZ, targetXZ);

                if (dist <= arrivalThreshold)
                {
                    var headOffset = new Vector3(
                        head.position.x - playerRigRoot.position.x,
                        0f,
                        head.position.z - playerRigRoot.position.z);
                    playerRigRoot.position = new Vector3(
                        target.position.x - headOffset.x,
                        playerRigRoot.position.y,
                        target.position.z - headOffset.z);
                    HasArrived = true;
                    LastDebugMessage = "Constraint: arrived";
                    break;
                }

                var dir = (targetXZ - headXZ).normalized;
                playerRigRoot.position += new Vector3(dir.x, 0f, dir.z) * pullSpeed * Time.deltaTime;
                LastDebugMessage = $"Constraint: pulling {dist:0.1f}m";
                yield return null;
            }

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
