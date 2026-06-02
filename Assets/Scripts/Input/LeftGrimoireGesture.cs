using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using CodexGenerated.GrimoirePages;

namespace ArcaneVR.Input
{
    /// <summary>
    /// 왼손 Gramo 포즈 → GrimoireBook 프리팹(PageTurner 포함) 손 위에 소환.
    /// RightPageTurnGesture에서 NextPage()/PreviousPage()를 호출합니다.
    /// </summary>
    public class LeftGrimoireGesture : MonoBehaviour
    {
        [Header("XR Hands")]
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [SerializeField] private XRHandShape grimoireShape;
        [Tooltip("Left Hand Tracking > L_Wrist Transform")]
        [SerializeField] private Transform wristTransform;

        [Header("Grimoire")]
        [SerializeField] private GameObject grimoirePrefab;
        [Tooltip("손목 기준 책 오프셋 (로컬 좌표)")]
        [SerializeField] private Vector3 positionOffset = new Vector3(0f, 0.05f, 0.1f);
        [SerializeField] private Vector3 rotationOffset = new Vector3(0f, 0f, 0f);
        [SerializeField] private float grimoireScale = 0.3f;

        [Header("Time Focus Rendering")]
        [SerializeField] private bool renderAfterTimeFocusPostProcess = true;
        [SerializeField] private string timeFocusExemptLayerName = "TimeFocusExempt";

        [Header("Palm Direction Guard")]
        [Tooltip("손가락 모양이 맞더라도 손바닥이 위를 향할 때만 마도서를 엽니다.")]
        [SerializeField] private bool requirePalmUp = true;
        [Tooltip("Palm joint 로컬 축 중 손바닥 법선으로 사용할 축입니다. 디바이스별로 반대면 값을 조정하세요.")]
        [SerializeField] private Vector3 palmNormalLocalAxis = Vector3.up;
        [Tooltip("손바닥 법선과 월드 Up의 dot 임계값입니다. 0.45는 약 63도 이내를 허용합니다.")]
        [SerializeField] private float palmUpDotThreshold = 0.45f;
        [Tooltip("포즈가 이 시간만큼 유지되어야 마도서를 엽니다.")]
        [SerializeField] private float poseHoldDuration = 0.15f;

        [Header("Pose Grace Period")]
        [Tooltip("포즈 상실 후 바로 숨기지 않고 유지할 시간(초). 스와이프 중 왼손이 가려질 때 끊김 방지.")]
        [SerializeField] private float poseLostGracePeriod = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;
        [SerializeField] private bool debugDetectionDetails = true;
        [SerializeField] private float debugLogInterval = 0.5f;

        [Header("Events")]
        public UnityEvent onGrimoireAppear;
        public UnityEvent onGrimoireDisappear;
        public UnityEvent onNextPage;
        public UnityEvent onPreviousPage;

        /// <summary>마도서가 현재 소환 중인지 외부에서 확인용</summary>
        public bool IsActive => _poseActive && !IsExternallySuppressed;
        public bool IsExternallySuppressed { get; private set; }

        private GameObject _grimoireInstance;
        private GrimoirePageTurner _pageTurner; // 소환된 인스턴스에서 가져옴
        private bool _poseActive;
        private float _poseHoldTimer;
        private float _poseLostTime = -1f; // -1 = 상실 중 아님
        private float _nextDebugLogTime;
        private string _lastDebugReason;

        // ───────────────────────────────────────────

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
            HideGrimoire();
        }

