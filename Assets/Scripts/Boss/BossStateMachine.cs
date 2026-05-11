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

        [Header("Pattern Timing")]
        [SerializeField] private bool runPatternsAutomatically = true;
        [SerializeField] private float firstPatternDelay = 3f;
        [SerializeField] private float attackInterval = 6f;
        [SerializeField] private float defenseInterval = 25f;
        [SerializeField] private float defenseDuration = 8f;
        [SerializeField] private float chargeCounterDuration = 3f;
        [SerializeField] private float responseWindowDuration = 1.25f;

        [Header("HP Phase Triggers")]
        [SerializeField, Range(0f, 1f)] private float phaseOneHpRatio = 0.7f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoHpRatio = 0.4f;
        [SerializeField, Range(0f, 1f)] private float finalPhaseHpRatio = 0.15f;

        private float nextAttackTime;
        private float nextDefenseTime;
        private float stateLockUntilTime;
        private bool phaseOneTriggered;
        private bool phaseTwoTriggered;
        private bool finalPhaseTriggered;
        private bool subscribed;

        public string LastPatternStatus { get; private set; } = "BossSM: idle";
        public float NextAttackIn => Mathf.Max(0f, nextAttackTime - Time.time);
        public float NextDefenseIn => Mathf.Max(0f, nextDefenseTime - Time.time);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForBattleScenes()
        {
            CreateForScene(SceneManager.GetActiveScene().name);
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            CreateForScene(scene.name);
        }

        private static void CreateForScene(string sceneName)
        {
            if (!IsBattleScene(sceneName))
                return;

            if (FindAnyObjectByType<BossStateMachine>() != null)
                return;

            var host = GameObject.Find("BattleManager") ??
                       GameObject.Find("Arcane Test Hub") ??
                       new GameObject("Boss State Machine");

            if (FindAnyObjectByType<BossAI>() == null)
                host.AddComponent<BossAI>();

            host.AddComponent<BossStateMachine>();
        }

        private static bool IsBattleScene(string sceneName)
        {
            return sceneName == "BattleSceen2" ||
                   sceneName == "BattleScene2" ||
                   sceneName.EndsWith("Coloseum");
        }

        private void Awake()
        {
            ResolveReferences();
            ResetTimers();
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

            if (!runPatternsAutomatically || bossAI == null || golemTarget == null)
                return;

            if (bossAI.CurrentState == BossState.Dead || golemTarget.CurrentHealth <= 0f)
                return;

            CheckHpPhaseTriggers();

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

        public void TriggerAttackNow(BossAttackType attackType)
        {
            ResolveReferences();
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
                TriggerCharge("HP 40% charge");
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
            ResolveReferences();
            bossAI?.BeginCharge();
            patternBridge?.BeginChargeCounterWindow(chargeCounterDuration);
            stateLockUntilTime = Time.time + chargeCounterDuration;
            ScheduleNextAttack(chargeCounterDuration + 1f);
            LastPatternStatus = $"BossSM: {reason}";
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (current <= 0f)
                bossAI?.Die();
        }

        private void HandleDefeated()
        {
            bossAI?.Die();
            LastPatternStatus = "BossSM: dead";
        }

        private void HandleBarrierStarted()
        {
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

            if (patternBridge == null)
                patternBridge = GetComponent<BossPatternCombatBridge>() ?? FindAnyObjectByType<BossPatternCombatBridge>();
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
            nextDefenseTime = Time.time + defenseInterval;
            stateLockUntilTime = 0f;
        }

        private void ScheduleNextAttack(float extraDelay = 0f)
        {
            var speedMultiplier = golemTarget != null ? Mathf.Max(0.25f, golemTarget.ActionSpeedMultiplier) : 1f;
            nextAttackTime = Time.time + (attackInterval / speedMultiplier) + Mathf.Max(0f, extraDelay);
        }
    }
}
