using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Input;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// 마법 발동 조율자. 제스처 이벤트를 받아 속성 모듈에 위임하고,
    /// 조합 마법과 음성 부스트를 직접 처리한다.
    /// FindAnyObjectByType 런타임 탐색을 사용하지 않는다.
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        [Header("── 핵심 참조 (ArcanePlayerRig 빌더가 자동 연결) ──")]
        [SerializeField] private SpellDatabase spellDatabase;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private FeedbackManager feedbackManager;
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private CombinationFocusModeController focusModeController;
        [SerializeField] private ElementAuraManager elementAuraManager;

        [Header("── 손 / 머리 Transform ──")]
        [SerializeField] private Transform leftHandSpawnPoint;
        [SerializeField] private Transform rightHandSpawnPoint;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform spellSpawnRoot;

        [Header("── 속성 모듈 ──")]
        [SerializeField] private FireSpellModule fireModule;
        [SerializeField] private IceSpellModule iceModule;
        [SerializeField] private ThunderSpellModule thunderModule;

        [Header("── 조합 마법 설정 ──")]
        [SerializeField] private bool allowCombinationSpellCasts = true;
        [SerializeField] private float fallbackProjectileLifetime = 5f;
        [SerializeField] private bool useDebugPrimitiveProjectiles = true;
        [SerializeField] private float debugProjectileScale = 1f;
        [SerializeField] private bool showCombinationAura = true;
        [SerializeField] private float combinationReadyAuraScale = 0.18f;
        [SerializeField] private float combinationCompleteAuraScale = 0.42f;
        [SerializeField] private float combinationCompleteAuraHoldSeconds = 3f;

        [Header("── 음성 부스트 ──")]
        [SerializeField] private AudioClip voiceBoostClip;
        [SerializeField] private float voiceBoostDuration = 4f;
        [SerializeField] private bool ignoreVoiceDuringCombinationFocus = true;

        [Header("── 타임 포커스 레이어 ──")]
        [SerializeField] private string auraTimeFocusExemptLayerName = "TimeFocusExempt";

        // ── 내부 상태 ─────────────────────────────────────────────────────────

        private string _currentRightGesture = string.Empty;
        private Vector3 _prevTrackingPos;
        private bool _hasPrevTrackingPos;

        // 조합 오라 피드백
        private GameObject _combinationAuraRoot;
        private SpellId _combinationAuraSpell = SpellId.None;
        private bool _combinationAuraCompleted;
        private float _combinationAuraUntilTime = -999f;

        // 음성 부스트
        private bool _isVoiceBoostActive;
        private float _voiceBoostExpiry = -999f;
        private AudioSource _voiceBoostAudio;
        private AudioClip _cachedBoostTone;

        // 캐스팅 억제
        private bool _isCastingSuppressed;
        private string _suppressionSource;

        // 마지막 캐스트 기록 (디버그용)
        private SpellId _lastCastSpellId = SpellId.None;
        private ElementType _lastCastElement = ElementType.None;
        private float _lastManaCost;
        private string _lastCastStatus    = "대기";
        private string _lastManaCostStatus = "마나: 대기";
        private string _lastVoiceBoostStatus = "음성: 비활성";

        // ── 공개 속성 (UI/디버그용) ───────────────────────────────────────────

        public SpellDatabase Database
        {
            get => spellDatabase;
            set => spellDatabase = value;
        }

        public string PrototypeDebugStatus { get; private set; } = "대기";
        public string LastCastStatus       => _lastCastStatus;
        public string LastManaCostStatus   => _lastManaCostStatus;
        public string LastVoiceBoostStatus => _lastVoiceBoostStatus;
        public SpellId LastCastSpellId     => _lastCastSpellId;
        public ElementType LastCastElement => _lastCastElement;
        public float LastManaCost          => _lastManaCost;
        public bool IsCastingSuppressed    => _isCastingSuppressed;
        public bool IsPrototypeArmed       => !string.IsNullOrEmpty(_currentRightGesture);
        public bool IsPrototypeVoiceBoosted => _isVoiceBoostActive;

        public string PrototypeArmStatus => IsPrototypeArmed
            ? $"준비 {PrototypeArmedElement} 제스처:{_currentRightGesture}"
            : _lastCastStatus;

        public ElementType PrototypeArmedElement => _currentRightGesture switch
        {
            "Fire"        => ElementType.Fire,
            "Ice"         => ElementType.Ice,
            "Thunder"     => ElementType.Thunder,
            "ThunderShoot"=> ElementType.Thunder,
            _             => ElementType.None
        };

        // ── Unity 콜백 ────────────────────────────────────────────────────────

        private void Awake()
        {
            InitModules();
        }

        private void OnEnable()
        {
            InitModules();
            SubscribeGestureDetector();

            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;
                combinationChecker.OnComboReadyChanged  += HandleComboReadyChanged;
                combinationChecker.OnCombinationFail    += HandleCombinationFail;
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
                combinationChecker.OnComboReadyChanged  -= HandleComboReadyChanged;
                combinationChecker.OnCombinationFail    -= HandleCombinationFail;
            }

            if (voiceRecognizer != null)
                voiceRecognizer.OnVoiceCommand -= HandleVoiceCommand;

            DisarmAllModules();
            StopCombinationAuraFeedback();
        }

        private void Update()
        {
            UpdateRightGestureAttack();
            UpdateCombinationAuraFeedback();
            UpdateTimeFocusVisibility();
            UpdateVoiceBoost();
        }

        // ── 공개 메서드 ───────────────────────────────────────────────────────

        public void SetCastingSuppressed(bool suppressed, string source)
        {
            _isCastingSuppressed = suppressed;
            _suppressionSource   = suppressed ? (string.IsNullOrWhiteSpace(source) ? "unknown" : source) : string.Empty;
            if (suppressed) _lastCastStatus = $"차단: {_suppressionSource}";
        }

        public bool Cast(SpellId spellId)
        {
            if (!IsCombinationCastAllowed(spellId))
            {
                _lastCastStatus    = $"조합 잠김: {SpellHitData.GetDisplayName(spellId)}";
                _lastManaCostStatus = "마나: 조합 잠김";
                return false;
            }

            if (spellDatabase == null)
            {
                _lastCastStatus = $"차단: SpellDatabase 없음 ({spellId})";
                return false;
            }

            var data = spellDatabase.Get(spellId);
            if (data == null)
            {
                _lastCastStatus = $"차단: 데이터 없음 ({spellId})";
                return false;
            }

            if (combatManager != null && !combatManager.TryConsumeMana(data.manaCost))
            {
                RecordCastBlocked(spellId, data.element, data.manaCost,
                    $"마나 부족 {combatManager.CurrentMana:0.#}/{data.manaCost:0.#}");
                return false;
            }

            var spawnPos  = ResolveSpawnPoint(spellId)?.position ?? transform.position;
            var direction = ResolveAimDirection(spawnPos);
            spawnPos += direction * 0.25f;

            var element = data.element;
            if (combinationChecker != null && IsSingleSpell(spellId) && combinationChecker.CurrentElement != ElementType.None)
                element = combinationChecker.CurrentElement;

            var projectileObj = CreateProjectileObject(data, element, spawnPos, Quaternion.LookRotation(direction, Vector3.up));
            ParentToSpellRoot(projectileObj);

            var projectile = projectileObj.GetComponent<SpellProjectile>();
            if (projectile == null)
                projectile = projectileObj.AddComponent<SpellProjectile>();
            projectile.Initialize(spellId, element, data.damage, data.projectileSpeed,
                data.statusEffect, data.statusDuration, direction, combatManager,
                data.statusMagnitude, data.statusTickInterval);

            RememberCast(element, spellId, data.manaCost,
                combatManager != null ? $"마나 소비 {data.manaCost:0.#}" : "마나: CombatManager 없음");

            _lastCastStatus = SpellHitData.IsComboSpellId(spellId)
                ? $"발동: {SpellHitData.GetDisplayName(spellId)}"
                : $"발동: {element} {spellId}";

            Destroy(projectileObj, fallbackProjectileLifetime);
            feedbackManager?.OnSpellCast(spellId);
            if (SpellHitData.IsComboSpellId(spellId))
                StartCombinationAuraFeedback(spellId, true);

            return true;
        }

        // GestureSpellPrototypeBootstrap 등 런타임 바인딩 경로용 (레거시 호환)
        public void ConfigureGesturePrototype(
            GestureDetector detector,
            GestureEventRouter router,
            OVRHand hand,
            Transform spawnPoint,
            Transform spawnRoot)
        {
            UnsubscribeGestureDetector();
            gestureDetector = detector;
            if (spawnPoint != null) rightHandSpawnPoint = spawnPoint;
            if (spawnRoot  != null) spellSpawnRoot      = spawnRoot;
            InitModules();
            SubscribeGestureDetector();
        }

        // ── 초기화 ────────────────────────────────────────────────────────────

        private void InitModules()
        {
            fireModule?.Init(rightHandSpawnPoint, spellSpawnRoot, headTransform, spellDatabase, elementAuraManager);
            iceModule?.Init(rightHandSpawnPoint, spellSpawnRoot, headTransform, spellDatabase, elementAuraManager);
            thunderModule?.Init(rightHandSpawnPoint, spellSpawnRoot, headTransform, spellDatabase, elementAuraManager);
        }

        // ── 제스처 구독 ───────────────────────────────────────────────────────

        private void SubscribeGestureDetector()
        {
            if (gestureDetector == null) return;
            gestureDetector.OnGestureConfirmed -= HandleGestureConfirmed;
            gestureDetector.OnGestureConfirmed += HandleGestureConfirmed;
            gestureDetector.OnGestureCleared   -= HandleGestureCleared;
            gestureDetector.OnGestureCleared   += HandleGestureCleared;
        }

        private void UnsubscribeGestureDetector()
        {
            if (gestureDetector == null) return;
            gestureDetector.OnGestureConfirmed -= HandleGestureConfirmed;
            gestureDetector.OnGestureCleared   -= HandleGestureCleared;
        }

        private void HandleGestureConfirmed(bool isLeft, string gestureName, PoseType _)
        {
            if (isLeft) return;

            if (gestureName != "Fire" && gestureName != "Ice" &&
                gestureName != "Thunder" && gestureName != "ThunderShoot")
                return;

            _currentRightGesture = gestureName;
            _hasPrevTrackingPos  = false;
            PrototypeDebugStatus = $"준비: {gestureName}";

            // 모듈 전환
            fireModule?.Disarm();
            iceModule?.Disarm();
            thunderModule?.Disarm();

            if (gestureName == "Fire")
                fireModule?.Arm();
            else if (gestureName == "Ice")
                iceModule?.Arm();
            else
                thunderModule?.Arm(gestureName);
        }

        private void HandleGestureCleared(bool isLeft, string gestureName)
        {
            if (isLeft || gestureName != _currentRightGesture) return;
            if (gestureName == "ThunderShoot" && thunderModule != null && thunderModule.IsBeamActive)
                return;

            DisarmAllModules();
            _currentRightGesture = string.Empty;
            _hasPrevTrackingPos  = false;
            PrototypeDebugStatus = "대기";
            DeactivateVoiceBoost();
            elementAuraManager?.Hide();
        }

        private void DisarmAllModules()
        {
            fireModule?.Disarm();
            iceModule?.Disarm();
            thunderModule?.Disarm();
        }

        // ── 매 프레임 갱신 ────────────────────────────────────────────────────