        private System.Collections.IEnumerator ReconnectSubsystem()
        {
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
                if (debugLog) Debug.Log("[마도서] XRHandTrackingEvents 재연결 완료", this);
            }
        }

        private void Update()
        {
            if (_poseActive) UpdateGrimoireTransform();
        }

        // ───────────────────────────────────────────

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (!isActiveAndEnabled) return;
            if (IsExternallySuppressed)
            {
                if (_poseActive || _grimoireInstance != null)
                {
                    _poseActive = false;
                    HideGrimoire();
                    onGrimoireDisappear?.Invoke();
                }

                _poseHoldTimer = 0f;
                _poseLostTime = -1f;
                return;
            }

            var isTracked = args.hand.isTracked;
            var shapeAccepted = isTracked &&
                                grimoireShape != null &&
                                grimoireShape.CheckConditions(args);
            var palmDebug = shapeAccepted ? string.Empty : "palm not checked";
            var palmAccepted = shapeAccepted && IsPalmDirectionAccepted(args, out palmDebug);
            var isDetected = shapeAccepted && palmAccepted;

            LogDetectionState(isTracked, shapeAccepted, palmAccepted, palmDebug);

            if (isDetected)
            {
                _poseLostTime = -1f; // 다시 감지 → 타이머 리셋
                _poseHoldTimer += Time.unscaledDeltaTime;

                if (!_poseActive && _poseHoldTimer >= poseHoldDuration)
                {
                    _poseActive = true;
                    ShowGrimoire();
                    onGrimoireAppear?.Invoke();
                    if (debugLog) Debug.Log("[마도서] 포즈 인식 — 소환!", this);
                }
            }
            else if (_poseActive)
            {
                // Grace Period: 포즈 상실 후 N초 동안 유지
                if (_poseLostTime < 0f)
                {
                    _poseLostTime = Time.unscaledTime;
                    if (debugLog) Debug.Log($"[마도서] 포즈 상실 — {poseLostGracePeriod:F1}초 유예", this);
                }

                if (Time.unscaledTime - _poseLostTime >= poseLostGracePeriod)
                {
                    _poseActive = false;
                    _poseLostTime = -1f;
                    HideGrimoire();
                    onGrimoireDisappear?.Invoke();
                    if (debugLog) Debug.Log("[마도서] 유예 만료 — 제거", this);
                }
                // 유예 중에도 위치는 계속 추적
            }
            else
            {
                _poseHoldTimer = 0f;
                _poseLostTime = -1f;
            }

            if (_poseActive && _grimoireInstance != null)
                UpdateGrimoireTransform();
        }

        private bool IsPalmDirectionAccepted(XRHandJointsUpdatedEventArgs args, out string debug)
        {
            if (!requirePalmUp)
            {
                debug = "palm guard off";
                return true;
            }

            if (!args.hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose))
            {
                debug = "no palm pose";
                return false;
            }

            var localAxis = palmNormalLocalAxis.sqrMagnitude > 0.001f
                ? palmNormalLocalAxis.normalized
                : Vector3.up;
            var palmNormal = palmPose.rotation * localAxis;
            var upDot = Vector3.Dot(palmNormal.normalized, Vector3.up);
            debug = $"palmDot={upDot:0.00} threshold={palmUpDotThreshold:0.00} axis={localAxis}";
            return upDot >= palmUpDotThreshold;
        }

        private void LogDetectionState(bool isTracked, bool shapeAccepted, bool palmAccepted, string palmDebug)
        {
            if (!debugLog || !debugDetectionDetails)
                return;

            string reason;
            if (!isTracked)
                reason = "not tracked";
            else if (grimoireShape == null)
                reason = "missing shape";
            else if (!shapeAccepted)
                reason = HasUnspecifiedShapeTargets() ? "shape rejected: unspecified target" : "shape rejected";
            else if (!palmAccepted)
                reason = "palm rejected";
            else if (_poseHoldTimer < poseHoldDuration)
                reason = "holding";
            else
                reason = "accepted";

            if (reason == _lastDebugReason && Time.unscaledTime < _nextDebugLogTime)
                return;

            _lastDebugReason = reason;
            _nextDebugLogTime = Time.unscaledTime + Mathf.Max(0.05f, debugLogInterval);

            Debug.Log(
                $"[마도서 감지] {reason} | tracked={isTracked} shape={shapeAccepted} palm={palmAccepted} " +
                $"hold={_poseHoldTimer:0.00}/{poseHoldDuration:0.00} | {palmDebug}",
                this);
        }

        private bool HasUnspecifiedShapeTargets()
        {
            if (grimoireShape == null)
                return false;

            foreach (var condition in grimoireShape.fingerShapeConditions)
            {
                if (condition?.targets == null)
                    continue;

                foreach (var target in condition.targets)
                {
                    if (target.shapeType == XRFingerShapeType.Unspecified)
                        return true;
                }
            }

            return false;
        }

        // ───────────────────────────────────────────

        private void ShowGrimoire()
        {
            if (IsExternallySuppressed)
                return;

            if (grimoirePrefab == null)
            {
                Debug.LogWarning("[마도서] grimoirePrefab 미설정!", this);
                return;
            }
            if (_grimoireInstance != null) Destroy(_grimoireInstance);

            _grimoireInstance = Instantiate(grimoirePrefab);
            _grimoireInstance.name = "Grimoire_Instance";
            _grimoireInstance.transform.localScale = Vector3.one * grimoireScale;
            ApplyTimeFocusExemptLayer(_grimoireInstance);

            // 소환된 인스턴스에서 PageTurner 가져오기
            _pageTurner = _grimoireInstance.GetComponent<GrimoirePageTurner>();
            if (_pageTurner != null)
                _pageTurner.SetSpread(0); // 첫 페이지부터 시작

            UpdateGrimoireTransform();

            if (debugLog) Debug.Log($"[마도서] 생성 — PageTurner={(_pageTurner != null ? "있음" : "없음")}", this);
        }

        private void HideGrimoire()
        {
            if (_grimoireInstance != null)
            {
                Destroy(_grimoireInstance);
                _grimoireInstance = null;
            }
            _pageTurner = null;
            _poseHoldTimer = 0f;
        }

        public void SetExternalSuppressed(bool suppressed, string source)
        {
            if (IsExternallySuppressed == suppressed)
                return;

            IsExternallySuppressed = suppressed;
            if (!suppressed)
                return;

            var wasActive = _poseActive || _grimoireInstance != null;
            _poseActive = false;
            _poseLostTime = -1f;
            HideGrimoire();

            if (wasActive)
                onGrimoireDisappear?.Invoke();

            if (debugLog)
                Debug.Log($"[Grimoire Gesture] suppressed by {source}", this);
        }

        private void UpdateGrimoireTransform()
        {
            if (_grimoireInstance == null || wristTransform == null) return;
            var worldPos = wristTransform.TransformPoint(positionOffset);
            var worldRot = wristTransform.rotation * Quaternion.Euler(rotationOffset);
            _grimoireInstance.transform.SetPositionAndRotation(worldPos, worldRot);
        }

        private void ApplyTimeFocusExemptLayer(GameObject root)
        {
            if (!renderAfterTimeFocusPostProcess || root == null)
                return;

            var layer = LayerMask.NameToLayer(timeFocusExemptLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[Grimoire] Time focus exempt layer '{timeFocusExemptLayerName}' does not exist.", this);
                return;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        // ───────────────────────────────────────────

        /// <summary>RightPageTurnGesture에서 호출 — 다음 페이지</summary>
        public void NextPage()
        {
            if (!_poseActive) return;
            onNextPage?.Invoke();

            if (_pageTurner != null)
                _pageTurner.NextPage();
            else if (debugLog)
                Debug.Log("[마도서] NextPage — PageTurner 없음", this);
        }

        /// <summary>RightPageTurnGesture에서 호출 — 이전 페이지</summary>
        public void PreviousPage()
        {
            if (!_poseActive) return;
            onPreviousPage?.Invoke();

            if (_pageTurner != null)
                _pageTurner.PreviousPage();
            else if (debugLog)
                Debug.Log("[마도서] PreviousPage — PageTurner 없음", this);
        }
    }
}
