using ArcaneVR.Core;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// 불 마법 전용 모듈. SpellCaster에서 Init()으로 공유 참조를 주입받아 동작한다.
    /// </summary>
    public class FireSpellModule : MonoBehaviour
    {
        [Header("── 발사체 프리팹 ──")]
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private Material explosionMaterialOverride;

        [Header("── 오라 프리팹 (비워두면 기본 오라 사용) ──")]
        [SerializeField] private GameObject auraPrefab;
        [SerializeField] private Color auraColor = new Color(1f, 0.35f, 0.05f, 1f);

        [Header("── 발사 설정 ──")]
        [SerializeField] private float spawnForwardOffset = 0.15f;
        [SerializeField] private float projectileSpeed = 13f;
        [SerializeField] private float recoilVelocityThreshold = 0.5f;
        [SerializeField] private float cooldown = 0.25f;
        [SerializeField] private float projectileScale = 0.01f;
        [SerializeField] private float explosionScale = 0.15f;
        [SerializeField] private float explosionLifetime = 1.5f;
        [SerializeField] private float projectileLifetime = 5f;

        [Header("── 오라 위치 설정 ──")]
        [SerializeField] private Vector3 auraOffset = new Vector3(0f, 0f, 0.08f);
        [SerializeField] private float auraScale = 0.18f;
        [SerializeField] private string timeFocusExemptLayer = "TimeFocusExempt";

        // SpellCaster에서 주입
        private Transform _rightSpawn;
        private Transform _spawnRoot;
        private Transform _head;
        private SpellDatabase _database;
        private ElementAuraManager _auraManager;

        private GameObject _auraInstance;
        private float _lastShotTime = -999f;
        private bool _armed;

        public bool IsArmed => _armed;

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
        }

        public void Disarm()
        {
            _armed = false;
            HideAura();
        }

        // ── 매 프레임 갱신 (SpellCaster.UpdateRightGestureAttack에서 호출) ────

        /// <returns>이번 프레임에 발사체가 생성되면 true</returns>
        public bool Tick(Vector3 worldTrackingVelocity)
        {
            if (!_armed || _rightSpawn == null) return false;

            UpdateAuraTransform();

            var upSpeed = Vector3.Dot(worldTrackingVelocity, Vector3.up);
            if (upSpeed < recoilVelocityThreshold || Time.time - _lastShotTime <= cooldown)
                return false;

            _lastShotTime = Time.time;
            FireProjectile();
            return true;
        }

        // ── 내부 로직 ─────────────────────────────────────────────────────────

        private void FireProjectile()
        {
            var aimDir = ResolveAimDir();
            var origin  = _rightSpawn.position + aimDir * spawnForwardOffset;

            var projectile = fireballPrefab != null
                ? Instantiate(fireballPrefab, origin, Quaternion.LookRotation(aimDir))
                : CreateFallbackProjectile(origin, aimDir);

            ParentToRoot(projectile);

            if (projectile.TryGetComponent<FireballProjectile>(out var fb))
            {
                fb.ConfigureLaunch(projectileSpeed, projectileScale);
                fb.ConfigureImpact(explosionPrefab, explosionScale, explosionLifetime, explosionMaterialOverride);
            }

            var data = _database?.Get(SpellId.Single_Pointer);
            var sp   = projectile.GetComponent<SpellProjectile>() ?? projectile.AddComponent<SpellProjectile>();
            sp.spellId = SpellId.Single_Pointer;
            sp.InitializePrototype(
                data?.projectileSpeed > 0f ? data.projectileSpeed : projectileSpeed,
                aimDir,
                ElementType.Fire,
                data?.statusEffect   ?? StatusEffect.Burn,
                data?.damage         ?? 10f,
                data?.statusDuration ?? 3f);

            Destroy(projectile, projectileLifetime);
        }

        private void ShowAura()
        {
            if (_auraManager != null)
            {
                _auraManager.Show(ElementType.Fire, _rightSpawn);
                return;
            }

            if (_auraInstance != null || _rightSpawn == null) return;

            _auraInstance = auraPrefab != null
                ? Instantiate(auraPrefab)
                : ElementAuraDummy.Create("FireAura_Dummy", auraColor, auraScale, timeFocusExemptLayer).gameObject;

            ParentToRoot(_auraInstance);
            ApplyTimeFocusLayer(_auraInstance);
            UpdateAuraTransform();
        }

        private void HideAura()
        {
            if (_auraInstance != null) Destroy(_auraInstance);
            _auraInstance = null;
        }

        private void UpdateAuraTransform()
        {
            if (_auraInstance == null || _rightSpawn == null) return;
            _auraInstance.transform.SetPositionAndRotation(
                _rightSpawn.position + _rightSpawn.rotation * auraOffset,
                _rightSpawn.rotation);
        }

        private Vector3 ResolveAimDir()
        {
            var dir = _head != null ? _head.forward : transform.forward;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
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

        private static GameObject CreateFallbackProjectile(Vector3 pos, Vector3 dir)
        {
            var go = new GameObject("Fire_Prototype");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.08f;
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity  = false;
            rb.isKinematic = true;
            return go;
        }
    }
}
