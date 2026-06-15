using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArcaneVR.Combat;

namespace ArcaneVR.Boss
{
    /// <summary>
    /// Manages BossAI state transitions. Handles HP threshold triggers (70%, 40%, 15%) and periodic Defense state (every 25s).
    /// </summary>
    public class BossStateMachine : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossAI bossAI;
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private ArcaneVR.Input.ConstraintController constraintController;

        [Header("디버그 / 테스트")]
        [Tooltip("체크하면 HP 페이즈 트리거 및 자동 패턴이 모두 멈춥니다.")]
        [SerializeField] private bool debugFreeze;
        [Tooltip("체크하면 플레이 모드 시작 5초 후 CenterFixed 페이즈를 자동으로 발동합니다.")]
        [SerializeField] private bool debugAutoTriggerCenterFixed;

        public bool DebugFreeze { get => debugFreeze; set => debugFreeze = value; }

        [Header("Pattern Timing")]
        [SerializeField] private bool runPatternsAutomatically;
        [SerializeField] private bool createResponsePatternHelpers;
        [SerializeField] private float firstPatternDelay = 3f;
        [SerializeField] private float attackInterval = 6f;
        [SerializeField] private float firstBarrierDelay = 18f;
        [SerializeField] private float defenseInterval = 22f;
        [SerializeField] private float defenseDuration = 14f;
        [SerializeField] private float chargeCounterDuration = 3f;
        [SerializeField] private float responseWindowDuration = 1.25f;

        [Header("중앙 고정 페이즈")]
        [Tooltip("보스가 이동할 경기장 외곽 위치 (Circle2).")]
        [SerializeField] private Transform bossPerimeterAnchor;
        [Tooltip("보스가 외곽으로 이동하는 속도 (m/s).")]
        [SerializeField] private float bossMoveSpeed = 5f;
        [SerializeField] private float centerFixedDuration = 12f;
        [SerializeField] private float centerFixedStateLockDuration = 1.5f;

        [Header("HP Phase Triggers")]
        [SerializeField, Range(0f, 1f)] private float phaseOneHpRatio = 0.7f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoHpRatio = 0.4f;
        [SerializeField, Range(0f, 1f)] private float finalPhaseHpRatio = 0.15f;

        private float nextAttackTime;
        private float nextDefenseTime;
        private float stateLockUntilTime;
        private float centerFixedEndTime;
        private bool phaseOneTriggered;
        private bool phaseTwoTriggered;
        private bool finalPhaseTriggered;
        private bool subscribed;
        private BossChaseController chaseController;
        private bool battleHelpersEnsured;
        private bool isCenterFixedPhaseActive;
        private bool isBossMovingToPerimeter;
        private Coroutine bossPerimeterRoutine;

        public string LastPatternStatus { get; private set; } = "BossSM: idle";
        public float NextAttackIn => Mathf.Max(0f, nextAttackTime - Time.time);
        public float NextDefenseIn => Mathf.Max(0f, nextDefenseTime - Time.time);

        private void Awake()
        {
            ResolveReferences();
            ResetTimers();
        }

        private void Start()
        {
            if (debugAutoTriggerCenterFixed)
                StartCoroutine(DebugAutoTriggerRoutine());
        }

