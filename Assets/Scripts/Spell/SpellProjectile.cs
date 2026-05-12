using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Input;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// Attached to spell prefab. Handles projectile movement and reports collision to CombatManager.
    /// </summary>
    public class SpellProjectile : MonoBehaviour
    {
        public float speed = 15f;
        public SpellId spellId;
        public ElementType element;
        public float damage;
        public StatusEffect statusEffect;
        public float statusDuration;
        public float statusMagnitude = 1f;
        public float statusTickInterval = 0.5f;
        public PoseType prototypePose;

        private Vector3 direction = Vector3.forward;
        private CombatManager combatManager;
        private bool hasHit;
        private float destroyAfterSeconds = 3f;

        public void Initialize(
            SpellId newSpellId,
            ElementType newElement,
            float newDamage,
            float newSpeed,
            StatusEffect newStatusEffect,
            float newStatusDuration,
            Vector3 newDirection,
            CombatManager newCombatManager,
            float newStatusMagnitude = 1f,
            float newStatusTickInterval = 0.5f)
        {
            spellId = newSpellId;
            element = newElement;
            damage = newDamage;
            speed = newSpeed;
            statusEffect = newStatusEffect;
            statusDuration = newStatusDuration;
            statusMagnitude = newStatusMagnitude;
            statusTickInterval = Mathf.Max(0f, newStatusTickInterval);
            direction = newDirection.sqrMagnitude > 0.001f ? newDirection.normalized : transform.forward;
            combatManager = newCombatManager;
        }

        public void InitializePrototype(PoseType pose, float newSpeed, Vector3 newDirection)
        {
            InitializePrototype(pose, newSpeed, newDirection, ElementType.None, StatusEffect.None, 0f, 0f);
        }

        public void InitializePrototype(
            PoseType pose,
            float newSpeed,
            Vector3 newDirection,
            ElementType newElement,
            StatusEffect newStatusEffect,
            float newDamage,
            float newStatusDuration,
            float newStatusMagnitude = 1f,
            float newStatusTickInterval = 0.5f)
        {
            prototypePose = pose;
            spellId = SpellId.None;
            element = newElement;
            damage = newDamage;
            speed = newSpeed;
            statusEffect = newStatusEffect;
            statusDuration = newStatusDuration;
            statusMagnitude = newStatusMagnitude;
            statusTickInterval = Mathf.Max(0f, newStatusTickInterval);
            direction = newDirection.sqrMagnitude > 0.001f ? newDirection.normalized : transform.forward;
            combatManager = null;
        }

        public SpellHitData GetHitData()
        {
            return new SpellHitData(
                spellId,
                element,
                statusEffect,
                damage,
                statusDuration,
                statusMagnitude,
                statusTickInterval);
        }

        private void Awake()
        {
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
        }

        private void Start()
        {
            if (destroyAfterSeconds > 0f)
                Destroy(gameObject, destroyAfterSeconds);
        }

        private void Update()
        {
            transform.position += direction * (speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit || other.attachedRigidbody != null && other.attachedRigidbody.gameObject == gameObject)
                return;

            if (ArcanePlayerRigResolver.IsPlayerCollider(other))
                return;

            var spellTarget = other.GetComponentInParent<ISpellTarget>();
            if (spellTarget != null)
            {
                hasHit = true;
                var hitData = GetHitData();
                ArcaneSpellProjectileVfx.SpawnImpact(hitData, ResolveImpactPosition(other));
                spellTarget.OnHit(hitData);
                Destroy(gameObject);
                return;
            }

            var hitTestTarget = other.gameObject.tag == "TestTarget" ||
                                other.transform.root.gameObject.tag == "TestTarget";
            if (hitTestTarget)
            {
                hasHit = true;
                ArcaneSpellProjectileVfx.SpawnImpact(GetHitData(), ResolveImpactPosition(other));
                Debug.Log($"[HIT TestTarget] {element} | {statusEffect} | DMG:{damage}");
                Destroy(gameObject);
                return;
            }

            var boss = other.GetComponentInParent<BossAI>();
            if (boss == null)
                return;

            hasHit = true;

            if (boss != null && combatManager != null)
            {
                ArcaneSpellProjectileVfx.SpawnImpact(GetHitData(), ResolveImpactPosition(other));
                combatManager.ApplyBossHit(this);
            }

            Destroy(gameObject);
        }

        private Vector3 ResolveImpactPosition(Collider other)
        {
            if (other == null)
                return transform.position;

            var closest = other.ClosestPoint(transform.position);
            return closest.sqrMagnitude > 0.0001f ? closest : transform.position;
        }
    }
}
