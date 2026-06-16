using System.Collections.Generic;
using ArcaneVR.Core;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// 얼음 마법 전용 모듈. SpellCaster에서 Init()으로 공유 참조를 주입받아 동작한다.
    /// </summary>
    public class IceSpellModule : MonoBehaviour
    {
        [Header("── 발사체 프리팹 ──")]
        [SerializeField] private GameObject iceOrbPrefab;
        [SerializeField] private GameObject iceProjectilePrefab;
        [SerializeField] private GameObject impactVfxPrefab;

        [Header("── 발사 설정 ──")]
        [SerializeField] private Vector3 palmOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float orbScale = 0.12f;
        [SerializeField] private float projectileScale = 0.12f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float projectileLifetime = 6f;
        [SerializeField] private float minThrowSpeed = 1.5f;
        [SerializeField] private float launchCooldown = 1.2f;
        [SerializeField] private float velocitySampleDuration = 0.12f;

        [Header("── 포물선 궤적 설정 ──")]
        [SerializeField] private bool useArcTrajectory = true;
        [SerializeField] private float arcVelocityMultiplier = 4f;
        [SerializeField] private float arcUpwardBoost = 1.4f;
        [SerializeField] private float minArcLaunchSpeed = 20f;
        [SerializeField] private float maxArcLaunchSpeed = 40f;

        [Header("── 오라 위치 설정 ──")]
        [SerializeField] private Vector3 auraOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float auraScale = 0.18f;
        [SerializeField] private string timeFocusExemptLayer = "TimeFocusExempt";
        [SerializeField] private AudioClip auraLoopClip;
        [SerializeField] private float auraLoopVolume = 0.55f;

        [Header("── 선언 / 발사 사운드 ──")]
        [SerializeField] private AudioClip armSfxClip;
        [SerializeField] private AudioClip castSfxClip;
        [SerializeField] private AudioClip impactSfxClip;
        [SerializeField] private float armSfxVolume = 0.72f;
        [SerializeField] private float castSfxVolume = 0.95f;
        [SerializeField] private float impactSfxVolume = 1f;

        // SpellCaster에서 주입
        private Transform _rightSpawn;
        private Transform _spawnRoot;
        private Transform _head;
        private SpellDatabase _database;
        private ElementAuraManager _auraManager;

        private readonly Queue<(float time, Vector3 pos)> _velocitySamples = new();
        private GameObject _orbInstance;
        private GameObject _auraInstance;
        private AudioSource _sfxAudioSource;
        private AudioSource _loopAudioSource;
        private float _lastLaunchTime = -999f;
        private bool _armed;
        private int _activeProjectileCount;

        public bool IsArmed => _armed;
        public bool HasActiveProjectile => _activeProjectileCount > 0;

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

        public void Arm()
        {
            _armed = true;
            ShowAura();
            ShowOrb();
            StartAuraLoop();
            if (armSfxClip != null || auraLoopClip == null)
                PlayElementSfx(armSfxClip, ArcaneSpellSfxCue.ElementArm, armSfxVolume);
        }

        public void Disarm()
        {
            _armed = false;
            StopAuraLoop();
            HideOrb();
            RefreshAuraVisibility();
        }

        // 타임 포커스(마도서 등) 중 구체 표시 제어
        public void SetOrbVisible(bool visible)
        {
            if (_orbInstance != null)
                _orbInstance.SetActive(visible);
        }

        // ── 매 프레임 갱신 ────────────────────────────────────────────────────

        /// <returns>이번 프레임에 발사체가 생성되면 true</returns>
        public bool Tick(Vector3 trackingPosition)
        {
            if (!_armed || _rightSpawn == null) return false;

            UpdateOrbTransform();
            UpdateAuraTransform();

            // 속도 샘플 수집
            _velocitySamples.Enqueue((Time.time, trackingPosition));
            while (_velocitySamples.Count > 0 && _velocitySamples.Peek().time < Time.time - velocitySampleDuration)
                _velocitySamples.Dequeue();

            if (_velocitySamples.Count < 2 || Time.time - _lastLaunchTime <= launchCooldown)
                return false;

            var oldest  = _velocitySamples.Peek();
            var dt      = Mathf.Max(0.001f, Time.time - oldest.time);
            var localVel = (trackingPosition - oldest.pos) / dt;
            var worldVel = ToWorldVector(localVel);
            var forward  = _head != null ? _head.forward : transform.forward;
            var fwdSpeed = Vector3.Dot(worldVel, forward);

            if (fwdSpeed < minThrowSpeed) return false;

            LaunchProjectile(worldVel);
            return true;
        }

        // ── 내부 로직 ─────────────────────────────────────────────────────────

        private void LaunchProjectile(Vector3 worldVelocity)
        {
            _lastLaunchTime = Time.time;
            var launchPos = _rightSpawn.TransformPoint(palmOffset);
            var forward   = _head != null ? _head.forward : transform.forward;
            var direction = worldVelocity.sqrMagnitude > 0.01f
                ? worldVelocity.normalized
                : forward.normalized;

            var launchVel = useArcTrajectory
                ? ComputeArcVelocity(worldVelocity, forward)
                : direction * projectileSpeed;

            var projectile = iceProjectilePrefab != null
                ? Instantiate(iceProjectilePrefab, launchPos, Quaternion.LookRotation(direction))
                : CreateFallbackProjectile(launchPos, direction);

            ParentToRoot(projectile);
            projectile.transform.localScale = Vector3.one * projectileScale;

            var data = _database?.Get(SpellId.Single_Wave);
            var sp = projectile.GetComponent<SpellProjectile>();
            if (sp == null)
                sp = projectile.AddComponent<SpellProjectile>();
            sp.spellId = SpellId.Single_Wave;
            sp.InitializePrototype(
                useArcTrajectory ? 0f : projectileSpeed,
                direction,
                ElementType.Ice,
                data?.statusEffect   ?? StatusEffect.Slow,
                data?.damage         ?? 8f,
                data?.statusDuration ?? 3f);
            sp.ConfigureImpactAudio(impactSfxClip, impactSfxVolume);
            sp.ConfigureImpactVfx(impactVfxPrefab);

            var rb = projectile.GetComponent<Rigidbody>();
            if (rb == null)
                rb = projectile.AddComponent<Rigidbody>();
            rb.isKinematic           = false;
            rb.useGravity            = useArcTrajectory;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity        = launchVel;

            TrackProjectileLifetime(projectile);
            PlayElementSfx(castSfxClip, ArcaneSpellSfxCue.SpellCast, castSfxVolume);

            HideOrb();
            RefreshAuraVisibility();
            Destroy(projectile, projectileLifetime);
        }

        private Vector3 ComputeArcVelocity(Vector3 throwVelocity, Vector3 cameraForward)
        {
            var baseVel = throwVelocity.sqrMagnitude > 0.01f
                ? throwVelocity * Mathf.Max(0.1f, arcVelocityMultiplier)
                : cameraForward.normalized * minArcLaunchSpeed;

            baseVel += Vector3.up * arcUpwardBoost;
            var speed = baseVel.magnitude;
            if (speed < minArcLaunchSpeed && baseVel.sqrMagnitude > 0.001f)
                baseVel = baseVel.normalized * minArcLaunchSpeed;
            else if (speed > maxArcLaunchSpeed)
                baseVel = baseVel.normalized * maxArcLaunchSpeed;

            return baseVel;
        }

        private void ShowOrb()
        {
            if (_orbInstance != null) Destroy(_orbInstance);
            if (_rightSpawn == null) return;

            var pos = _rightSpawn.TransformPoint(palmOffset);
            _orbInstance = iceOrbPrefab != null
                ? Instantiate(iceOrbPrefab, pos, Quaternion.identity)
                : CreateFallbackSphere("IceOrb_Dummy", pos, orbScale);

            ParentToRoot(_orbInstance);
            _orbInstance.transform.localScale = Vector3.one * orbScale;
            _velocitySamples.Clear();
        }

        private void HideOrb()
        {
            if (_orbInstance != null) Destroy(_orbInstance);
            _orbInstance = null;
            _velocitySamples.Clear();
        }

        private void UpdateOrbTransform()
        {
            if (_orbInstance == null || _rightSpawn == null) return;
            _orbInstance.transform.position = _rightSpawn.TransformPoint(palmOffset);
        }

        private void ShowAura()
        {
            if (_auraManager != null)
            {
                _auraManager.Show(ElementType.Ice, _rightSpawn, auraScale);
                return;
            }

            if (_auraInstance != null || _rightSpawn == null) return;

            _auraInstance = ElementAuraDummy
                .Create("IceAura_Dummy", new Color(0.24f, 0.78f, 1f, 1f), auraScale, timeFocusExemptLayer)
                .gameObject;

            ParentToRoot(_auraInstance);
            ApplyTimeFocusLayer(_auraInstance);
            UpdateAuraTransform();
        }

        private void HideAura()
        {
            _auraManager?.Hide();
            if (_auraInstance != null) Destroy(_auraInstance);
            _auraInstance = null;
        }

        private void RefreshAuraVisibility()
        {
            if (_armed || _activeProjectileCount > 0)
                ShowAura();
            else
                HideAura();
        }

        private void TrackProjectileLifetime(GameObject projectile)
        {
            if (projectile == null)
                return;

            _activeProjectileCount++;
            var tracker = projectile.GetComponent<IceProjectileTracker>();
            if (tracker == null)
                tracker = projectile.AddComponent<IceProjectileTracker>();

            tracker.Bind(this);
        }

        private void NotifyProjectileDestroyed()
        {
            _activeProjectileCount = Mathf.Max(0, _activeProjectileCount - 1);
            if (!_armed)
                RefreshAuraVisibility();
        }

        private void UpdateAuraTransform()
        {
            if (_auraInstance == null || _rightSpawn == null) return;
            _auraInstance.transform.SetPositionAndRotation(
                _rightSpawn.position + _rightSpawn.rotation * auraOffset,
                _rightSpawn.rotation);
        }

        private Vector3 ToWorldVector(Vector3 localVec)
        {
            if (_rightSpawn == null) return localVec;
            var parent = _rightSpawn;
            while (parent != null)
            {
                if (parent.name is "TrackingSpace" or "Camera Offset" or "XR Origin")
                    return parent.TransformVector(localVec);
                parent = parent.parent;
            }
            return localVec;
        }

        private void ParentToRoot(GameObject obj)
        {
            if (obj != null && _spawnRoot != null)
                obj.transform.SetParent(_spawnRoot, true);
        }

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

            ArcaneSpellSfx.Play(audioSource, ElementType.Ice, cue, volume);
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

        private void StartAuraLoop()
        {
            if (auraLoopClip == null)
                return;

            var audioSource = EnsureLoopAudioSource();
            audioSource.clip = auraLoopClip;
            audioSource.volume = Mathf.Clamp01(auraLoopVolume);
            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        private void StopAuraLoop()
        {
            if (_loopAudioSource == null)
                return;

            _loopAudioSource.Stop();
            _loopAudioSource.clip = null;
        }

        private AudioSource EnsureLoopAudioSource()
        {
            if (_loopAudioSource != null)
                return _loopAudioSource;

            _loopAudioSource = gameObject.AddComponent<AudioSource>();
            _loopAudioSource.playOnAwake = false;
            _loopAudioSource.loop = true;
            _loopAudioSource.spatialBlend = 0f;
            _loopAudioSource.dopplerLevel = 0f;
            return _loopAudioSource;
        }

        private static GameObject CreateFallbackProjectile(Vector3 pos, Vector3 dir)
        {
            var go = new GameObject("Ice_Prototype");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.1f;
            return go;
        }

        private static GameObject CreateFallbackSphere(string goName, Vector3 pos, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = goName;
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            return go;
        }

        private sealed class IceProjectileTracker : MonoBehaviour
        {
            private IceSpellModule _owner;

            public void Bind(IceSpellModule owner)
            {
                _owner = owner;
            }

            private void OnDestroy()
            {
                _owner?.NotifyProjectileDestroyed();
            }
        }
    }
}