private void UpdateRightGestureAttack()
        {
            if (_isCastingSuppressed || string.IsNullOrEmpty(_currentRightGesture)) return;

            var spawnPoint = rightHandSpawnPoint != null ? rightHandSpawnPoint : (Transform)null;
            if (spawnPoint == null || Time.deltaTime <= 0f)
            {
                PrototypeDebugStatus = "No spawn point";
                return;
            }

            var currentPos = ResolveTrackingPosition(spawnPoint.position);
            if (!_hasPrevTrackingPos)
            {
                _prevTrackingPos = currentPos;
                _hasPrevTrackingPos = true;
                return;
            }

            var localVelocity = (currentPos - _prevTrackingPos) / Time.deltaTime;
            _prevTrackingPos = currentPos;
            var worldVelocity = ToWorldVector(localVelocity);

            if (_currentRightGesture == "Fire")
            {
                PrototypeDebugStatus = $"Fire ready up:{Vector3.Dot(worldVelocity, Vector3.up):0.00}";
                if (fireModule != null && fireModule.Tick(worldVelocity))
                {
                    RememberCast(ElementType.Fire, SpellId.Single_Pointer, 0f, "Mana: dummy");
                    _lastCastStatus = "Cast: Fire dummy";
                }
            }
            else if (_currentRightGesture == "Ice")
            {
                PrototypeDebugStatus = "Ice ready";
                if (iceModule != null && iceModule.Tick(currentPos))
                {
                    RememberCast(ElementType.Ice, SpellId.Single_Wave, 0f, "Mana: dummy");
                    _lastCastStatus = "Cast: Ice dummy";
                }
            }
            else if (_currentRightGesture == "Thunder")
            {
                PrototypeDebugStatus = "Thunder charging";
                if (thunderModule != null && thunderModule.Tick())
                {
                    RememberCast(ElementType.Thunder, SpellId.Single_Strike, 0f, "Mana: dummy");
                    _lastCastStatus = "Cast: Thunder dummy";
                }
            }
            else if (_currentRightGesture == "ThunderShoot")
            {
                PrototypeDebugStatus = "Thunder firing";
                if (thunderModule != null && thunderModule.Tick())
                {
                    RememberCast(ElementType.Thunder, SpellId.Single_Strike, 0f, "Mana: dummy");
                    _lastCastStatus = "Cast: Thunder dummy";
                }
                else if (thunderModule == null || !thunderModule.IsBeamActive)
                {
                    thunderModule?.Disarm();
                    _currentRightGesture = string.Empty;
                    _hasPrevTrackingPos = false;
                    PrototypeDebugStatus = "Idle";
                }
            }
        }

        private void UpdateTimeFocusVisibility()
        {
            var grimoireOpen = grimoireManager != null && grimoireManager.IsOpen;
            elementAuraManager?.SetSuppressed(grimoireOpen);

            var timeStopped = grimoireOpen || (focusModeController != null && focusModeController.IsFocusActive);
            iceModule?.SetOrbVisible(!timeStopped);
        }

        // ── 조합 마법 콜백 ────────────────────────────────────────────────────

        private void HandleCombinationSuccess(SpellId spellId)
        {
            if (IsCombinationCastAllowed(spellId)) Cast(spellId);
        }

        private void HandleCombinationFail()
        {
            StopCombinationAuraFeedback();
        }

        private void HandleComboReadyChanged(SpellId spellId, bool ready)
        {
            if (ready && SpellHitData.IsComboSpellId(spellId))
                StartCombinationAuraFeedback(spellId, false);
            else if (!_combinationAuraCompleted)
                StopCombinationAuraFeedback();
        }

        // ── 조합 오라 피드백 ──────────────────────────────────────────────────

        private void StartCombinationAuraFeedback(SpellId spellId, bool completed)
        {
            if (!showCombinationAura || !SpellHitData.IsComboSpellId(spellId)) return;

            _combinationAuraSpell     = spellId;
            _combinationAuraCompleted = completed;
            _combinationAuraUntilTime = completed ? Time.time + combinationCompleteAuraHoldSeconds : -999f;

            EnsureCombinationAura();
            if (_combinationAuraRoot == null) return;

            _combinationAuraRoot.SetActive(true);
            _combinationAuraRoot.transform.position   = ResolveCombinationAuraPosition();
            _combinationAuraRoot.transform.localScale  = Vector3.one * (completed ? combinationCompleteAuraScale : combinationReadyAuraScale);

            var aura = _combinationAuraRoot.GetComponent<ElementAuraDummy>();
            if (aura != null)
                aura.Configure(GetComboAuraColor(spellId), completed ? combinationCompleteAuraScale : combinationReadyAuraScale, auraTimeFocusExemptLayerName);
        }

        private void StopCombinationAuraFeedback()
        {
            _combinationAuraSpell = SpellId.None;
            _combinationAuraCompleted = false;
            if (_combinationAuraRoot != null) _combinationAuraRoot.SetActive(false);
        }

        private void UpdateCombinationAuraFeedback()
        {
            if (_combinationAuraRoot == null || _combinationAuraSpell == SpellId.None) return;
            _combinationAuraRoot.transform.position = ResolveCombinationAuraPosition();
            if (_combinationAuraCompleted && Time.time > _combinationAuraUntilTime)
                StopCombinationAuraFeedback();
        }

        private void EnsureCombinationAura()
        {
            if (_combinationAuraRoot != null) return;
            _combinationAuraRoot = ElementAuraDummy
                .Create("CombinationAura_Dummy", Color.white, combinationReadyAuraScale, auraTimeFocusExemptLayerName)
                .gameObject;
            _combinationAuraRoot.hideFlags = HideFlags.DontSave;
            _combinationAuraRoot.SetActive(false);
        }

        // ── 음성 부스트 ───────────────────────────────────────────────────────

        private void HandleVoiceCommand(ElementType element)
        {
            if (IsVoiceInputSuppressedForCombinationFocus()) return;
            if (PrototypeArmedElement == ElementType.None || PrototypeArmedElement != element) return;
            ActivateVoiceBoost(element);
        }

        private void ActivateVoiceBoost(ElementType element)
        {
            _isVoiceBoostActive = true;
            _voiceBoostExpiry   = Time.time + voiceBoostDuration;
            elementAuraManager?.SetVoiceBoosted(true);
            PlayVoiceBoostSfx();
            _lastVoiceBoostStatus = $"음성 부스트: {element} 활성";
            PrototypeDebugStatus  = $"음성 부스트: {element}";
        }

        private void DeactivateVoiceBoost()
        {
            if (!_isVoiceBoostActive) return;
            _isVoiceBoostActive = false;
            _voiceBoostExpiry   = -999f;
            elementAuraManager?.SetVoiceBoosted(false);
            _lastVoiceBoostStatus = "음성: 비활성";
        }

        private void UpdateVoiceBoost()
        {
            if (!_isVoiceBoostActive) return;
            if (Time.time >= _voiceBoostExpiry || string.IsNullOrEmpty(_currentRightGesture))
                DeactivateVoiceBoost();
        }

        private void PlayVoiceBoostSfx()
        {
            if (_voiceBoostAudio == null)
            {
                _voiceBoostAudio = gameObject.AddComponent<AudioSource>();
                _voiceBoostAudio.playOnAwake  = false;
                _voiceBoostAudio.spatialBlend = 0f;
                _voiceBoostAudio.volume       = 0.8f;
            }

            if (voiceBoostClip != null)
                _voiceBoostAudio.clip = voiceBoostClip;
            else
            {
                _cachedBoostTone ??= GenerateVoiceBoostTone();
                _voiceBoostAudio.clip = _cachedBoostTone;
            }

            _voiceBoostAudio.Stop();
            _voiceBoostAudio.Play();
        }

        private static AudioClip GenerateVoiceBoostTone()
        {
            const int sampleRate = 44100;
            const float duration = 0.35f;
            var samples = (int)(sampleRate * duration);
            var data    = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                float t        = (float)i / sampleRate;
                float freq     = Mathf.Lerp(520f, 1040f, t / duration);
                float envelope = Mathf.Sin(Mathf.PI * t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.45f;
            }
            var clip = AudioClip.Create("VoiceBoostTone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ── 유틸리티 ─────────────────────────────────────────────────────────

        private void RememberCast(ElementType element, SpellId spellId, float manaCost, string costStatus)
        {
            _lastCastElement    = element;
            _lastCastSpellId    = spellId;
            _lastManaCost       = manaCost;
            _lastManaCostStatus = costStatus;
        }

        private void RecordCastBlocked(SpellId spellId, ElementType element, float manaCost, string costStatus)
        {
            _lastCastSpellId    = spellId;
            _lastCastElement    = element;
            _lastManaCost       = manaCost;
            _lastManaCostStatus = costStatus;
            _lastCastStatus     = $"차단: {element} {spellId}";
        }

        private Transform ResolveSpawnPoint(SpellId spellId)
        {
            if (IsSingleSpell(spellId))
                return rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;
            return leftHandSpawnPoint != null ? leftHandSpawnPoint : rightHandSpawnPoint;
        }

        private Vector3 ResolveAimDirection(Vector3 _)
        {
            var dir = headTransform != null ? headTransform.forward : transform.forward;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
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
            var parent = rightHandSpawnPoint;
            while (parent != null)
            {
                if (parent.name is "TrackingSpace" or "Camera Offset" or "XR Origin")
                    return parent;
                parent = parent.parent;
            }
            return null;
        }

        private void ParentToSpellRoot(GameObject obj)
        {
            if (obj != null && spellSpawnRoot != null)
                obj.transform.SetParent(spellSpawnRoot, true);
        }

        private Vector3 ResolveCombinationAuraPosition()
        {
            if (leftHandSpawnPoint != null && rightHandSpawnPoint != null)
                return (leftHandSpawnPoint.position + rightHandSpawnPoint.position) * 0.5f;
            return rightHandSpawnPoint != null ? rightHandSpawnPoint.position : transform.position;
        }

        private bool IsCombinationCastAllowed(SpellId spellId) =>
            !SpellHitData.IsComboSpellId(spellId) ||
            allowCombinationSpellCasts ||
            (focusModeController != null && focusModeController.IsFocusActive);

        private bool IsVoiceInputSuppressedForCombinationFocus() =>
            ignoreVoiceDuringCombinationFocus &&
            focusModeController != null &&
            focusModeController.IsFocusActive;

        private static bool IsSingleSpell(SpellId spellId) =>
            spellId is SpellId.Single_Pointer or SpellId.Single_Wave or SpellId.Single_Strike;

        private GameObject CreateProjectileObject(SpellDatabase.SpellData data, ElementType element, Vector3 position, Quaternion rotation)
        {
            if (!useDebugPrimitiveProjectiles && data.prefab != null)
                return Instantiate(data.prefab, position, rotation);

            var go  = new GameObject($"Spell_{data.spellId}");
            go.transform.SetPositionAndRotation(position, rotation);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = (IsSingleSpell(data.spellId) ? 0.22f : 0.34f) * debugProjectileScale;
            var rb  = go.AddComponent<Rigidbody>();
            rb.useGravity  = false;
            rb.isKinematic = true;
            return go;
        }

        private static Color GetComboAuraColor(SpellId spellId) => spellId switch
        {
            SpellId.Combo_FireIce     => new Color(0.9f, 0.55f, 1f,  1f),
            SpellId.Combo_IceThunder  => new Color(0.35f, 0.9f, 1f,  1f),
            SpellId.Combo_ThunderFire => new Color(1f, 0.55f, 0.1f,  1f),
            _                         => Color.white
        };
    }
}
