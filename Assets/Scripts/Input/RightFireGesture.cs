using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace ArcaneVR.Input
{
    /// <summary>
    /// 오른손 총 모양 포즈를 유지한 채 손목을 뒤로 젖히면 화염 발사체를 발사합니다.
    /// </summary>
    public class RightFireGesture : MonoBehaviour
    {
        [Header("XR Hands")]
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [SerializeField] private XRHandShape gunShape;
        [Tooltip("Right Hand Tracking > R_Wrist Transform — 구 OVR WristBone과 동일한 역할")]
        [SerializeField] private Transform wristTransform;

        [Header("VFX")]
        [SerializeField] private GameObject auraPrefab;
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private Color fireAuraColor = new Color(1f, 0.35f, 0.05f);
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private float explosionScale = 0.2f;
        [SerializeField] private float explosionLifetime = 2f;
        [SerializeField] private Material explosionMaterialOverride;

        [Header("Spawn")]
        [SerializeField] private float spawnForwardOffset = 0.15f;
        [SerializeField] private float projectileScale = 1f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private Vector3 projectileEulerOffset = Vector3.zero;

        [Header("Recoil Detection")]
        [SerializeField] private float recoilVelocityThreshold = 0.5f;
        [SerializeField] private float cooldown = 0.25f;

        [Header("Aura")]
        [SerializeField] private float auraScale = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        [Header("Events")]
        public UnityEvent onShot;
        public UnityEvent onPoseStart;
        public UnityEvent onPoseEnd;

        private GameObject _auraInstance;
        private WristAuraController _auraController;
        private bool _poseActive;
        private float _lastShotTime = -999f;
        private int _jointUpdateCount;
        private float _nextSubsystemLog;

        private Pose _wristPose;
        private Pose _prevWristPose;
        private bool _hasPrevWrist;
        private float _lastJointsTime;

        private void OnEnable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            StartCoroutine(ReconnectSubsystem());
        }

        private System.Collections.IEnumerator ReconnectSubsystem()
        {
            // XRHandSubsystem이 시작된 뒤 XRHandTrackingEvents를 재연결
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            while (true)
            {
                UnityEngine.SubsystemManager.GetSubsystems(subsystems);
                if (subsystems.Count > 0 && subsystems[0].running) break;
                subsystems.Clear();
                yield return new WaitForSeconds(0.5f);
            }
            if (handTrackingEvents != null)
            {
                handTrackingEvents.enabled = false;
                handTrackingEvents.enabled = true;
                if (debugLog) Debug.Log("[화염] XRHandTrackingEvents 재연결 완료", this);
            }
        }

        private void Update()
        {
            if (!debugLog) return;
            if (Time.time < _nextSubsystemLog) return;
            _nextSubsystemLog = Time.time + 4f;

            var cam = Camera.main;
            var camY = cam != null ? cam.transform.position.y : 0f;

            if (_auraInstance != null)
            {
                var auraPos = _auraInstance.transform.position;
                Debug.Log($"[화염] 오라 추적 — 카메라Y={camY:F2}, 오라Y={auraPos.y:F2}, 차이={camY - auraPos.y:F2}m (아래)", this);
            }
            else
            {
                var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
                UnityEngine.SubsystemManager.GetSubsystems(subsystems);
                string sub = subsystems.Count == 0 ? "없음" :
                    $"running={subsystems[0].running}, rightTracked={subsystems[0].rightHand.isTracked}";
                Debug.Log($"[화염] 진단 — {sub} | joints={_jointUpdateCount} | tracked={handTrackingEvents?.handIsTracked}", this);
            }
        }

        private void OnDisable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            DeactivatePose();
        }

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (!isActiveAndEnabled) return;
            _jointUpdateCount++;

            // 100 업데이트마다 CheckConditions 결과 로그
            if (debugLog && args.hand.isTracked && _jointUpdateCount % 100 == 0)
            {
                var condResult = gunShape != null && gunShape.CheckConditions(args);
                Debug.Log($"[화염] 조건체크 — isTracked={args.hand.isTracked}, CheckConditions={condResult}", this);
            }

            var isDetected = args.hand.isTracked &&
                             gunShape != null && gunShape.CheckConditions(args);

            if (!args.hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose))
                return;

            _prevWristPose = _wristPose;
            _wristPose = wristPose;
            var dt = Time.time - _lastJointsTime;
            _lastJointsTime = Time.time;

            if (isDetected && !_poseActive)
            {
                _poseActive = true;
                _hasPrevWrist = false;
                ActivatePose(wristPose);
                onPoseStart?.Invoke();
                if (debugLog) Debug.Log("[화염] 총 모양 인식 — 오라 활성화", this);
            }
            else if (!isDetected && _poseActive)
            {
                _poseActive = false;
                DeactivatePose();
                onPoseEnd?.Invoke();
                _hasPrevWrist = false;
                if (debugLog) Debug.Log("[화염] 포즈 해제 — 오라 비활성화", this);
                return;
            }

            if (!_poseActive) return;

            // 오라 위치 갱신 — wristTransform(R_Wrist) 우선, 없으면 subsystem pose
            if (_auraInstance != null)
            {
                var pos = wristTransform != null ? wristTransform.position : wristPose.position;
                var rot = wristTransform != null ? wristTransform.rotation : wristPose.rotation;
                _auraInstance.transform.SetPositionAndRotation(pos, rot);
            }

            // 발사 감지: 포즈 유지 중 손을 Y축 위로 빠르게 올리면 발사
            if (!_hasPrevWrist) { _hasPrevWrist = true; return; }
            if (dt <= 0f) return;

            var velocity = (_wristPose.position - _prevWristPose.position) / dt;
            var upSpeed = Vector3.Dot(velocity, Vector3.up);

            if (upSpeed > recoilVelocityThreshold && Time.time - _lastShotTime > cooldown)
            {
                _lastShotTime = Time.time;
                FireProjectile(wristPose);
                onShot?.Invoke();
                if (debugLog) Debug.Log($"[화염] 발사! 위쪽 속도={upSpeed:F2} m/s", this);
            }
        }

        private void ActivatePose(Pose wristPose)
        {
            if (auraPrefab == null) return;
            if (_auraInstance != null) Destroy(_auraInstance);

            // wristTransform(R_Wrist) 우선 사용 — 구 OVR WristBone과 동일 방식
            var spawnPos = wristTransform != null ? wristTransform.position : wristPose.position;
            var spawnRot = wristTransform != null ? wristTransform.rotation : wristPose.rotation;

            _auraInstance = Instantiate(auraPrefab, spawnPos, spawnRot);
            _auraInstance.transform.localScale = Vector3.one * auraScale;
            _auraController = _auraInstance.GetComponent<WristAuraController>();
            if (_auraController != null)
            {
                _auraController.auraColor = fireAuraColor;
                _auraController.skeleton = null;
                _auraController.ManaPct = 1f;
            }
            if (debugLog) Debug.Log($"[화염] 오라 생성 — 위치={spawnPos:F2} (wristTransform={(wristTransform != null ? wristTransform.name : "NULL → subsystem 사용")})", this);
        }

        private void DeactivatePose()
        {
            if (_auraInstance != null)
            {
                Destroy(_auraInstance);
                _auraInstance = null;
            }
        }

        private void FireProjectile(Pose wristPose)
        {
            if (fireballPrefab == null) return;

            // wristTransform(R_Wrist) 우선 — 없으면 subsystem pose
            var origin = wristTransform != null
                ? wristTransform.position + wristTransform.forward * spawnForwardOffset
                : wristPose.position + wristPose.forward * spawnForwardOffset;

            var baseDirection = wristTransform != null ? wristTransform.forward : wristPose.forward;
            var direction = ResolveProjectileDirection(baseDirection);

            if (debugLog) Debug.Log($"[화염] 발사 — 위치={origin:F2}, 방향={direction:F2}", this);

            var go = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(direction));
            if (go.TryGetComponent<FireballProjectile>(out var fireball))
            {
                fireball.ConfigureLaunch(projectileSpeed, projectileScale);
                fireball.ConfigureImpact(explosionPrefab, explosionScale, explosionLifetime, explosionMaterialOverride);
            }
            else
            {
                go.transform.localScale = Vector3.one * Mathf.Max(0.01f, projectileScale);
            }

            if (go.TryGetComponent<ArcaneVR.Spell.SpellProjectile>(out var proj))
            {
                proj.InitializePrototype(
                    ArcaneVR.Input.PoseType.OpenPalm,
                    projectileSpeed,
                    direction,
                    ArcaneVR.Spell.ElementType.Fire,
                    ArcaneVR.Spell.StatusEffect.Burn,
                    20f,
                    3f);
            }

            if (go.TryGetComponent<Rigidbody>(out var rb))
                rb.linearVelocity = direction * projectileSpeed;

            if (fireball == null)
                Destroy(go, 5f);
        }

        private Vector3 ResolveProjectileDirection(Vector3 baseDirection)
        {
            var normalized = baseDirection.sqrMagnitude > 0.001f ? baseDirection.normalized : transform.forward;
            return (Quaternion.LookRotation(normalized) * Quaternion.Euler(projectileEulerOffset) * Vector3.forward).normalized;
        }
    }
}
