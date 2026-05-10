using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// Instantiates and fires spell prefabs at hand position with head-direction aim assist. Reads spell data from SpellDatabase.
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        [SerializeField] private SpellDatabase spellDatabase;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private FeedbackManager feedbackManager;
        [SerializeField] private Transform leftHandSpawnPoint;
        [SerializeField] private Transform rightHandSpawnPoint;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform spellSpawnRoot;
        [SerializeField] private float fallbackProjectileLifetime = 5f;
        [SerializeField] private bool useDebugPrimitiveProjectiles = true;
        [SerializeField] private float debugProjectileScale = 1f;
        [SerializeField] private float voiceRefundMatchWindow = 3f;
        [SerializeField] private float voiceRefundCooldown = 0.75f;
        [SerializeField] private float voiceBoostDuration = 4f;
        [SerializeField] private float voiceBoostDamageMultiplier = 1.25f;
        [SerializeField] private float voiceBoostStatusMagnitudeMultiplier = 1.15f;

        [Header("Gesture Spell Prototype")]
        [SerializeField] private bool enableGesturePrototype;
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GestureEventRouter gestureRouter;
        [SerializeField] private OVRHand prototypeHand;
        [SerializeField] private Transform prototypeSpawnPoint;
        [SerializeField] private Transform prototypeTrackingSpaceRoot;
        [SerializeField] private Transform prototypeSpellSpawnRoot;
        [SerializeField] private float prototypeThrustThreshold = 0.65f;
        [SerializeField] private float prototypeSpellSpeed = 12f;
        [SerializeField] private float prototypeCooldown = 0.5f;
        [SerializeField] private float prototypeProjectileScale = 0.12f;
        [SerializeField] private float prototypeSpawnForwardOffset = 0.18f;
        [SerializeField] private float prototypeMinimumProjectileSpeed = 8f;
        [SerializeField] private float prototypeFallbackManaCost = 1f;
        [SerializeField] private float prototypeArmReadyDelay = 0.2f;
        [SerializeField] private bool allowPrototypeCastWithoutVoice;
        [SerializeField] private bool prototypeAimAtViewCenter = true;
        [SerializeField] private float prototypeAimDistance = 18f;
        [SerializeField] private bool showPrototypeArmedAura = true;
        [SerializeField] private float prototypeAuraBaseSize = 0.055f;
        [SerializeField] private float prototypeAuraBoostSize = 0.11f;
        [SerializeField] private float prototypeAuraPulseDuration = 0.85f;
        [SerializeField] private float prototypeAuraVoicePulseMultiplier = 4.5f;
        [SerializeField] private float prototypeVoiceFeedbackVolume = 0.9f;
        [SerializeField] private float prototypeArmFeedbackVolume = 0.45f;
        [SerializeField] private float prototypeCastFeedbackVolume = 0.85f;
        [SerializeField] private GameObject spellPrefabOpenPalm;
        [SerializeField] private GameObject spellPrefabFist;
        [SerializeField] private GameObject spellPrefabThumbsUp;

        private PoseType currentPrototypePose = PoseType.None;
        private PoseType prototypeArmedPose = PoseType.None;
        private ElementType prototypeArmedElement = ElementType.None;
        private Vector3 previousPrototypeWristPosition;
        private float prototypeArmedTime = -999f;
        private float prototypeCooldownTimer;
        private bool hasPreviousPrototypeWristPosition;
        private bool prototypeEventsSubscribed;
        private bool prototypeRouterEventsSubscribed;
        private bool voiceEventsSubscribed;
        private bool prototypeReadyForThrust = true;
        private float prototypeLastForwardSpeed;
        private float prototypeLastHandForwardSpeed;
        private float prototypeLastHeadForwardSpeed;
        private float prototypeLastAwayFromHeadSpeed;
        private string prototypeDebugStatus = "CAST: waiting";
        private bool prototypeVoiceBoosted;
        private ElementType prototypeVoiceBoostElement = ElementType.None;
        private float prototypeVoiceBoostTime = -999f;
        private GameObject prototypeAuraRoot;
        private ParticleSystem prototypeAuraParticles;
        private ParticleSystem prototypeAuraBurstParticles;
        private ParticleSystemRenderer prototypeAuraRenderer;
        private Light prototypeAuraLight;
        private Material prototypeAuraMaterial;
        private AudioSource prototypeAuraAudioSource;
        private float prototypeAuraPulseStartTime = -999f;
        private float prototypeAuraPulseEndTime = -999f;
        private float lastPrototypeArmSfxTime = -999f;
        private ElementType lastPrototypeArmSfxElement = ElementType.None;
        private SpellId lastComboReadySfxSpell = SpellId.None;
        private float lastComboReadySfxTime = -999f;
        private ElementType lastCastElement = ElementType.None;
        private float lastCastTime = -999f;
        private float lastVoiceRefundTime = -999f;
        private SpellId lastCastSpellId = SpellId.None;
        private string lastCastStatus = "Cast: idle";
        private string lastManaCostStatus = "Cost: idle";
        private string lastVoiceBoostStatus = "VoiceLink: waiting";
        private float lastManaCost;
        private float lastProcessedVoiceRecognitionTime = -999f;

        public SpellDatabase Database
        {
            get => spellDatabase;
            set => spellDatabase = value;
        }

        public string PrototypeDebugStatus => prototypeDebugStatus;
        public string LastCastStatus => lastCastStatus;
        public string LastManaCostStatus => lastManaCostStatus;
        public string LastVoiceBoostStatus => lastVoiceBoostStatus;
        public SpellId LastCastSpellId => lastCastSpellId;
        public ElementType LastCastElement => lastCastElement;
        public float LastManaCost => lastManaCost;
        public ElementType PrototypeArmedElement => prototypeArmedElement;
        public bool IsPrototypeArmed => prototypeArmedElement != ElementType.None && prototypeArmedPose != PoseType.None;
        public bool IsPrototypeVoiceBoosted => IsPrototypeVoiceBoostActive(prototypeArmedElement);
        public float PrototypeLastForwardSpeed => prototypeLastForwardSpeed;
        public string PrototypeArmStatus => IsPrototypeArmed
            ? $"Arm {prototypeArmedElement} Voice {(IsPrototypeVoiceBoosted ? "O" : "X")} Link {(voiceEventsSubscribed ? "O" : "X")} Push {prototypeLastForwardSpeed:0.00}"
            : lastCastStatus;
        private float EffectivePrototypeAuraBaseSize => Mathf.Max(0.06f, prototypeAuraBaseSize);
        private float EffectivePrototypeAuraBoostSize => Mathf.Max(0.12f, prototypeAuraBoostSize);
        private float EffectivePrototypeAuraPulseDuration => Mathf.Max(0.85f, prototypeAuraPulseDuration);
        private float EffectivePrototypeAuraVoicePulseMultiplier => Mathf.Max(4.5f, prototypeAuraVoicePulseMultiplier);
        private float EffectivePrototypeVoiceFeedbackVolume => Mathf.Clamp01(Mathf.Max(0.9f, prototypeVoiceFeedbackVolume));
        private float EffectivePrototypeArmFeedbackVolume => Mathf.Clamp01(Mathf.Max(0.35f, prototypeArmFeedbackVolume));
        private float EffectivePrototypeCastFeedbackVolume => Mathf.Clamp01(Mathf.Max(0.75f, prototypeCastFeedbackVolume));

        private void Awake()
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");

            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();

            if (feedbackManager == null)
                feedbackManager = FindAnyObjectByType<FeedbackManager>();

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            ResolvePrototypeReferences();
        }

        private void OnEnable()
        {
            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
                combinationChecker.OnComboReadyChanged += HandleComboReadyChanged;
            }

            EnsureVoiceSubscription();

            SubscribePrototypeEvents();
            SubscribePrototypeRouterEvents();
        }

        private void OnDisable()
        {
            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
                combinationChecker.OnComboReadyChanged -= HandleComboReadyChanged;
            }

            UnsubscribeVoiceEvents();

            UnsubscribePrototypeEvents();
            UnsubscribePrototypeRouterEvents();
        }

        private void Update()
        {
            EnsureVoiceSubscription();
            ProcessLatestVoiceRecognitionIfNeeded();
            UpdateGesturePrototypeCasting();
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            Cast(spellId);
        }

        private void HandleComboReadyChanged(SpellId spellId, bool ready)
        {
            if (!ready || !SpellHitData.IsComboSpellId(spellId))
                return;

            if (lastComboReadySfxSpell == spellId && Time.time - lastComboReadySfxTime < 0.35f)
                return;

            lastComboReadySfxSpell = spellId;
            lastComboReadySfxTime = Time.time;
            lastCastStatus = $"Combo ready: {SpellHitData.GetDisplayName(spellId)}";
            ArcaneSpellSfx.PlayCombo(
                EnsurePrototypeFeedbackAudioSource(),
                spellId,
                ArcaneSpellSfxCue.ComboReady,
                0.75f);
        }

        private void EnsureVoiceSubscription()
        {
            if (voiceRecognizer == null)
            {
                if (voiceEventsSubscribed)
                    voiceEventsSubscribed = false;

                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();
            }

            if (voiceRecognizer == null || voiceEventsSubscribed)
                return;

            voiceRecognizer.OnVoiceCommand += HandleVoiceCommand;
            voiceEventsSubscribed = true;
            lastVoiceBoostStatus = "VoiceLink: subscribed";
        }

        private void UnsubscribeVoiceEvents()
        {
            if (!voiceEventsSubscribed || voiceRecognizer == null)
                return;

            voiceRecognizer.OnVoiceCommand -= HandleVoiceCommand;
            voiceEventsSubscribed = false;
            lastVoiceBoostStatus = "VoiceLink: unsubscribed";
        }

        private void HandleVoiceCommand(ElementType spokenElement)
        {
            if (spokenElement == ElementType.None)
                return;

            MarkVoiceRecognitionProcessed(spokenElement);

            if (TryApplyPrototypeVoiceBoost(spokenElement))
                return;

            lastVoiceBoostStatus = IsPrototypeArmed
                ? $"VoiceLink: heard {spokenElement}, armed {prototypeArmedElement}"
                : $"VoiceLink: heard {spokenElement}, no arm";
            lastCastStatus = IsPrototypeArmed
                ? $"Voice mismatch: {spokenElement}/{prototypeArmedElement}"
                : $"Voice heard: {spokenElement}";

            var expectedElement = ResolveVoiceRefundElement();
            if (expectedElement == ElementType.None || expectedElement != spokenElement)
                return;

            if (Time.time - lastVoiceRefundTime < voiceRefundCooldown)
                return;

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            combatManager?.RefundVoiceMana();
            lastVoiceRefundTime = Time.time;
        }

        private void ProcessLatestVoiceRecognitionIfNeeded()
        {
            if (voiceRecognizer == null ||
                voiceRecognizer.LastRecognizedElement == ElementType.None ||
                voiceRecognizer.LastRecognizedTime <= lastProcessedVoiceRecognitionTime + 0.001f)
            {
                return;
            }

            HandleVoiceCommand(voiceRecognizer.LastRecognizedElement);
        }

        private void MarkVoiceRecognitionProcessed(ElementType spokenElement)
        {
            if (voiceRecognizer != null &&
                voiceRecognizer.LastRecognizedElement == spokenElement &&
                voiceRecognizer.LastRecognizedTime > lastProcessedVoiceRecognitionTime)
            {
                lastProcessedVoiceRecognitionTime = voiceRecognizer.LastRecognizedTime;
                return;
            }

            lastProcessedVoiceRecognitionTime = Time.time;
        }

        public void ConfigureGesturePrototype(GestureDetector detector, OVRHand hand, Transform spawnPoint, Transform spawnRoot)
        {
            ConfigureGesturePrototype(detector, FindAnyObjectByType<GestureEventRouter>(), hand, spawnPoint, spawnRoot);
        }

        public void ConfigureGesturePrototype(
            GestureDetector detector,
            GestureEventRouter router,
            OVRHand hand,
            Transform spawnPoint,
            Transform spawnRoot)
        {
            if (gestureDetector != detector)
                UnsubscribePrototypeEvents();

            if (gestureRouter != router)
                UnsubscribePrototypeRouterEvents();

            gestureDetector = detector;
            gestureRouter = router;
            prototypeHand = hand;
            prototypeSpawnPoint = spawnPoint;
            prototypeSpellSpawnRoot = spawnRoot;
            enableGesturePrototype = true;
            prototypeThrustThreshold = Mathf.Min(prototypeThrustThreshold, 0.65f);
            ResolvePrototypeReferences();
            SubscribePrototypeEvents();
            SubscribePrototypeRouterEvents();
        }

        private void SubscribePrototypeEvents()
        {
            if (prototypeEventsSubscribed)
                return;

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (gestureDetector == null)
                return;

            gestureDetector.OnRightPoseConfirmed += HandleRightPrototypePoseConfirmed;
            gestureDetector.OnRightPoseCleared += HandleRightPrototypePoseCleared;
            prototypeEventsSubscribed = true;
        }

        private void SubscribePrototypeRouterEvents()
        {
            if (prototypeRouterEventsSubscribed)
                return;

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (gestureRouter == null)
                return;

            gestureRouter.OnRightPoseConfirmed += HandleRightPrototypePoseConfirmed;
            gestureRouter.OnRightPoseCleared += HandleRightPrototypePoseCleared;
            prototypeRouterEventsSubscribed = true;
        }

        private void UnsubscribePrototypeEvents()
        {
            if (!prototypeEventsSubscribed || gestureDetector == null)
                return;

            gestureDetector.OnRightPoseConfirmed -= HandleRightPrototypePoseConfirmed;
            gestureDetector.OnRightPoseCleared -= HandleRightPrototypePoseCleared;
            prototypeEventsSubscribed = false;

            UnsubscribePrototypeRouterEvents();
        }

        private void UnsubscribePrototypeRouterEvents()
        {
            if (!prototypeRouterEventsSubscribed || gestureRouter == null)
                return;

            gestureRouter.OnRightPoseConfirmed -= HandleRightPrototypePoseConfirmed;
            gestureRouter.OnRightPoseCleared -= HandleRightPrototypePoseCleared;
            prototypeRouterEventsSubscribed = false;
        }

        private void HandleRightPrototypePoseConfirmed(PoseType pose)
        {
            if (currentPrototypePose != pose)
                prototypeReadyForThrust = false;

            currentPrototypePose = pose;
            hasPreviousPrototypeWristPosition = false;
        }

        private void HandleRightPrototypePoseCleared()
        {
            currentPrototypePose = PoseType.None;
            ClearPrototypeArm();
            hasPreviousPrototypeWristPosition = false;
            prototypeReadyForThrust = true;
        }

        private void UpdateGesturePrototypeCasting()
        {
            if (!enableGesturePrototype)
            {
                prototypeDebugStatus = "CAST: disabled";
                return;
            }

            prototypeCooldownTimer -= Time.deltaTime;
            ResolvePrototypeReferences();
            SyncPrototypePoseFromDetector();
            UpdatePrototypeArmingState(currentPrototypePose);

            var spawnPoint = ResolvePrototypeSpawnPoint();
            UpdatePrototypeAura(spawnPoint);
            if (spawnPoint == null || Time.deltaTime <= 0f)
            {
                prototypeDebugStatus = "CAST: no spawn point";
                return;
            }

            var castOrigin = ResolvePrototypeCastOrigin(spawnPoint);
            var thrustPosition = ResolvePrototypeThrustTrackingPosition(spawnPoint, castOrigin);
            if (!hasPreviousPrototypeWristPosition)
            {
                previousPrototypeWristPosition = thrustPosition;
                hasPreviousPrototypeWristPosition = true;
                prototypeDebugStatus = $"CAST {PrototypeArmStatus} speed:0.00/{prototypeThrustThreshold:0.00} init";
                return;
            }

            var wristVelocity = (thrustPosition - previousPrototypeWristPosition) / Time.deltaTime;
            previousPrototypeWristPosition = thrustPosition;

            var fireDirection = ResolvePrototypeFireDirection(spawnPoint, castOrigin);
            var forwardSpeed = ResolvePrototypeForwardSpeed(spawnPoint, thrustPosition, wristVelocity);
            prototypeLastForwardSpeed = forwardSpeed;
            prototypeDebugStatus =
                $"CAST {PrototypeArmStatus} speed:{forwardSpeed:0.00}/{prototypeThrustThreshold:0.00} cd:{Mathf.Max(0f, prototypeCooldownTimer):0.00}";

            if (forwardSpeed < prototypeThrustThreshold * 0.25f)
                prototypeReadyForThrust = true;

            var armReady = IsPrototypeArmed &&
                           currentPrototypePose == prototypeArmedPose &&
                           Time.time - prototypeArmedTime >= prototypeArmReadyDelay;

            if (forwardSpeed <= prototypeThrustThreshold ||
                !armReady ||
                prototypeCooldownTimer > 0f ||
                !prototypeReadyForThrust)
            {
                return;
            }

            if (!allowPrototypeCastWithoutVoice && !IsPrototypeVoiceBoostActive(prototypeArmedElement))
            {
                prototypeReadyForThrust = false;
                lastCastStatus = $"Blocked: say {prototypeArmedElement}";
                lastManaCostStatus = "Cost: waiting voice";
                prototypeDebugStatus = $"CAST blocked:{prototypeArmedElement} voice required";
                return;
            }

            FirePrototypeSpell(prototypeArmedPose, castOrigin, fireDirection);
            prototypeCooldownTimer = prototypeCooldown;
            prototypeReadyForThrust = false;
        }

        private void SyncPrototypePoseFromDetector()
        {
            if (gestureRouter != null && gestureRouter.HasReceivedGestureEvent)
            {
                if (currentPrototypePose != gestureRouter.CurrentRightPose)
                {
                    currentPrototypePose = gestureRouter.CurrentRightPose;
                    prototypeReadyForThrust = false;
                }
                return;
            }

            if (gestureDetector == null)
                return;

            var detectorPose = gestureDetector.CurrentRightPrototypePose;
            if (detectorPose == PoseType.None)
                detectorPose = ConvertPoseIdToPrototypePose(gestureDetector.CurrentRightPose);

            if (currentPrototypePose != detectorPose)
            {
                currentPrototypePose = detectorPose;
                prototypeReadyForThrust = false;
            }
        }

        private void UpdatePrototypeArmingState(PoseType pose)
        {
            var element = ResolveDefaultPrototypeElement(pose);
            if (pose == PoseType.None || element == ElementType.None)
            {
                ClearPrototypeArm();
                return;
            }

            if (prototypeArmedPose == pose && prototypeArmedElement == element)
                return;

            prototypeArmedPose = pose;
            prototypeArmedElement = element;
            prototypeArmedTime = Time.time;
            prototypeVoiceBoosted = false;
            prototypeVoiceBoostElement = ElementType.None;
            prototypeVoiceBoostTime = -999f;
            prototypeReadyForThrust = false;
            lastCastStatus = $"Armed: {element}";
            PlayPrototypeElementArmFeedback(element);
        }

        private void ClearPrototypeArm()
        {
            prototypeArmedPose = PoseType.None;
            prototypeArmedElement = ElementType.None;
            prototypeArmedTime = -999f;
            prototypeVoiceBoosted = false;
            prototypeVoiceBoostElement = ElementType.None;
            prototypeVoiceBoostTime = -999f;
            StopPrototypeAura();
        }

        private bool TryApplyPrototypeVoiceBoost(ElementType spokenElement)
        {
            if (!IsPrototypeArmed || spokenElement != prototypeArmedElement)
                return false;

            prototypeVoiceBoosted = true;
            prototypeVoiceBoostElement = spokenElement;
            prototypeVoiceBoostTime = Time.time;
            lastCastStatus = $"Armed: {prototypeArmedElement} voice+";
            lastVoiceBoostStatus = $"VoiceLink: boosted {spokenElement}";
            prototypeDebugStatus = $"CAST armed:{prototypeArmedElement} voice boost";
            PlayPrototypeVoiceBoostFeedback(spokenElement);
            return true;
        }

        private bool IsPrototypeVoiceBoostActive(ElementType element)
        {
            return element != ElementType.None &&
                   prototypeVoiceBoosted &&
                   prototypeVoiceBoostElement == element &&
                   Time.time - prototypeVoiceBoostTime <= voiceBoostDuration;
        }

        private static PoseType ConvertPoseIdToPrototypePose(PoseId pose)
        {
            return pose switch
            {
                PoseId.OpenPalm => PoseType.OpenPalm,
                PoseId.Fist => PoseType.Fist,
                PoseId.FistPush => PoseType.Fist,
                _ => PoseType.None
            };
        }

        private void ResolvePrototypeReferences()
        {
            if (prototypeSpellSpawnRoot == null)
                prototypeSpellSpawnRoot = spellSpawnRoot;

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (prototypeSpawnPoint == null)
                prototypeSpawnPoint = rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;

            if (prototypeHand != null)
                return;

            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand.GetHand() != OVRPlugin.Hand.HandRight)
                    continue;

                prototypeHand = hand;
                if (prototypeSpawnPoint == null)
                    prototypeSpawnPoint = hand.transform;
                return;
            }
        }

        private Transform ResolvePrototypeSpawnPoint()
        {
            if (prototypeSpawnPoint != null)
                return prototypeSpawnPoint;

            if (prototypeHand != null)
                return prototypeHand.transform;

            return rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;
        }

        private void UpdatePrototypeAura(Transform spawnPoint)
        {
            if (!showPrototypeArmedAura || !IsPrototypeArmed || spawnPoint == null)
            {
                StopPrototypeAura();
                return;
            }

            EnsurePrototypeAura(spawnPoint);
            if (prototypeAuraRoot == null)
                return;

            var boosted = IsPrototypeVoiceBoostActive(prototypeArmedElement);
            var pulse = GetPrototypeAuraPulse01();
            var color = boosted
                ? Color.Lerp(GetElementColor(prototypeArmedElement), Color.white, 0.25f + pulse * 0.25f)
                : GetElementColor(prototypeArmedElement);

            var auraTransform = prototypeAuraRoot.transform;
            if (auraTransform.parent != spawnPoint)
                auraTransform.SetParent(spawnPoint, false);

            auraTransform.localPosition = Vector3.zero;
            auraTransform.localRotation = Quaternion.identity;
            auraTransform.localScale = Vector3.one * Mathf.Lerp(1f, 2.6f, pulse);

            if (!prototypeAuraRoot.activeSelf)
                prototypeAuraRoot.SetActive(true);

            ApplyAuraColor(color, boosted, pulse);
            if (prototypeAuraParticles != null && !prototypeAuraParticles.isPlaying)
                prototypeAuraParticles.Play(true);
        }

        private void EnsurePrototypeAura(Transform spawnPoint)
        {
            if (prototypeAuraRoot != null)
                return;

            prototypeAuraRoot = new GameObject("ArcaneArmedAura_Right");
            prototypeAuraRoot.name = "ArcaneArmedAura_Right";
            prototypeAuraRoot.hideFlags = HideFlags.DontSave;
            prototypeAuraRoot.transform.SetParent(spawnPoint, false);
            prototypeAuraRoot.transform.localPosition = Vector3.zero;
            prototypeAuraRoot.transform.localRotation = Quaternion.identity;
            prototypeAuraRoot.transform.localScale = Vector3.one;

            prototypeAuraParticles = prototypeAuraRoot.AddComponent<ParticleSystem>();
            ConfigurePrototypeAuraParticles(prototypeAuraParticles);

            prototypeAuraRenderer = prototypeAuraRoot.GetComponent<ParticleSystemRenderer>();
            prototypeAuraMaterial = CreateAuraMaterial(new Color(1f, 1f, 1f, 0.7f));
            if (prototypeAuraRenderer != null && prototypeAuraMaterial != null)
            {
                prototypeAuraRenderer.material = prototypeAuraMaterial;
                prototypeAuraRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                prototypeAuraRenderer.sortingFudge = 2f;
                prototypeAuraRenderer.maxParticleSize = 0.16f;
            }
            else if (prototypeAuraRenderer != null)
            {
                prototypeAuraRenderer.enabled = false;
            }

            prototypeAuraBurstParticles = CreatePrototypeAuraBurstParticles(prototypeAuraRoot.transform, prototypeAuraMaterial);

            prototypeAuraLight = prototypeAuraRoot.AddComponent<Light>();
            prototypeAuraLight.type = LightType.Point;
            prototypeAuraLight.range = 0.45f;
            prototypeAuraLight.intensity = 0.55f;

            prototypeAuraAudioSource = prototypeAuraRoot.AddComponent<AudioSource>();
            ConfigurePrototypeFeedbackAudioSource(prototypeAuraAudioSource);

            prototypeAuraRoot.SetActive(false);
        }

        private AudioSource EnsurePrototypeFeedbackAudioSource()
        {
            var spawnPoint = ResolvePrototypeSpawnPoint();
            if (showPrototypeArmedAura && spawnPoint != null)
            {
                EnsurePrototypeAura(spawnPoint);
                if (prototypeAuraRoot != null && !prototypeAuraRoot.activeSelf)
                    prototypeAuraRoot.SetActive(true);
            }

            if (prototypeAuraAudioSource == null)
            {
                prototypeAuraAudioSource = GetComponent<AudioSource>();
                if (prototypeAuraAudioSource == null)
                    prototypeAuraAudioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigurePrototypeFeedbackAudioSource(prototypeAuraAudioSource);
            return prototypeAuraAudioSource;
        }

        private static void ConfigurePrototypeFeedbackAudioSource(AudioSource audioSource)
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 0.15f;
            audioSource.maxDistance = 8f;
            audioSource.dopplerLevel = 0f;
            audioSource.ignoreListenerPause = true;
            audioSource.mute = false;
            audioSource.volume = 1f;
        }

        private ParticleSystem CreatePrototypeAuraBurstParticles(Transform parent, Material material)
        {
            var burstObject = new GameObject("ArcaneVoiceConfirmBurst")
            {
                hideFlags = HideFlags.DontSave
            };
            burstObject.transform.SetParent(parent, false);
            burstObject.transform.localPosition = Vector3.zero;
            burstObject.transform.localRotation = Quaternion.identity;
            burstObject.transform.localScale = Vector3.one;

            var particles = burstObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new Color(1f, 1f, 1f, 0.95f);
            main.maxParticles = 180;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.035f;
            shape.radiusThickness = 0.2f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 2.5f;
            noise.scrollSpeed = 0.6f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.2f),
                    new Keyframe(0.12f, 1.35f),
                    new Keyframe(1f, 0f)));

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0.8f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            var renderer = burstObject.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && material != null)
            {
                renderer.material = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = 3f;
                renderer.maxParticleSize = 0.35f;
            }
            else if (renderer != null)
            {
                renderer.enabled = false;
            }

            return particles;
        }

        private void ConfigurePrototypeAuraParticles(ParticleSystem particles)
        {
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 0.45f;
            main.startSpeed = 0.035f;
            main.startSize = EffectivePrototypeAuraBaseSize;
            main.startColor = new Color(1f, 1f, 1f, 0.65f);
            main.maxParticles = 240;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 48f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.075f;
            shape.radiusThickness = 0.55f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.035f;
            noise.frequency = 1.7f;
            noise.scrollSpeed = 0.25f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.22f, 1f),
                    new Keyframe(1f, 0.15f)));

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;
        }

        private void StopPrototypeAura()
        {
            if (prototypeAuraRoot == null)
                return;

            if (prototypeAuraParticles != null)
                prototypeAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (prototypeAuraBurstParticles != null)
                prototypeAuraBurstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            prototypeAuraRoot.SetActive(false);
        }

        private void ApplyAuraColor(Color color, bool boosted, float pulse)
        {
            var auraColor = Color.Lerp(color, Color.white, Mathf.Clamp01(pulse * 0.4f));
            auraColor.a = Mathf.Clamp01((boosted ? 0.78f : 0.52f) + pulse * 0.16f);

            var boost01 = boosted ? 1f : 0f;
            var startSize = Mathf.Lerp(EffectivePrototypeAuraBaseSize, EffectivePrototypeAuraBoostSize, boost01);
            startSize *= Mathf.Lerp(1f, EffectivePrototypeAuraVoicePulseMultiplier, pulse);

            if (prototypeAuraParticles != null)
            {
                var main = prototypeAuraParticles.main;
                main.startColor = auraColor;
                main.startSize = Mathf.Max(0.018f, startSize);
                main.startSpeed = Mathf.Lerp(0.045f, 0.095f, boost01) + pulse * 0.11f;

                var emission = prototypeAuraParticles.emission;
                emission.rateOverTime = Mathf.Lerp(62f, 130f, boost01) + pulse * 360f;

                var shape = prototypeAuraParticles.shape;
                shape.radius = Mathf.Lerp(0.09f, 0.18f, boost01) + pulse * 0.18f;
            }

            if (prototypeAuraBurstParticles != null)
            {
                var main = prototypeAuraBurstParticles.main;
                main.startColor = auraColor;
            }

            if (prototypeAuraMaterial != null)
            {
                if (prototypeAuraMaterial.HasProperty("_BaseColor"))
                    prototypeAuraMaterial.SetColor("_BaseColor", auraColor);
                if (prototypeAuraMaterial.HasProperty("_Color"))
                    prototypeAuraMaterial.SetColor("_Color", auraColor);
            }

            if (prototypeAuraRenderer != null)
                prototypeAuraRenderer.enabled = prototypeAuraMaterial != null;

            if (prototypeAuraLight != null)
            {
                prototypeAuraLight.color = color;
                prototypeAuraLight.range = Mathf.Lerp(0.55f, 0.95f, boost01) + pulse * 0.55f;
                prototypeAuraLight.intensity = Mathf.Lerp(0.8f, 1.75f, boost01) + pulse * 3.6f;
            }
        }

        private float GetPrototypeAuraPulse01()
        {
            if (Time.time >= prototypeAuraPulseEndTime)
                return 0f;

            var duration = Mathf.Max(0.01f, prototypeAuraPulseEndTime - prototypeAuraPulseStartTime);
            var normalized = Mathf.Clamp01((Time.time - prototypeAuraPulseStartTime) / duration);
            return 1f - Mathf.SmoothStep(0f, 1f, normalized);
        }

        private void PlayPrototypeElementArmFeedback(ElementType element)
        {
            if (element == ElementType.None)
                return;

            if (lastPrototypeArmSfxElement == element && Time.time - lastPrototypeArmSfxTime < 0.28f)
                return;

            lastPrototypeArmSfxElement = element;
            lastPrototypeArmSfxTime = Time.time;

            var spawnPoint = ResolvePrototypeSpawnPoint();
            if (showPrototypeArmedAura && spawnPoint != null)
            {
                EnsurePrototypeAura(spawnPoint);
                if (prototypeAuraRoot != null && !prototypeAuraRoot.activeSelf)
                    prototypeAuraRoot.SetActive(true);

                prototypeAuraPulseStartTime = Time.time;
                prototypeAuraPulseEndTime = Time.time + 0.18f;
                ApplyAuraColor(GetElementColor(element), false, 0.35f);
                prototypeAuraParticles?.Emit(18);
            }

            ArcaneSpellSfx.Play(
                EnsurePrototypeFeedbackAudioSource(),
                element,
                ArcaneSpellSfxCue.ElementArm,
                EffectivePrototypeArmFeedbackVolume);
        }

        private void PlayPrototypeVoiceBoostFeedback(ElementType element)
        {
            prototypeAuraPulseStartTime = Time.time;
            prototypeAuraPulseEndTime = Time.time + EffectivePrototypeAuraPulseDuration;

            var spawnPoint = ResolvePrototypeSpawnPoint();
            if (showPrototypeArmedAura && spawnPoint != null)
            {
                EnsurePrototypeAura(spawnPoint);
                if (prototypeAuraRoot != null && !prototypeAuraRoot.activeSelf)
                    prototypeAuraRoot.SetActive(true);

                var color = Color.Lerp(GetElementColor(element), Color.white, 0.25f);
                prototypeAuraRoot.transform.localScale = Vector3.one * 2.6f;
                ApplyAuraColor(color, true, 1f);
                prototypeAuraParticles?.Emit(90);
                prototypeAuraBurstParticles?.Emit(110);
            }

            ArcaneSpellSfx.Play(
                EnsurePrototypeFeedbackAudioSource(),
                element,
                ArcaneSpellSfxCue.VoiceConfirm,
                EffectivePrototypeVoiceFeedbackVolume);
        }

        private static Material CreateAuraMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "ArcaneRuntimeAuraMaterial",
                hideFlags = HideFlags.DontSave,
                renderQueue = 3000
            };

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            var particleTexture = CreateAuraParticleTexture();
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", particleTexture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", particleTexture);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            return material;
        }

        private static Texture2D CreateAuraParticleTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ArcaneRuntimeAuraParticleTexture",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = ((x + 0.5f) / size) * 2f - 1f;
                    var v = ((y + 0.5f) / size) * 2f - 1f;
                    var distance = Mathf.Sqrt(u * u + v * v);
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private Vector3 ResolvePrototypeCastOrigin(Transform fallback)
        {
            if (TryGetPrototypeBonePosition(
                    out var palmPosition,
                    OVRSkeleton.BoneId.XRHand_Palm,
                    OVRSkeleton.BoneId.XRHand_Wrist,
                    OVRSkeleton.BoneId.Hand_WristRoot) &&
                IsUsablePrototypeHandPosition(palmPosition))
            {
                return palmPosition;
            }

            if (TryGetPrototypeBonePosition(
                    out var indexTipPosition,
                    OVRSkeleton.BoneId.XRHand_IndexTip,
                    OVRSkeleton.BoneId.Hand_IndexTip) &&
                IsUsablePrototypeHandPosition(indexTipPosition))
            {
                return indexTipPosition;
            }

            if (fallback != null)
                return fallback.position;

            if (TryGetPrototypePointerPose(out var pointerPosition) &&
                IsUsablePrototypeHandPosition(pointerPosition))
            {
                return pointerPosition;
            }

            return transform.position;
        }

        private Vector3 ResolvePrototypeThrustPosition(Transform fallback, Vector3 castOrigin)
        {
            if (TryGetPrototypeBonePosition(
                    out var palmPosition,
                    OVRSkeleton.BoneId.XRHand_Palm,
                    OVRSkeleton.BoneId.XRHand_Wrist,
                    OVRSkeleton.BoneId.Hand_WristRoot) &&
                IsUsablePrototypeHandPosition(palmPosition))
            {
                return palmPosition;
            }

            if (fallback != null)
                return fallback.position;

            if (TryGetPrototypePointerPose(out var pointerPosition) &&
                IsUsablePrototypeHandPosition(pointerPosition))
            {
                return pointerPosition;
            }

            return castOrigin;
        }

        private Vector3 ResolvePrototypeThrustTrackingPosition(Transform fallback, Vector3 castOrigin)
        {
            var worldPosition = ResolvePrototypeThrustPosition(fallback, castOrigin);
            var trackingSpace = ResolvePrototypeTrackingSpaceRoot();
            return trackingSpace != null ? trackingSpace.InverseTransformPoint(worldPosition) : worldPosition;
        }

        private bool TryGetPrototypePointerPose(out Vector3 position)
        {
            position = Vector3.zero;
            if (prototypeHand == null || !prototypeHand.IsPointerPoseValid || prototypeHand.PointerPose == null)
                return false;

            position = prototypeHand.PointerPose.position;
            return true;
        }

        private bool TryGetPrototypeBonePosition(out Vector3 position, params OVRSkeleton.BoneId[] boneIds)
        {
            position = Vector3.zero;
            if (prototypeHand == null)
                return false;

            foreach (var skeleton in prototypeHand.GetComponentsInChildren<OVRSkeleton>(true))
            {
                if (TryGetSkeletonBonePosition(skeleton, out position, boneIds))
                    return true;
            }

            var expectedHand = prototypeHand.GetHand();
            foreach (var skeleton in FindObjectsByType<OVRSkeleton>(FindObjectsInactive.Include))
            {
                if (!MatchesPrototypeHand(skeleton, expectedHand))
                    continue;

                if (TryGetSkeletonBonePosition(skeleton, out position, boneIds))
                    return true;
            }

            return false;
        }

        private static bool TryGetSkeletonBonePosition(OVRSkeleton skeleton, out Vector3 position, params OVRSkeleton.BoneId[] boneIds)
        {
            position = Vector3.zero;
            if (skeleton == null || skeleton.Bones == null)
                return false;

            foreach (var requestedId in boneIds)
            {
                foreach (var bone in skeleton.Bones)
                {
                    if (bone == null || bone.Transform == null || bone.Id != requestedId)
                        continue;

                    position = bone.Transform.position;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesPrototypeHand(OVRSkeleton skeleton, OVRPlugin.Hand expectedHand)
        {
            var skeletonType = skeleton.GetSkeletonType();
            return expectedHand switch
            {
                OVRPlugin.Hand.HandLeft => skeletonType == OVRSkeleton.SkeletonType.HandLeft ||
                                           skeletonType == OVRSkeleton.SkeletonType.XRHandLeft,
                OVRPlugin.Hand.HandRight => skeletonType == OVRSkeleton.SkeletonType.HandRight ||
                                            skeletonType == OVRSkeleton.SkeletonType.XRHandRight,
                _ => false
            };
        }

        private bool IsUsablePrototypeHandPosition(Vector3 position)
        {
            var head = ResolvePrototypeHeadTransform();
            return head == null || Vector3.Distance(position, head.position) > 0.12f;
        }

        private Vector3 ResolvePrototypeFireDirection(Transform spawnPoint, Vector3 wristPosition)
        {
            var head = ResolvePrototypeHeadTransform();
            var headForward = head != null ? head.forward : transform.forward;
            var handForward = spawnPoint.forward;
            var awayFromHead = head != null ? wristPosition - head.position : Vector3.zero;

            if (prototypeAimAtViewCenter && head != null)
            {
                var aimPoint = ResolvePrototypeViewCenterAimPoint(head);
                var viewCenterDirection = aimPoint - wristPosition;
                if (viewCenterDirection.sqrMagnitude > 0.001f)
                    return viewCenterDirection.normalized;
            }

            if (awayFromHead.sqrMagnitude > 0.001f)
                awayFromHead.Normalize();

            var direction = handForward;
            if (direction.sqrMagnitude < 0.001f || Vector3.Dot(direction.normalized, headForward.normalized) < 0.15f)
                direction = headForward;

            if (direction.sqrMagnitude < 0.001f)
                direction = awayFromHead;

            return direction.sqrMagnitude < 0.001f ? transform.forward : direction.normalized;
        }

        private Vector3 ResolvePrototypeViewCenterAimPoint(Transform head)
        {
            var headForward = head.forward.sqrMagnitude > 0.001f ? head.forward.normalized : transform.forward.normalized;
            return head.position + headForward * Mathf.Max(1f, prototypeAimDistance);
        }

        private float ResolvePrototypeForwardSpeed(Transform spawnPoint, Vector3 wristTrackingPosition, Vector3 wristTrackingVelocity)
        {
            var head = ResolvePrototypeHeadTransform();
            var wristWorldPosition = ToPrototypeWorldPoint(wristTrackingPosition);
            var aimForward = ToPrototypeTrackingVector(ResolvePrototypeFireDirection(spawnPoint, wristWorldPosition));
            var headForward = head != null && head.forward.sqrMagnitude > 0.001f
                ? ToPrototypeTrackingVector(head.forward)
                : ToPrototypeTrackingVector(transform.forward);
            var awayFromHead = Vector3.zero;

            if (head != null)
            {
                var headTrackingPosition = ToPrototypeTrackingPoint(head.position);
                awayFromHead = wristTrackingPosition - headTrackingPosition;
                if (awayFromHead.sqrMagnitude > 0.001f)
                    awayFromHead.Normalize();
            }

            prototypeLastHandForwardSpeed = aimForward == Vector3.zero ? 0f : Vector3.Dot(wristTrackingVelocity, aimForward);
            prototypeLastHeadForwardSpeed = headForward == Vector3.zero ? 0f : Vector3.Dot(wristTrackingVelocity, headForward);
            prototypeLastAwayFromHeadSpeed = awayFromHead == Vector3.zero ? 0f : Vector3.Dot(wristTrackingVelocity, awayFromHead);

            return Mathf.Max(prototypeLastHandForwardSpeed, prototypeLastHeadForwardSpeed, prototypeLastAwayFromHeadSpeed);
        }

        private Vector3 ToPrototypeTrackingPoint(Vector3 worldPosition)
        {
            var trackingSpace = ResolvePrototypeTrackingSpaceRoot();
            return trackingSpace != null ? trackingSpace.InverseTransformPoint(worldPosition) : worldPosition;
        }

        private Vector3 ToPrototypeWorldPoint(Vector3 trackingPosition)
        {
            var trackingSpace = ResolvePrototypeTrackingSpaceRoot();
            return trackingSpace != null ? trackingSpace.TransformPoint(trackingPosition) : trackingPosition;
        }

        private Vector3 ToPrototypeTrackingVector(Vector3 worldVector)
        {
            var trackingSpace = ResolvePrototypeTrackingSpaceRoot();
            var trackingVector = trackingSpace != null ? trackingSpace.InverseTransformDirection(worldVector) : worldVector;
            return trackingVector.sqrMagnitude > 0.001f ? trackingVector.normalized : Vector3.zero;
        }

        private Transform ResolvePrototypeTrackingSpaceRoot()
        {
            if (prototypeTrackingSpaceRoot != null)
                return prototypeTrackingSpaceRoot;

            if (prototypeHand != null)
            {
                var rig = prototypeHand.GetComponentInParent<OVRCameraRig>();
                if (rig != null)
                {
                    rig.EnsureGameObjectIntegrity();
                    if (rig.trackingSpace != null)
                    {
                        prototypeTrackingSpaceRoot = rig.trackingSpace;
                        return prototypeTrackingSpaceRoot;
                    }
                }

                prototypeTrackingSpaceRoot = FindPrototypeParentNamed(prototypeHand.transform, "TrackingSpace");
                if (prototypeTrackingSpaceRoot != null)
                    return prototypeTrackingSpaceRoot;
            }

            if (prototypeSpawnPoint != null)
            {
                prototypeTrackingSpaceRoot = FindPrototypeParentNamed(prototypeSpawnPoint, "TrackingSpace");
                if (prototypeTrackingSpaceRoot != null)
                    return prototypeTrackingSpaceRoot;
            }

            if (Camera.main != null)
            {
                var rig = Camera.main.GetComponentInParent<OVRCameraRig>();
                if (rig != null)
                {
                    rig.EnsureGameObjectIntegrity();
                    if (rig.trackingSpace != null)
                    {
                        prototypeTrackingSpaceRoot = rig.trackingSpace;
                        return prototypeTrackingSpaceRoot;
                    }
                }
            }

            return null;
        }

        private static Transform FindPrototypeParentNamed(Transform start, string name)
        {
            var current = start;
            while (current != null)
            {
                if (current.name == name)
                    return current;

                current = current.parent;
            }

            return null;
        }

        private Transform ResolvePrototypeHeadTransform()
        {
            if (headTransform != null)
                return headTransform;

            if (Camera.main != null)
            {
                headTransform = Camera.main.transform;
                return headTransform;
            }

            return null;
        }

        private void FirePrototypeSpell(PoseType pose, Vector3 originPosition, Vector3 direction)
        {
            direction = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : (ResolvePrototypeHeadTransform() != null ? ResolvePrototypeHeadTransform().forward : transform.forward).normalized;

            var data = ResolvePrototypeSpellData(pose);
            var element = data != null ? data.element : ResolveDefaultPrototypeElement(pose);
            var statusEffect = data != null ? data.statusEffect : ResolveDefaultPrototypeStatusEffect(pose);
            var damage = data != null ? data.damage : ResolveDefaultPrototypeDamage(pose);
            var statusDuration = data != null ? data.statusDuration : ResolveDefaultPrototypeStatusDuration(pose);
            var statusMagnitude = data != null ? data.statusMagnitude : ResolveDefaultPrototypeStatusMagnitude(statusEffect);
            var statusTickInterval = data != null ? data.statusTickInterval : ResolveDefaultPrototypeStatusTickInterval(statusEffect);
            var spellId = ResolvePrototypeSpellId(pose);
            var manaCost = ResolvePrototypeManaCost(spellId);
            var voiceBoosted = IsPrototypeVoiceBoostActive(element);
            if (voiceBoosted)
            {
                damage *= Mathf.Max(1f, voiceBoostDamageMultiplier);
                statusMagnitude *= Mathf.Max(1f, voiceBoostStatusMagnitudeMultiplier);
            }

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (combatManager == null)
            {
                RecordCastBlocked(spellId, element, manaCost, "Cost: no CombatManager");
                prototypeDebugStatus = $"CAST blocked:{pose} no CombatManager";
                return;
            }

            if (!combatManager.TryConsumeMana(manaCost))
            {
                RecordCastBlocked(spellId, element, manaCost, $"Cost: NoMana {combatManager.CurrentMana:0.#}/{manaCost:0.#}");
                prototypeDebugStatus = $"CAST blocked:{pose} no mana {combatManager.CurrentMana:0.#}/{manaCost:0.#}";
                return;
            }

            lastManaCost = manaCost;
            var manaAfterConsume = combatManager.CurrentMana;
            lastManaCostStatus = $"Cost: Paid {manaCost:0.#} -> {manaAfterConsume:0.#}";
            if (voiceBoosted && Time.time - lastVoiceRefundTime >= voiceRefundCooldown)
            {
                combatManager.RefundVoiceMana();
                lastVoiceRefundTime = Time.time;
                lastManaCostStatus = $"Cost: Paid {manaCost:0.#} -> {combatManager.CurrentMana:0.#} voice+";
            }

            var projectileSpeed = Mathf.Max(
                prototypeMinimumProjectileSpeed,
                data != null ? data.projectileSpeed : prototypeSpellSpeed);
            var prefab = data != null && data.prefab != null ? data.prefab : ResolvePrototypePrefab(pose);
            var spawnPosition = originPosition + direction * prototypeSpawnForwardOffset;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var projectileObject = prefab != null
                ? Instantiate(prefab, spawnPosition, rotation)
                : CreatePrototypeProjectileObject(pose, element, spawnPosition, rotation);

            if (prototypeSpellSpawnRoot != null && prototypeSpellSpawnRoot.parent == null)
                projectileObject.transform.SetParent(prototypeSpellSpawnRoot, true);

            var projectile = projectileObject.GetComponent<SpellProjectile>();
            if (projectile == null)
                projectile = projectileObject.AddComponent<SpellProjectile>();

            EnsurePrototypeProjectilePhysics(projectileObject);
            projectile.InitializePrototype(
                pose,
                projectileSpeed,
                direction,
                element,
                statusEffect,
                damage,
                statusDuration,
                statusMagnitude,
                statusTickInterval);
            projectile.spellId = spellId;
            RememberCastElement(element);
            lastCastSpellId = spellId;
            lastCastStatus = voiceBoosted
                ? $"Cast: {element} {spellId} voice+"
                : $"Cast: {element} {spellId}";
            prototypeDebugStatus = voiceBoosted
                ? $"CAST fired:{pose} {element}/{statusEffect} voice+ speed:{prototypeLastForwardSpeed:0.00}"
                : $"CAST fired:{pose} {element}/{statusEffect} speed:{prototypeLastForwardSpeed:0.00}";
            PlayPrototypeSpellCastFeedback(element);
            prototypeVoiceBoosted = false;
            prototypeVoiceBoostElement = ElementType.None;
            prototypeVoiceBoostTime = -999f;
            Debug.Log($"[SPELL CAST] {pose} | {element} | {statusEffect} | DMG:{damage} | Dir:{direction}");
        }

        private void PlayPrototypeSpellCastFeedback(ElementType element)
        {
            if (element == ElementType.None)
                return;

            ArcaneSpellSfx.Play(
                EnsurePrototypeFeedbackAudioSource(),
                element,
                ArcaneSpellSfxCue.SpellCast,
                EffectivePrototypeCastFeedbackVolume);
        }

        private void RecordCastBlocked(SpellId spellId, ElementType element, float manaCost, string costStatus)
        {
            lastCastSpellId = spellId;
            lastCastElement = element;
            lastManaCost = manaCost;
            lastManaCostStatus = costStatus;
            lastCastStatus = $"Blocked: {element} {spellId}";
        }

        private SpellId ResolvePrototypeSpellId(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => SpellId.Single_Pointer,
                PoseType.Fist => SpellId.Single_Wave,
                PoseType.ThumbsUp => SpellId.Single_Strike,
                _ => SpellId.None
            };
        }

        private float ResolvePrototypeManaCost(SpellId spellId)
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");

            var spellData = spellDatabase != null ? spellDatabase.Get(spellId) : null;
            return spellData != null ? Mathf.Max(0f, spellData.manaCost) : Mathf.Max(0f, prototypeFallbackManaCost);
        }

        private SpellDatabase.PoseSpellData ResolvePrototypeSpellData(PoseType pose)
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");

            return spellDatabase != null && spellDatabase.TryGet(pose, out var data) ? data : null;
        }

        private GameObject ResolvePrototypePrefab(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => spellPrefabOpenPalm,
                PoseType.Fist => spellPrefabFist,
                PoseType.ThumbsUp => spellPrefabThumbsUp,
                _ => null
            };
        }

        private GameObject CreatePrototypeProjectileObject(PoseType pose, ElementType element, Vector3 position, Quaternion rotation)
        {
            var projectileObject = new GameObject($"PrototypeSpell_{pose}");
            projectileObject.transform.SetPositionAndRotation(position, rotation);
            var color = GetPrototypeSpellColor(pose, element);

            var collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = prototypeProjectileScale * 0.5f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = $"{pose}_Sphere";
            visual.transform.SetParent(projectileObject.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * prototypeProjectileScale;

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Destroy(visualCollider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateDebugMaterial(color);

            var light = projectileObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 1.4f;
            light.intensity = 2f;

            return projectileObject;
        }

        private static void EnsurePrototypeProjectilePhysics(GameObject projectileObject)
        {
            var collider = projectileObject.GetComponent<Collider>();
            if (collider == null)
            {
                var sphereCollider = projectileObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.06f;
                collider = sphereCollider;
            }

            collider.isTrigger = true;

            var rigidbody = projectileObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = projectileObject.AddComponent<Rigidbody>();

            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
        }

        private static Color GetPrototypePoseColor(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => new Color(1f, 0.2f, 0.2f, 1f),
                PoseType.Fist => new Color(0.2f, 0.4f, 1f, 1f),
                PoseType.ThumbsUp => new Color(1f, 0.86f, 0f, 1f),
                _ => Color.white
            };
        }

        private static Color GetPrototypeSpellColor(PoseType pose, ElementType element)
        {
            return element == ElementType.None ? GetPrototypePoseColor(pose) : GetElementColor(element);
        }

        private static ElementType ResolveDefaultPrototypeElement(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => ElementType.Fire,
                PoseType.Fist => ElementType.Ice,
                PoseType.ThumbsUp => ElementType.Thunder,
                _ => ElementType.None
            };
        }

        private static StatusEffect ResolveDefaultPrototypeStatusEffect(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => StatusEffect.Burn,
                PoseType.Fist => StatusEffect.Slow,
                PoseType.ThumbsUp => StatusEffect.Stagger,
                _ => StatusEffect.None
            };
        }

        private static float ResolveDefaultPrototypeDamage(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => 10f,
                PoseType.Fist => 8f,
                PoseType.ThumbsUp => 12f,
                _ => 0f
            };
        }

        private static float ResolveDefaultPrototypeStatusDuration(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => 3f,
                PoseType.Fist => 3f,
                PoseType.ThumbsUp => 1f,
                _ => 0f
            };
        }

        private static float ResolveDefaultPrototypeStatusMagnitude(StatusEffect statusEffect)
        {
            return statusEffect switch
            {
                StatusEffect.Burn => 2f,
                StatusEffect.Slow => 0.45f,
                StatusEffect.Stagger => 1f,
                _ => 0f
            };
        }

        private static float ResolveDefaultPrototypeStatusTickInterval(StatusEffect statusEffect)
        {
            return statusEffect == StatusEffect.Burn ? 1f : 0f;
        }

        public bool Cast(SpellId spellId)
        {
            if (spellDatabase == null)
            {
                Debug.LogWarning("SpellCaster needs a SpellDatabase reference.");
                return false;
            }

            var data = spellDatabase.Get(spellId);
            if (data == null)
            {
                Debug.LogWarning($"SpellDatabase has no entry for {spellId}.");
                lastCastStatus = $"Blocked: missing {spellId}";
                lastManaCostStatus = "Cost: missing data";
                return false;
            }

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (combatManager == null)
            {
                RecordCastBlocked(spellId, data.element, data.manaCost, "Cost: no CombatManager");
                return false;
            }

            if (!combatManager.TryConsumeMana(data.manaCost))
            {
                RecordCastBlocked(spellId, data.element, data.manaCost, $"Cost: NoMana {combatManager.CurrentMana:0.#}/{data.manaCost:0.#}");
                return false;
            }

            lastManaCost = data.manaCost;
            lastManaCostStatus = $"Cost: Paid {data.manaCost:0.#} -> {combatManager.CurrentMana:0.#}";

            var spawnPoint = ResolveSpawnPoint(spellId);
            var spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var direction = ResolveAimDirection(spawnPosition);
            spawnPosition += direction * 0.25f;
            var element = data.element;
            if (combinationChecker != null && IsSingleSpell(spellId) && combinationChecker.CurrentElement != ElementType.None)
                element = combinationChecker.CurrentElement;

            var projectileObject = CreateProjectileObject(data, element, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            var projectile = projectileObject.GetComponent<SpellProjectile>();

            if (projectile == null)
                projectile = projectileObject.AddComponent<SpellProjectile>();

            projectile.Initialize(
                spellId,
                element,
                data.damage,
                data.projectileSpeed,
                data.statusEffect,
                data.statusDuration,
                direction,
                combatManager,
                data.statusMagnitude,
                data.statusTickInterval);
            RememberCastElement(element);
            lastCastSpellId = spellId;
            lastCastStatus = SpellHitData.IsComboSpellId(spellId)
                ? $"Cast: {SpellHitData.GetDisplayName(spellId)}"
                : $"Cast: {element} {spellId}";

            if (spellSpawnRoot != null)
                projectileObject.transform.SetParent(spellSpawnRoot, true);

            Destroy(projectileObject, fallbackProjectileLifetime);
            feedbackManager?.OnSpellCast(spellId);
            PlaySpellCastFeedback(spellId, element);
            return true;
        }

        private void PlaySpellCastFeedback(SpellId spellId, ElementType element)
        {
            var audioSource = EnsurePrototypeFeedbackAudioSource();
            if (SpellHitData.IsComboSpellId(spellId))
            {
                ArcaneSpellSfx.PlayCombo(
                    audioSource,
                    spellId,
                    ArcaneSpellSfxCue.ComboCast,
                    EffectivePrototypeCastFeedbackVolume);

                prototypeAuraPulseStartTime = Time.time;
                prototypeAuraPulseEndTime = Time.time + 0.35f;
                prototypeAuraBurstParticles?.Emit(120);
                return;
            }

            ArcaneSpellSfx.Play(
                audioSource,
                element,
                ArcaneSpellSfxCue.SpellCast,
                EffectivePrototypeCastFeedbackVolume);
        }

        private ElementType ResolveVoiceRefundElement()
        {
            if (combinationChecker != null && combinationChecker.CurrentElement != ElementType.None)
                return combinationChecker.CurrentElement;

            var prototypeElement = ResolveDefaultPrototypeElement(currentPrototypePose);
            if (prototypeElement != ElementType.None)
                return prototypeElement;

            return Time.time - lastCastTime <= voiceRefundMatchWindow ? lastCastElement : ElementType.None;
        }

        private void RememberCastElement(ElementType element)
        {
            if (element == ElementType.None)
                return;

            lastCastElement = element;
            lastCastTime = Time.time;
        }

        private Transform ResolveSpawnPoint(SpellId spellId)
        {
            if (spellId == SpellId.Single_Pointer || spellId == SpellId.Single_Wave || spellId == SpellId.Single_Strike)
                return rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;

            return leftHandSpawnPoint != null ? leftHandSpawnPoint : rightHandSpawnPoint;
        }

        private Vector3 ResolveAimDirection(Vector3 spawnPosition)
        {
            if (headTransform == null)
                return transform.forward;

            var direction = headTransform.forward;
            if (direction.sqrMagnitude < 0.001f)
                direction = headTransform.position + headTransform.forward * 10f - spawnPosition;

            return direction.normalized;
        }

        private GameObject CreateProjectileObject(SpellDatabase.SpellData data, ElementType element, Vector3 position, Quaternion rotation)
        {
            if (!useDebugPrimitiveProjectiles && data.prefab != null)
                return Instantiate(data.prefab, position, rotation);

            var projectileObject = new GameObject($"Spell_{data.spellId}_DebugShape");
            projectileObject.name = $"Spell_{data.spellId}";
            projectileObject.transform.SetPositionAndRotation(position, rotation);

            var collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = GetDebugColliderRadius(data.spellId) * debugProjectileScale;

            var rigidbody = projectileObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            CreateDebugProjectileVisual(data.spellId, element, projectileObject.transform);
            return projectileObject;
        }

        private void CreateDebugProjectileVisual(SpellId spellId, ElementType element, Transform parent)
        {
            var color = GetElementColor(element);
            switch (spellId)
            {
                case SpellId.Single_Pointer:
                    AddPrimitiveVisual("Pointer_Sphere", PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * 0.18f, color);
                    break;
                case SpellId.Single_Wave:
                    AddPrimitiveVisual("Wave_Plate", PrimitiveType.Cube, parent, Vector3.zero, new Vector3(0.48f, 0.08f, 0.18f), color);
                    AddPrimitiveVisual("Wave_Crest", PrimitiveType.Sphere, parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.08f, 0.16f), Color.Lerp(color, Color.white, 0.35f));
                    break;
                case SpellId.Single_Strike:
                    AddPrimitiveVisual("Strike_Capsule", PrimitiveType.Capsule, parent, Vector3.zero, new Vector3(0.18f, 0.38f, 0.18f), color);
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case SpellId.Combo_FireIce:
                    AddPrimitiveVisual("FireIce_LeftOrb", PrimitiveType.Sphere, parent, new Vector3(-0.13f, 0f, 0f), Vector3.one * 0.18f, GetElementColor(ElementType.Fire));
                    AddPrimitiveVisual("FireIce_RightOrb", PrimitiveType.Sphere, parent, new Vector3(0.13f, 0f, 0f), Vector3.one * 0.18f, GetElementColor(ElementType.Ice));
                    break;
                case SpellId.Combo_IceThunder:
                    AddPrimitiveVisual("IceThunder_Cylinder", PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.24f, 0.16f, 0.24f), Color.Lerp(GetElementColor(ElementType.Ice), GetElementColor(ElementType.Thunder), 0.5f));
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case SpellId.Combo_ThunderFire:
                    AddPrimitiveVisual("ThunderFire_Diamond", PrimitiveType.Cube, parent, Vector3.zero, Vector3.one * 0.28f, Color.Lerp(GetElementColor(ElementType.Thunder), GetElementColor(ElementType.Fire), 0.45f));
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(45f, 45f, 0f);
                    break;
                default:
                    AddPrimitiveVisual("Fallback_Sphere", PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * 0.18f, color);
                    break;
            }

            var light = parent.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = IsSingleSpell(spellId) ? 1.4f : 2.2f;
            light.intensity = IsSingleSpell(spellId) ? 1.6f : 2.6f;
        }

        private void AddPrimitiveVisual(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale * debugProjectileScale;

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateDebugMaterial(color);
        }

        private static Material CreateDebugMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }

        private static Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => new Color(1f, 0.22f, 0.06f, 1f),
                ElementType.Ice => new Color(0.24f, 0.78f, 1f, 1f),
                ElementType.Thunder => new Color(1f, 0.88f, 0.12f, 1f),
                _ => new Color(0.75f, 0.75f, 0.85f, 1f)
            };
        }

        private static float GetDebugColliderRadius(SpellId spellId)
        {
            return IsSingleSpell(spellId) ? 0.22f : 0.34f;
        }

        private static bool IsSingleSpell(SpellId spellId)
        {
            return spellId == SpellId.Single_Pointer ||
                   spellId == SpellId.Single_Wave ||
                   spellId == SpellId.Single_Strike;
        }
    }
}
