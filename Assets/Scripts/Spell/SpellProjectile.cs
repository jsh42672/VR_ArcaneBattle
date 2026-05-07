using ArcaneVR.Boss;
using ArcaneVR.Combat;
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
            CombatManager newCombatManager)
        {
            spellId = newSpellId;
            element = newElement;
            damage = newDamage;
            speed = newSpeed;
            statusEffect = newStatusEffect;
            statusDuration = newStatusDuration;
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
            float newStatusDuration)
        {
            prototypePose = pose;
            spellId = SpellId.None;
            element = newElement;
            damage = newDamage;
            speed = newSpeed;
            statusEffect = newStatusEffect;
            statusDuration = newStatusDuration;
            direction = newDirection.sqrMagnitude > 0.001f ? newDirection.normalized : transform.forward;
            combatManager = null;
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

            if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
                return;

            var spellTarget = other.GetComponentInParent<ISpellTarget>();
            if (spellTarget != null)
            {
                hasHit = true;
                spellTarget.OnHit(element, statusEffect, damage, statusDuration);
                Destroy(gameObject);
                return;
            }

            var hitTestTarget = other.gameObject.tag == "TestTarget" ||
                                other.transform.root.gameObject.tag == "TestTarget";
            if (hitTestTarget)
            {
                hasHit = true;
                Debug.Log($"[HIT TestTarget] {element} | {statusEffect} | DMG:{damage}");
                Destroy(gameObject);
                return;
            }

            var boss = other.GetComponentInParent<BossAI>();
            if (boss == null)
                return;

            hasHit = true;

            if (boss != null && combatManager != null)
                combatManager.ApplyBossHit(this);

            Destroy(gameObject);
        }
    }
}
