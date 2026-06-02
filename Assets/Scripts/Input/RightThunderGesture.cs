using System.Collections;
using System.Collections.Generic;
using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Right-hand thunder prototype gesture. Holding the charge shape shows a dummy aura,
    /// then the shoot shape fires a short-lived 20m laser ray.
    /// </summary>
    public class RightThunderGesture : MonoBehaviour
    {
        public const float defaultRangeMeters = 20f;
        public const float defaultDamage = 16f;
        public const float defaultChargeGraceSeconds = 0.4f;
        public const float defaultContinuousFireSeconds = 5f;
        public const float defaultShootPoseGraceSeconds = 0.2f;
        public const float defaultLaserDownAngleDegrees = 8f;

        [Header("XR Hands")]
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [SerializeField] private XRHandShape thunderShape;
        [SerializeField] private XRHandShape thunderShootShape;
        [SerializeField] private Transform wristTransform;

        [Header("Aura")]
        [SerializeField] private GameObject auraPrefab;
        [SerializeField] private AudioClip auraAudioClip;
        [SerializeField] private Vector3 auraOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float auraScale = 0.16f;

        [Header("Laser")]
        [SerializeField] private float rangeMeters = defaultRangeMeters;
        [SerializeField] private float damage = defaultDamage;
        [SerializeField] private float statusDuration = 2.5f;
        [SerializeField] private float chargeGraceSeconds = defaultChargeGraceSeconds;
        [SerializeField] private float continuousFireSeconds = defaultContinuousFireSeconds;
        [SerializeField] private float shootPoseGraceSeconds = defaultShootPoseGraceSeconds;
        [SerializeField] private float hitTickInterval = 0.25f;
        [SerializeField] private float laserWidth = 0.035f;
        [SerializeField] private float laserDownAngleDegrees = defaultLaserDownAngleDegrees;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Color laserColor = new Color(1f, 0.88f, 0.15f, 1f);

        [Header("Debug")]
        [SerializeField] private bool debugLog;
        [SerializeField] private bool tuningFeedback = true;
        [SerializeField] private float tuningLogInterval = 0.5f;
        [SerializeField] private Color chargeFeedbackColor = new Color(1f, 0.88f, 0.15f, 0.9f);
        [SerializeField] private Color shootFeedbackColor = new Color(0.35f, 0.95f, 1f, 1f);

        [Header("Events")]
        public UnityEvent onAuraStart;
        public UnityEvent onAuraEnd;
        public UnityEvent<Vector3> onShot;

        private GameObject auraInstance;
        private AudioSource auraAudioSource;
        private bool auraActive;
        private bool previousFireEligible;
        private bool charged;
        private float lastChargeDetectedTime = -999f;
        private float lastShootDetectedTime = -999f;
        private float beamEndTime = -999f;
        private float nextHitTime = -999f;
        private float nextTuningLogTime;
        private Renderer auraRenderer;
        private GameObject beamInstance;
        private LineRenderer beamLine;

        public static SpellHitData CreateDefaultHitData()
        {
            return new SpellHitData(
                SpellId.Single_Strike,
                ElementType.Thunder,
                StatusEffect.Stagger,
                defaultDamage,
                2.5f,
                1f,
                0f);
        }

        private void OnEnable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);

            StartCoroutine(ReconnectSubsystem());
        }

        private void OnDisable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);

            SetAuraActive(false);
            StopBeam();
            previousFireEligible = false;
        }

        private IEnumerator ReconnectSubsystem()
        {
            var subsystems = new List<XRHandSubsystem>();
            while (true)
            {
                SubsystemManager.GetSubsystems(subsystems);
                if (subsystems.Count > 0 && subsystems[0].running)
                    break;

                subsystems.Clear();
                yield return new WaitForSeconds(0.5f);
            }

            if (handTrackingEvents != null)
            {
                handTrackingEvents.enabled = false;
                handTrackingEvents.enabled = true;
            }
        }

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (!isActiveAndEnabled)
                return;

            var isTracked = args.hand.isTracked;
            var chargeDetected = isTracked &&
                                 thunderShape != null &&
                                 thunderShape.CheckConditions(args);
            var shootDetected = isTracked &&
                                thunderShootShape != null &&
                                thunderShootShape.CheckConditions(args);

            if (shootDetected)
                lastShootDetectedTime = Time.time;

            if (chargeDetected)
                lastChargeDetectedTime = Time.time;

            charged = chargeDetected ||
                      Time.time - lastChargeDetectedTime <= Mathf.Max(0f, chargeGraceSeconds);

            SetAuraActive(charged);
            UpdateTuningFeedback(args.hand, chargeDetected, charged, shootDetected);

            var fireEligible = charged && shootDetected;
            if (fireEligible && !previousFireEligible)
                StartBeamWindow();

            var shootPoseMaintained = shootDetected ||
                                      Time.time - lastShootDetectedTime <= Mathf.Max(0f, shootPoseGraceSeconds);
            if (Time.time <= beamEndTime && shootPoseMaintained)
                UpdateBeam(args, true);
            else
                StopBeam();

            previousFireEligible = fireEligible;
        }

        private void SetAuraActive(bool active)
        {
            if (auraActive == active)
            {
                if (active)
                    UpdateAuraTransform();
                return;
            }

            auraActive = active;
            if (auraActive)
            {
                ShowAura();
                onAuraStart?.Invoke();
            }
            else
            {
                HideAura();
                onAuraEnd?.Invoke();
            }
        }

        private void ShowAura()
        {
            if (auraInstance != null)
                Destroy(auraInstance);

            if (auraPrefab != null)
            {
                auraInstance = Instantiate(auraPrefab);
            }
            else
            {
                auraInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                auraInstance.name = "ThunderAura_Dummy";
                var collider = auraInstance.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = false;

                var renderer = auraInstance.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = CreateLaserMaterial(laserColor);
            }

            auraInstance.transform.localScale = Vector3.one * auraScale;
            auraAudioSource = auraInstance.GetComponent<AudioSource>() ?? auraInstance.AddComponent<AudioSource>();
            auraAudioSource.playOnAwake = false;
            auraAudioSource.loop = true;
            auraAudioSource.clip = auraAudioClip;
            if (auraAudioClip != null)
                auraAudioSource.Play();

            auraRenderer = auraInstance.GetComponentInChildren<Renderer>();
            ApplyAuraColor(chargeFeedbackColor);
            UpdateAuraTransform();
        }

        private void HideAura()
        {
            if (auraInstance != null)
                Destroy(auraInstance);

            auraInstance = null;
            auraAudioSource = null;
            auraRenderer = null;
        }

        private void UpdateAuraTransform()
        {
            if (auraInstance == null)
                return;

            var pose = GetWristPose();
            auraInstance.transform.SetPositionAndRotation(
                pose.position + pose.rotation * auraOffset,
                pose.rotation);
        }

        private void StartBeamWindow()
        {
            beamEndTime = Time.time + Mathf.Max(0f, continuousFireSeconds);
            nextHitTime = -999f;
        }

        private void UpdateBeam(XRHandJointsUpdatedEventArgs args, bool canApplyHit)
        {
            if (Time.time > beamEndTime)
            {
                StopBeam();
                return;
            }

            var pose = GetWristPose(args);
            var origin = pose.position + pose.rotation * auraOffset;
            var direction = pose.rotation * Vector3.forward;
            if (direction.sqrMagnitude < 0.001f)
                direction = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;

            direction = Vector3.RotateTowards(
                direction.normalized,
                Vector3.down,
                Mathf.Max(0f, laserDownAngleDegrees) * Mathf.Deg2Rad,
                0f);
            direction.Normalize();

            var hitPoint = origin + direction * rangeMeters;
            Collider hitCollider = null;
            if (Physics.Raycast(origin, direction, out var hit, rangeMeters, hitMask, QueryTriggerInteraction.Collide))
            {
                hitPoint = hit.point;
                hitCollider = hit.collider;
            }

            EnsureBeam();
            beamLine.SetPosition(0, origin);
            beamLine.SetPosition(1, hitPoint);

            if (canApplyHit && Time.time >= nextHitTime)
            {
                nextHitTime = Time.time + Mathf.Max(0.05f, hitTickInterval);
                ApplyHit(hitCollider);
                onShot?.Invoke(direction);
            }

            if (debugLog)
                Debug.Log($"[Thunder] Beam active range={(hitPoint - origin).magnitude:F1}m", this);
        }

        private void EnsureBeam()
        {
            if (beamLine != null)
                return;

            beamInstance = new GameObject("ThunderLaser_Dummy");
            beamLine = beamInstance.AddComponent<LineRenderer>();
            beamLine.positionCount = 2;
            beamLine.startWidth = laserWidth;
            beamLine.endWidth = laserWidth * 0.45f;
            beamLine.material = CreateLaserMaterial(laserColor);
            beamLine.startColor = laserColor;
            beamLine.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.15f);
        }

        private void StopBeam()
        {
            beamEndTime = -999f;
            nextHitTime = -999f;

            if (beamInstance != null)
                Destroy(beamInstance);

            beamInstance = null;
            beamLine = null;
        }

        private void FireLaser(XRHandJointsUpdatedEventArgs args)
        {
            beamEndTime = Time.time + Mathf.Max(0f, continuousFireSeconds);
            UpdateBeam(args, true);
        }

        private bool IsBeamActive => beamLine != null;

        private void UpdateTuningFeedback(in XRHand hand, bool chargeDetected, bool isCharged, bool shootDetected)
        {
            if (!tuningFeedback)
                return;

            if (auraRenderer != null)
                ApplyAuraColor(shootDetected ? shootFeedbackColor : chargeFeedbackColor);

            if (Time.time < nextTuningLogTime)
                return;

            nextTuningLogTime = Time.time + Mathf.Max(0.1f, tuningLogInterval);
            var chargeReport = XRHandShapeTuningUtility.BuildCompactReport(hand, thunderShape);
            var shootReport = XRHandShapeTuningUtility.BuildCompactReport(hand, thunderShootShape);
            Debug.Log(
                $"[Thunder Tune] tracked={hand.isTracked} charge={chargeDetected} charged={isCharged} shoot={shootDetected} " +
                $"charge({chargeReport}) shoot({shootReport}) " +
                $"chargeShape={(thunderShape != null ? thunderShape.name : "null")} " +
                $"shootShape={(thunderShootShape != null ? thunderShootShape.name : "null")}",
                this);
        }

        private void ApplyAuraColor(Color color)
        {
            if (auraRenderer == null)
                return;

            var material = auraRenderer.material;
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

        private void ApplyHit(Collider hitCollider)
        {
            if (hitCollider == null || ArcanePlayerRigResolver.IsPlayerCollider(hitCollider))
                return;

            var hitData = new SpellHitData(
                SpellId.Single_Strike,
                ElementType.Thunder,
                StatusEffect.Stagger,
                damage,
                statusDuration,
                1f,
                0f);

            var spellTarget = hitCollider.GetComponentInParent<ISpellTarget>();
            if (spellTarget != null)
            {
                spellTarget.OnHit(hitData);
                return;
            }

            var boss = hitCollider.GetComponentInParent<BossAI>();
            if (boss == null)
                return;

            var golemTarget = boss.GetComponent<GolemCombatTarget>() ?? boss.GetComponentInParent<GolemCombatTarget>();
            if (golemTarget != null)
                golemTarget.OnHit(hitData);
        }

        private Pose GetWristPose()
        {
            if (wristTransform != null)
                return new Pose(wristTransform.position, wristTransform.rotation);

            return new Pose(transform.position, transform.rotation);
        }

        private Pose GetWristPose(XRHandJointsUpdatedEventArgs args)
        {
            if (wristTransform != null)
                return new Pose(wristTransform.position, wristTransform.rotation);

            if (args.hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose))
                return wristPose;

            return new Pose(transform.position, transform.rotation);
        }

        private static Material CreateLaserMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }

            return material;
        }
    }
}