        private IEnumerator DebugAutoTriggerRoutine()
        {
            yield return new WaitForSeconds(5f);
            TriggerCenterFixed("debug: 5초 자동 발동");
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();

            if (debugFreeze || !runPatternsAutomatically || bossAI == null || golemTarget == null)
                return;

            if (bossAI.CurrentState == BossState.Dead || golemTarget.CurrentHealth <= 0f)
                return;

            CheckHpPhaseTriggers();

            if (isCenterFixedPhaseActive)
            {
                if (bossAI.CurrentState == BossState.Weakness &&
                    !golemTarget.IsWeakExposed &&
                    !golemTarget.IsStaggered)
                {
                    bossAI.EnterCenterFixed();
                }

                if (Time.time >= centerFixedEndTime)
                {
                    EndCenterFixedPhase("BossSM: center-fixed complete");
                    return;
                }

                // 보스가 외곽으로 이동 중이면 공격 대기
                if (isBossMovingToPerimeter)
                {
                    LastPatternStatus = "BossSM: boss moving to perimeter";
                    return;
                }

                // 보스 도착, 플레이어 아직 미도착 — 공격 시간 계속 밀기
                if (constraintController != null && !constraintController.HasArrived)
                {
                    nextAttackTime = Time.time + 0.5f;
                    LastPatternStatus = "BossSM: waiting for player";
                    return;
                }

                if (Time.time < stateLockUntilTime)
                    return;

                if (!golemTarget.CanAct)
                    return;

                if (Time.time >= nextAttackTime)
                {
                    bossAI.EnterCenterFixed();
                    TriggerAttackPattern();
                }

                return;
            }

            if (Time.time < stateLockUntilTime)
                return;

            if (bossAI.CurrentState == BossState.Weakness &&
                !golemTarget.IsWeakExposed &&
                !golemTarget.IsStaggered)
            {
                bossAI.ChangeState(BossState.Idle);
            }

            if (!golemTarget.CanAct)
                return;

            if (Time.time >= nextDefenseTime)
            {
                TriggerDefense("periodic defense");
                return;
            }

            if (Time.time >= nextAttackTime)
            {
                if (chaseController != null && !chaseController.IsInAttackRange)
                {
                    LastPatternStatus = $"BossSM: chase closing {chaseController.DistanceToTarget:0.0}m";
                    ScheduleNextAttack(1f);
                    return;
                }

                TriggerAttackPattern();
            }
        }

        public void TriggerDefenseNow()
        {
            TriggerDefense("manual defense");
        }

        public void TriggerChargeNow()
        {
            TriggerCharge("manual charge");
        }

        public void TriggerCenterFixedNow()
        {
            TriggerCenterFixed("manual center-fixed");
        }

        public void TriggerAttackNow(BossAttackType attackType)
        {
            if (!runPatternsAutomatically)
            {
                LastPatternStatus = "BossSM: attack patterns disabled";
                return;
            }

            ResolveReferences();
            if (isCenterFixedPhaseActive)
                bossAI?.EnterCenterFixed();
            else
                bossAI?.ChangeState(BossState.Idle);
            patternBridge?.BeginAttackResponseWindow(attackType, responseWindowDuration);
            LastPatternStatus = $"BossSM: attack {attackType}";
            ScheduleNextAttack();
        }

        private void CheckHpPhaseTriggers()
        {
            if (golemTarget.MaxHealth <= 0f)
                return;

            var hpRatio = golemTarget.CurrentHealth / golemTarget.MaxHealth;

            if (!phaseOneTriggered && hpRatio <= phaseOneHpRatio)
            {
                phaseOneTriggered = true;
                TriggerDefense("HP 70% defense");
                return;
            }

            if (!phaseTwoTriggered && hpRatio <= phaseTwoHpRatio)
            {
                phaseTwoTriggered = true;
                TriggerCenterFixed("HP 40% center-fixed");
                return;
            }

            if (!finalPhaseTriggered && hpRatio <= finalPhaseHpRatio)
            {
                finalPhaseTriggered = true;
                TriggerDefense("HP 15% final barrier");
            }
        }

        private void TriggerAttackPattern()
        {
            var attackType = PickAttackType();
            TriggerAttackNow(attackType);
        }

        private BossAttackType PickAttackType()
        {
            var roll = Random.value;
            if (roll < 0.35f)
                return BossAttackType.High;

            if (roll < 0.7f)
                return BossAttackType.Middle;

            return BossAttackType.Low;
        }

        private void TriggerDefense(string reason)
        {
            isCenterFixedPhaseActive = false;
            centerFixedEndTime = -1f;
            ResolveReferences();
            bossAI?.EnterDefense();
            patternBridge?.BeginGolemBarrier(defenseDuration);
            stateLockUntilTime = Time.time + Mathf.Min(defenseDuration, 2.5f);
            nextDefenseTime = Time.time + defenseInterval;
            ScheduleNextAttack(2f);
            LastPatternStatus = $"BossSM: {reason}";
        }

