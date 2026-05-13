using System.Collections;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using CombatDodgeDetector = ArcaneVR.Combat.DodgeDetector;

namespace ArcaneVR.Input
{
    [DefaultExecutionOrder(90)]
    public class MagicSystemTestDriver : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private ExternalVoiceCommandBridge externalVoiceBridge;
        [SerializeField] private MetaVoiceSdkAutoBridge metaVoiceSdkAutoBridge;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private GestureEventRouter gestureRouter;
        [SerializeField] private ArcaneActionModeController actionModeController;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private BarrierPlayerDamageBridge barrierPlayerDamageBridge;
        [SerializeField] private BossPatternCombatBridge bossPatternBridge;
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private BossElementStatusVfx golemStatusVfx;
        [SerializeField] private CombatDodgeDetector dodgeDetector;
        [SerializeField] private DodgePlayerDamageBridge dodgePlayerDamageBridge;
        [SerializeField] private BossAttackTelegraphController bossAttackTelegraph;
        [SerializeField] private BossAttackAnimatorBridge bossAttackAnimatorBridge;
        [SerializeField] private BarrierVisualController barrierVisualController;
        [SerializeField] private GestureConflictDiagnostics gestureDiagnostics;
        [SerializeField] private ArcaneDebugStatusPanel debugStatusPanel;
        [SerializeField] private Transform spellSpawnRoot;
        [SerializeField] private bool normalizeOvrHandsAtRuntime = true;
        [SerializeField] private float handRigNormalizeDuration = 3f;
        [SerializeField] private float handRigNormalizeInterval = 0.5f;
        [SerializeField] private float referenceRefreshInterval = 0.75f;
        [SerializeField] private bool enableAutomaticTestWindows;
        [SerializeField] private bool createRuntimeDummyGolem = true;
        [SerializeField] private bool configureRuntimeSpellCaster = true;
        [SerializeField] private bool applyVoiceThunderToChargeWindow = true;
        [SerializeField] private bool applyComboDirectlyToGolem = true;
        [SerializeField] private float firstWindowDelay = 4f;
        [SerializeField] private float chargeInterval = 10f;
        [SerializeField] private float chargeWindowDuration = 3f;
        [SerializeField] private float barrierResponseInterval = 14f;
        [SerializeField] private float barrierResponseDuration = 2f;
        [SerializeField] private float golemBarrierInterval = 18f;
        [SerializeField] private float golemBarrierDuration = 7f;

        private float nextChargeTime;
        private float nextBarrierResponseTime;
        private float nextGolemBarrierTime;
        private float nextReferenceResolveTime;
        private bool spellCasterConfigured;
        private bool movementConfigured;
        private bool handRigNormalized;
        private OVRHand cachedLeftHand;
        private OVRHand cachedRightHand;
        private float nextHandRigNormalizeTime;
        private Coroutine smokeTestRoutine;
        private bool smokeTestPreviousAutomaticWindows;

        public string DriverStatus { get; private set; } = "TestDriver: idle";
        public bool IsSpellCasterConfigured => spellCasterConfigured;
        public bool IsAutomaticTestWindowsEnabled => enableAutomaticTestWindows;
        public float NextBarrierResponseIn => enableAutomaticTestWindows ? Mathf.Max(0f, nextBarrierResponseTime - Time.time) : -1f;
        public float NextChargeWindowIn => enableAutomaticTestWindows ? Mathf.Max(0f, nextChargeTime - Time.time) : -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForArcaneScenes()
        {
            CreateForScene(SceneManager.GetActiveScene().name);
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            CreateForScene(scene.name);
        }

        private static void CreateForScene(string sceneName)
        {
            if (sceneName == "World_main")
                return;

            if (!HandGestureDebugOverlay.IsGestureOverlayScene(sceneName))
                return;

            if (FindAnyObjectByType<MagicSystemTestDriver>() != null)
                return;

            var host = GameObject.Find("Arcane Test Hub") ??
                       GameObject.Find("MagicSystemTestDriver") ??
                       new GameObject("MagicSystemTestDriver");
            host.AddComponent<MagicSystemTestDriver>();
        }

        private void Awake()
        {
            ResolveOrCreateReferences();
            ResetTimers();
        }

        private void OnEnable()
        {
            ResolveOrCreateReferences();
            if (voiceRecognizer != null)
                voiceRecognizer.OnVoiceCommand += HandleVoiceCommand;
            if (combinationChecker != null)
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
        }

        private void OnDisable()
        {
            if (voiceRecognizer != null)
                voiceRecognizer.OnVoiceCommand -= HandleVoiceCommand;
            if (combinationChecker != null)
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
        }

