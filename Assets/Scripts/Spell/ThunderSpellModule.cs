using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using DigitalRuby.LightningBolt;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// 번개 마법 전용 모듈. SpellCaster에서 Init()으로 공유 참조를 주입받아 동작한다.
    /// </summary>
    public class ThunderSpellModule : MonoBehaviour
    {
        [Header("── 오라 프리팹 / 루프 사운드 ──")]
        [SerializeField] private GameObject auraPrefab;
        [SerializeField] private AudioClip auraAudioClip;
        [SerializeField] private AudioClip beamLoopAudioClip;
        [SerializeField] private float auraLoopVolume = 0.55f;
        [SerializeField] private float beamLoopVolume = 0.8f;
        [SerializeField] private GameObject beamPrefab;
        [SerializeField] private float beamPrefabScale = 1f;
        [Tooltip("번개 움직임 폭 (ChaosFactor). 낮을수록 직선에 가깝고 좁게 움직임. 기본값 0.15")]
        [SerializeField] private float beamChaosFactor = 0.15f;
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField] private float impactVfxScale = 1f;
        [SerializeField] private float impactVfxLifetime = 1.25f;
        [SerializeField] private float impactVfxInterval = 0.12f;

        [Header("── 빔 설정 ──")]
        [SerializeField] private float rangeMeters = 20f;
        [SerializeField] private float damage = 16f;
        [SerializeField] private float statusDuration = 2.5f;
        [SerializeField] private float continuousFireSeconds = 1.5f;
        [SerializeField] private float hitTickInterval = 0.25f;
        [SerializeField] private float laserWidth = 0.08f;
        [SerializeField] private float laserDownAngleDegrees = 8f;
        [Tooltip("히트 판정 구체 반경 (m). 클수록 번개 시각 오차를 흡수. 기본값 0.15")]
        [SerializeField] private float sphereCastRadius = 0.15f;
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

        [Header("── 선언 / 발사 사운드 ──")]
        [SerializeField] private AudioClip armSfxClip;
        [SerializeField] private AudioClip castSfxClip;
        [SerializeField] private float armSfxVolume = 0.72f;
        [SerializeField] private float castSfxVolume = 1f;

        // SpellCaster에서 주입
        private Transform _rightSpawn;
        private Transform _spawnRoot;
        private Transform _head;
        private SpellDatabase _database;
        private ElementAuraManager _auraManager;

        private GameObject _auraInstance;
        private AudioSource _loopAudio;
        private AudioSource _sfxAudioSource;
        private Renderer _auraRenderer;
        private GameObject _beamInstance;
        private LineRenderer _beamLine;
        private LightningBoltScript _lightningBolt;
        private int _beamCreatedFrame = -1;

        private bool _charged;
        private float _lastChargeTime = -999f;
        private float _lastShootTime = -999f;
        private float _beamEndTime = -999f;
        private float _nextHitTime = -999f;
        private float _lastImpactVfxTime = -999f;

        private bool _armed;
        private bool _isShootMode;

        public bool IsArmed => _armed;
        public bool IsBeamActive => (_beamLine != null || _lightningBolt != null) && Time.time <= _beamEndTime;

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
                PlayLoopClip(auraAudioClip, auraLoopVolume);
                if (armSfxClip != null || auraAudioClip == null)
                    PlayElementSfx(armSfxClip, ArcaneSpellSfxCue.ElementArm, armSfxVolume);
            }
            else if (!requireChargeBeforeShoot || IsChargeAvailable())
            {
                _charged = true;
                ShowAura();
                _lastShootTime = Time.time;
                StartBeam();
                PlayLoopClip(beamLoopAudioClip != null ? beamLoopAudioClip : auraAudioClip,
                    beamLoopAudioClip != null ? beamLoopVolume : auraLoopVolume);
                PlayElementSfx(castSfxClip, ArcaneSpellSfxCue.SpellCast, castSfxVolume);
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
            StopLoopClip();
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
            if ((_beamLine != null || _lightningBolt != null) && Time.time < _beamEndTime)
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
            return _beamLine != null || _lightningBolt != null;
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

            var radius = Mathf.Max(0.01f, sphereCastRadius);
            if (Physics.SphereCast(origin, radius, direction, out var hit, rangeMeters - radius, hitMask, QueryTriggerInteraction.Collide))
            {
                hitPoint = hit.point;
                hitCol   = hit.collider;
            }

            EnsureBeam();
            if (_lightningBolt != null)
            {
                _lightningBolt.StartObject = null;
                _lightningBolt.EndObject = null;
                _lightningBolt.StartPosition = origin;
                _lightningBolt.EndPosition = hitPoint;
                if (Time.frameCount > _beamCreatedFrame)
                    _lightningBolt.Trigger();
            }
            else if (_beamLine != null)
            {
                _beamLine.SetPosition(0, origin);
                _beamLine.SetPosition(1, hitPoint);
            }

            if (Time.time >= _nextHitTime)
            {
                _nextHitTime = Time.time + Mathf.Max(0.05f, hitTickInterval);
                ApplyHit(hitCol, hitPoint);
            }
        }

        private void ApplyHit(Collider hitCollider, Vector3 hitPoint)
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
                SpawnImpactVfx(hitPoint);
                return;
            }

            var boss = hitCollider.GetComponentInParent<BossAI>();
            var golemTarget = boss != null
                ? boss.GetComponent<GolemCombatTarget>() ?? boss.GetComponentInParent<GolemCombatTarget>()
                : hitCollider.GetComponentInParent<GolemCombatTarget>();
            golemTarget?.OnHit(hitData);
            SpawnImpactVfx(hitPoint);
        }

        private void EnsureBeam()
        {
            if (_beamLine != null || _lightningBolt != null) return;

            if (beamPrefab != null)
            {
                _beamInstance = Instantiate(beamPrefab);
                _beamInstance.transform.localScale = Vector3.one * Mathf.Max(0.001f, beamPrefabScale);
                if (_spawnRoot != null)
                    _beamInstance.transform.SetParent(_spawnRoot, true);

                _lightningBolt = _beamInstance.GetComponent<LightningBoltScript>();
                if (_lightningBolt != null)
                {
                    _lightningBolt.ManualMode = true;
                    _lightningBolt.Duration = Mathf.Max(0.02f, hitTickInterval);
                    _lightningBolt.ChaosFactor = Mathf.Max(0f, beamChaosFactor);
                    var lr = _beamInstance.GetComponent<LineRenderer>();
                    if (lr != null)
                        lr.widthMultiplier = Mathf.Max(0.001f, beamPrefabScale);
                    _beamCreatedFrame = Time.frameCount;
                    return;
                }

                _beamLine = _beamInstance.GetComponent<LineRenderer>();
                if (_beamLine != null)
                {
                    _beamCreatedFrame = Time.frameCount;
                    return;
                }
            }

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
            _beamCreatedFrame = Time.frameCount;
        }

        private void StopBeam()
        {
            _beamEndTime = -999f;
            _nextHitTime = -999f;
            if (_isShootMode)
                StopLoopClip();
            if (_beamInstance != null) Destroy(_beamInstance);
            _beamInstance = null;
            _beamLine     = null;
            _lightningBolt = null;
            _beamCreatedFrame = -1;
        }

        private void ShowAura()
        {
            if (_auraManager != null)
            {
                _auraManager.Show(ElementType.Thunder, _rightSpawn, auraScale);
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

            UpdateAuraTransform();
        }

        private void HideAura()
        {
            if (_auraInstance != null) Destroy(_auraInstance);
            _auraInstance = null;
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

        private void SpawnImpactVfx(Vector3 hitPoint)
        {
            if (impactVfxPrefab == null || Time.time - _lastImpactVfxTime < Mathf.Max(0.01f, impactVfxInterval))
                return;

            _lastImpactVfxTime = Time.time;
            var impact = Instantiate(impactVfxPrefab, hitPoint, Quaternion.identity);
            impact.transform.localScale = Vector3.one * Mathf.Max(0.01f, impactVfxScale);

            foreach (var particle in impact.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            Destroy(impact, Mathf.Max(0.05f, impactVfxLifetime));
        }

        private bool IsChargeAvailable() =>
            _charged || Time.time - _lastChargeTime <= Mathf.Max(0f, chargeGraceSeconds);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var spawnRef = _rightSpawn != null ? _rightSpawn : transform;
            var origin   = spawnRef.position + spawnRef.rotation * auraOffset;
            var dir      = spawnRef.forward.sqrMagnitude > 0.001f ? spawnRef.forward.normalized : transform.forward;
            dir = Vector3.RotateTowards(dir, Vector3.down, Mathf.Max(0f, laserDownAngleDegrees) * Mathf.Deg2Rad, 0f).normalized;
            var radius   = Mathf.Max(0.01f, sphereCastRadius);
            var length   = Mathf.Max(0f, rangeMeters - radius);

            // 판정 볼륨 전체를 구체 배열로 시각화
            UnityEngine.Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.15f);
            int steps = Mathf.Max(2, Mathf.RoundToInt(length / radius));
            for (int i = 0; i <= steps; i++)
                UnityEngine.Gizmos.DrawSphere(origin + dir * (length * i / steps), radius);

            // 외곽선
            UnityEngine.Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.7f);
            UnityEngine.Gizmos.DrawWireSphere(origin, radius);
            UnityEngine.Gizmos.DrawWireSphere(origin + dir * length, radius);
        }
