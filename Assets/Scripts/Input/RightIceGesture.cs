using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace ArcaneVR.Input
{
    /// <summary>
    /// 오른손 Snow 포즈 → 손 앞에 얼음 구체 생성.
    /// 포즈를 유지한 채 앞으로 빠르게 던지면 발사.
    /// 포즈가 오래 끊기면 구체 소멸.
    /// </summary>
    public class RightIceGesture : MonoBehaviour
    {
        [Header("XR Hands")]
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [SerializeField] private XRHandShape snowShape;
        [Tooltip("Optional shape that suppresses ice while a higher-priority gesture is active.")]
        [SerializeField] private XRHandShape blockedByShape;
        [Tooltip("Right Hand Tracking > R_Wrist Transform")]
        [SerializeField] private Transform wristTransform;

        [Header("포즈 유예")]
        [Tooltip("포즈 상실 후 구체를 유지할 시간(초). 던지는 순간 살짝 끊겨도 발사 가능하게.")]
        [SerializeField] private float poseLostGracePeriod = 0.5f;

        [Header("얼음 구체 VFX")]
        [Tooltip("손 앞에 떠있을 얼음 구체 프리팹 (없으면 흰 구체 대체)")]
        [SerializeField] private GameObject iceOrbPrefab;
        [SerializeField] private float orbScale = 0.12f;
        [Tooltip("손목 기준 오프셋 (로컬 좌표)")]
        [SerializeField] private Vector3 palmOffset = new Vector3(0f, 0f, 0.1f);

        [Header("발사체")]
        [Tooltip("발사될 얼음 마법 프리팹 (SpellProjectile 포함 권장)")]
        [SerializeField] private GameObject iceProjectilePrefab;
        [SerializeField] private float projectileScale = 0.12f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float iceBallLifetime = 6f;
        [SerializeField] private bool useArcTrajectory = true;
        [SerializeField] private float arcVelocityMultiplier = 2.2f;
        [SerializeField] private float arcUpwardBoost = 2.0f;
        [SerializeField] private float minArcLaunchSpeed = 7f;
        [SerializeField] private float maxArcLaunchSpeed = 15f;

        [Header("던지기 감지")]
        [Tooltip("카메라 전방 속도가 이 값 이상이면 발사 (m/s)")]
        [SerializeField] private float minThrowSpeed = 2.0f;
        [Tooltip("발사 후 재소환 쿨다운 (초)")]
        [SerializeField] private float launchCooldown = 1.2f;
        [Tooltip("속도 샘플링 구간 (초)")]
        [SerializeField] private float velocitySampleDuration = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;
        [SerializeField] private bool tuningFeedback;
        [SerializeField] private float tuningLogInterval = 0.5f;

        [Header("Events")]
        public UnityEvent onGrab;
        public UnityEvent onRelease;
        public UnityEvent<Vector3> onThrow;

        // ─── 상태 ───────────────────────────────────────
        private bool _poseDetected;      // 현재 프레임 포즈 감지 여부
        private bool _orbActive;         // 구체가 활성화 상태인지
        private float _poseLostTime = -1f;
        private float _lastLaunchTime = -999f;
        private float _nextTuningLogTime;

        private GameObject _orbInstance;

        // 속도 샘플링 (플레이어/카메라 기준 로컬 위치)
        private readonly Queue<(float time, Vector3 pos)> _wristSamples = new();

        // ─── 생명주기 ────────────────────────────────────

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
            HideOrb();
        }

        private IEnumerator ReconnectSubsystem()
        {
            var subsystems = new List<XRHandSubsystem>();
            while (true)
            {
                SubsystemManager.GetSubsystems(subsystems);
                if (subsystems.Count > 0 && subsystems[0].running) break;
                subsystems.Clear();
                yield return new WaitForSeconds(0.5f);
            }
            if (handTrackingEvents != null)
            {
                handTrackingEvents.enabled = false;
                handTrackingEvents.enabled = true;
                if (debugLog) Debug.Log("[얼음] XRHandTrackingEvents 재연결 완료", this);
            }
        }

        // ─── 조인트 업데이트 ────────────────────────────

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (!isActiveAndEnabled) return;

            var blocked = args.hand.isTracked &&
                          blockedByShape != null &&
                          blockedByShape.CheckConditions(args);

            _poseDetected = !blocked &&
                            args.hand.isTracked &&
                            snowShape != null && snowShape.CheckConditions(args);

            LogTuningState(args.hand, blocked, _poseDetected);

            if (_poseDetected)
            {
                _poseLostTime = -1f;

                if (!_orbActive && Time.time - _lastLaunchTime > launchCooldown)
                {
                    _orbActive = true;
                    ShowOrb();
                    onGrab?.Invoke();
                    if (debugLog) Debug.Log("[얼음] 쥐기 포즈 인식 — 얼음 구체 생성", this);
                }
            }
            else if (_orbActive)
            {
                // Grace Period 시작
                if (_poseLostTime < 0f)
                {
                    _poseLostTime = Time.time;
                    if (debugLog) Debug.Log($"[얼음] 포즈 상실 — {poseLostGracePeriod:F1}초 유예", this);
                }

                if (Time.time - _poseLostTime >= poseLostGracePeriod)
                {
                    _orbActive = false;
                    _poseLostTime = -1f;
                    HideOrb();
                    onRelease?.Invoke();
                    if (debugLog) Debug.Log("[얼음] 유예 만료 — 구체 제거", this);
                }
            }
        }

        // ─── Update: 위치 추적 + 발사 감지 ────────────

        private void Update()
        {
            if (!_orbActive || _orbInstance == null) return;

            // 구체 위치 갱신
            var orbPos = GetOrbWorldPos();
            _orbInstance.transform.position = orbPos;

            // 속도 샘플 기록. 월드 좌표가 아니라 카메라 기준 로컬 좌표를 써서
            // left-hand pull movement로 플레이어가 움직이는 것을 right-hand throw로 오판하지 않는다.
            var samplePos = GetVelocitySamplePos(orbPos);
            _wristSamples.Enqueue((Time.time, samplePos));
            while (_wristSamples.Count > 0 && _wristSamples.Peek().time < Time.time - velocitySampleDuration)
                _wristSamples.Dequeue();

            // 던지기 감지
            CheckThrow(samplePos);
        }

        // ─── 던지기 ────────────────────────────────────

        private void CheckThrow(Vector3 currentSamplePos)
        {
            if (_wristSamples.Count < 2) return;

            var samples = new List<(float time, Vector3 pos)>(_wristSamples);
            var oldest = samples[0];
            var dt = Time.time - oldest.time;
            if (dt <= 0f) return;

            var localVelocity = (currentSamplePos - oldest.pos) / dt;

            // 카메라 기준 로컬 +Z가 플레이어가 보는 전방이다.
            var cam = Camera.main;
            var forward = cam != null ? cam.transform.forward : Vector3.forward;
            var forwardSpeed = cam != null ? localVelocity.z : Vector3.Dot(localVelocity, forward);

            if (forwardSpeed >= minThrowSpeed)
            {
                var velocity = cam != null ? cam.transform.TransformVector(localVelocity) : localVelocity;
                LaunchOrb(velocity, forward);
            }
        }

        private void LaunchOrb(Vector3 velocity, Vector3 cameraForward)
        {
            _lastLaunchTime = Time.time;
            _orbActive = false;

            var launchPos = GetOrbWorldPos();
            // 발사 방향: 손 속도 방향 (카메라 방향 보정)
            var direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : cameraForward;
            var launchVelocity = useArcTrajectory
                ? ComputeArcLaunchVelocity(velocity, cameraForward)
                : direction * projectileSpeed;

            if (debugLog) Debug.Log($"[얼음] 발사! 속도={velocity.magnitude:F2} m/s", this);

            if (iceProjectilePrefab != null)
            {
                var proj = Instantiate(iceProjectilePrefab, launchPos, Quaternion.LookRotation(direction));
                proj.transform.localScale = Vector3.one * projectileScale;

                // SpellProjectile 있으면 초기화
                if (proj.TryGetComponent<ArcaneVR.Spell.SpellProjectile>(out var sp))
                {
                    sp.InitializePrototype(
                        useArcTrajectory ? 0f : projectileSpeed,
                        direction,
                        ArcaneVR.Spell.ElementType.Ice,
                        ArcaneVR.Spell.StatusEffect.Slow,
                        20f,
                        3f);
                }

                ApplyProjectileVelocity(proj, launchVelocity);

                Destroy(proj, iceBallLifetime);
            }
            else
            {
                // 프리팹 없으면 구체 자체를 발사
                if (_orbInstance != null)
                {
                    _orbInstance.name = "IceOrb_Projectile";
                    if (_orbInstance.TryGetComponent<SphereCollider>(out var col))
                        col.isTrigger = false;

                    var rb = _orbInstance.GetComponent<Rigidbody>() ?? _orbInstance.AddComponent<Rigidbody>();
                    rb.isKinematic = false;
                    rb.useGravity = useArcTrajectory;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.linearVelocity = launchVelocity;
                    Destroy(_orbInstance, iceBallLifetime);
                    _orbInstance = null; // HideOrb가 파괴하지 않도록
                }
            }

            HideOrb();
            onThrow?.Invoke(launchVelocity);
        }

        // ─── 구체 생성/제거 ─────────────────────────────

        private void ShowOrb()
        {
            if (_orbInstance != null) Destroy(_orbInstance);

            var pos = GetOrbWorldPos();

            if (iceOrbPrefab != null)
            {
                _orbInstance = Instantiate(iceOrbPrefab, pos, Quaternion.identity);
                _orbInstance.transform.localScale = Vector3.one * orbScale;
            }
            else
            {
                // 폴백: 흰 구체
                _orbInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _orbInstance.name = "IceOrb";
                _orbInstance.transform.position = pos;
                _orbInstance.transform.localScale = Vector3.one * orbScale;

                var col = _orbInstance.GetComponent<SphereCollider>();
                if (col != null) col.isTrigger = true;

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.5f, 0.9f, 1f, 0.85f);
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.renderQueue = 3000;
                }
                _orbInstance.GetComponent<Renderer>().material = mat;
            }

            _wristSamples.Clear();
        }

        private void HideOrb()
        {
            if (_orbInstance != null)
            {
                Destroy(_orbInstance);
                _orbInstance = null;
            }
            _wristSamples.Clear();
        }

        private Vector3 GetOrbWorldPos()
        {
            if (wristTransform != null)
                return wristTransform.TransformPoint(palmOffset);

            return Vector3.zero;
        }

        private static Vector3 GetVelocitySamplePos(Vector3 worldPos)
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.InverseTransformPoint(worldPos) : worldPos;
        }

        private Vector3 ComputeArcLaunchVelocity(Vector3 throwVelocity, Vector3 cameraForward)
        {
            var baseVelocity = throwVelocity.sqrMagnitude > 0.01f
                ? throwVelocity * Mathf.Max(0.1f, arcVelocityMultiplier)
                : cameraForward.normalized * minArcLaunchSpeed;

            baseVelocity += Vector3.up * arcUpwardBoost;

            var speed = baseVelocity.magnitude;
            if (speed < minArcLaunchSpeed && baseVelocity.sqrMagnitude > 0.001f)
                baseVelocity = baseVelocity.normalized * minArcLaunchSpeed;
            else if (speed > maxArcLaunchSpeed)
                baseVelocity = baseVelocity.normalized * maxArcLaunchSpeed;

            return baseVelocity;
        }

        private void ApplyProjectileVelocity(GameObject projectile, Vector3 launchVelocity)
        {
            var rb = projectile.GetComponent<Rigidbody>() ?? projectile.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = useArcTrajectory;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = launchVelocity;
        }

        private void LogTuningState(in XRHand hand, bool blocked, bool iceDetected)
        {
            if (!tuningFeedback || Time.time < _nextTuningLogTime)
                return;

            _nextTuningLogTime = Time.time + Mathf.Max(0.1f, tuningLogInterval);
            var shapeReport = XRHandShapeTuningUtility.BuildCompactReport(hand, snowShape);
            Debug.Log(
                $"[Ice Tune] tracked={hand.isTracked} ice={iceDetected} blocked={blocked} {shapeReport} " +
                $"shape={(snowShape != null ? snowShape.name : "null")} " +
                $"blockedBy={(blockedByShape != null ? blockedByShape.name : "null")}",
                this);
        }
    }
}