        private void Update()
        {
            if (Time.time >= nextReferenceResolveTime)
            {
                nextReferenceResolveTime = Time.time + Mathf.Max(0.1f, referenceRefreshInterval);
                ResolveOrCreateReferences();
            }

            if (!enableAutomaticTestWindows || !IsCombatScene(SceneManager.GetActiveScene().name))
                return;

            if (golemTarget != null && Time.time >= nextChargeTime)
            {
                bossPatternBridge?.BeginChargeCounterWindow(chargeWindowDuration);
                nextChargeTime = Time.time + chargeInterval;
                DriverStatus = "TestDriver: CHARGE window";
            }

            if (barrierController != null && Time.time >= nextBarrierResponseTime)
            {
                bossPatternBridge?.BeginAttackResponseWindow(BossAttackType.Low, barrierResponseDuration);
                nextBarrierResponseTime = Time.time + barrierResponseInterval;
                DriverStatus = "TestDriver: BARRIER response";
            }

            if (golemTarget != null && Time.time >= nextGolemBarrierTime)
            {
                bossPatternBridge?.BeginGolemBarrier(golemBarrierDuration);
                nextGolemBarrierTime = Time.time + golemBarrierInterval;
                DriverStatus = "TestDriver: GOLEM barrier";
            }
        }

        [ContextMenu("Test/Begin Charge Counter Window")]
        public void BeginChargeCounterWindow()
        {
            if (!CanRunContextTest())
                return;

            ResolveOrCreateReferences();
            bossPatternBridge?.BeginChargeCounterWindow(chargeWindowDuration);
            DriverStatus = "TestDriver: CHARGE window";
        }

        [ContextMenu("Test/Begin Barrier Response Window")]
        public void BeginBarrierResponseWindow()
        {
            if (!CanRunContextTest())
                return;

            ResolveOrCreateReferences();
            bossPatternBridge?.BeginAttackResponseWindow(BossAttackType.Low, barrierResponseDuration);
            DriverStatus = "TestDriver: BARRIER response";
        }

        [ContextMenu("Test/Begin Golem Barrier")]
        public void BeginGolemBarrier()
        {
            if (!CanRunContextTest())
                return;

            ResolveOrCreateReferences();
            bossPatternBridge?.BeginGolemBarrier(golemBarrierDuration);
            DriverStatus = "TestDriver: GOLEM barrier";
        }

        [ContextMenu("Test/Voice FIRE")]
        public void TestVoiceFire()
        {
            if (!CanRunContextTest())
                return;

            SubmitVoiceCommand("fire");
        }

        [ContextMenu("Test/Voice ICE")]
        public void TestVoiceIce()
        {
            if (!CanRunContextTest())
                return;

            SubmitVoiceCommand("ice");
        }

        [ContextMenu("Test/Voice THUNDER")]
        public void TestVoiceThunder()
        {
            if (!CanRunContextTest())
                return;

            SubmitVoiceCommand("thunder");
        }

        [ContextMenu("Test/Combo Fire + Ice")]
        public void TestComboFireIce()
        {
            if (!CanRunContextTest())
                return;

            SimulateCombo(ElementType.Fire, ElementType.Ice);
        }

        [ContextMenu("Test/Combo Ice + Thunder")]
        public void TestComboIceThunder()
        {
            if (!CanRunContextTest())
                return;

            SimulateCombo(ElementType.Ice, ElementType.Thunder);
        }

        [ContextMenu("Test/Combo Thunder + Fire")]
        public void TestComboThunderFire()
        {
            if (!CanRunContextTest())
                return;

            SimulateCombo(ElementType.Thunder, ElementType.Fire);
        }

        [ContextMenu("Test/Thunder Charge Counter")]
        public void TestThunderChargeCounter()
        {
            if (!CanRunContextTest())
                return;

            ResolveOrCreateReferences();
            bossPatternBridge?.BeginChargeCounterWindow(chargeWindowDuration);
            golemTarget?.OnHit(new SpellHitData(
                SpellId.Single_Strike,
                ElementType.Thunder,
                StatusEffect.Stagger,
                1f,
                chargeWindowDuration,
                1f,
                0f));
            DriverStatus = "TestDriver: THUNDER counter";
        }

        [ContextMenu("Test/Refill Mana")]
        public void RefillMana()
        {
            if (!CanRunContextTest())
                return;

            ResolveOrCreateReferences();
            if (combatManager != null)
                combatManager.RefundMana(combatManager.MaxMana);
            DriverStatus = "TestDriver: mana refilled";
        }