#endif

        private void ApplyTimeFocusLayer(GameObject root)
        {
            var layer = LayerMask.NameToLayer(timeFocusExemptLayer);
            if (layer < 0 || root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private void PlayElementSfx(AudioClip explicitClip, ArcaneSpellSfxCue cue, float volume)
        {
            var audioSource = EnsureSfxAudioSource();
            if (audioSource == null)
                return;

            if (explicitClip != null)
            {
                audioSource.PlayOneShot(explicitClip, Mathf.Clamp01(volume));
                return;
            }

            ArcaneSpellSfx.Play(audioSource, ElementType.Thunder, cue, volume);
        }

        private AudioSource EnsureSfxAudioSource()
        {
            if (_sfxAudioSource != null)
                return _sfxAudioSource;

            _sfxAudioSource = gameObject.AddComponent<AudioSource>();
            _sfxAudioSource.playOnAwake = false;
            _sfxAudioSource.loop = false;
            _sfxAudioSource.spatialBlend = 0f;
            _sfxAudioSource.dopplerLevel = 0f;
            return _sfxAudioSource;
        }

        private void PlayLoopClip(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            var audioSource = EnsureLoopAudioSource();
            if (audioSource.clip != clip)
                audioSource.clip = clip;

            audioSource.volume = Mathf.Clamp01(volume);
            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        private void StopLoopClip()
        {
            if (_loopAudio == null)
                return;

            _loopAudio.Stop();
            _loopAudio.clip = null;
        }

        private AudioSource EnsureLoopAudioSource()
        {
            if (_loopAudio != null)
                return _loopAudio;

            _loopAudio = gameObject.AddComponent<AudioSource>();
            _loopAudio.playOnAwake = false;
            _loopAudio.loop = true;
            _loopAudio.spatialBlend = 0f;
            _loopAudio.dopplerLevel = 0f;
            return _loopAudio;
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
