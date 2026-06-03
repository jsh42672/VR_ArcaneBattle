using System.Collections.Generic;
using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Input;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// XR gesture driven spell caster. GestureDetector owns recognition; this
    /// component owns dummy spell spawning, projectile parenting, and hit data.
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private SpellDatabase spellDatabase;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private FeedbackManager feedbackManager;
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private CombinationFocusModeController focusModeController;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private Transform leftHandSpawnPoint;
        [SerializeField] private Transform rightHandSpawnPoint;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform spellSpawnRoot;
        [SerializeField] private ElementAuraManager elementAuraManager;

        [Header("General Casting")]
        [SerializeField] private float fallbackProjectileLifetime = 5f;
        [SerializeField] private bool useDebugPrimitiveProjectiles = true;
        [SerializeField] private float debugProjectileScale = 1f;
        [SerializeField] private bool allowCombinationSpellCasts = true;
        [SerializeField] private bool ignoreVoiceDuringCombinationFocus = true;

        [Header("Voice Boost")]
        [SerializeField] private AudioClip voiceBoostClip;
        [SerializeField] private float voiceBoostDuration = 4f;

        [Header("Common Dummy Auras")]
        [SerializeField] private bool useCommonDummyAuras = true;
        [SerializeField] private string auraTimeFocusExemptLayerName = "TimeFocusExempt";
        [SerializeField] private Vector3 rightElementAuraOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float rightElementAuraScale = 0.18f;

        [Header("Right Fire Dummy")]
        [SerializeField] private GameObject rightFireAuraPrefab;
        [SerializeField] private GameObject rightFireballPrefab;
        [SerializeField] private GameObject rightFireExplosionPrefab;
        [SerializeField] private Material rightFireExplosionMaterialOverride;
        [SerializeField] private Color rightFireAuraColor = new Color(1f, 0.35f, 0.05f, 1f);
        [SerializeField] private float rightFireSpawnForwardOffset = 0.15f;
        [SerializeField] private float rightFireProjectileScale = 0.01f;
        [SerializeField] private float rightFireProjectileSpeed = 13f;
        [SerializeField] private float rightFireRecoilVelocityThreshold = 0.5f;
        [SerializeField] private float rightFireCooldown = 0.25f;
        [SerializeField] private float rightFireAuraScale = 0.01f;
        [SerializeField] private float rightFireExplosionScale = 0.15f;
        [SerializeField] private float rightFireExplosionLifetime = 1.5f;

        [Header("Right Ice Dummy")]
        [SerializeField] private GameObject rightIceOrbPrefab;
        [SerializeField] private GameObject rightIceProjectilePrefab;
        [SerializeField] private Vector3 rightIcePalmOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float rightIceOrbScale = 0.12f;
        [SerializeField] private float rightIceProjectileScale = 0.12f;
        [SerializeField] private float rightIceProjectileSpeed = 14f;
        [SerializeField] private float rightIceBallLifetime = 6f;
        [SerializeField] private bool rightIceUseArcTrajectory = true;
        [SerializeField] private float rightIceArcVelocityMultiplier = 4f;
        [SerializeField] private float rightIceArcUpwardBoost = 1.4f;
        [SerializeField] private float rightIceMinArcLaunchSpeed = 20f;
        [SerializeField] private float rightIceMaxArcLaunchSpeed = 40f;
        [SerializeField] private float rightIceMinThrowSpeed = 1.5f;
        [SerializeField] private float rightIceLaunchCooldown = 1.2f;
        [SerializeField] private float rightIceVelocitySampleDuration = 0.12f;

        [Header("Right Thunder Dummy")]
        [SerializeField] private GameObject rightThunderAuraPrefab;
        [SerializeField] private AudioClip rightThunderAuraAudioClip;
        [SerializeField] private Vector3 rightThunderAuraOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float rightThunderAuraScale = 0.16f;
        [SerializeField] private float rightThunderRangeMeters = 20f;
        [SerializeField] private float rightThunderDamage = 16f;
        [SerializeField] private float rightThunderStatusDuration = 2.5f;
        [SerializeField] private float rightThunderChargeGraceSeconds = 0.4f;
        [SerializeField] private float rightThunderContinuousFireSeconds = 5f;
        [SerializeField] private float rightThunderShootPoseGraceSeconds = 0.2f;
        [SerializeField] private float rightThunderHitTickInterval = 0.25f;
        [SerializeField] private float rightThunderLaserWidth = 0.035f;
        [SerializeField] private float rightThunderLaserDownAngleDegrees = 8f;
        [SerializeField] private LayerMask rightThunderHitMask = ~0;
        [SerializeField] private Color rightThunderLaserColor = new Color(1f, 0.88f, 0.15f, 1f);

        [Header("Feedback")]
        [SerializeField] private bool showPrototypeArmedAura = true;
        [SerializeField] private bool showCombinationAura = true;
        [SerializeField] private float combinationReadyAuraScale = 0.18f;
        [SerializeField] private float combinationCompleteAuraScale = 0.42f;
        [SerializeField] private float combinationCompleteAuraHoldSeconds = 3f;

        private readonly Queue<(float time, Vector3 pos)> rightIceSamples = new Queue<(float time, Vector3 pos)>();
        private string currentRightGesture = string.Empty;
        private Vector3 previousRightTrackingPosition;
        private bool hasPreviousRightTrackingPosition;
        private float rightFireLastShotTime = -999f;
        private float rightIceLastLaunchTime = -999f;
        private GameObject rightFireAuraInstance;
        private GameObject rightIceAuraInstance;
        private GameObject rightIceOrbInstance;
        private GameObject rightThunderAuraInstance;
        private AudioSource rightThunderAuraAudioSource;
        private GameObject rightThunderBeamInstance;
        private LineRenderer rightThunderBeamLine;
        private Renderer rightThunderAuraRenderer;
        private float rightThunderBeamEndTime = -999f;
        private float rightThunderNextHitTime = -999f;
        private float rightThunderLastChargeTime = -999f;
        private float rightThunderLastShootTime = -999f;
        private bool rightThunderCharged;
        private GameObject combinationAuraRoot;
        private SpellId currentCombinationAuraSpell = SpellId.None;
        private bool combinationAuraCompleted;
        private float combinationAuraUntilTime = -999f;
        private float lastCastTime = -999f;
        private SpellId lastCastSpellId = SpellId.None;
        private ElementType lastCastElement = ElementType.None;
        private float lastManaCost;
        private string lastCastStatus = "Cast: idle";
        private string lastManaCostStatus = "Cost: idle";
        private string lastVoiceBoostStatus = "VoiceLink: idle";
        private bool isVoiceBoostActive;
        private float voiceBoostExpiry = -999f;
        private AudioSource voiceBoostAudioSource;
        private AudioClip _cachedBoostTone;
        private bool isCastingSuppressed;
        private string castingSuppressionSource = string.Empty;

        public SpellDatabase Database
        {
            get => spellDatabase;
            set => spellDatabase = value;
        }

        public string PrototypeDebugStatus { get; private set; } = "CAST: waiting";
        public string LastCastStatus => lastCastStatus;
        public string LastManaCostStatus => lastManaCostStatus;
        public string LastVoiceBoostStatus => lastVoiceBoostStatus;
        public SpellId LastCastSpellId => lastCastSpellId;
        public ElementType LastCastElement => lastCastElement;
        public float LastManaCost => lastManaCost;
        public ElementType PrototypeArmedElement => ResolveElement(currentRightGesture);
        public bool IsCastingSuppressed => isCastingSuppressed;
        public bool IsPrototypeArmed => !string.IsNullOrEmpty(currentRightGesture);
        public bool IsPrototypeVoiceBoosted => isVoiceBoostActive;
        public float PrototypeLastForwardSpeed { get; private set; }
        public string PrototypeArmStatus => IsPrototypeArmed
            ? $"Arm {PrototypeArmedElement} gesture:{currentRightGesture} speed:{PrototypeLastForwardSpeed:0.00}"
            : lastCastStatus;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeGestureDetector();

            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
                combinationChecker.OnComboReadyChanged += HandleComboReadyChanged;
                combinationChecker.OnCombinationFail += HandleCombinationFail;
            }

            if (voiceRecognizer != null)
                voiceRecognizer.OnVoiceCommand += HandleVoiceCommand;
        }

        private void OnDisable()
        {
            UnsubscribeGestureDetector();

            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
                combinationChecker.OnComboReadyChanged -= HandleComboReadyChanged;
                combinationChecker.OnCombinationFail -= HandleCombinationFail;
            }

            if (voiceRecognizer != null)
                voiceRecognizer.OnVoiceCommand -= HandleVoiceCommand;

            ClearRightGestureState();
            StopThunderBeam();
            HideRightFireAura();
            HideRightIceOrb();
            HideRightThunderAura();
        }

        private void Update()
        {
            ResolveReferences();
            UpdateRightGestureAttack();
            UpdateCombinationAuraFeedback();
            UpdateTimeFocusVisibility();
            UpdateVoiceBoost();
        }

        public void SetCastingSuppressed(bool suppressed, string source)
        {
            isCastingSuppressed = suppressed;
            castingSuppressionSource = suppressed ? (string.IsNullOrWhiteSpace(source) ? "unknown" : source) : string.Empty;
            if (suppressed)
                lastCastStatus = $"Blocked: {castingSuppressionSource}";
        }

        public void ConfigureGesturePrototype(GestureDetector detector, OVRHand hand, Transform spawnPoint, Transform spawnRoot)
        {
            ConfigureGesturePrototype(detector, null, hand, spawnPoint, spawnRoot);
        }

        public void ConfigureGesturePrototype(
            GestureDetector detector,
            GestureEventRouter router,
            OVRHand hand,
            Transform spawnPoint,
            Transform spawnRoot)
        {
            UnsubscribeGestureDetector();
            gestureDetector = detector;
            rightHandSpawnPoint = spawnPoint != null ? spawnPoint : rightHandSpawnPoint;
            spellSpawnRoot = spawnRoot != null ? spawnRoot : spellSpawnRoot;
            SubscribeGestureDetector();
        }

        private void ResolveReferences()
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();
            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();
            if (feedbackManager == null)
                feedbackManager = FindAnyObjectByType<FeedbackManager>();
            if (focusModeController == null)
                focusModeController = FindAnyObjectByType<CombinationFocusModeController>();
            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
            if (rightHandSpawnPoint == null)
                rightHandSpawnPoint = GameObject.Find("R_Wrist")?.transform;
            if (leftHandSpawnPoint == null)
                leftHandSpawnPoint = GameObject.Find("L_Wrist")?.transform;
        }

        private void SubscribeGestureDetector()
        {
            if (gestureDetector == null)
                return;

            gestureDetector.OnGestureConfirmed -= HandleGestureConfirmed;
            gestureDetector.OnGestureConfirmed += HandleGestureConfirmed;
            gestureDetector.OnGestureCleared -= HandleGestureCleared;
            gestureDetector.OnGestureCleared += HandleGestureCleared;
        }

        private void UnsubscribeGestureDetector()
        {
            if (gestureDetector == null)
                return;

            gestureDetector.OnGestureConfirmed -= HandleGestureConfirmed;
            gestureDetector.OnGestureCleared -= HandleGestureCleared;
        }

        private void HandleGestureConfirmed(bool isLeft, string gestureName, PoseType _)
        {
            if (isLeft)
                return;

            if (gestureName != "Fire" &&
                gestureName != "Ice" &&
                gestureName != "Thunder" &&
                gestureName != "ThunderShoot")
                return;

            currentRightGesture = gestureName;
            hasPreviousRightTrackingPosition = false;
            PrototypeDebugStatus = $"CAST: armed {gestureName}";

            if (gestureName == "Fire")
                ShowRightFireAura();
            else
                HideRightFireAura();

            if (gestureName == "Ice")
            {
                ShowRightElementAura(ElementType.Ice);
                ShowRightIceOrb();
            }
            else
            {
                HideRightIceAura();
                HideRightIceOrb();
            }

            if (gestureName == "Thunder" || gestureName == "ThunderShoot")
            {
                if (gestureName == "Thunder")
                {
                    rightThunderCharged = true;
                    rightThunderLastChargeTime = Time.time;
                    ShowRightThunderAura();
                }
                else if (IsRightThunderChargeAvailable())
                {
                    ShowRightThunderAura();
                    rightThunderLastShootTime = Time.time;
                    StartThunderBeam();
                }
                else
                {
                    PrototypeDebugStatus = "CAST Thunder: shoot ignored, no charge";
                    HideRightThunderAura();
                    StopThunderBeam();
                }
            }
            else
            {
                rightThunderCharged = false;
                HideRightThunderAura();
                StopThunderBeam();
            }
        }

        private void HandleGestureCleared(bool isLeft, string gestureName)
        {
            if (isLeft || gestureName != currentRightGesture)
                return;

            ClearRightGestureState();
        }

        private void ClearRightGestureState()
        {
            currentRightGesture = string.Empty;
            hasPreviousRightTrackingPosition = false;
            PrototypeLastForwardSpeed = 0f;
            PrototypeDebugStatus = "CAST: waiting";
            DeactivateVoiceBoost();
            elementAuraManager?.Hide();
            HideRightFireAura();
            HideRightIceAura();
            HideRightIceOrb();
            HideRightThunderAura();
            StopThunderBeam();
            rightThunderCharged = false;
        }

        private void UpdateRightGestureAttack()
        {
            if (isCastingSuppressed || string.IsNullOrEmpty(currentRightGesture))
                return;

            var spawnPoint = ResolveRightSpawnPoint();
            if (spawnPoint == null || Time.deltaTime <= 0f)
            {
                PrototypeDebugStatus = "CAST: no XR spawn point";
                return;
            }

            var currentTrackingPosition = ResolveTrackingPosition(spawnPoint.position);
            if (!hasPreviousRightTrackingPosition)
            {
                previousRightTrackingPosition = currentTrackingPosition;
                hasPreviousRightTrackingPosition = true;
                return;
            }

            var trackingVelocity = (currentTrackingPosition - previousRightTrackingPosition) / Time.deltaTime;
            previousRightTrackingPosition = currentTrackingPosition;
            var cameraForward = headTransform != null ? headTransform.forward : transform.forward;
            PrototypeLastForwardSpeed = Vector3.Dot(ToWorldVector(trackingVelocity), cameraForward);

            if (currentRightGesture == "Fire")
                UpdateFireDummy(spawnPoint, trackingVelocity);
            else if (currentRightGesture == "Ice")
                UpdateIceDummy(spawnPoint, currentTrackingPosition, trackingVelocity);
            else if (currentRightGesture == "Thunder")
                UpdateThunderCharge(spawnPoint);
            else if (currentRightGesture == "ThunderShoot")
                UpdateThunderShoot(spawnPoint);
        }

        private void UpdateFireDummy(Transform spawnPoint, Vector3 trackingVelocity)
        {
            UpdateFireAuraTransform(spawnPoint);
            var upSpeed = Vector3.Dot(ToWorldVector(trackingVelocity), Vector3.up);
            PrototypeDebugStatus = $"CAST Fire up:{upSpeed:0.00}/{rightFireRecoilVelocityThreshold:0.00}";
            if (upSpeed < rightFireRecoilVelocityThreshold ||
                Time.time - rightFireLastShotTime <= rightFireCooldown)
            {
                return;
            }

            rightFireLastShotTime = Time.time;
            FireRightFireProjectile(spawnPoint);
        }

        private void UpdateIceDummy(Transform spawnPoint, Vector3 trackingPosition, Vector3 trackingVelocity)
        {
            UpdateIceOrbTransform(spawnPoint);
            rightIceSamples.Enqueue((Time.time, trackingPosition));
            while (rightIceSamples.Count > 0 && rightIceSamples.Peek().time < Time.time - rightIceVelocitySampleDuration)
                rightIceSamples.Dequeue();

            if (rightIceSamples.Count < 2 || Time.time - rightIceLastLaunchTime <= rightIceLaunchCooldown)
                return;

            var oldest = rightIceSamples.Peek();
            var dt = Mathf.Max(0.001f, Time.time - oldest.time);
            var localVelocity = (trackingPosition - oldest.pos) / dt;
            var forwardSpeed = Vector3.Dot(ToWorldVector(localVelocity), headTransform != null ? headTransform.forward : transform.forward);
            PrototypeDebugStatus = $"CAST Ice throw:{forwardSpeed:0.00}/{rightIceMinThrowSpeed:0.00}";
            if (forwardSpeed < rightIceMinThrowSpeed)
                return;

            LaunchRightIceProjectile(spawnPoint, ToWorldVector(localVelocity));
        }

        private void UpdateThunderCharge(Transform spawnPoint)
        {
            rightThunderCharged = true;
            rightThunderLastChargeTime = Time.time;
            UpdateThunderAuraTransform(spawnPoint);

            // If a beam is still within its end time (arm-window expired while beam was active),
            // sustain it rather than stopping it prematurely.
            if (rightThunderBeamLine != null && Time.time < rightThunderBeamEndTime)
            {
                rightThunderLastShootTime = Time.time; // prevent grace-period cutoff in UpdateThunderBeam
                UpdateThunderBeam();
                PrototypeDebugStatus = "CAST Thunder: beam sustain";
            }
            else
            {
                StopThunderBeam();
                PrototypeDebugStatus = "CAST Thunder: charged";
            }
        }

        private void UpdateThunderShoot(Transform spawnPoint)
        {
            if (!IsRightThunderChargeAvailable())
            {
                StopThunderBeam();
                PrototypeDebugStatus = "CAST Thunder: waiting for charge";
                return;
            }

            rightThunderLastShootTime = Time.time;
            UpdateThunderBeam();
        }

        private void FireRightFireProjectile(Transform spawnPoint)
        {
            var direction = ResolveAimDirection(spawnPoint.position);
            var origin = spawnPoint.position + direction * rightFireSpawnForwardOffset;
            var projectile = rightFireballPrefab != null
                ? Instantiate(rightFireballPrefab, origin, Quaternion.LookRotation(direction))
                : CreatePrototypeProjectileObject(ElementType.Fire, origin, Quaternion.LookRotation(direction));

            ParentToSpellRoot(projectile);
            if (projectile.TryGetComponent<FireballProjectile>(out var fireball))
            {
                fireball.ConfigureLaunch(rightFireProjectileSpeed, rightFireProjectileScale);
                fireball.ConfigureImpact(
                    rightFireExplosionPrefab,
                    rightFireExplosionScale,
                    rightFireExplosionLifetime,
                    rightFireExplosionMaterialOverride);
            }

            var fireData = GetSpellData(SpellId.Single_Pointer);
            var speed = fireData?.projectileSpeed > 0f ? fireData.projectileSpeed : rightFireProjectileSpeed;
            InitializePrototypeProjectile(
                projectile,
                SpellId.Single_Pointer,
                ElementType.Fire,
                fireData?.statusEffect ?? StatusEffect.Burn,
                fireData?.damage ?? 10f,
                fireData?.statusDuration ?? 3f,
                speed,
                direction);

            lastCastStatus = "Cast: Fire dummy";
            RememberCast(ElementType.Fire, SpellId.Single_Pointer, 0f, "Cost: dummy");
            Destroy(projectile, fallbackProjectileLifetime);
        }

        private void ShowRightFireAura()
        {
            if (!showPrototypeArmedAura) return;

            if (elementAuraManager != null)
            {
                elementAuraManager.Show(ElementType.Fire, ResolveRightSpawnPoint());
                return;
            }

            if (rightFireAuraInstance != null) return;
            var spawnPoint = ResolveRightSpawnPoint();
            if (spawnPoint == null) return;

            if (useCommonDummyAuras)
            {
                rightFireAuraInstance = CreateRightElementAuraObject("RightFireAura_Dummy", ElementType.Fire, rightElementAuraScale);
            }
            else if (rightFireAuraPrefab != null)
            {
                rightFireAuraInstance = Instantiate(rightFireAuraPrefab, spawnPoint.position, spawnPoint.rotation);
                rightFireAuraInstance.transform.localScale = Vector3.one * rightFireAuraScale;
                if (rightFireAuraInstance.TryGetComponent<WristAuraController>(out var aura))
                {
                    aura.auraColor = rightFireAuraColor;
                    aura.skeleton = null;
                    aura.ManaPct = 1f;
                }
            }

            if (rightFireAuraInstance == null) return;
            ParentToSpellRoot(rightFireAuraInstance);
            ApplyTimeFocusExemptLayer(rightFireAuraInstance);
            UpdateFireAuraTransform(spawnPoint);
        }

        private void UpdateFireAuraTransform(Transform spawnPoint)
        {
            if (rightFireAuraInstance != null)
                rightFireAuraInstance.transform.SetPositionAndRotation(
                    spawnPoint.position + spawnPoint.rotation * rightElementAuraOffset,
                    spawnPoint.rotation);
        }

        private void HideRightFireAura()
        {
            if (rightFireAuraInstance != null)
                Destroy(rightFireAuraInstance);
            rightFireAuraInstance = null;
        }

        private void ShowRightIceOrb()
        {
            if (rightIceOrbInstance != null)
                Destroy(rightIceOrbInstance);

            var spawnPoint = ResolveRightSpawnPoint();
            if (spawnPoint == null)
                return;

            var pos = spawnPoint.TransformPoint(rightIcePalmOffset);
            rightIceOrbInstance = rightIceOrbPrefab != null
                ? Instantiate(rightIceOrbPrefab, pos, Quaternion.identity)
                : CreateFallbackSphere("IceOrb", pos, rightIceOrbScale, GetElementColor(ElementType.Ice));
            ParentToSpellRoot(rightIceOrbInstance);
            rightIceOrbInstance.transform.localScale = Vector3.one * rightIceOrbScale;
            rightIceSamples.Clear();
        }

        private void UpdateIceOrbTransform(Transform spawnPoint)
        {
            UpdateRightElementAuraTransform(rightIceAuraInstance, spawnPoint);
            if (rightIceOrbInstance == null) return;
            var timeStopped = Time.timeScale < 0.5f;
            rightIceOrbInstance.SetActive(!timeStopped);
            if (!timeStopped)
                rightIceOrbInstance.transform.position = spawnPoint.TransformPoint(rightIcePalmOffset);
        }

        private void HideRightIceOrb()
        {
            if (rightIceOrbInstance != null)
                Destroy(rightIceOrbInstance);
            rightIceOrbInstance = null;
            rightIceSamples.Clear();
        }

        private void ShowRightElementAura(ElementType element)
        {
            if (!showPrototypeArmedAura) return;

            if (elementAuraManager != null)
            {
                elementAuraManager.Show(element, ResolveRightSpawnPoint());
                return;
            }

            var spawnPoint = ResolveRightSpawnPoint();
            if (spawnPoint == null) return;

            var aura = element switch
            {
                ElementType.Fire    => rightFireAuraInstance,
                ElementType.Ice     => rightIceAuraInstance,
                ElementType.Thunder => rightThunderAuraInstance,
                _                   => null
            };

            if (aura == null)
            {
                aura = CreateRightElementAuraObject($"Right{element}Aura_Dummy", element, rightElementAuraScale);
                ParentToSpellRoot(aura);
                ApplyTimeFocusExemptLayer(aura);
            }

            if (element == ElementType.Fire)       rightFireAuraInstance    = aura;
            else if (element == ElementType.Ice)   rightIceAuraInstance     = aura;
            else if (element == ElementType.Thunder) rightThunderAuraInstance = aura;

            UpdateRightElementAuraTransform(aura, spawnPoint);
        }

        private GameObject CreateRightElementAuraObject(string name, ElementType element, float scale)
        {
            if (useCommonDummyAuras)
            {
                var aura = ElementAuraDummy.Create(name, GetElementColor(element), scale, auraTimeFocusExemptLayerName);
                return aura.gameObject;
            }

            return CreateFallbackSphere(name, Vector3.zero, scale, GetElementColor(element));
        }

        private void UpdateRightElementAuraTransform(GameObject aura, Transform spawnPoint)
        {
            if (aura == null || spawnPoint == null)
                return;

            aura.transform.SetPositionAndRotation(
                spawnPoint.position + spawnPoint.rotation * rightElementAuraOffset,
                spawnPoint.rotation);
        }

        private void HideRightIceAura()
        {
            if (rightIceAuraInstance != null)
                Destroy(rightIceAuraInstance);
            rightIceAuraInstance = null;
        }

        private void LaunchRightIceProjectile(Transform spawnPoint, Vector3 worldVelocity)
        {
            rightIceLastLaunchTime = Time.time;
            var launchPos = spawnPoint.TransformPoint(rightIcePalmOffset);
            var fallbackForward = headTransform != null ? headTransform.forward : transform.forward;
            var direction = worldVelocity.sqrMagnitude > 0.01f ? worldVelocity.normalized : fallbackForward.normalized;
            var launchVelocity = rightIceUseArcTrajectory
                ? ComputeIceArcVelocity(worldVelocity, fallbackForward)
                : direction * rightIceProjectileSpeed;

            var projectile = rightIceProjectilePrefab != null
                ? Instantiate(rightIceProjectilePrefab, launchPos, Quaternion.LookRotation(direction))
                : CreatePrototypeProjectileObject(ElementType.Ice, launchPos, Quaternion.LookRotation(direction));
            ParentToSpellRoot(projectile);
            projectile.transform.localScale = Vector3.one * rightIceProjectileScale;

            var iceData = GetSpellData(SpellId.Single_Wave);
            InitializePrototypeProjectile(
                projectile,
                SpellId.Single_Wave,
                ElementType.Ice,
                iceData?.statusEffect ?? StatusEffect.Slow,
                iceData?.damage ?? 8f,
                iceData?.statusDuration ?? 3f,
                rightIceUseArcTrajectory ? 0f : rightIceProjectileSpeed,
                direction);

            var rb = projectile.GetComponent<Rigidbody>() ?? projectile.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = rightIceUseArcTrajectory;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = launchVelocity;

            HideRightIceOrb();
            HideRightIceAura();
            lastCastStatus = "Cast: Ice dummy";
            RememberCast(ElementType.Ice, SpellId.Single_Wave, 0f, "Cost: dummy");
            Destroy(projectile, rightIceBallLifetime);
        }

        private Vector3 ComputeIceArcVelocity(Vector3 throwVelocity, Vector3 cameraForward)
        {
            var baseVelocity = throwVelocity.sqrMagnitude > 0.01f
                ? throwVelocity * Mathf.Max(0.1f, rightIceArcVelocityMultiplier)
                : cameraForward.normalized * rightIceMinArcLaunchSpeed;

            baseVelocity += Vector3.up * rightIceArcUpwardBoost;
            var speed = baseVelocity.magnitude;
            if (speed < rightIceMinArcLaunchSpeed && baseVelocity.sqrMagnitude > 0.001f)
                baseVelocity = baseVelocity.normalized * rightIceMinArcLaunchSpeed;
            else if (speed > rightIceMaxArcLaunchSpeed)
                baseVelocity = baseVelocity.normalized * rightIceMaxArcLaunchSpeed;

            return baseVelocity;
        }

        private void ShowRightThunderAura()
        {
            if (!showPrototypeArmedAura) return;

            if (elementAuraManager != null)
            {
                elementAuraManager.Show(ElementType.Thunder, ResolveRightSpawnPoint());
                return;
            }

            if (rightThunderAuraInstance != null) return;
            var spawnPoint = ResolveRightSpawnPoint();
            if (spawnPoint == null) return;

            if (useCommonDummyAuras)
            {
                rightThunderAuraInstance = CreateRightElementAuraObject("RightThunderAura_Dummy", ElementType.Thunder, rightElementAuraScale);
            }
            else
            {
                rightThunderAuraInstance = rightThunderAuraPrefab != null
                    ? Instantiate(rightThunderAuraPrefab)
                    : CreateFallbackSphere("ThunderAura_Dummy", spawnPoint.position, rightThunderAuraScale, rightThunderLaserColor);
                rightThunderAuraInstance.transform.localScale = Vector3.one * rightThunderAuraScale;
            }

            ParentToSpellRoot(rightThunderAuraInstance);
            ApplyTimeFocusExemptLayer(rightThunderAuraInstance);
            rightThunderAuraRenderer = rightThunderAuraInstance.GetComponentInChildren<Renderer>();
            rightThunderAuraAudioSource = rightThunderAuraInstance.AddComponent<AudioSource>();
            rightThunderAuraAudioSource.playOnAwake = false;
            rightThunderAuraAudioSource.loop = true;
            rightThunderAuraAudioSource.clip = rightThunderAuraAudioClip;
            if (rightThunderAuraAudioClip != null)
                rightThunderAuraAudioSource.Play();
            UpdateThunderAuraTransform(spawnPoint);
        }

        private void UpdateThunderAuraTransform(Transform spawnPoint)
        {
            if (rightThunderAuraInstance == null)
                return;

            rightThunderAuraInstance.transform.SetPositionAndRotation(
                spawnPoint.position + spawnPoint.rotation * (useCommonDummyAuras ? rightElementAuraOffset : rightThunderAuraOffset),
                spawnPoint.rotation);

            if (rightThunderAuraRenderer != null)
                ApplyMaterialColor(rightThunderAuraRenderer.material, rightThunderLaserColor);
        }

        private void HideRightThunderAura()
        {
            if (rightThunderAuraInstance != null)
                Destroy(rightThunderAuraInstance);
            rightThunderAuraInstance = null;
            rightThunderAuraAudioSource = null;
            rightThunderAuraRenderer = null;
        }

        private void StartThunderBeam()
        {
            var duration = rightThunderContinuousFireSeconds;
            var dbData = GetSpellData(SpellId.Single_Strike);
            if (dbData != null && dbData.continuousFireSeconds > 0f)
                duration = dbData.continuousFireSeconds;

            rightThunderBeamEndTime = Time.time + Mathf.Max(0f, duration);
            rightThunderNextHitTime = -999f;
        }

        private bool IsRightThunderChargeAvailable()
        {
            return rightThunderCharged ||
                   Time.time - rightThunderLastChargeTime <= Mathf.Max(0f, rightThunderChargeGraceSeconds);
        }

        private void UpdateThunderBeam()
        {
            var spawnPoint = ResolveRightSpawnPoint();
            if (spawnPoint == null)
                return;

            UpdateThunderAuraTransform(spawnPoint);
            if (Time.time - rightThunderLastShootTime > Mathf.Max(0f, rightThunderShootPoseGraceSeconds) &&
                currentRightGesture != "ThunderShoot")
            {
                StopThunderBeam();
                return;
            }

            if (Time.time > rightThunderBeamEndTime)
            {
                StopThunderBeam();
                return;
            }

            var origin = spawnPoint.position + spawnPoint.rotation * rightThunderAuraOffset;
            var direction = spawnPoint.forward.sqrMagnitude > 0.001f
                ? spawnPoint.forward.normalized
                : (headTransform != null ? headTransform.forward : transform.forward).normalized;
            direction = Vector3.RotateTowards(
                direction,
                Vector3.down,
                Mathf.Max(0f, rightThunderLaserDownAngleDegrees) * Mathf.Deg2Rad,
                0f).normalized;

            var hitPoint = origin + direction * rightThunderRangeMeters;
            Collider hitCollider = null;
            if (Physics.Raycast(origin, direction, out var hit, rightThunderRangeMeters, rightThunderHitMask, QueryTriggerInteraction.Collide))
            {
                hitPoint = hit.point;
                hitCollider = hit.collider;
            }

            EnsureThunderBeam();
            rightThunderBeamLine.SetPosition(0, origin);
            rightThunderBeamLine.SetPosition(1, hitPoint);

            if (Time.time >= rightThunderNextHitTime)
            {
                rightThunderNextHitTime = Time.time + Mathf.Max(0.05f, rightThunderHitTickInterval);
                ApplyThunderHit(hitCollider);
                RememberCast(ElementType.Thunder, SpellId.Single_Strike, 0f, "Cost: dummy");
                lastCastStatus = "Cast: Thunder dummy";
            }

            PrototypeDebugStatus = $"CAST Thunder beam:{(hitPoint - origin).magnitude:0.0}m";
        }

        private void EnsureThunderBeam()
        {
            if (rightThunderBeamLine != null)
                return;

            rightThunderBeamInstance = new GameObject("ThunderLaser_Dummy");
            ParentToSpellRoot(rightThunderBeamInstance);
            rightThunderBeamLine = rightThunderBeamInstance.AddComponent<LineRenderer>();
            rightThunderBeamLine.positionCount = 2;
            rightThunderBeamLine.startWidth = rightThunderLaserWidth;
            rightThunderBeamLine.endWidth = rightThunderLaserWidth * 0.45f;
            rightThunderBeamLine.material = CreateUnlitMaterial(rightThunderLaserColor);
            rightThunderBeamLine.startColor = rightThunderLaserColor;
            rightThunderBeamLine.endColor = new Color(rightThunderLaserColor.r, rightThunderLaserColor.g, rightThunderLaserColor.b, 0.15f);
        }

        private void StopThunderBeam()
        {
            rightThunderBeamEndTime = -999f;
            rightThunderNextHitTime = -999f;
            if (rightThunderBeamInstance != null)
                Destroy(rightThunderBeamInstance);
            rightThunderBeamInstance = null;
            rightThunderBeamLine = null;
        }

        private void ApplyThunderHit(Collider hitCollider)
        {
            if (hitCollider == null || ArcanePlayerRigResolver.IsPlayerCollider(hitCollider))
                return;

            var dbData = GetSpellData(SpellId.Single_Strike);
            var hitData = new SpellHitData(
                SpellId.Single_Strike,
                ElementType.Thunder,
                dbData?.statusEffect ?? StatusEffect.Stagger,
                dbData?.damage ?? rightThunderDamage,
                dbData?.statusDuration ?? rightThunderStatusDuration,
                dbData?.statusMagnitude ?? 1f,
                dbData?.statusTickInterval ?? 0f);

            var spellTarget = hitCollider.GetComponentInParent<ISpellTarget>();
            if (spellTarget != null)
            {
                spellTarget.OnHit(hitData);
                return;
            }

            var boss = hitCollider.GetComponentInParent<BossAI>();
            var golemTarget = boss != null
                ? boss.GetComponent<GolemCombatTarget>() ?? boss.GetComponentInParent<GolemCombatTarget>()
                : hitCollider.GetComponentInParent<GolemCombatTarget>();
            golemTarget?.OnHit(hitData);
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            if (IsCombinationCastAllowed(spellId))
                Cast(spellId);
        }

        private void HandleCombinationFail()
        {
            StopCombinationAuraFeedback();
        }

        private void HandleComboReadyChanged(SpellId spellId, bool ready)
        {
            if (ready && SpellHitData.IsComboSpellId(spellId))
                StartCombinationAuraFeedback(spellId, false);
            else if (!combinationAuraCompleted)
                StopCombinationAuraFeedback();
        }

        public bool Cast(SpellId spellId)
        {
            if (!IsCombinationCastAllowed(spellId))
            {
                lastCastStatus = $"Combo cast locked: {SpellHitData.GetDisplayName(spellId)}";
                lastManaCostStatus = "Cost: combo locked";
                return false;
            }

            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");
            var data = spellDatabase != null ? spellDatabase.Get(spellId) : null;
            if (data == null)
            {
                lastCastStatus = $"Blocked: missing {spellId}";
                lastManaCostStatus = "Cost: missing data";
                return false;
            }

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
            if (combatManager != null && !combatManager.TryConsumeMana(data.manaCost))
            {
                RecordCastBlocked(spellId, data.element, data.manaCost, $"Cost: NoMana {combatManager.CurrentMana:0.#}/{data.manaCost:0.#}");
                return false;
            }

            var spawnPoint = ResolveSpawnPoint(spellId);
            var spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var direction = ResolveAimDirection(spawnPosition);
            spawnPosition += direction * 0.25f;
            var element = data.element;
            if (combinationChecker != null && IsSingleSpell(spellId) && combinationChecker.CurrentElement != ElementType.None)
                element = combinationChecker.CurrentElement;

            var projectileObject = CreateProjectileObject(data, element, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            ParentToSpellRoot(projectileObject);
            var projectile = projectileObject.GetComponent<SpellProjectile>() ?? projectileObject.AddComponent<SpellProjectile>();
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

            RememberCast(element, spellId, data.manaCost, combatManager != null ? $"Cost: Paid {data.manaCost:0.#}" : "Cost: no CombatManager");
            lastCastStatus = SpellHitData.IsComboSpellId(spellId)
                ? $"Cast: {SpellHitData.GetDisplayName(spellId)}"
                : $"Cast: {element} {spellId}";
            Destroy(projectileObject, fallbackProjectileLifetime);
            feedbackManager?.OnSpellCast(spellId);
            if (SpellHitData.IsComboSpellId(spellId))
                StartCombinationAuraFeedback(spellId, true);
            return true;
        }

        private SpellDatabase.SpellData GetSpellData(SpellId spellId)
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");
            return spellDatabase != null ? spellDatabase.Get(spellId) : null;
        }

        private void InitializePrototypeProjectile(
            GameObject projectileObject,
            SpellId spellId,
            ElementType element,
            StatusEffect statusEffect,
            float damage,
            float statusDuration,
            float speed,
            Vector3 direction)
        {
            var projectile = projectileObject.GetComponent<SpellProjectile>() ?? projectileObject.AddComponent<SpellProjectile>();
            projectile.spellId = spellId;
            projectile.InitializePrototype(
                speed,
                direction,
                element,
                statusEffect,
                damage,
                statusDuration);
        }

        private void RecordCastBlocked(SpellId spellId, ElementType element, float manaCost, string costStatus)
        {
            lastCastSpellId = spellId;
            lastCastElement = element;
            lastManaCost = manaCost;
            lastManaCostStatus = costStatus;
            lastCastStatus = $"Blocked: {element} {spellId}";
        }

        private void RememberCast(ElementType element, SpellId spellId, float manaCost, string costStatus)
        {
            lastCastElement = element;
            lastCastSpellId = spellId;
            lastManaCost = manaCost;
            lastManaCostStatus = costStatus;
            lastCastTime = Time.time;
        }

        private Transform ResolveRightSpawnPoint()
        {
            return rightHandSpawnPoint != null ? rightHandSpawnPoint : transform;
        }

        private Transform ResolveSpawnPoint(SpellId spellId)
        {
            if (spellId == SpellId.Single_Pointer || spellId == SpellId.Single_Wave || spellId == SpellId.Single_Strike)
                return rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;

            return leftHandSpawnPoint != null ? leftHandSpawnPoint : rightHandSpawnPoint;
        }

        private Vector3 ResolveAimDirection(Vector3 spawnPosition)
        {
            var direction = headTransform != null ? headTransform.forward : transform.forward;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        private Vector3 ResolveTrackingPosition(Vector3 worldPosition)
        {
            var root = ResolveTrackingSpaceRoot();
            return root != null ? root.InverseTransformPoint(worldPosition) : worldPosition;
        }

        private Vector3 ToWorldVector(Vector3 trackingVector)
        {
            var root = ResolveTrackingSpaceRoot();
            return root != null ? root.TransformVector(trackingVector) : trackingVector;
        }

        private Transform ResolveTrackingSpaceRoot()
        {
            if (rightHandSpawnPoint != null)
            {
                var parent = rightHandSpawnPoint;
                while (parent != null)
                {
                    if (parent.name == "TrackingSpace" || parent.name == "Camera Offset" || parent.name == "XR Origin")
                        return parent;
                    parent = parent.parent;
                }
            }

            return null;
        }

        private void ParentToSpellRoot(GameObject obj)
        {
            if (obj != null && spellSpawnRoot != null)
                obj.transform.SetParent(spellSpawnRoot, true);
        }

        private GameObject CreateProjectileObject(SpellDatabase.SpellData data, ElementType element, Vector3 position, Quaternion rotation)
        {
            if (!useDebugPrimitiveProjectiles && data.prefab != null)
                return Instantiate(data.prefab, position, rotation);

            var projectileObject = new GameObject($"Spell_{data.spellId}");
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

        private GameObject CreatePrototypeProjectileObject(ElementType element, Vector3 position, Quaternion rotation)
        {
            var projectileObject = new GameObject($"PrototypeSpell_{element}");
            projectileObject.transform.SetPositionAndRotation(position, rotation);
            var collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.02f, debugProjectileScale * 0.08f);
            var rigidbody = projectileObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            AddPrimitiveVisual($"{element}_Sphere", PrimitiveType.Sphere, projectileObject.transform, Vector3.zero, Vector3.one * 0.16f, GetElementColor(element));
            return projectileObject;
        }

        private GameObject CreateFallbackSphere(string name, Vector3 position, float scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateUnlitMaterial(color);
            ApplyTimeFocusExemptLayer(go);
            return go;
        }

        private void ApplyTimeFocusExemptLayer(GameObject root)
        {
            if (root == null)
                return;

            var layer = LayerMask.NameToLayer(auraTimeFocusExemptLayerName);
            if (layer < 0)
                return;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private void CreateDebugProjectileVisual(SpellId spellId, ElementType element, Transform parent)
        {
            var color = GetElementColor(element);
            switch (spellId)
            {
                case SpellId.Single_Wave:
                    AddPrimitiveVisual("Wave_Plate", PrimitiveType.Cube, parent, Vector3.zero, new Vector3(0.48f, 0.08f, 0.18f), color);
                    break;
                case SpellId.Single_Strike:
                    AddPrimitiveVisual("Strike_Capsule", PrimitiveType.Capsule, parent, Vector3.zero, new Vector3(0.18f, 0.38f, 0.18f), color);
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                default:
                    AddPrimitiveVisual("Pointer_Sphere", PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * GetDebugColliderRadius(spellId), color);
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
                Destroy(collider);
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateUnlitMaterial(color);
        }

        private void StartCombinationAuraFeedback(SpellId spellId, bool completed)
        {
            if (!showCombinationAura || !SpellHitData.IsComboSpellId(spellId))
                return;

            currentCombinationAuraSpell = spellId;
            combinationAuraCompleted = completed;
            combinationAuraUntilTime = completed ? Time.time + combinationCompleteAuraHoldSeconds : -999f;
            EnsureCombinationAura();
            if (combinationAuraRoot != null)
            {
                combinationAuraRoot.SetActive(true);
                combinationAuraRoot.transform.position = ResolveCombinationAuraPosition();
                combinationAuraRoot.transform.localScale = Vector3.one * (completed ? combinationCompleteAuraScale : combinationReadyAuraScale);
                var aura = combinationAuraRoot.GetComponent<ElementAuraDummy>();
                if (aura != null)
                    aura.Configure(GetComboAuraColor(spellId), completed ? combinationCompleteAuraScale : combinationReadyAuraScale, auraTimeFocusExemptLayerName);
                else
                {
                    var renderer = combinationAuraRoot.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                        renderer.material = CreateUnlitMaterial(GetComboAuraColor(spellId));
                    ApplyTimeFocusExemptLayer(combinationAuraRoot);
                }
            }
        }

        private void StopCombinationAuraFeedback()
        {
            currentCombinationAuraSpell = SpellId.None;
            combinationAuraCompleted = false;
            if (combinationAuraRoot != null)
                combinationAuraRoot.SetActive(false);
        }

        // ── Voice Boost ──────────────────────────────────────────────────────────

        private void HandleVoiceCommand(ElementType element)
        {
            var armedElement = ResolveElement(currentRightGesture);
            Debug.Log($"[VoiceBoost] OnVoiceCommand received: {element} | currentGesture={currentRightGesture} | armed={armedElement} | suppressed={IsVoiceInputSuppressedForCombinationFocus()}");

            if (IsVoiceInputSuppressedForCombinationFocus()) return;

            // Only boost if the element matches the currently armed gesture
            if (armedElement == ElementType.None || armedElement != element)
            {
                Debug.Log($"[VoiceBoost] No match — gesture not active or element mismatch");
                return;
            }

            ActivateVoiceBoost(element);
        }

        private void ActivateVoiceBoost(ElementType element)
        {
            isVoiceBoostActive = true;
            voiceBoostExpiry = Time.time + voiceBoostDuration;
            elementAuraManager?.SetVoiceBoosted(true);
            PlayVoiceBoostSfx();
            lastVoiceBoostStatus = $"VoiceLink: {element} BOOSTED";
            PrototypeDebugStatus = $"CAST: VoiceBoost {element}";
        }

        private void DeactivateVoiceBoost()
        {
            if (!isVoiceBoostActive) return;
            isVoiceBoostActive = false;
            voiceBoostExpiry = -999f;
            elementAuraManager?.SetVoiceBoosted(false);
            lastVoiceBoostStatus = "VoiceLink: idle";
        }

        private void UpdateVoiceBoost()
        {
            if (!isVoiceBoostActive) return;

            // Expire if duration passed or gesture was cleared
            if (Time.time >= voiceBoostExpiry || string.IsNullOrEmpty(currentRightGesture))
                DeactivateVoiceBoost();
        }

        private void PlayVoiceBoostSfx()
        {
            if (voiceBoostAudioSource == null)
            {
                voiceBoostAudioSource = gameObject.AddComponent<AudioSource>();
                voiceBoostAudioSource.playOnAwake = false;
                voiceBoostAudioSource.spatialBlend = 0f;
                voiceBoostAudioSource.volume = 0.8f;
            }

            if (voiceBoostClip != null)
            {
                voiceBoostAudioSource.clip = voiceBoostClip;
            }
            else
            {
                if (_cachedBoostTone == null)
                    _cachedBoostTone = GenerateVoiceBoostTone();
                voiceBoostAudioSource.clip = _cachedBoostTone;
            }

            voiceBoostAudioSource.Stop();
            voiceBoostAudioSource.Play();
            Debug.Log($"[VoiceBoost] PlayVoiceBoostSfx — clip={voiceBoostAudioSource.clip?.name} volume={voiceBoostAudioSource.volume} audioListener={FindAnyObjectByType<AudioListener>() != null}");
        }

        private static AudioClip GenerateVoiceBoostTone()
        {
            const int sampleRate = 44100;
            const float duration = 0.35f;
            var samples = (int)(sampleRate * duration);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(520f, 1040f, t / duration); // rising octave
                float envelope = Mathf.Sin(Mathf.PI * t / duration); // smooth fade in+out
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.45f;
            }
            var generatedClip = AudioClip.Create("VoiceBoostTone", samples, 1, sampleRate, false);
            generatedClip.SetData(data, 0);
            return generatedClip;
        }

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Controls aura/orb visibility based on the active time-focus mode.
        ///   Grimoire time stop  → hide element aura (suppress) + hide ice orb
        ///   Combination focus   → keep element aura, hide ice orb (timeScale check already does this)
        ///   Normal              → restore suppressed state
        /// </summary>
        private void UpdateTimeFocusVisibility()
        {
            var grimoireOpen = grimoireManager != null && grimoireManager.IsOpen;

            // Aura: suppress only during grimoire (player is reading the book)
            elementAuraManager?.SetSuppressed(grimoireOpen);

            // Ice orb: hidden during ANY time stop (grimoire or combination)
            // UpdateIceOrbTransform already uses Time.timeScale < 0.5f, but
            // also handle the case where the orb exists but Update hasn't run yet
            if (rightIceOrbInstance != null)
            {
                var timeStopped = grimoireOpen || (focusModeController != null && focusModeController.IsFocusActive);
                rightIceOrbInstance.SetActive(!timeStopped);
            }
        }

        private void UpdateCombinationAuraFeedback()
        {
            if (combinationAuraRoot == null || currentCombinationAuraSpell == SpellId.None)
                return;

            combinationAuraRoot.transform.position = ResolveCombinationAuraPosition();
            if (combinationAuraCompleted && Time.time > combinationAuraUntilTime)
                StopCombinationAuraFeedback();
        }

        private void EnsureCombinationAura()
        {
            if (combinationAuraRoot != null)
                return;

            combinationAuraRoot = ElementAuraDummy
                .Create("CombinationAura_Dummy", Color.white, combinationReadyAuraScale, auraTimeFocusExemptLayerName)
                .gameObject;
            combinationAuraRoot.hideFlags = HideFlags.DontSave;
            combinationAuraRoot.SetActive(false);
        }

        private Vector3 ResolveCombinationAuraPosition()
        {
            if (leftHandSpawnPoint != null && rightHandSpawnPoint != null)
                return (leftHandSpawnPoint.position + rightHandSpawnPoint.position) * 0.5f;
            var spawn = ResolveRightSpawnPoint();
            return spawn != null ? spawn.position : transform.position;
        }

        private static Color GetComboAuraColor(SpellId spellId)
        {
            return spellId switch
            {
                SpellId.Combo_FireIce => new Color(0.9f, 0.55f, 1f, 1f),
                SpellId.Combo_IceThunder => new Color(0.35f, 0.9f, 1f, 1f),
                SpellId.Combo_ThunderFire => new Color(1f, 0.55f, 0.1f, 1f),
                _ => Color.white
            };
        }

        private bool IsVoiceInputSuppressedForCombinationFocus()
        {
            if (focusModeController == null)
                focusModeController = FindAnyObjectByType<CombinationFocusModeController>();

            return ignoreVoiceDuringCombinationFocus &&
                   focusModeController != null &&
                   focusModeController.IsFocusActive;
        }

        private bool IsCombinationCastAllowed(SpellId spellId)
        {
            return !SpellHitData.IsComboSpellId(spellId) ||
                   allowCombinationSpellCasts ||
                   (focusModeController != null && focusModeController.IsFocusActive);
        }

        private static ElementType ResolveElement(string gestureName)
        {
            return gestureName switch
            {
                "Fire" => ElementType.Fire,
                "Ice" => ElementType.Ice,
                "Thunder" => ElementType.Thunder,
                "ThunderShoot" => ElementType.Thunder,
                _ => ElementType.None
            };
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

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader);
            ApplyMaterialColor(material, color);
            return material;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }
        }
    }
}
