using System.Collections;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Boss
{
    [DefaultExecutionOrder(120)]
    public class BossChaseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private BossAI bossAI;
        [SerializeField] private BossStateMachine stateMachine;
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private Transform target;
        [SerializeField] private Transform movementRoot;
        [SerializeField] private Animator bossAnimator;

        [Header("Chase")]
        [SerializeField] private bool enableChase = true;
        [SerializeField] private float moveSpeed = 8.4f;
        [SerializeField] private float stoppingDistance = 8.2f;
        [SerializeField] private float resumeDistance = 9.3f;
        [SerializeField] private float maxChaseDistance = 250f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float visualYawOffsetDegrees = 180f;
        [SerializeField] private bool keepGrounded = true;
        [SerializeField] private float groundProbeUpDistance = 4f;
        [SerializeField] private float groundProbeDownDistance = 20f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Melee Attack")]
        [SerializeField] private bool attackWhenInRange = true;
        [SerializeField] private float attackRange = 9.3f;
        [SerializeField] private float meleeAttackCooldown = 3.1f;
        [SerializeField] private float attackPauseDuration = 1.75f;
        [SerializeField] private bool enableAttackResponsePatterns;
        [SerializeField] private bool applyGenericMeleeDamage = true;
        [SerializeField] private float genericMeleeDamage = 10f;
        [SerializeField] private float meleeHitRange = 9.8f;
        [SerializeField] private float attackWindupDistance = 0.35f;
        [SerializeField] private float attackLungeDistance = 1.1f;
        [SerializeField] private float highAttackWeight = 0.34f;
        [SerializeField] private float middleAttackWeight = 0.34f;
        [SerializeField] private float lowAttackWeight = 0.32f;

        [Header("Animation")]
        [SerializeField] private string runControllerResourcePath = "ArcaneVR/ThunderGolemRunController";
        [SerializeField] private string attackControllerResourcePath = "ArcaneVR/ThunderGolemAttackController";
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string moveSpeedParameter = "MoveSpeed";
        [SerializeField] private string movingParameter = "IsMoving";
        [SerializeField] private string runStateName = "Armature|Armature|Armature|Armature|running|baselayer";
        [SerializeField] private string fallbackRunStateName = "Run";
        [SerializeField] private string genericAttackStateName = "Armature|Armature|Armature|Armature|Triple_Combo_Attack|baselayer";
        [SerializeField] private float animationCrossFadeDuration = 0.1f;
        [SerializeField] private float runAnimationSpeedMultiplier = 0.45f;
        [SerializeField] private float attackAnimationSpeedMultiplier = 0.72f;
        [SerializeField] private float attackControllerRestoreDelay = 1.15f;

        private RuntimeAnimatorController runController;
        private RuntimeAnimatorController attackController;
        private RuntimeAnimatorController controllerBeforeRun;
        private RuntimeAnimatorController controllerBeforeAttack;
        private BossPatternCombatBridge subscribedPatternBridge;
        private CombatManager combatManager;
        private float chasePausedUntilTime;
        private float nextMeleeAttackTime;
        private float restoreAttackControllerAtTime;
        private bool usingRunController;
        private bool usingAttackController;
        private bool wasMoving;
        private bool animatorWasMoving;
        private Coroutine fallbackAttackMotionRoutine;

        public string LastChaseStatus { get; private set; } = "Chase: idle";
        public bool IsChasing { get; private set; }
        public float DistanceToTarget { get; private set; } = -1f;
        public bool IsInAttackRange => DistanceToTarget >= 0f && DistanceToTarget <= attackRange;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForActiveScene()
        {
            EnsureForScene(SceneManager.GetActiveScene().name);
        }

        public static BossChaseController EnsureForTarget(GolemCombatTarget target)
        {
            if (target == null)
                return null;

            var chase = target.GetComponentInParent<BossChaseController>();
            if (chase == null)
                chase = target.gameObject.AddComponent<BossChaseController>();

            chase.golemTarget = target;
            chase.ApplyPresentationDefaults();
            chase.ResolveReferences();
            return chase;
        }

        public void ApplyPresentationDefaults()
        {
            enableChase = true;
            moveSpeed = 8.4f;
            stoppingDistance = Mathf.Max(stoppingDistance, 8.2f);
            resumeDistance = Mathf.Max(resumeDistance, stoppingDistance + 1.0f);
            maxChaseDistance = Mathf.Max(maxChaseDistance, 250f);
            rotationSpeed = Mathf.Max(rotationSpeed, 720f);
            visualYawOffsetDegrees = 180f;
            runAnimationSpeedMultiplier = 0.45f;
            attackRange = Mathf.Max(attackRange, 9.3f);
            meleeHitRange = Mathf.Max(meleeHitRange, attackRange + 0.5f);
            enableAttackResponsePatterns = false;
            meleeAttackCooldown = Mathf.Min(meleeAttackCooldown, 2.75f);
            attackPauseDuration = Mathf.Min(attackPauseDuration, 1.35f);
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene.name);
        }

        private static void EnsureForScene(string sceneName)
        {
            if (!IsBattleScene(sceneName))
                return;

            var target = FindAnyObjectByType<GolemCombatTarget>();
            if (target == null)
                target = ResolveOrCreateGolemTarget();

            EnsureForTarget(target);
        }

        private static bool IsBattleScene(string sceneName)
        {
            return sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

        private static GolemCombatTarget ResolveOrCreateGolemTarget()
        {
            var candidate = GameObject.Find("attack_golemn") ??
                            GameObject.Find("Golem_Placeholder") ??
                            GameObject.Find("GolemPlaceholder") ??
                            GameObject.Find("Test Dummy Golem") ??
                            GameObject.Find("Golem");

            if (candidate == null)
                return null;

            return candidate.GetComponent<GolemCombatTarget>() ??
                   candidate.AddComponent<GolemCombatTarget>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            IsChasing = false;
            UpdateAnimator(false, 0f);
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();
            RestoreAttackControllerIfNeeded();
            UpdateChase();
        }

        private void ResolveReferences()
        {
            if (golemTarget == null)
                golemTarget = GetComponent<GolemCombatTarget>() ?? GetComponentInParent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();

            if (bossAI == null)
                bossAI = GetComponent<BossAI>() ?? FindAnyObjectByType<BossAI>();

            if (stateMachine == null)
                stateMachine = GetComponent<BossStateMachine>() ?? FindAnyObjectByType<BossStateMachine>();

            if (patternBridge == null)
                patternBridge = GetComponent<BossPatternCombatBridge>() ?? FindAnyObjectByType<BossPatternCombatBridge>();

            if (target == null)
                target = ArcanePlayerRigResolver.FindHeadTransform() ?? ArcanePlayerRigResolver.FindPlayerRigTransform();

            movementRoot = ResolveMovementRoot();

            if (bossAnimator == null)
                bossAnimator = ResolveAnimator();

            if (runController == null && !string.IsNullOrEmpty(runControllerResourcePath))
                runController = Resources.Load<RuntimeAnimatorController>(runControllerResourcePath);

            if (attackController == null && !string.IsNullOrEmpty(attackControllerResourcePath))
                attackController = Resources.Load<RuntimeAnimatorController>(attackControllerResourcePath);

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private Animator ResolveAnimator()
        {
            if (golemTarget != null)
            {
                var animator = golemTarget.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    return animator;
            }

            return GetComponentInChildren<Animator>(true);
        }

        private void Subscribe()
        {
            if (patternBridge == null || subscribedPatternBridge == patternBridge)
                return;

            Unsubscribe();
            subscribedPatternBridge = patternBridge;
            subscribedPatternBridge.OnAttackResponseWindowStarted += HandleAttackStarted;
            subscribedPatternBridge.OnChargeCounterWindowStarted += HandleChargeStarted;
            subscribedPatternBridge.OnGolemBarrierStarted += HandleBarrierStarted;
        }

        private void Unsubscribe()
        {
            if (subscribedPatternBridge == null)
                return;

            subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;
            subscribedPatternBridge.OnChargeCounterWindowStarted -= HandleChargeStarted;
            subscribedPatternBridge.OnGolemBarrierStarted -= HandleBarrierStarted;
            subscribedPatternBridge = null;
        }

        private void HandleAttackStarted(BossAttackType attackType, float duration)
        {
            PauseChase(duration + 0.45f, $"Chase: attack {attackType}");
        }

        private void HandleChargeStarted(float duration)
        {
            PauseChase(duration, "Chase: charge");
        }

        private void HandleBarrierStarted(float duration)
        {
            PauseChase(Mathf.Min(duration, 2.5f), "Chase: barrier");
        }

        private void PauseChase(float duration, string status)
        {
            chasePausedUntilTime = Mathf.Max(chasePausedUntilTime, Time.time + Mathf.Max(0f, duration));
            LastChaseStatus = status;
        }

        private void UpdateChase()
        {
            IsChasing = false;

            if (!enableChase)
            {
                LastChaseStatus = "Chase: disabled";
                UpdateAnimator(false, 0f);
                return;
            }

            if (target == null)
                target = ArcanePlayerRigResolver.FindHeadTransform() ?? ArcanePlayerRigResolver.FindPlayerRigTransform();

            if (target == null)
            {
                DistanceToTarget = -1f;
                LastChaseStatus = "Chase: no player";
                UpdateAnimator(false, 0f);
                return;
            }

            if (!CanMoveNow())
            {
                UpdateAnimator(false, 0f);
                return;
            }

            var root = movementRoot != null ? movementRoot : transform;
            var toTarget = target.position - root.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            DistanceToTarget = distance;

            if (distance > maxChaseDistance)
            {
                LastChaseStatus = $"Chase: far closing {distance:0.0}m";
            }

            var stopDistance = wasMoving ? stoppingDistance : resumeDistance;
            if (distance <= stopDistance)
            {
                FaceTarget(toTarget);
                wasMoving = false;
                if (TryStartMeleeAttack(distance))
                    return;

                LastChaseStatus = $"Chase: in range {distance:0.0}m";
                UpdateAnimator(false, 0f);
                return;
            }

            var speedMultiplier = golemTarget != null ? golemTarget.MovementSpeedMultiplier : 1f;
            var speed = moveSpeed * Mathf.Clamp(speedMultiplier, 0f, 2f);
            if (speed <= 0.01f)
            {
                LastChaseStatus = "Chase: stopped by status";
                UpdateAnimator(false, 0f);
                return;
            }

            var direction = toTarget.normalized;
            var delta = direction * Mathf.Min(speed * Time.deltaTime, Mathf.Max(0f, distance - stoppingDistance));
            var nextPosition = root.position + delta;
            if (keepGrounded)
                nextPosition = SnapToGround(nextPosition);

            root.position = nextPosition;
            FaceTarget(direction);

            IsChasing = true;
            wasMoving = true;
            LastChaseStatus = $"Chase: moving {distance:0.0}m";
            UpdateAnimator(true, speed / Mathf.Max(0.01f, moveSpeed));
        }

        public bool TryStartMeleeAttack(float distance)
        {
            if (!attackWhenInRange ||
                distance > attackRange ||
                Time.time < nextMeleeAttackTime ||
                !CanStartAttackNow())
            {
                return false;
            }

            var attackType = enableAttackResponsePatterns
                ? PickMeleeAttackType()
                : BossAttackType.Middle;
            nextMeleeAttackTime = Time.time + meleeAttackCooldown;
            PauseChase(attackPauseDuration, "Chase: melee");
            UpdateAnimator(false, 0f);
            PlayGenericAttackAnimation();
            PlayFallbackAttackMotion(attackType);

            if (enableAttackResponsePatterns)
            {
                if (stateMachine != null)
                    stateMachine.TriggerAttackNow(attackType);
                else
                    patternBridge?.BeginAttackResponseWindow(attackType);
            }

            LastChaseStatus = "Chase: generic melee";
            return true;
        }

        private void PlayFallbackAttackMotion(BossAttackType attackType)
        {
            if (!isActiveAndEnabled)
                return;

            if (fallbackAttackMotionRoutine != null)
                StopCoroutine(fallbackAttackMotionRoutine);

            fallbackAttackMotionRoutine = StartCoroutine(FallbackAttackMotion(attackType));
        }

        private IEnumerator FallbackAttackMotion(BossAttackType attackType)
        {
            var root = movementRoot != null ? movementRoot : transform;
            var origin = root.position;
            var forward = target != null ? target.position - root.position : root.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;
            forward.Normalize();

            var lift = enableAttackResponsePatterns && attackType == BossAttackType.High
                ? 0.22f
                : enableAttackResponsePatterns && attackType == BossAttackType.Low
                    ? -0.1f
                    : 0f;
            var windup = origin - forward * Mathf.Max(0f, attackWindupDistance) + Vector3.up * lift;
            var strike = origin + forward * Mathf.Max(0f, attackLungeDistance) + Vector3.up * lift;

            yield return MoveAttackPhase(origin, windup, 0.16f);
            yield return MoveAttackPhase(windup, strike, 0.14f);
            TryApplyGenericMeleeDamage();
            yield return MoveAttackPhase(strike, origin, 0.2f);
            root.position = keepGrounded ? SnapToGround(origin) : origin;
            fallbackAttackMotionRoutine = null;
        }

        private void TryApplyGenericMeleeDamage()
        {
            if (!applyGenericMeleeDamage || genericMeleeDamage <= 0f)
                return;

            ResolveReferences();
            if (combatManager == null || target == null)
                return;

            var root = movementRoot != null ? movementRoot : transform;
            var toTarget = target.position - root.position;
            toTarget.y = 0f;
            if (toTarget.magnitude > Mathf.Max(0.1f, meleeHitRange))
                return;

            combatManager.ApplyPlayerHit(genericMeleeDamage);
        }

        private IEnumerator MoveAttackPhase(Vector3 from, Vector3 to, float duration)
        {
            var startTime = Time.time;
            var safeDuration = Mathf.Max(0.01f, duration);
            while (Time.time - startTime < safeDuration)
            {
                var t = Mathf.Clamp01((Time.time - startTime) / safeDuration);
                var eased = t * t * (3f - 2f * t);
                var next = Vector3.Lerp(from, to, eased);
                var root = movementRoot != null ? movementRoot : transform;
                root.position = keepGrounded ? SnapToGround(next) : next;
                yield return null;
            }
        }

        private Transform ResolveMovementRoot()
        {
            if (golemTarget != null)
                return golemTarget.transform;

            return transform;
        }

        private bool CanStartAttackNow()
        {
            if (Time.time < chasePausedUntilTime)
                return false;

            if (golemTarget != null && !golemTarget.CanAct)
                return false;

            if (!enableAttackResponsePatterns)
                return bossAI == null || bossAI.CurrentState != BossState.Dead;

            if (bossAI == null)
                return true;

            return bossAI.CurrentState == BossState.Idle ||
                   bossAI.CurrentState == BossState.Charging;
        }

        private BossAttackType PickMeleeAttackType()
        {
            var total = Mathf.Max(0.01f, highAttackWeight + middleAttackWeight + lowAttackWeight);
            var roll = Random.value * total;
            if (roll < highAttackWeight)
                return BossAttackType.High;

            roll -= highAttackWeight;
            return roll < middleAttackWeight ? BossAttackType.Middle : BossAttackType.Low;
        }

        private bool CanMoveNow()
        {
            if (Time.time < chasePausedUntilTime)
                return false;

            if (golemTarget != null && !golemTarget.CanAct)
            {
                LastChaseStatus = "Chase: target cannot act";
                return false;
            }

            if (bossAI == null)
                return true;

            switch (bossAI.CurrentState)
            {
                case BossState.Dead:
                case BossState.Defense:
                case BossState.Weakness:
                    LastChaseStatus = $"Chase: state {bossAI.CurrentState}";
                    return false;
                default:
                    return true;
            }
        }

        private void FaceTarget(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) *
                                 Quaternion.Euler(0f, visualYawOffsetDegrees, 0f);
            var root = movementRoot != null ? movementRoot : transform;
            root.rotation = Quaternion.RotateTowards(
                root.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        private Vector3 SnapToGround(Vector3 position)
        {
            if (TryGetGroundHeight(position, out var groundY))
                position.y = groundY;

            return position;
        }

        private bool TryGetGroundHeight(Vector3 position, out float groundY)
        {
            var origin = position + Vector3.up * Mathf.Max(0.1f, groundProbeUpDistance);
            var maxDistance = groundProbeUpDistance + groundProbeDownDistance;
            var hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, groundMask, QueryTriggerInteraction.Ignore);
            var bestDistance = float.PositiveInfinity;
            groundY = position.y;
            var found = false;

            foreach (var hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    (movementRoot != null && hit.collider.transform.IsChildOf(movementRoot)) ||
                    ArcanePlayerRigResolver.IsPlayerCollider(hit.collider) ||
                    hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
            }

            if (found)
                return true;

            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null)
                    continue;

                var terrainPosition = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (position.x < terrainPosition.x ||
                    position.z < terrainPosition.z ||
                    position.x > terrainPosition.x + size.x ||
                    position.z > terrainPosition.z + size.z)
                {
                    continue;
                }

                groundY = terrain.SampleHeight(position) + terrainPosition.y;
                return true;
            }

            return false;
        }

        private void UpdateAnimator(bool moving, float normalizedSpeed)
        {
            if (bossAnimator == null)
                return;

            bossAnimator.applyRootMotion = false;
            if (IsAttackAnimationActive())
            {
                SetBoolIfExists(movingParameter, false);
                return;
            }

            var animationDrive = moving
                ? Mathf.Clamp(normalizedSpeed * Mathf.Max(0.05f, runAnimationSpeedMultiplier), 0.15f, 0.8f)
                : 0f;
            bossAnimator.speed = moving ? animationDrive : 1f;

            SetFloatIfExists(speedParameter, animationDrive);
            SetFloatIfExists(moveSpeedParameter, animationDrive);
            SetBoolIfExists(movingParameter, moving);

            if (moving)
            {
                EnsureRunControllerIfNeeded();
                if (!animatorWasMoving)
                    PlayRunAnimation();
            }
            else if (usingRunController)
            {
                if (bossAnimator.runtimeAnimatorController == runController)
                    bossAnimator.runtimeAnimatorController = controllerBeforeRun;

                controllerBeforeRun = null;
                usingRunController = false;
            }

            animatorWasMoving = moving;
        }

        private void PlayRunAnimation()
        {
            if (TryCrossFade(runStateName))
                return;

            if (TryCrossFade(fallbackRunStateName))
                return;

            TryCrossFade("Walk");
        }

        private void EnsureRunControllerIfNeeded()
        {
            if (bossAnimator == null || runController == null || bossAnimator.runtimeAnimatorController != null)
                return;

            controllerBeforeRun = bossAnimator.runtimeAnimatorController;
            usingRunController = true;
            bossAnimator.runtimeAnimatorController = runController;
        }

        private bool TryCrossFade(string stateName)
        {
            if (string.IsNullOrEmpty(stateName) ||
                bossAnimator == null ||
                bossAnimator.runtimeAnimatorController == null)
            {
                return false;
            }

            var stateHash = Animator.StringToHash(stateName);
            for (var layer = 0; layer < bossAnimator.layerCount; layer++)
            {
                if (!bossAnimator.HasState(layer, stateHash))
                    continue;

                bossAnimator.CrossFadeInFixedTime(stateHash, animationCrossFadeDuration, layer);
                return true;
            }

            return false;
        }

        private void PlayGenericAttackAnimation()
        {
            if (bossAnimator == null)
                return;

            bossAnimator.applyRootMotion = false;
            bossAnimator.speed = Mathf.Max(0.05f, attackAnimationSpeedMultiplier);

            if (attackController != null && bossAnimator.runtimeAnimatorController != attackController)
            {
                controllerBeforeAttack = bossAnimator.runtimeAnimatorController;
                bossAnimator.runtimeAnimatorController = attackController;
                usingAttackController = true;
            }

            restoreAttackControllerAtTime = Time.time + Mathf.Max(0.1f, attackControllerRestoreDelay);

            if (TryCrossFade(genericAttackStateName) || TryCrossFade("Attack"))
                return;

            if (bossAnimator.runtimeAnimatorController != null)
                bossAnimator.Play(genericAttackStateName, 0, 0f);
        }

        private bool IsAttackAnimationActive()
        {
            return restoreAttackControllerAtTime > 0f && Time.time < restoreAttackControllerAtTime;
        }

        private void RestoreAttackControllerIfNeeded()
        {
            if (restoreAttackControllerAtTime <= 0f || Time.time < restoreAttackControllerAtTime)
                return;

            restoreAttackControllerAtTime = 0f;

            if (bossAnimator != null)
            {
                bossAnimator.speed = 1f;
                if (usingAttackController && bossAnimator.runtimeAnimatorController == attackController)
                    bossAnimator.runtimeAnimatorController = controllerBeforeAttack;
            }

            controllerBeforeAttack = null;
            usingAttackController = false;
        }

        private void SetFloatIfExists(string parameterName, float value)
        {
            if (string.IsNullOrEmpty(parameterName) || bossAnimator == null)
                return;

            foreach (var parameter in bossAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float &&
                    parameter.name == parameterName)
                {
                    bossAnimator.SetFloat(parameterName, value);
                    return;
                }
            }
        }

        private void SetBoolIfExists(string parameterName, bool value)
        {
            if (string.IsNullOrEmpty(parameterName) || bossAnimator == null)
                return;

            foreach (var parameter in bossAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool &&
                    parameter.name == parameterName)
                {
                    bossAnimator.SetBool(parameterName, value);
                    return;
                }
            }
        }
    }
}
