using System.Collections.Generic;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Presentation/debug visual layer for boss elemental states. It is intentionally runtime-created and removable.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public class BossElementStatusVfx : MonoBehaviour
    {
        [SerializeField] private GolemCombatTarget combatTarget;
        [SerializeField] private bool tintRenderers = true;
        [SerializeField] private bool showParticles = true;
        [SerializeField] private bool showBarrierRings = true;
        [SerializeField] private bool showWeakMarker = true;
        [SerializeField] private float boundsRefreshInterval = 0.35f;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private ParticleSystem burnParticles;
        private ParticleSystem frostParticles;
        private ParticleSystem sparkParticles;
        private LineRenderer barrierRingHorizontal;
        private LineRenderer barrierRingVerticalA;
        private LineRenderer barrierRingVerticalB;
        private LineRenderer weakMarker;
        private Transform vfxRoot;
        private Bounds cachedBounds;
        private float nextBoundsRefreshTime;
        private float hitFlashUntilTime;
        private ElementType lastHitElement = ElementType.None;

        public string LastVfxStatus { get; private set; } = "BossVFX: idle";

        private void Awake()
        {
            ResolveReferences();
            RefreshRenderers();
            EnsureVfxObjects();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (combatTarget != null)
            {
                combatTarget.OnSpellHitReceived += HandleSpellHitReceived;
                combatTarget.OnElementStatusChanged += HandleElementStatusChanged;
            }
        }

        private void OnDisable()
        {
            if (combatTarget != null)
            {
                combatTarget.OnSpellHitReceived -= HandleSpellHitReceived;
                combatTarget.OnElementStatusChanged -= HandleElementStatusChanged;
            }

            ClearRendererTint();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (combatTarget == null)
                return;

            if (Time.time >= nextBoundsRefreshTime)
            {
                nextBoundsRefreshTime = Time.time + Mathf.Max(0.1f, boundsRefreshInterval);
                RefreshRenderers();
                cachedBounds = CalculateBounds();
            }

            EnsureVfxObjects();
            var snapshot = combatTarget.GetStatusSnapshot();
            UpdateRendererTint(snapshot);
            UpdateParticles(snapshot);
            UpdateBarrierRings(snapshot);
            UpdateWeakMarker(snapshot);
            LastVfxStatus = $"BossVFX: {snapshot.combatCue}";
        }

        private void HandleSpellHitReceived(SpellHitData hitData)
        {
            if (hitData == null)
                return;

            lastHitElement = hitData.element;
            hitFlashUntilTime = Time.time + 0.18f;
        }

        private void HandleElementStatusChanged(BossElementStatusSnapshot snapshot)
        {
            LastVfxStatus = $"BossVFX: {snapshot.combatCue}";
        }

        private void ResolveReferences()
        {
            if (combatTarget == null)
                combatTarget = GetComponent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();
        }

        private void RefreshRenderers()
        {
            renderers.Clear();
            baseColors.Clear();

            foreach (var targetRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (targetRenderer == null ||
                    targetRenderer.GetComponent<TextMesh>() != null ||
                    targetRenderer.GetComponent<ParticleSystemRenderer>() != null ||
                    targetRenderer.GetComponent<LineRenderer>() != null)
                {
                    continue;
                }

                renderers.Add(targetRenderer);
                baseColors[targetRenderer] = ReadRendererColor(targetRenderer);
            }
        }

        private Bounds CalculateBounds()
        {
            if (renderers.Count == 0)
                return new Bounds(transform.position + Vector3.up * 0.8f, new Vector3(1.1f, 1.6f, 1.1f));

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Count; i++)
                bounds.Encapsulate(renderers[i].bounds);

            if (bounds.size.sqrMagnitude < 0.01f)
                bounds = new Bounds(transform.position + Vector3.up * 0.8f, new Vector3(1.1f, 1.6f, 1.1f));

            return bounds;
        }

        private void EnsureVfxObjects()
        {
            if (vfxRoot == null)
            {
                var rootObject = new GameObject("Arcane Boss Status VFX")
                {
                    hideFlags = HideFlags.DontSave
                };
                rootObject.transform.SetParent(transform, false);
                rootObject.transform.localPosition = Vector3.zero;
                rootObject.transform.localRotation = Quaternion.identity;
                rootObject.transform.localScale = Vector3.one;
                vfxRoot = rootObject.transform;
            }

            if (burnParticles == null)
                burnParticles = CreateStatusParticles("Burn Embers", new Color(1f, 0.28f, 0.04f, 0.95f), 0.08f, 0.28f);
            if (frostParticles == null)
                frostParticles = CreateStatusParticles("Frost Shards", new Color(0.55f, 0.92f, 1f, 0.9f), 0.045f, 0.16f);
            if (sparkParticles == null)
                sparkParticles = CreateStatusParticles("Thunder Sparks", new Color(1f, 0.92f, 0.1f, 0.95f), 0.035f, 0.14f);

            if (barrierRingHorizontal == null)
                barrierRingHorizontal = CreateRing("Barrier Ring H", new Color(0.25f, 0.86f, 1f, 0.78f));
            if (barrierRingVerticalA == null)
                barrierRingVerticalA = CreateRing("Barrier Ring VA", new Color(0.25f, 0.86f, 1f, 0.58f));
            if (barrierRingVerticalB == null)
                barrierRingVerticalB = CreateRing("Barrier Ring VB", new Color(0.25f, 0.86f, 1f, 0.58f));
            if (weakMarker == null)
                weakMarker = CreateRing("Weak Marker", new Color(1f, 0.22f, 0.12f, 0.92f), 48);
        }

        private void UpdateRendererTint(BossElementStatusSnapshot snapshot)
        {
            if (!tintRenderers)
                return;

            var tint = Color.white;
            var weight = 0f;

            if (snapshot.isBurning)
                BlendTint(ref tint, ref weight, new Color(1f, 0.2f, 0.04f, 1f), 0.34f + Pulse(7f) * 0.16f);
            if (snapshot.isSlowed)
                BlendTint(ref tint, ref weight, new Color(0.35f, 0.85f, 1f, 1f), 0.32f);
            if (snapshot.isStaggered)
                BlendTint(ref tint, ref weight, new Color(1f, 0.9f, 0.1f, 1f), 0.42f + Pulse(16f) * 0.2f);
            if (snapshot.isWeakExposed)
                BlendTint(ref tint, ref weight, new Color(1f, 0.12f, 0.08f, 1f), 0.26f + Pulse(4.2f) * 0.18f);
            if (snapshot.isBarrierActive)
                BlendTint(ref tint, ref weight, new Color(0.25f, 0.78f, 1f, 1f), 0.2f);
            if (Time.time < hitFlashUntilTime)
                BlendTint(ref tint, ref weight, Color.Lerp(GetElementColor(lastHitElement), Color.white, 0.25f), 0.5f);

            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer == null)
                    continue;

                var baseColor = baseColors.TryGetValue(targetRenderer, out var storedColor) ? storedColor : Color.white;
                var color = weight > 0f ? Color.Lerp(baseColor, tint, Mathf.Clamp01(weight)) : baseColor;
                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void UpdateParticles(BossElementStatusSnapshot snapshot)
        {
            if (!showParticles)
            {
                SetParticleRate(burnParticles, 0f);
                SetParticleRate(frostParticles, 0f);
                SetParticleRate(sparkParticles, 0f);
                return;
            }

            var center = cachedBounds.center;
            var radius = Mathf.Max(0.25f, Mathf.Max(cachedBounds.extents.x, cachedBounds.extents.z));
            PositionParticleSystem(burnParticles, center, radius);
            PositionParticleSystem(frostParticles, center, radius);
            PositionParticleSystem(sparkParticles, center, radius);

            SetParticleRate(burnParticles, snapshot.isBurning ? 42f : 0f);
            SetParticleRate(frostParticles, snapshot.isSlowed ? 36f : 0f);
            SetParticleRate(sparkParticles, snapshot.isStaggered ? 74f : Time.time < hitFlashUntilTime && lastHitElement == ElementType.Thunder ? 58f : 0f);
        }

        private void UpdateBarrierRings(BossElementStatusSnapshot snapshot)
        {
            var active = showBarrierRings && snapshot.isBarrierActive;
            SetRendererActive(barrierRingHorizontal, active);
            SetRendererActive(barrierRingVerticalA, active);
            SetRendererActive(barrierRingVerticalB, active);
            if (!active)
                return;

            var center = cachedBounds.center;
            var radius = Mathf.Max(0.65f, Mathf.Max(cachedBounds.extents.x, cachedBounds.extents.z) + 0.22f);
            var heightRadius = Mathf.Max(0.75f, cachedBounds.extents.y + 0.18f);
            var spin = Time.time * 40f;
            UpdateRing(barrierRingHorizontal, center, Quaternion.Euler(90f, spin, 0f), radius);
            UpdateRing(barrierRingVerticalA, center, Quaternion.Euler(0f, spin, 0f), heightRadius);
            UpdateRing(barrierRingVerticalB, center, Quaternion.Euler(0f, spin + 90f, 90f), heightRadius);
        }

        private void UpdateWeakMarker(BossElementStatusSnapshot snapshot)
        {
            var active = showWeakMarker && snapshot.isWeakExposed;
            SetRendererActive(weakMarker, active);
            if (!active)
                return;

            var center = cachedBounds.center + Vector3.up * (cachedBounds.extents.y + 0.22f);
            var radius = Mathf.Max(0.18f, Mathf.Min(cachedBounds.extents.x + cachedBounds.extents.z, 1.2f) * 0.28f);
            UpdateRing(weakMarker, center, Quaternion.LookRotation(Camera.main != null ? Camera.main.transform.forward : Vector3.forward), radius + Pulse(5f) * 0.06f);
        }

        private ParticleSystem CreateStatusParticles(string objectName, Color color, float minSize, float maxSize)
        {
            var particleObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
            particleObject.transform.SetParent(vfxRoot, false);

            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.maxParticles = 180;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            shape.radiusThickness = 0.65f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 2.4f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFadeGradient(color);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateRuntimeMaterial(color, true, GetSoftParticleTexture());
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;
            renderer.maxParticleSize = 0.28f;

            particles.Play(true);
            return particles;
        }

        private LineRenderer CreateRing(string objectName, Color color, int segments = 96)
        {
            var ringObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
            ringObject.transform.SetParent(vfxRoot, false);

            var line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = 0.026f;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.colorGradient = CreateRingGradient(color);
            line.material = CreateRuntimeMaterial(color, true, GetSoftParticleTexture());
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static void PositionParticleSystem(ParticleSystem particles, Vector3 center, float radius)
        {
            if (particles == null)
                return;

            particles.transform.position = center;
            var shape = particles.shape;
            shape.radius = radius;
        }

        private static void SetParticleRate(ParticleSystem particles, float rate)
        {
            if (particles == null)
                return;

            var emission = particles.emission;
            emission.rateOverTime = rate;
            if (rate > 0f && !particles.isPlaying)
                particles.Play(true);
        }

        private static void UpdateRing(LineRenderer ring, Vector3 center, Quaternion rotation, float radius)
        {
            if (ring == null)
                return;

            var count = ring.positionCount;
            for (var i = 0; i < count; i++)
            {
                var t = (float)i / count * Mathf.PI * 2f;
                var point = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                ring.SetPosition(i, center + rotation * point);
            }
        }

        private static void SetRendererActive(Renderer renderer, bool active)
        {
            if (renderer != null)
                renderer.enabled = active;
        }

        private void ClearRendererTint()
        {
            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer != null)
                    targetRenderer.SetPropertyBlock(null);
            }
        }

        private static void BlendTint(ref Color tint, ref float weight, Color addColor, float addWeight)
        {
            var clampedWeight = Mathf.Clamp01(addWeight);
            tint = weight <= 0f ? addColor : Color.Lerp(tint, addColor, clampedWeight);
            weight = Mathf.Clamp01(weight + clampedWeight);
        }

        private static float Pulse(float speed)
        {
            return (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        }

        private static Color ReadRendererColor(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return Color.white;

            var material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");

            return Color.white;
        }

        private static Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => new Color(1f, 0.22f, 0.04f, 1f),
                ElementType.Ice => new Color(0.35f, 0.86f, 1f, 1f),
                ElementType.Thunder => new Color(1f, 0.9f, 0.12f, 1f),
                _ => Color.white
            };
        }

        private static Gradient CreateFadeGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.25f), 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a * 0.86f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateRingGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.35f), 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a * 0.45f, 0.55f),
                    new GradientAlphaKey(color.a, 1f)
                });
            return gradient;
        }

        private static Material CreateRuntimeMaterial(Color color, bool transparent, Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Transparent") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Standard");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "ArcaneRuntimeBossStatusVfxMaterial",
                hideFlags = HideFlags.DontSave,
                renderQueue = transparent ? 3000 : 2000
            };

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", transparent ? 1f : 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", transparent
                    ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                    : (int)UnityEngine.Rendering.BlendMode.Zero);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (texture != null && material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (texture != null && material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);

            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHABLEND_ON");
            }

            return material;
        }

        private static Texture2D GetSoftParticleTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ArcaneRuntimeBossSoftParticle",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = ((x + 0.5f) / size) * 2f - 1f;
                    var v = ((y + 0.5f) / size) * 2f - 1f;
                    var distance = Mathf.Sqrt(u * u + v * v);
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
