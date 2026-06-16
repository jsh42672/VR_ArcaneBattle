using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
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
        private Vector3 direction = Vector3.forward;
        private CombatManager combatManager;
        private bool hasHit;
        private float destroyAfterSeconds = 3f;
        private AudioClip impactAudioClip;
        private float impactAudioVolume = 1f;

        public void SetLifetime(float seconds)
        {
            destroyAfterSeconds = Mathf.Max(0f, seconds);
        }

        public void ConfigureImpactAudio(AudioClip clip, float volume = 1f)
        {
            impactAudioClip = clip;
            impactAudioVolume = Mathf.Clamp01(volume);
        }

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

        public void InitializePrototype(
            float newSpeed,
            Vector3 newDirection,
            ElementType newElement,
            StatusEffect newStatusEffect,
            float newDamage,
            float newStatusDuration,
            float newStatusMagnitude = 1f,
            float newStatusTickInterval = 0.5f)
        {
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
                spellTarget.OnHit(GetHitData());
                PlayImpactAudio(other.ClosestPoint(transform.position));
                Destroy(gameObject);
                return;
            }

            var hitTestTarget = other.gameObject.tag == "TestTarget" ||
                                other.transform.root.gameObject.tag == "TestTarget";
            if (hitTestTarget)
            {
                hasHit = true;
                Debug.Log($"[HIT TestTarget] {element} | {statusEffect} | DMG:{damage}");
                PlayImpactAudio(other.ClosestPoint(transform.position));
                Destroy(gameObject);
                return;
            }

            var boss = other.GetComponentInParent<BossAI>();
            if (boss == null)
                return;

            hasHit = true;

            var golemTarget = boss.GetComponent<GolemCombatTarget>() ??
                              boss.GetComponentInParent<GolemCombatTarget>();
            if (golemTarget != null)
                golemTarget.OnHit(GetHitData());
            else if (combatManager != null)
                combatManager.ApplyBossHit(this);

            PlayImpactAudio(other.ClosestPoint(transform.position));
            Destroy(gameObject);
        }

        private void PlayImpactAudio(Vector3 position)
        {
            if (impactAudioClip == null)
                return;

            var audioObject = new GameObject($"{element}_ImpactSfx");
            audioObject.transform.position = position;

            var audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.clip = impactAudioClip;
            audioSource.volume = impactAudioVolume;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1.2f;
            audioSource.maxDistance = 18f;
            audioSource.dopplerLevel = 0f;
            audioSource.Play();

            Destroy(audioObject, Mathf.Max(0.2f, impactAudioClip.length + 0.1f));
        }
    }
}