        private void TriggerCharge(string reason)
        {
            isCenterFixedPhaseActive = false;
            ResolveReferences();
            bossAI?.BeginCharge();
            patternBridge?.BeginChargeCounterWindow(chargeCounterDuration);
            stateLockUntilTime = Time.time + chargeCounterDuration;
            ScheduleNextAttack(chargeCounterDuration + 1f);
            LastPatternStatus = $"BossSM: {reason}";
        }

        private void TriggerCenterFixed(string reason)
        {
            ResolveReferences();

            isCenterFixedPhaseActive = true;
            isBossMovingToPerimeter = bossPerimeterAnchor != null;
            centerFixedEndTime = Time.time + Mathf.Max(0.5f, centerFixedDuration);
            stateLockUntilTime = Time.time + Mathf.Max(0f, centerFixedStateLockDuration);
            nextDefenseTime = Mathf.Max(nextDefenseTime, centerFixedEndTime);
            nextAttackTime = Time.time + Mathf.Max(0.1f, centerFixedStateLockDuration);

            bossAI?.EnterCenterFixed();
            constraintController?.BeginConstraint();

            // 진행 중인 근접 돌진 모션 취소
            chaseController?.CancelFallbackAttackMotion();

            if (bossPerimeterAnchor != null)
            {
                if (bossPerimeterRoutine != null)
                    StopCoroutine(bossPerimeterRoutine);
                bossPerimeterRoutine = StartCoroutine(MoveBossToPerimeterRoutine());
            }

            LastPatternStatus = $"BossSM: {reason}";
        }

        private IEnumerator MoveBossToPerimeterRoutine()
        {
            var bossTransform = golemTarget != null ? golemTarget.transform : transform;
            var target = bossPerimeterAnchor;

            while (bossTransform != null && target != null)
            {
                var current = bossTransform.position;
                var dest = new Vector3(target.position.x, current.y, target.position.z);
                var dist = Vector3.Distance(new Vector3(current.x, 0f, current.z),
                                            new Vector3(dest.x, 0f, dest.z));

                if (dist <= 0.5f)
                {
                    bossTransform.position = dest;
                    break;
                }

                bossTransform.position = Vector3.MoveTowards(current, dest, bossMoveSpeed * Time.deltaTime);
                yield return null;
            }

            isBossMovingToPerimeter = false;
            bossPerimeterRoutine = null;
            LastPatternStatus = "BossSM: boss arrived, waiting for player";

            // 플레이어도 경기장 중앙에 도착할 때까지 대기
            while (constraintController != null && !constraintController.HasArrived && isCenterFixedPhaseActive)
                yield return null;

            // 둘 다 도착 — 1초 후 첫 공격 시작
            nextAttackTime = Time.time + 1f;
            LastPatternStatus = "BossSM: both arrived, attacks begin";
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (current <= 0f)
                bossAI?.Die();
        }

        private void HandleDefeated()
        {
            isCenterFixedPhaseActive = false;
            bossAI?.Die();
            LastPatternStatus = "BossSM: dead";
        }

        private void HandleBarrierStarted()
        {
            if (!isCenterFixedPhaseActive)
                bossAI?.EnterDefense();
            LastPatternStatus = "BossSM: barrier active";
        }

        private void HandleBarrierBroken()
        {
            bossAI?.ExposeWeakness();
            stateLockUntilTime = Time.time + 1.5f;
            ScheduleNextAttack(3f);
            LastPatternStatus = "BossSM: barrier broken";
        }

        private void HandleWeaknessExposed()
        {
            bossAI?.ExposeWeakness();
            stateLockUntilTime = Time.time + Mathf.Max(1.5f, golemTarget != null ? golemTarget.WeakRemaining : 1.5f);
            LastPatternStatus = "BossSM: weakness exposed";
        }

        private void HandleChargeCounterSucceeded()
        {
            bossAI?.ExposeWeakness();
            stateLockUntilTime = Time.time + 2f;
            ScheduleNextAttack(4f);
            LastPatternStatus = "BossSM: charge countered";
        }

