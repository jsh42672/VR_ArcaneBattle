using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using UnityEngine;

public class FireballProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float burnDuration = 3f;
    [SerializeField] private SpellId spellId = SpellId.Single_Pointer;
    [SerializeField] private StatusEffect statusEffect = StatusEffect.Burn;
    [SerializeField] private float statusMagnitude = 1f;
    [SerializeField] private float statusTickInterval = 0.5f;

    private Rigidbody rb;
    private bool hasHit;
    private float explosionScale = 0.2f;
    private float explosionLifetime = 2f;
    private Material explosionMaterialOverride;
    private static Material fallbackExplosionMaterial;

    public void ConfigureLaunch(float launchSpeed, float projectileScale)
    {
        speed = Mathf.Max(0f, launchSpeed);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, projectileScale);
        ApplyParticleScalingMode();

        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = transform.forward * speed;
    }

    public void ConfigureImpact(GameObject overrideExplosionPrefab, float scale, float lifetimeSeconds, Material materialOverride)
    {
        if (overrideExplosionPrefab != null)
            explosionPrefab = overrideExplosionPrefab;

        explosionScale = Mathf.Max(0.01f, scale);
        explosionLifetime = Mathf.Max(0.05f, lifetimeSeconds);
        explosionMaterialOverride = materialOverride;
    }

    public void ConfigureHitData(
        SpellId newSpellId,
        float newDamage,
        StatusEffect newStatusEffect,
        float newStatusDuration,
        float newStatusMagnitude,
        float newStatusTickInterval)
    {
        spellId = newSpellId;
        damage = Mathf.Max(0f, newDamage);
        statusEffect = newStatusEffect;
        burnDuration = Mathf.Max(0f, newStatusDuration);
        statusMagnitude = Mathf.Max(0f, newStatusMagnitude);
        statusTickInterval = Mathf.Max(0f, newStatusTickInterval);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }
        
        Destroy(gameObject, lifetime);
    }

    private void ApplyParticleScalingMode()
    {
        foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || ArcanePlayerRigResolver.IsPlayerCollider(other))
            return;

        var shouldImpact = other.CompareTag("Hittable") ||
                           !other.isTrigger ||
                           other.GetComponentInParent<ISpellTarget>() != null ||
                           other.GetComponentInParent<BossAI>() != null;

        if (shouldImpact)
        {
            ApplyHit(other);
            Explode();
        }
    }

    private void ApplyHit(Collider other)
    {
        var hitData = new SpellHitData(
            spellId,
            ElementType.Fire,
            statusEffect,
            damage,
            burnDuration,
            statusMagnitude,
            statusTickInterval);

        var spellTarget = other.GetComponentInParent<ISpellTarget>();
        if (spellTarget != null)
        {
            hasHit = true;
            spellTarget.OnHit(hitData);
            return;
        }

        var boss = other.GetComponentInParent<BossAI>();
        if (boss == null)
            return;

        hasHit = true;
        var golemTarget = boss.GetComponent<GolemCombatTarget>() ?? boss.GetComponentInParent<GolemCombatTarget>();
        if (golemTarget != null)
            golemTarget.OnHit(hitData);
    }

    private void Explode()
    {
        if (explosionPrefab != null)
        {
            var explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            ConfigureExplosionInstance(explosion);
        }
        else
        {
            // Simple placeholder explosion effect logic if prefab is missing
            Debug.Log("Fireball Exploded!");
        }
        
        Destroy(gameObject);
    }

    private void ConfigureExplosionInstance(GameObject explosion)
    {
        if (explosion == null)
            return;

        explosion.transform.localScale = Vector3.one * explosionScale;
        var material = explosionMaterialOverride != null ? explosionMaterialOverride : GetFallbackExplosionMaterial();

        foreach (var particle in explosion.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        foreach (var renderer in explosion.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer == null)
                continue;

            var current = renderer.sharedMaterial;
            if (current == null || current.shader == null || current.shader.name == "Hidden/InternalErrorShader")
                renderer.sharedMaterial = material;
        }

        Destroy(explosion, explosionLifetime);
    }

    private static Material GetFallbackExplosionMaterial()
    {
        if (fallbackExplosionMaterial != null)
            return fallbackExplosionMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        fallbackExplosionMaterial = new Material(shader)
        {
            name = "Runtime_FireExplosionFallback",
            color = new Color(1f, 0.35f, 0.04f, 0.85f),
            hideFlags = HideFlags.DontSave
        };

        if (fallbackExplosionMaterial.HasProperty("_BaseColor"))
            fallbackExplosionMaterial.SetColor("_BaseColor", new Color(1f, 0.35f, 0.04f, 0.85f));
        if (fallbackExplosionMaterial.HasProperty("_Color"))
            fallbackExplosionMaterial.SetColor("_Color", new Color(1f, 0.35f, 0.04f, 0.85f));

        return fallbackExplosionMaterial;
    }
}
