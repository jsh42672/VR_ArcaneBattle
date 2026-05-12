using System;
using System.Collections.Generic;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Input
{
    [DefaultExecutionOrder(85)]
    public class ArcaneActionModeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private Transform trackingSpaceRoot;

        [Header("Triangle Gesture")]
        [SerializeField] private bool enableTriangleGesture = true;
        [SerializeField] private float gestureHoldTime = 0.45f;
        [SerializeField] private float toggleCooldown = 0.8f;
        [SerializeField] private float indexTipTouchDistance = 0.095f;
        [SerializeField] private float thumbTipTouchDistance = 0.095f;
        [SerializeField] private float sameHandIndexThumbMinDistance = 0.105f;
        [SerializeField] private float triangleHeightMin = 0.09f;
        [SerializeField] private float triangleLateralOffsetMax = 0.14f;
        [SerializeField] private float indexAboveThumbMinDistance = 0.045f;
        [SerializeField] private bool requireOuterFingersOpen;
        [SerializeField] private float openFingerTipPalmMinDistance = 0.075f;
        [SerializeField] private bool requireTriangleFacingHead = true;
        [SerializeField] private float triangleFacingHeadDotMin = 0.2f;

        [Header("Cast Mode")]
        [SerializeField] private float castModeTimeout = 10f;
        [SerializeField] private bool exitAfterComboCast = true;

        [Header("Triangle Feedback")]
        [SerializeField] private bool showTriangleFeedback = true;
        [SerializeField] private float triangleFeedbackBaseSize = 0.055f;
        [SerializeField] private float triangleFeedbackReadySize = 0.18f;
        [SerializeField] private float triangleFeedbackVolume = 0.8f;
        [SerializeField] private Color triangleMoveColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] private Color triangleCastColor = new Color(1f, 0.72f, 0.18f, 0.9f);

        private readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem handSubsystem;
        private float triangleHoldTimer;
        private float lastToggleTime = -999f;
        private float castModeStartTime = -999f;
        private bool comboSubscribed;
        private bool triangleReadyForToggle = true;
        private bool wasTriangleGestureActive;
        private bool hasCurrentTriangleCenter;
        private Vector3 currentTriangleCenter;
        private GameObject triangleFeedbackRoot;
        private ParticleSystem triangleFeedbackParticles;
        private ParticleSystem triangleFeedbackBurstParticles;
        private ParticleSystemRenderer triangleFeedbackRenderer;
        private Material triangleFeedbackMaterial;
        private Light triangleFeedbackLight;
        private AudioSource triangleFeedbackAudioSource;
        private AudioClip triangleAcquireClip;
        private AudioClip triangleCastOnClip;
        private AudioClip triangleCastOffClip;

        public event Action<bool> OnCastModeChanged;

        public bool IsCastModeActive { get; private set; }
        public bool IsTriangleGestureActive { get; private set; }
        public string TriangleDebugText { get; private set; } = "Triangle: waiting";
        public float TriangleHoldProgress => gestureHoldTime <= 0f ? 1f : Mathf.Clamp01(triangleHoldTimer / gestureHoldTime);
        public string StatusText { get; private set; } = "Mode: Move";
        public string ShortStatusText => IsCastModeActive ? "Cast" : "Move";
        public float CastModeRemaining => IsCastModeActive && castModeTimeout > 0f
            ? Mathf.Max(0f, castModeTimeout - (Time.time - castModeStartTime))
            : -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForArcaneScenes()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!HandGestureDebugOverlay.IsGestureOverlayScene(sceneName))
                return;

            if (FindAnyObjectByType<ArcaneActionModeController>() != null)
                return;

            var host = GameObject.Find("Arcane Test Hub") ??
                       GameObject.Find("MagicSystemTestDriver") ??
                       new GameObject("Arcane Action Mode Controller");
            host.AddComponent<ArcaneActionModeController>();
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshHandSubsystem();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeCombo();
        }

        private void OnDisable()
        {
            UnsubscribeCombo();
            SetCastMode(false, "Mode: disabled");
        }

        private void Update()
        {
            ResolveReferences();
            RefreshHandSubsystem();
            UpdateTriangleGesture();
            UpdateCastModeTimeout();
            ApplyPullSuppression();
        }

        public void SetCastMode(bool active, string reason)
        {
            if (IsCastModeActive == active)
            {
                StatusText = reason;
                ApplyPullSuppression();
                return;
            }

            IsCastModeActive = active;
            castModeStartTime = active ? Time.time : -999f;
            StatusText = reason;
            PlayTriangleModeChangedFeedback(active);
            OnCastModeChanged?.Invoke(IsCastModeActive);
            ApplyPullSuppression();
        }

        public void ToggleCastMode()
        {
            SetCastMode(!IsCastModeActive, IsCastModeActive ? "Mode: Move by triangle" : "Mode: Cast by triangle");
        }

        private void ResolveReferences()
        {
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (trackingSpaceRoot == null)
            {
                var cameraRig = FindAnyObjectByType<OVRCameraRig>();
                if (cameraRig != null)
                    trackingSpaceRoot = cameraRig.trackingSpace != null ? cameraRig.trackingSpace : cameraRig.transform;
            }

            if (combinationChecker == null)
            {
                combinationChecker = FindAnyObjectByType<CombinationChecker>();
                SubscribeCombo();
            }
        }

        private void SubscribeCombo()
        {
            if (comboSubscribed || combinationChecker == null)
                return;

            combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
            comboSubscribed = true;
        }

        private void UnsubscribeCombo()
        {
            if (!comboSubscribed || combinationChecker == null)
                return;

            combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;
            comboSubscribed = false;
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            if (!exitAfterComboCast || !IsCastModeActive || !IsComboSpell(spellId))
                return;

            SetCastMode(false, $"Mode: Move after {spellId}");
            triangleHoldTimer = 0f;
            lastToggleTime = Time.time;
        }

        private void UpdateTriangleGesture()
        {
            var active = enableTriangleGesture && IsTriangleGestureDetected();
            IsTriangleGestureActive = active;

            if (!active)
            {
                if (wasTriangleGestureActive)
                    StopTriangleFeedback();

                wasTriangleGestureActive = false;
                triangleHoldTimer = 0f;
                triangleReadyForToggle = true;
                return;
            }

            if (!wasTriangleGestureActive)
                PlayTriangleAcquireFeedback();

            wasTriangleGestureActive = true;
            triangleHoldTimer += Time.deltaTime;
            UpdateTriangleFeedback();
            StatusText = IsCastModeActive
                ? $"Mode: Cast triangle {TriangleHoldProgress:0.0}"
                : $"Mode: Move triangle {TriangleHoldProgress:0.0}";

            if (!triangleReadyForToggle ||
                triangleHoldTimer < gestureHoldTime ||
                Time.time - lastToggleTime < toggleCooldown)
            {
                return;
            }

            lastToggleTime = Time.time;
            triangleHoldTimer = 0f;
            triangleReadyForToggle = false;
            ToggleCastMode();
        }

        private void UpdateCastModeTimeout()
        {
            if (!IsCastModeActive || castModeTimeout <= 0f)
                return;

            if (Time.time - castModeStartTime < castModeTimeout)
                return;

            SetCastMode(false, "Mode: Move by timeout");
        }

        private void ApplyPullSuppression()
        {
            if (handPullMovement == null)
                return;

            handPullMovement.SetMovementSuppressed(IsCastModeActive, "Cast Mode");
        }

        private void UpdateTriangleFeedback()
        {
            if (!showTriangleFeedback || !hasCurrentTriangleCenter)
                return;

            EnsureTriangleFeedback();
            if (triangleFeedbackRoot == null)
                return;

            if (!triangleFeedbackRoot.activeSelf)
                triangleFeedbackRoot.SetActive(true);

            if (trackingSpaceRoot != null && triangleFeedbackRoot.transform.parent != trackingSpaceRoot)
                triangleFeedbackRoot.transform.SetParent(trackingSpaceRoot, false);
            else if (trackingSpaceRoot == null && triangleFeedbackRoot.transform.parent != null)
                triangleFeedbackRoot.transform.SetParent(null, true);

            if (trackingSpaceRoot != null)
                triangleFeedbackRoot.transform.localPosition = currentTriangleCenter;
            else
                triangleFeedbackRoot.transform.position = currentTriangleCenter;

            triangleFeedbackRoot.transform.localRotation = Quaternion.identity;

            var progress = TriangleHoldProgress;
            var color = Color.Lerp(triangleMoveColor, triangleCastColor, IsCastModeActive ? 1f : progress);
            ApplyTriangleFeedbackColor(color, progress);

            if (triangleFeedbackParticles != null && !triangleFeedbackParticles.isPlaying)
                triangleFeedbackParticles.Play(true);
        }

        private void EnsureTriangleFeedback()
        {
            if (triangleFeedbackRoot != null)
                return;

            triangleFeedbackRoot = new GameObject("Arcane Triangle Mode Feedback")
            {
                hideFlags = HideFlags.DontSave
            };

            if (trackingSpaceRoot != null)
                triangleFeedbackRoot.transform.SetParent(trackingSpaceRoot, false);

            triangleFeedbackParticles = triangleFeedbackRoot.AddComponent<ParticleSystem>();
            ConfigureTriangleFeedbackParticles(triangleFeedbackParticles);

            triangleFeedbackRenderer = triangleFeedbackRoot.GetComponent<ParticleSystemRenderer>();
            triangleFeedbackMaterial = CreateTriangleFeedbackMaterial(new Color(1f, 1f, 1f, 0.85f));
            if (triangleFeedbackRenderer != null && triangleFeedbackMaterial != null)
            {
                triangleFeedbackRenderer.material = triangleFeedbackMaterial;
                triangleFeedbackRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                triangleFeedbackRenderer.sortingFudge = 4f;
                triangleFeedbackRenderer.maxParticleSize = 0.42f;
            }
            else if (triangleFeedbackRenderer != null)
            {
                triangleFeedbackRenderer.enabled = false;
            }

            triangleFeedbackBurstParticles = CreateTriangleBurstParticles(triangleFeedbackRoot.transform, triangleFeedbackMaterial);

            triangleFeedbackLight = triangleFeedbackRoot.AddComponent<Light>();
            triangleFeedbackLight.type = LightType.Point;
            triangleFeedbackLight.range = 0.7f;
            triangleFeedbackLight.intensity = 0.8f;

            triangleFeedbackAudioSource = triangleFeedbackRoot.AddComponent<AudioSource>();
            ConfigureTriangleAudioSource(triangleFeedbackAudioSource);
            triangleFeedbackRoot.SetActive(false);
        }

        private void ConfigureTriangleFeedbackParticles(ParticleSystem particles)
        {
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.38f;
            main.startSpeed = 0.045f;
            main.startSize = triangleFeedbackBaseSize;
            main.startColor = triangleMoveColor;
            main.maxParticles = 260;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 70f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.08f;
            shape.radiusThickness = 0.35f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 1.8f;
            noise.scrollSpeed = 0.4f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0.08f)));
        }

        private ParticleSystem CreateTriangleBurstParticles(Transform parent, Material material)
        {
            var burstObject = new GameObject("Arcane Triangle Mode Burst")
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.48f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = triangleCastColor;
            main.maxParticles = 160;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.035f;

            var renderer = burstObject.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && material != null)
            {
                renderer.material = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = 5f;
                renderer.maxParticleSize = 0.45f;
            }
            else if (renderer != null)
            {
                renderer.enabled = false;
            }

            return particles;
        }

        private void ApplyTriangleFeedbackColor(Color color, float progress)
        {
            var size = Mathf.Lerp(triangleFeedbackBaseSize, triangleFeedbackReadySize, progress);
            var alphaColor = color;
            alphaColor.a = Mathf.Clamp01(0.55f + progress * 0.35f);

            if (triangleFeedbackParticles != null)
            {
                var main = triangleFeedbackParticles.main;
                main.startColor = alphaColor;
                main.startSize = Mathf.Max(0.025f, size);
                main.startSpeed = Mathf.Lerp(0.035f, 0.12f, progress);

                var emission = triangleFeedbackParticles.emission;
                emission.rateOverTime = Mathf.Lerp(70f, 240f, progress);

                var shape = triangleFeedbackParticles.shape;
                shape.radius = Mathf.Lerp(0.08f, 0.18f, progress);
            }

            if (triangleFeedbackBurstParticles != null)
            {
                var main = triangleFeedbackBurstParticles.main;
                main.startColor = alphaColor;
            }

            if (triangleFeedbackMaterial != null)
            {
                if (triangleFeedbackMaterial.HasProperty("_BaseColor"))
                    triangleFeedbackMaterial.SetColor("_BaseColor", alphaColor);
                if (triangleFeedbackMaterial.HasProperty("_Color"))
                    triangleFeedbackMaterial.SetColor("_Color", alphaColor);
            }

            if (triangleFeedbackLight != null)
            {
                triangleFeedbackLight.color = color;
                triangleFeedbackLight.range = Mathf.Lerp(0.7f, 1.35f, progress);
                triangleFeedbackLight.intensity = Mathf.Lerp(0.8f, 2.6f, progress);
            }
        }

        private void PlayTriangleAcquireFeedback()
        {
            if (!showTriangleFeedback)
                return;

            EnsureTriangleFeedback();
            triangleAcquireClip ??= CreateTriangleFeedbackClip(false, false);
            PlayTriangleClip(triangleAcquireClip, 0.45f);
        }

        private void PlayTriangleModeChangedFeedback(bool castModeActive)
        {
            if (!showTriangleFeedback || !isActiveAndEnabled || Time.timeSinceLevelLoad < 0.2f)
                return;

            EnsureTriangleFeedback();
            if (triangleFeedbackRoot != null && !triangleFeedbackRoot.activeSelf)
                triangleFeedbackRoot.SetActive(true);

            triangleFeedbackBurstParticles?.Emit(castModeActive ? 120 : 70);
            var clip = castModeActive
                ? triangleCastOnClip
                : triangleCastOffClip;
            if (clip == null)
            {
                clip = CreateTriangleFeedbackClip(true, castModeActive);
                if (castModeActive)
                    triangleCastOnClip = clip;
                else
                    triangleCastOffClip = clip;
            }
            PlayTriangleClip(clip, castModeActive ? 0.95f : 0.65f);
        }

        private void PlayTriangleClip(AudioClip clip, float volumeScale)
        {
            if (clip == null)
                return;

            EnsureTriangleFeedback();
            if (triangleFeedbackAudioSource == null)
                return;

            ConfigureTriangleAudioSource(triangleFeedbackAudioSource);
            triangleFeedbackAudioSource.PlayOneShot(clip, Mathf.Clamp01(triangleFeedbackVolume * volumeScale));
        }

        private static void ConfigureTriangleAudioSource(AudioSource audioSource)
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 0.1f;
            audioSource.maxDistance = 6f;
            audioSource.dopplerLevel = 0f;
            audioSource.ignoreListenerPause = true;
            audioSource.mute = false;
            audioSource.volume = 1f;
        }

        private void StopTriangleFeedback()
        {
            if (triangleFeedbackParticles != null)
                triangleFeedbackParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (triangleFeedbackRoot != null)
                triangleFeedbackRoot.SetActive(false);
        }

        private void RefreshHandSubsystem()
        {
            if (handSubsystem != null && handSubsystem.running)
                return;

            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            handSubsystem = null;

            foreach (var subsystem in handSubsystems)
            {
                if (subsystem == null || !subsystem.running)
                    continue;

                handSubsystem = subsystem;
                return;
            }

            if (handSubsystems.Count > 0)
                handSubsystem = handSubsystems[0];
        }

        private bool IsTriangleGestureDetected()
        {
            hasCurrentTriangleCenter = false;

            if (handSubsystem == null || !handSubsystem.running)
                return RejectTriangle("Triangle: no XR hand subsystem");

            var left = handSubsystem.leftHand;
            var right = handSubsystem.rightHand;
            if (!left.isTracked || !right.isTracked)
                return RejectTriangle("Triangle: hands not tracked");

            if (!TryGetTriangleJoints(left, out var leftPalm, out var leftThumb, out var leftIndex, out var leftMiddle, out var leftRing, out var leftLittle) ||
                !TryGetTriangleJoints(right, out var rightPalm, out var rightThumb, out var rightIndex, out var rightMiddle, out var rightRing, out var rightLittle))
            {
                return RejectTriangle("Triangle: missing joints");
            }

            var indexDistance = Vector3.Distance(leftIndex, rightIndex);
            var thumbDistance = Vector3.Distance(leftThumb, rightThumb);
            var leftFrameSide = Vector3.Distance(leftIndex, leftThumb);
            var rightFrameSide = Vector3.Distance(rightIndex, rightThumb);
            var indexMidpoint = (leftIndex + rightIndex) * 0.5f;
            var thumbMidpoint = (leftThumb + rightThumb) * 0.5f;
            var triangleHeight = Vector3.Distance(indexMidpoint, thumbMidpoint);

            if (indexDistance > indexTipTouchDistance)
                return RejectTriangle($"Triangle: index {indexDistance:0.00}/{indexTipTouchDistance:0.00}");

            if (thumbDistance > thumbTipTouchDistance)
                return RejectTriangle($"Triangle: thumb {thumbDistance:0.00}/{thumbTipTouchDistance:0.00}");

            if (leftFrameSide < sameHandIndexThumbMinDistance || rightFrameSide < sameHandIndexThumbMinDistance)
                return RejectTriangle($"Triangle: side L{leftFrameSide:0.00} R{rightFrameSide:0.00}");

            if (triangleHeight < triangleHeightMin)
                return RejectTriangle($"Triangle: height {triangleHeight:0.00}/{triangleHeightMin:0.00}");

            if (!IsViewfinderGeometry(indexMidpoint, thumbMidpoint))
                return false;

            if (!IsTriangleFacingHead(leftIndex, rightIndex, leftThumb, rightThumb))
                return false;

            if (requireOuterFingersOpen &&
                (!AreNonFrameFingersOpen(leftPalm, leftMiddle, leftRing, leftLittle) ||
                 !AreNonFrameFingersOpen(rightPalm, rightMiddle, rightRing, rightLittle)))
            {
                return RejectTriangle("Triangle: outer fingers");
            }

            currentTriangleCenter = (leftIndex + rightIndex + leftThumb + rightThumb) * 0.25f;
            hasCurrentTriangleCenter = true;
            TriangleDebugText = $"Triangle: active h{triangleHeight:0.00} i{indexDistance:0.00} t{thumbDistance:0.00}";
            return true;
        }

        private bool IsViewfinderGeometry(Vector3 indexMidpoint, Vector3 thumbMidpoint)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null)
                return true;

            var indexWorld = ToWorldPoint(indexMidpoint);
            var thumbWorld = ToWorldPoint(thumbMidpoint);
            var indexToThumb = indexWorld - thumbWorld;
            var vertical = Vector3.Dot(indexToThumb, head.up);
            if (vertical < indexAboveThumbMinDistance)
                return RejectTriangle($"Triangle: apex below {vertical:0.00}/{indexAboveThumbMinDistance:0.00}");

            var lateral = Mathf.Abs(Vector3.Dot(indexToThumb, head.right));
            if (lateral > triangleLateralOffsetMax)
                return RejectTriangle($"Triangle: skew {lateral:0.00}/{triangleLateralOffsetMax:0.00}");

            return true;
        }

        private bool IsTriangleFacingHead(Vector3 leftIndex, Vector3 rightIndex, Vector3 leftThumb, Vector3 rightThumb)
        {
            if (!requireTriangleFacingHead)
                return true;

            var indexLine = rightIndex - leftIndex;
            var thumbMidpoint = (leftThumb + rightThumb) * 0.5f;
            var indexMidpoint = (leftIndex + rightIndex) * 0.5f;
            var frameHeight = thumbMidpoint - indexMidpoint;
            var normal = ToWorldVector(Vector3.Cross(indexLine, frameHeight));
            if (normal.sqrMagnitude <= 0.0001f)
                return RejectTriangle("Triangle: flat frame");

            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null)
                return true;

            var triangleCenter = ToWorldPoint((leftIndex + rightIndex + leftThumb + rightThumb) * 0.25f);
            var toHead = head.position - triangleCenter;
            if (toHead.sqrMagnitude <= 0.0001f)
                return true;

            var facingDot = Mathf.Abs(Vector3.Dot(normal.normalized, toHead.normalized));
            if (facingDot < triangleFacingHeadDotMin)
                return RejectTriangle($"Triangle: facing {facingDot:0.00}/{triangleFacingHeadDotMin:0.00}");

            return true;
        }

        private Vector3 ToWorldPoint(Vector3 trackingPoint)
        {
            return trackingSpaceRoot != null ? trackingSpaceRoot.TransformPoint(trackingPoint) : trackingPoint;
        }

        private Vector3 ToWorldVector(Vector3 trackingVector)
        {
            return trackingSpaceRoot != null ? trackingSpaceRoot.TransformVector(trackingVector) : trackingVector;
        }

        private bool RejectTriangle(string reason)
        {
            TriangleDebugText = reason;
            return false;
        }

        private static bool TryGetTriangleJoints(
            XRHand hand,
            out Vector3 palm,
            out Vector3 thumbTip,
            out Vector3 indexTip,
            out Vector3 middleTip,
            out Vector3 ringTip,
            out Vector3 littleTip)
        {
            palm = thumbTip = indexTip = middleTip = ringTip = littleTip = Vector3.zero;

            return TryGetJointPosition(hand, XRHandJointID.Palm, out palm) &&
                   TryGetJointPosition(hand, XRHandJointID.ThumbTip, out thumbTip) &&
                   TryGetJointPosition(hand, XRHandJointID.IndexTip, out indexTip) &&
                   TryGetJointPosition(hand, XRHandJointID.MiddleTip, out middleTip) &&
                   TryGetJointPosition(hand, XRHandJointID.RingTip, out ringTip) &&
                   TryGetJointPosition(hand, XRHandJointID.LittleTip, out littleTip);
        }

        private bool AreNonFrameFingersOpen(Vector3 palm, Vector3 middleTip, Vector3 ringTip, Vector3 littleTip)
        {
            return Vector3.Distance(middleTip, palm) >= openFingerTipPalmMinDistance &&
                   Vector3.Distance(ringTip, palm) >= openFingerTipPalmMinDistance &&
                   Vector3.Distance(littleTip, palm) >= openFingerTipPalmMinDistance;
        }

        private static bool TryGetJointPosition(XRHand hand, XRHandJointID jointId, out Vector3 position)
        {
            position = Vector3.zero;

            var joint = hand.GetJoint(jointId);
            if (!joint.TryGetPose(out var pose))
                return false;

            position = pose.position;
            return true;
        }

        private static Material CreateTriangleFeedbackMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "ArcaneRuntimeTriangleFeedbackMaterial",
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

            var texture = CreateTriangleParticleTexture();
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            return material;
        }

        private static Texture2D CreateTriangleParticleTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ArcaneRuntimeTriangleParticleTexture",
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
                    alpha *= alpha * alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static AudioClip CreateTriangleFeedbackClip(bool toggle, bool castModeOn)
        {
            const int sampleRate = 44100;
            var length = toggle ? 0.36f : 0.16f;
            var sampleCount = Mathf.CeilToInt(sampleRate * length);
            var samples = new float[sampleCount];
            var startFrequency = toggle
                ? castModeOn ? 360f : 680f
                : 540f;
            var endFrequency = toggle
                ? castModeOn ? 880f : 320f
                : 720f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / sampleRate;
                var n = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                var attack = Mathf.Clamp01(n / 0.05f);
                var decay = 1f - Mathf.SmoothStep(toggle ? 0.55f : 0.4f, 1f, n);
                var envelope = attack * decay;
                var frequency = Mathf.Lerp(startFrequency, endFrequency, Mathf.SmoothStep(0f, 1f, n));
                var tone = Mathf.Sin(Tau * frequency * t);
                var overtone = Mathf.Sin(Tau * frequency * 1.5f * t) * 0.26f;
                var shimmer = Mathf.Sin(Tau * frequency * 2.25f * t) * 0.14f * (1f - n);
                var click = toggle
                    ? Mathf.Sin(Tau * 1600f * t) * Mathf.Clamp01(1f - n / 0.12f) * 0.12f
                    : 0f;
                samples[i] = SoftLimit((tone + overtone + shimmer + click) * envelope * (toggle ? 0.72f : 0.42f));
            }

            var clip = AudioClip.Create(toggle ? "ArcaneTriangleModeToggle" : "ArcaneTriangleAcquire", sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static float SoftLimit(float value)
        {
            return (float)Math.Tanh(value * 1.25f) * 0.9f;
        }

        private static bool IsComboSpell(SpellId spellId)
        {
            return spellId == SpellId.Combo_FireIce ||
                   spellId == SpellId.Combo_IceThunder ||
                   spellId == SpellId.Combo_ThunderFire;
        }

        private const float Tau = 6.28318530718f;
    }
}
