using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// 번개 마법 전용 모듈. SpellCaster에서 Init()으로 공유 참조를 주입받아 동작한다.
    /// </summary>
    public class ThunderSpellModule : MonoBehaviour
    {
        [Header("── 오라 / 사운드 ──")]
        [SerializeField] private GameObject auraPrefab;
        [SerializeField] private AudioClip auraAudioClip;

        [Header("── 빔 설정 ──")]
        [SerializeField] private float rangeMeters = 20f;
        [SerializeField] private float damage = 16f;
        [SerializeField] private float statusDuration = 2.5f;
        [SerializeField] private float continuousFireSeconds = 1.5f;
        [SerializeField] private float hitTickInterval = 0.25f;
        [SerializeField] private float laserWidth = 0.08f;
        [SerializeField] private float laserDownAngleDegrees = 8f;
        [SerializeField] private Color laserColor = new Color(1f, 0.88f, 0.15f, 1f);
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("── 충전 / 유예 시간 ──")]
        [SerializeField] private bool requireChargeBeforeShoot;
        [SerializeField] private float chargeGraceSeconds = 2.0f;
        [SerializeField] private float shootPoseGraceSeconds = 0.6f;

        [Header("── 오라 위치 설정 ──")]
        [SerializeField] private Vector3 auraOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float auraScale = 0.16f;
        [SerializeField] private string timeFocusExemptLayer = "TimeFocusExempt";

        // SpellCaster에서 주입
        private Transform _rightSpawn;
        private Transform _spawnRoot;
        private Transform _head;
        private SpellDatabase _database;
        private ElementAuraManager _auraManager;

        private GameObject _auraInstance;
        private AudioSource _auraAudio;
        private Renderer _auraRenderer;
        private GameObject _beamInstance;
        private LineRenderer _beamLine;

        private bool _charged;
        private float _lastChargeTime = -999f;
        private float _lastShootTime = -999f;
        private float _beamEndTime = -999f;
        private float _nextHitTime = -999f;

        private bool _armed;
        private bool _isShootMode;

        public bool IsArmed => _armed;
        public bool IsBeamActive => _beamLine != null && Time.time <= _beamEndTime;

        // ── 초기화 ────────────────────────────────────────────────────────────

        public void Init(
            Transform rightSpawn,
            Transform spawnRoot,
            Transform head,
            SpellDatabase database,
            ElementAuraManager auraManager)
        {
            _rightSpawn  = rightSpawn;
            _spawnRoot   = spawnRoot;
            _head        = head;
            _database    = database;
            _auraManager = auraManager;
        }

        // ── 상태 전환 ─────────────────────────────────────────────────────────

        // gestureName: "Thunder"(충전) 또는 "ThunderShoot"(발사)
        public void Arm(string gestureName)
        {
            _armed       = true;
            _isShootMode = gestureName == "ThunderShoot";

            if (!_isShootMode)
            {
                _charged       = true;
                _lastChargeTime = Time.time;
                ShowAura();
            }
            else if (!requireChargeBeforeShoot || IsChargeAvailable())
            {
                _charged = true;
                ShowAura();
                _lastShootTime = Time.time;
                StartBeam();
            }
            else
            {
                HideAura();
                StopBeam();
            }
        }

        public void Disarm()
        {
            _armed       = false;
            _isShootMode = false;
            _charged     = false;
            HideAura();
            StopBeam();
        }

        // ── 매 프레임 갱신 ────────────────────────────────────────────────────

        /// <returns>이번 프레임에 히트 판정이 발생하면 true</returns>
        public bool Tick()
        {
            if (!_armed || _rightSpawn == null) return false;

            if (!_isShootMode)
                return TickCharge();
            else
                return TickShoot();
        }

        // ── 내부 로직 ─────────────────────────────────────────────────────────

        private bool TickCharge()
        {
            _charged        = true;
            _lastChargeTime = Time.time;
            UpdateAuraTransform();

            // 빔이 아직 활성 중이면 유지
            if (_beamLine != null && Time.time < _beamEndTime)
            {
                _lastShootTime = Time.time;
                UpdateBeam();
                return true;
            }

            StopBeam();
            return false;
        }

        private bool TickShoot()
        {
            if (requireChargeBeforeShoot && !IsChargeAvailable())
            {
                StopBeam();
                return false;
            }

            _lastShootTime = Time.time;
            UpdateBeam();
            return _beamLine != null;
        }

        private void StartBeam()
        {
            var data = _database?.Get(SpellId.Single_Strike);
            var dur  = continuousFireSeconds > 0f ? continuousFireSeconds : data?.continuousFireSeconds ?? 0f;
            _beamEndTime  = Time.time + Mathf.Max(0f, dur);
            _nextHitTime  = -999f;
        }

        private void UpdateBeam()
        {
            UpdateAuraTransform();

            if (Time.time - _lastShootTime > Mathf.Max(0f, shootPoseGraceSeconds) && !_isShootMode)
            {
                StopBeam();
                return;
            }

            if (Time.time > _beamEndTime)
            {
                StopBeam();
                return;
            }

            var origin    = _rightSpawn.position + _rightSpawn.rotation * auraOffset;
            var direction = _rightSpawn.forward.sqrMagnitude > 0.001f
                ? _rightSpawn.forward.normalized
                : (_head != null ? _head.forward : transform.forward).normalized;

            direction = Vector3.RotateTowards(
                direction,
                Vector3.down,
                Mathf.Max(0f, laserDownAngleDegrees) * Mathf.Deg2Rad,
                0f).normalized;

            var hitPoint  = origin + direction * rangeMeters;
            Collider hitCol = null;

            if (Physics.Raycast(origin, direction, out var hit, rangeMeters, hitMask, QueryTriggerInteraction.Collide))
            {
                hitPoint = hit.point;
                hitCol   = hit.collider;
            }

            EnsureBeam();
            _beamLine.SetPosition(0, origin);
            _beamLine.SetPosition(1, hitPoint);

            if (Time.time >= _nextHitTime)
            {
                _nextHitTime = Time.time + Mathf.Max(0.05f, hitTickInterval);
                ApplyHit(hitCol);
            }
        }

        private void ApplyHit(Collider hitCollider)
        {
            if (hitCollider == null || ArcanePlayerRigResolver.IsPlayerCollider(hitCollider))
                return;

            var data    = _database?.Get(SpellId.Single_Strike);
            var hitData = new SpellHitData(
                SpellId.Single_Strike,
                ElementType.Thunder,
                data?.statusEffect      ?? StatusEffect.Stagger,
                data?.damage            ?? damage,
                data?.statusDuration    ?? statusDuration,
                data?.statusMagnitude   ?? 1f,
                data?.statusTickInterval ?? 0f);

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

        private void EnsureBeam()
        {
            if (_beamLine != null) return;

            _beamInstance = new GameObject("ThunderLaser_Dummy");
            if (_spawnRoot != null)
                _beamInstance.transform.SetParent(_spawnRoot, true);

            _beamLine = _beamInstance.AddComponent<LineRenderer>();
            _beamLine.positionCount = 2;
            _beamLine.startWidth    = laserWidth;
            _beamLine.endWidth      = laserWidth * 0.45f;
            _beamLine.material      = CreateUnlitMaterial(laserColor);
            _beamLine.startColor    = laserColor;
            _beamLine.endColor      = new Color(laserColor.r, laserColor.g, laserColor.b, 0.15f);
        }

        private void StopBeam()
        {
            _beamEndTime = -999f;
            _nextHitTime = -999f;
            if (_beamInstance != null) Destroy(_beamInstance);
            _beamInstance = null;
            _beamLine     = null;
        }

        private void ShowAura()
        {
            if (_auraManager != null)
            {
                _auraManager.Show(ElementType.Thunder, _rightSpawn);
                return;
            }

            if (_auraInstance != null || _rightSpawn == null) return;

            _auraInstance = auraPrefab != null
                ? Instantiate(auraPrefab)
                : ElementAuraDummy.Create("ThunderAura_Dummy", laserColor, auraScale, timeFocusExemptLayer).gameObject;

            _auraInstance.transform.localScale = Vector3.one * auraScale;
            if (_spawnRoot != null) _auraInstance.transform.SetParent(_spawnRoot, true);
            ApplyTimeFocusLayer(_auraInstance);

            _auraRenderer = _auraInstance.GetComponentInChildren<Renderer>();
            _auraAudio    = _auraInstance.AddComponent<AudioSource>();
            _auraAudio.playOnAwake = false;
            _auraAudio.loop        = true;
            _auraAudio.clip        = auraAudioClip;
            if (auraAudioClip != null) _auraAudio.Play();

            UpdateAuraTransform();
        }

        private void HideAura()
        {
            if (_auraInstance != null) Destroy(_auraInstance);
            _auraInstance = null;
            _auraAudio    = null;
            _auraRenderer = null;
        }

        private void UpdateAuraTransform()
        {
            if (_auraInstance == null || _rightSpawn == null) return;
            _auraInstance.transform.SetPositionAndRotation(
                _rightSpawn.position + _rightSpawn.rotation * auraOffset,
                _rightSpawn.rotation);

            if (_auraRenderer != null)
                ApplyMaterialColor(_auraRenderer.material, laserColor);
        }

        private bool IsChargeAvailable() =>
            _charged || Time.time - _lastChargeTime <= Mathf.Max(0f, chargeGraceSeconds);

        private void ApplyTimeFocusLayer(GameObject root)
        {
            var layer = LayerMask.NameToLayer(timeFocusExemptLayer);
            if (layer < 0 || root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Standard");
            var mat = new Material(shader);
            ApplyMaterialColor(mat, color);
            return mat;
        }

        private static void ApplyMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor"))    mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))        mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color);
            }
        }
    }
}