        [ContextMenu("Demo/Run Spell Combat Smoke Test")]
        public void RunSpellCombatSmokeTest()
        {
            if (!CanRunContextTest())
                return;

            if (smokeTestRoutine != null)
            {
                StopCoroutine(smokeTestRoutine);
                smokeTestRoutine = null;
                enableAutomaticTestWindows = smokeTestPreviousAutomaticWindows;
            }

            smokeTestPreviousAutomaticWindows = enableAutomaticTestWindows;
            enableAutomaticTestWindows = false;
            smokeTestRoutine = StartCoroutine(SpellCombatSmokeTest());
        }

        [ContextMenu("Demo/Stop Spell Combat Smoke Test")]
        public void StopSpellCombatSmokeTest()
        {
            if (!Application.isPlaying)
                return;

            if (smokeTestRoutine != null)
            {
                StopCoroutine(smokeTestRoutine);
                smokeTestRoutine = null;
            }

            enableAutomaticTestWindows = smokeTestPreviousAutomaticWindows;
            DriverStatus = "TestDriver: smoke test stopped";
        }

        private void ResolveOrCreateReferences()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>() ?? gameObject.AddComponent<GestureDetector>();

            if (gestureRouter == null)
            {
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();
                if (gestureRouter == null)
                    gestureRouter = gameObject.AddComponent<GestureEventRouter>();
            }

            gestureDetector.BindGestureEventRouter(gestureRouter);

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>() ?? gameObject.AddComponent<CombatManager>();

            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>() ?? gameObject.AddComponent<VoiceRecognizer>();

            if (externalVoiceBridge == null)
                externalVoiceBridge = FindAnyObjectByType<ExternalVoiceCommandBridge>() ?? gameObject.AddComponent<ExternalVoiceCommandBridge>();

            if (metaVoiceSdkAutoBridge == null)
                metaVoiceSdkAutoBridge = FindAnyObjectByType<MetaVoiceSdkAutoBridge>() ?? gameObject.AddComponent<MetaVoiceSdkAutoBridge>();

            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>() ?? gameObject.AddComponent<CombinationChecker>();

            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>() ?? gameObject.AddComponent<SpellCaster>();

            if (actionModeController == null)
                actionModeController = FindAnyObjectByType<ArcaneActionModeController>() ?? gameObject.AddComponent<ArcaneActionModeController>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>() ?? gameObject.AddComponent<HandPullMovementController>();

            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>() ?? gameObject.AddComponent<BarrierController>();

            if (barrierPlayerDamageBridge == null)
                barrierPlayerDamageBridge = FindAnyObjectByType<BarrierPlayerDamageBridge>() ?? gameObject.AddComponent<BarrierPlayerDamageBridge>();

            if (bossPatternBridge == null)
                bossPatternBridge = FindAnyObjectByType<BossPatternCombatBridge>() ?? gameObject.AddComponent<BossPatternCombatBridge>();

            if (dodgeDetector == null)
                dodgeDetector = FindAnyObjectByType<CombatDodgeDetector>() ?? gameObject.AddComponent<CombatDodgeDetector>();

            if (IsCombatScene(SceneManager.GetActiveScene().name))
            {
                if (dodgePlayerDamageBridge == null)
                    dodgePlayerDamageBridge = FindAnyObjectByType<DodgePlayerDamageBridge>() ?? gameObject.AddComponent<DodgePlayerDamageBridge>();

                if (bossAttackTelegraph == null)
                    bossAttackTelegraph = FindAnyObjectByType<BossAttackTelegraphController>() ?? gameObject.AddComponent<BossAttackTelegraphController>();

                if (bossAttackAnimatorBridge == null)
                    bossAttackAnimatorBridge = FindAnyObjectByType<BossAttackAnimatorBridge>() ?? gameObject.AddComponent<BossAttackAnimatorBridge>();

                if (barrierVisualController == null)
                    barrierVisualController = FindAnyObjectByType<BarrierVisualController>() ?? gameObject.AddComponent<BarrierVisualController>();
            }

            if (golemTarget == null)
                golemTarget = ResolveOrCreateGolemTarget();

            if (golemTarget != null && golemStatusVfx == null)
                golemStatusVfx = golemTarget.GetComponent<BossElementStatusVfx>() ?? golemTarget.gameObject.AddComponent<BossElementStatusVfx>();

            if (gestureDiagnostics == null)
                gestureDiagnostics = FindAnyObjectByType<GestureConflictDiagnostics>() ?? gameObject.AddComponent<GestureConflictDiagnostics>();

            if (debugStatusPanel == null)
                debugStatusPanel = FindAnyObjectByType<ArcaneDebugStatusPanel>();