        private void ResolveReferences()
        {
            if (bossAI == null)
                bossAI = GetComponent<BossAI>() ?? FindAnyObjectByType<BossAI>();

            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();

            if (golemTarget != null)
                chaseController = BossChaseController.EnsureForTarget(golemTarget);

            if (constraintController == null)
                constraintController = FindAnyObjectByType<ArcaneVR.Input.ConstraintController>();

            if (runPatternsAutomatically && patternBridge == null)
                patternBridge = GetComponent<BossPatternCombatBridge>() ??
                                FindAnyObjectByType<BossPatternCombatBridge>() ??
                                gameObject.AddComponent<BossPatternCombatBridge>();

            if (runPatternsAutomatically && createResponsePatternHelpers)
                EnsureBattleHelpers();
        }

        private void EnsureBattleHelpers()
        {
            if (battleHelpersEnsured)
                return;

            if (FindAnyObjectByType<DodgeDetector>() == null)
                gameObject.AddComponent<DodgeDetector>();

            if (FindAnyObjectByType<BarrierController>() == null)
                gameObject.AddComponent<BarrierController>();

            if (FindAnyObjectByType<DodgePlayerDamageBridge>() == null)
                gameObject.AddComponent<DodgePlayerDamageBridge>();

            if (FindAnyObjectByType<BarrierPlayerDamageBridge>() == null)
                gameObject.AddComponent<BarrierPlayerDamageBridge>();

            if (FindAnyObjectByType<BossAttackTelegraphController>() == null)
                gameObject.AddComponent<BossAttackTelegraphController>();

            if (FindAnyObjectByType<BossAttackEffectController>() == null)
                gameObject.AddComponent<BossAttackEffectController>();

            if (FindAnyObjectByType<BossAttackAnimatorBridge>() == null)
                gameObject.AddComponent<BossAttackAnimatorBridge>();

            if (FindAnyObjectByType<BarrierVisualController>() == null)
                gameObject.AddComponent<BarrierVisualController>();

            if (FindAnyObjectByType<BossCombatFeedbackController>() == null)
                gameObject.AddComponent<BossCombatFeedbackController>();

            battleHelpersEnsured = true;
        }

        private void Subscribe()
        {
            if (subscribed || golemTarget == null)
                return;

            golemTarget.OnHealthChanged += HandleHealthChanged;
            golemTarget.OnDefeated += HandleDefeated;
            golemTarget.OnBarrierStarted += HandleBarrierStarted;
            golemTarget.OnBarrierBroken += HandleBarrierBroken;
            golemTarget.OnWeaknessExposed += HandleWeaknessExposed;
            golemTarget.OnChargeCounterSucceeded += HandleChargeCounterSucceeded;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || golemTarget == null)
                return;

            golemTarget.OnHealthChanged -= HandleHealthChanged;
            golemTarget.OnDefeated -= HandleDefeated;
            golemTarget.OnBarrierStarted -= HandleBarrierStarted;
            golemTarget.OnBarrierBroken -= HandleBarrierBroken;
            golemTarget.OnWeaknessExposed -= HandleWeaknessExposed;
            golemTarget.OnChargeCounterSucceeded -= HandleChargeCounterSucceeded;
            subscribed = false;
        }

        private void ResetTimers()
        {
            nextAttackTime = Time.time + firstPatternDelay;
            nextDefenseTime = Time.time + firstBarrierDelay;
            stateLockUntilTime = 0f;
            centerFixedEndTime = -1f;
            isCenterFixedPhaseActive = false;
        }

        private void ScheduleNextAttack(float extraDelay = 0f)
        {
            var speedMultiplier = golemTarget != null ? Mathf.Max(0.25f, golemTarget.ActionSpeedMultiplier) : 1f;
            nextAttackTime = Time.time + (attackInterval / speedMultiplier) + Mathf.Max(0f, extraDelay);
        }

        private void EndCenterFixedPhase(string reason)
        {
            isCenterFixedPhaseActive = false;
            isBossMovingToPerimeter = false;
            centerFixedEndTime = -1f;

            if (bossPerimeterRoutine != null)
            {
                StopCoroutine(bossPerimeterRoutine);
                bossPerimeterRoutine = null;
            }

            bossAI?.ChangeState(BossState.Idle);
            constraintController?.EndConstraint();
            nextDefenseTime = Time.time + defenseInterval;
            ScheduleNextAttack(1f);
            LastPatternStatus = $"BossSM: {reason}";
        }
    }
}