            ConfigureSpellAndMovementRuntime();
        }

        private void SubmitVoiceCommand(string phrase)
        {
            ResolveOrCreateReferences();
            voiceRecognizer?.SubmitVoiceCommand(phrase);
            DriverStatus = $"TestDriver: voice {phrase}";
        }

        private void SimulateCombo(ElementType leftElement, ElementType rightElement)
        {
            ResolveOrCreateReferences();

            var spellId = SpellHitData.ResolveComboSpell(leftElement, rightElement);
            if (spellId == SpellId.None)
            {
                DriverStatus = "TestDriver: invalid combo";
                return;
            }

            actionModeController?.SetCastMode(true, "Mode: Cast by test");
            var leftDeclared = combinationChecker != null &&
                               combinationChecker.SubmitElementDeclarationForTest(true, leftElement);
            var rightDeclared = combinationChecker != null &&
                                combinationChecker.SubmitElementDeclarationForTest(false, rightElement);
            var pushed = combinationChecker != null && combinationChecker.ReportCombinePushForTest();

            if (!pushed)
            {
                spellCaster?.Cast(spellId);
                if (applyComboDirectlyToGolem && golemTarget != null)
                    golemTarget.OnHit(SpellHitData.CreateComboHitData(spellId));
            }

            DriverStatus = $"TestDriver: combo {SpellHitData.GetDisplayName(spellId)} L{Mark(leftDeclared)} R{Mark(rightDeclared)} P{Mark(pushed)}";
        }

        private IEnumerator SpellCombatSmokeTest()
        {
            ResolveOrCreateReferences();
            RefillManaInternal();

            DriverStatus = "Smoke: barrier break setup";
            bossPatternBridge?.BeginGolemBarrier(golemBarrierDuration);
            yield return new WaitForSeconds(0.45f);

            SimulateCombo(ElementType.Ice, ElementType.Thunder);
            DriverStatus = "Smoke: Barrier Break";
            yield return new WaitForSeconds(1.2f);

            DriverStatus = "Smoke: charge counter setup";
            bossPatternBridge?.BeginChargeCounterWindow(chargeWindowDuration);
            yield return new WaitForSeconds(0.45f);

            SubmitVoiceCommand("thunder");
            DriverStatus = "Smoke: Thunder counter";
            yield return new WaitForSeconds(1.2f);

            SimulateCombo(ElementType.Thunder, ElementType.Fire);
            DriverStatus = "Smoke: Overload Flame";
            yield return new WaitForSeconds(1.2f);

            SimulateCombo(ElementType.Fire, ElementType.Ice);
            DriverStatus = "Smoke: Steam Burst done";
            enableAutomaticTestWindows = smokeTestPreviousAutomaticWindows;
            smokeTestRoutine = null;
        }

        private void RefillManaInternal()
        {
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (combatManager != null)
                combatManager.RefundMana(combatManager.MaxMana);
        }

        private static bool CanRunContextTest()
        {
            if (Application.isPlaying)
                return true;

            Debug.LogWarning("MagicSystemTestDriver test actions run only in Play Mode.");
            return false;
        }

        private void ConfigureSpellAndMovementRuntime()
        {
            NormalizeHandRigIfNeeded();

            if (!movementConfigured && handPullMovement != null)
            {
                handPullMovement.ConfigurePrototype(ArcanePlayerRigResolver.FindPlayerRigTransform(), Camera.main != null ? Camera.main.transform : null);
                movementConfigured = true;
            }

            if (!configureRuntimeSpellCaster || spellCaster == null || gestureDetector == null)
                return;

            if (spellCasterConfigured && cachedRightHand != null)
                return;

            if (spellSpawnRoot == null)
            {
                var existingRoot = GameObject.Find("SpellSpawnRoot") ?? GameObject.Find("Arcane Runtime SpellSpawnRoot");
                if (existingRoot == null)
                    existingRoot = new GameObject("Arcane Runtime SpellSpawnRoot");

                spellSpawnRoot = existingRoot.transform;
            }

            if (cachedLeftHand == null)
                cachedLeftHand = FindOvrHand(true);

            if (cachedRightHand == null)
                cachedRightHand = FindOvrHand(false);

            if (cachedLeftHand != null || cachedRightHand != null)
                gestureDetector.BindHands(cachedLeftHand, cachedRightHand);

            var rightHand = cachedRightHand;
            if (rightHand == null)
            {
                DriverStatus = "TestDriver: waiting for right OVRHand";
                return;
            }

            spellCaster.ConfigureGesturePrototype(
                gestureDetector,
                gestureRouter,
                rightHand,
                rightHand != null ? rightHand.transform : null,
                spellSpawnRoot);
            spellCasterConfigured = true;
        }

        private void NormalizeHandRigIfNeeded()
        {
            if (!normalizeOvrHandsAtRuntime)
                return;

            var shouldRecheckStartupRig =
                Time.timeSinceLevelLoad <= handRigNormalizeDuration &&
                Time.unscaledTime >= nextHandRigNormalizeTime;

            if (handRigNormalized && cachedLeftHand != null && cachedRightHand != null && !shouldRecheckStartupRig)
                return;

            if (shouldRecheckStartupRig)
                nextHandRigNormalizeTime = Time.unscaledTime + Mathf.Max(0.1f, handRigNormalizeInterval);

            if (!GestureSpellPrototypeBootstrap.NormalizeSceneOvrHands(out var leftHand, out var rightHand, out _))
                return;

            cachedLeftHand = leftHand != null ? leftHand : cachedLeftHand;
            cachedRightHand = rightHand != null ? rightHand : cachedRightHand;

            if (gestureDetector != null && (cachedLeftHand != null || cachedRightHand != null))
                gestureDetector.BindHands(cachedLeftHand, cachedRightHand);

            handRigNormalized = cachedLeftHand != null && cachedRightHand != null;
            if (handRigNormalized)
                DriverStatus = "TestDriver: OVR hands normalized";
        }

        private static OVRHand FindOvrHand(bool isLeft)
        {
            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            OVRHand bestHand = null;
            var bestScore = int.MinValue;

            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand == null || hand.GetHand() != expected)
                    continue;

                var score = hand.gameObject.activeInHierarchy ? 100 : 0;
                if (hand.enabled)
                    score += 20;
                if (hand.IsTracked)
                    score += 30;
                if (hand.GetComponentInParent<OVRCameraRig>() != null)
                    score += 40;

                if (score <= bestScore)
                    continue;

                bestHand = hand;
                bestScore = score;
            }

            return bestHand;
        }

        private GolemCombatTarget ResolveOrCreateGolemTarget()
        {
            var existing = FindAnyObjectByType<GolemCombatTarget>();
            if (existing != null)
                return existing;

            var candidate = GameObject.Find("Golem_Placeholder") ??
                            GameObject.Find("GolemPlaceholder") ??
                            GameObject.Find("Test Dummy Golem") ??
                            GameObject.Find("Golem");
            if (candidate != null)
                return candidate.GetComponent<GolemCombatTarget>() ?? candidate.AddComponent<GolemCombatTarget>();

            if (!createRuntimeDummyGolem)
                return null;

            return CreateRuntimeDummyGolem();
        }

        private GolemCombatTarget CreateRuntimeDummyGolem()
        {
            var targetRoot = new GameObject("Runtime Test GolemTarget");
            var camera = Camera.main;
            targetRoot.transform.position = camera != null
                ? camera.transform.position + camera.transform.forward * 4f + Vector3.down * 0.25f
                : new Vector3(0f, 1.1f, 4f);

            targetRoot.hideFlags = HideFlags.DontSave;
            return targetRoot.AddComponent<GolemCombatTarget>();
        }

        private void ResetTimers()
        {
            nextChargeTime = Time.time + firstWindowDelay;
            nextBarrierResponseTime = Time.time + firstWindowDelay + 2f;
            nextGolemBarrierTime = Time.time + firstWindowDelay + 4f;
            spellCasterConfigured = false;
        }

        private static bool IsCombatScene(string sceneName)
        {
            return sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

        private void HandleVoiceCommand(ElementType element)
        {
            if (!applyVoiceThunderToChargeWindow ||
                element != ElementType.Thunder ||
                golemTarget == null ||
                !golemTarget.IsChargeCounterWindowOpen)
            {
                return;
            }

            golemTarget.OnHit(new SpellHitData(
                SpellId.Single_Strike,
                ElementType.Thunder,
                StatusEffect.Stagger,
                1f,
                chargeWindowDuration,
                1f,
                0f));
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            if (!applyComboDirectlyToGolem || golemTarget == null)
                return;

            if (spellId != SpellId.Combo_FireIce &&
                spellId != SpellId.Combo_IceThunder &&
                spellId != SpellId.Combo_ThunderFire)
            {
                return;
            }

            golemTarget.OnHit(CreateComboHitData(spellId));
        }

        private static SpellHitData CreateComboHitData(SpellId spellId)
        {
            return SpellHitData.CreateComboHitData(spellId);
        }

        private static string Mark(bool value)
        {
            return value ? "O" : "X";
        }
    }
}
