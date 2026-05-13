using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// Runtime-only elemental projectile visuals. Keeps prototype spells asset-light while avoiding primitive sphere debug shapes.
    /// </summary>
    public class ArcaneSpellProjectileVfx : MonoBehaviour
    {
        private const float BaseScale = 1f;
        private static Texture2D softParticleTexture;
        private static Mesh shardMesh;

        private SpellId spellId;
        private ElementType element;
        private float visualScale = BaseScale;
        private LineRenderer lightningLine;
        private float nextLightningRefreshTime;

        public void Configure(SpellId newSpellId, ElementType newElement, float newVisualScale = BaseScale)
        {
            spellId = newSpellId;
            element = newElement == ElementType.None ? ResolveElementFromSpell(newSpellId) : newElement;
            visualScale = Mathf.Clamp(newVisualScale, 0.45f, 3.4f);

            ClearChildren();
            BuildVisuals();
        }

        public static void SpawnImpact(SpellHitData hitData, Vector3 position)
        {
            if (hitData == null)
                return;

            var element = hitData.element == ElementType.None ? ResolveElementFromSpell(hitData.spellId) : hitData.element;
            if (element == ElementType.None && !SpellHitData.IsComboSpellId(hitData.spellId))
                return;

            var impact = new GameObject($"SpellImpact_{hitData.spellId}_{element}")
            {
                hideFlags = HideFlags.DontSave
            };
            impact.transform.position = position;

            var vfx = impact.AddComponent<ArcaneSpellProjectileVfx>();
            vfx.ConfigureImpact(hitData.spellId, element);
            Destroy(impact, 1.25f);
        }

        private void ConfigureImpact(SpellId impactSpellId, ElementType impactElement)
        {
            spellId = impactSpellId;
            element = impactElement;
            visualScale = SpellHitData.IsComboSpellId(spellId) ? 1.35f : 1f;

            if (SpellHitData.TryGetComboElements(spellId, out var first, out var second))
            {
                CreateImpactBurst(first, new Vector3(-0.04f, 0f, 0f), 44);
                CreateImpactBurst(second, new Vector3(0.04f, 0f, 0f), 44);
                CreateImpactLight(Color.Lerp(GetElementColor(first), GetElementColor(second), 0.5f), 2.3f, 2.8f);
                return;
            }

            CreateImpactBurst(element, Vector3.zero, 64);
            CreateImpactLight(GetElementColor(element), 1.7f, 2.1f);
        }

        private void LateUpdate()
        {
            if (lightningLine == null || Time.time < nextLightningRefreshTime)
                return;

            nextLightningRefreshTime = Time.time + 0.035f;
            UpdateLightningLine(lightningLine, visualScale);
        }

        private void BuildVisuals()
        {
            if (SpellHitData.TryGetComboElements(spellId, out var first, out var second))
            {
                BuildComboVisual(first, second);
                return;
            }

            switch (element)
            {
                case ElementType.Fire:
                    BuildFireVisual();
                    break;
                case ElementType.Ice:
                    BuildIceVisual();
                    break;
                case ElementType.Thunder:
                    BuildThunderVisual();
                    break;
                default:
                    BuildNeutralVisual();
                    break;
            }
        }

        private void BuildComboVisual(ElementType first, ElementType second)
        {
            var arcaneCore = new Color(0.92f, 0.18f, 1f, 0.92f);
            var whiteHot = new Color(1f, 0.92f, 0.7f, 0.8f);

            if (spellId == SpellId.Combo_FireIce)
            {
                CreateTrail("ArcaneFusionRibbon", arcaneCore, whiteHot, 0.58f, 0.32f);
                CreateParticleCloud("ArcaneFusionCore", arcaneCore, 120f, 0.2f, 0.5f, 0.09f, 0.26f);
                CreateTrail("SteamTrail", new Color(1f, 0.45f, 0.12f, 0.9f), new Color(0.5f, 0.92f, 1f, 0.78f), 0.36f, 0.22f);
                CreateParticleCloud("SteamVapor", Color.Lerp(GetElementColor(first), Color.white, 0.55f), 75f, 0.28f, 0.7f, 0.13f, 0.34f);
                CreateParticleCloud("ColdMist", Color.Lerp(GetElementColor(second), Color.white, 0.45f), 55f, 0.22f, 0.58f, 0.08f, 0.24f);
                CreateGlowLight(Color.Lerp(arcaneCore, Color.Lerp(GetElementColor(first), GetElementColor(second), 0.45f), 0.55f), 3.2f, 3.4f);
                return;
            }

            if (spellId == SpellId.Combo_IceThunder)
            {
                CreateTrail("ArcaneFusionRibbon", arcaneCore, whiteHot, 0.56f, 0.3f);
                CreateParticleCloud("ArcaneFusionCore", arcaneCore, 115f, 0.18f, 0.48f, 0.085f, 0.24f);
                BuildIceVisual();
                lightningLine = CreateLightningLine("BarrierBreakArc", 0.62f, 0.11f, GetElementColor(ElementType.Thunder));
                CreateParticleCloud("BreakSparks", GetElementColor(ElementType.Thunder), 90f, 0.14f, 0.34f, 0.035f, 0.09f);
                CreateGlowLight(Color.Lerp(arcaneCore, GetElementColor(ElementType.Thunder), 0.35f), 3f, 3.1f);
                return;
            }

            if (spellId == SpellId.Combo_ThunderFire)
            {
                CreateTrail("ArcaneFusionRibbon", arcaneCore, whiteHot, 0.56f, 0.31f);
                CreateParticleCloud("ArcaneFusionCore", arcaneCore, 125f, 0.18f, 0.48f, 0.09f, 0.26f);
                BuildFireVisual();
                lightningLine = CreateLightningLine("OverloadArc", 0.58f, 0.12f, GetElementColor(ElementType.Thunder));
                CreateParticleCloud("OverloadSparks", GetElementColor(ElementType.Thunder), 110f, 0.12f, 0.3f, 0.04f, 0.11f);
                CreateGlowLight(Color.Lerp(arcaneCore, GetElementColor(ElementType.Fire), 0.35f), 3.1f, 3.2f);
                return;
            }

            CreateTrail("ComboTrail", arcaneCore, Color.Lerp(GetElementColor(first), GetElementColor(second), 0.5f), 0.5f, 0.28f);
            CreateParticleCloud("ComboCore", Color.Lerp(GetElementColor(first), GetElementColor(second), 0.5f), 70f, 0.22f, 0.5f, 0.08f, 0.22f);
            CreateParticleCloud("ArcaneFusionCore", arcaneCore, 105f, 0.18f, 0.46f, 0.08f, 0.24f);
            CreateGlowLight(Color.Lerp(arcaneCore, Color.Lerp(GetElementColor(first), GetElementColor(second), 0.5f), 0.5f), 3f, 3f);
        }

        private void BuildFireVisual()
        {
            CreateTrail("FlameTrail", new Color(1f, 0.16f, 0.02f, 1f), new Color(1f, 0.82f, 0.18f, 0.72f), 0.42f, 0.2f);
            CreateOffsetTrail("LeftFlameRibbon", new Vector3(-0.04f, 0.015f, -0.02f), new Color(1f, 0.33f, 0.04f, 0.9f), 0.27f, 0.08f);
            CreateOffsetTrail("RightFlameRibbon", new Vector3(0.04f, -0.012f, -0.02f), new Color(1f, 0.7f, 0.12f, 0.8f), 0.24f, 0.07f);
            CreateParticleCloud("FlameCore", new Color(1f, 0.34f, 0.04f, 0.95f), 115f, 0.18f, 0.44f, 0.055f, 0.17f);
            CreateParticleCloud("Embers", new Color(1f, 0.82f, 0.18f, 0.85f), 42f, 0.3f, 0.72f, 0.025f, 0.07f);
            CreateGlowLight(GetElementColor(ElementType.Fire), 1.8f, 2.2f);
        }

        private void BuildIceVisual()
        {
            CreateTrail("FrostTrail", new Color(0.5f, 0.92f, 1f, 0.78f), new Color(0.85f, 1f, 1f, 0.9f), 0.34f, 0.13f);
            CreateIceShard("IceShard_Main", Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 1.55f));
            CreateIceShard("IceShard_Left", new Vector3(-0.035f, 0.018f, -0.04f), Quaternion.Euler(0f, 0f, 24f), new Vector3(0.62f, 0.62f, 1.15f));
            CreateIceShard("IceShard_Right", new Vector3(0.035f, -0.014f, -0.035f), Quaternion.Euler(0f, 0f, -26f), new Vector3(0.58f, 0.58f, 1.05f));
            CreateParticleCloud("FrostBits", new Color(0.72f, 0.96f, 1f, 0.9f), 62f, 0.18f, 0.52f, 0.025f, 0.075f);
            CreateGlowLight(GetElementColor(ElementType.Ice), 1.55f, 1.7f);
        }

        private void BuildThunderVisual()
        {
            CreateTrail("VoltageTrail", new Color(1f, 0.95f, 0.18f, 0.92f), new Color(0.45f, 0.92f, 1f, 0.65f), 0.24f, 0.1f);
            lightningLine = CreateLightningLine("LightningCore", 0.54f, 0.1f, GetElementColor(ElementType.Thunder));
            CreateParticleCloud("SparkBurst", new Color(1f, 0.9f, 0.16f, 0.95f), 135f, 0.08f, 0.24f, 0.025f, 0.08f);
            CreateGlowLight(GetElementColor(ElementType.Thunder), 2.1f, 2.7f);
        }

        private void BuildNeutralVisual()
        {
            CreateTrail("ArcaneTrail", new Color(0.75f, 0.78f, 1f, 0.85f), Color.white, 0.28f, 0.12f);
            CreateParticleCloud("ArcaneCore", new Color(0.75f, 0.78f, 1f, 0.9f), 50f, 0.18f, 0.45f, 0.05f, 0.13f);
            CreateGlowLight(new Color(0.75f, 0.78f, 1f, 1f), 1.4f, 1.6f);
        }

        private void CreateParticleCloud(
            string objectName,
            Color color,
            float emissionRate,
            float minLifetime,
            float maxLifetime,
            float minSize,
            float maxSize)
        {
            var particleObject = CreateChild(objectName);
            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f * visualScale, 0.28f * visualScale);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize * visualScale, maxSize * visualScale);
            main.startColor = color;
            main.maxParticles = 220;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.045f * visualScale;
            shape.radiusThickness = 0.55f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.z = new ParticleSystem.MinMaxCurve(-0.28f * visualScale, -0.06f * visualScale);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.08f * visualScale;
            noise.frequency = 2.4f;
            noise.scrollSpeed = 0.85f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFadeGradient(color);

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.24f, 1f),
                    new Keyframe(1f, 0.05f)));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateRuntimeMaterial(color, true, GetSoftParticleTexture());
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 1.5f;
            renderer.maxParticleSize = 0.32f * visualScale;
        }

        private void CreateImpactBurst(ElementType burstElement, Vector3 localPosition, int count)
        {
            var color = GetElementColor(burstElement);
            var burstObject = CreateChild($"Impact_{burstElement}");
            burstObject.transform.localPosition = localPosition;

            var particles = burstObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.62f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f * visualScale, 1.6f * visualScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f * visualScale, 0.18f * visualScale);
            main.startColor = Color.Lerp(color, Color.white, 0.15f);
            main.maxParticles = Mathf.Max(80, count + 20);

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f * visualScale;
            shape.radiusThickness = 0.25f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFadeGradient(Color.Lerp(color, Color.white, 0.1f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateRuntimeMaterial(color, true, GetSoftParticleTexture());
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2.5f;
            renderer.maxParticleSize = 0.32f * visualScale;

            particles.Emit(count);
        }

        private void CreateTrail(string objectName, Color headColor, Color hotColor, float time, float width)
        {
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.name = objectName;
            trail.time = time;
            trail.minVertexDistance = 0.012f;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, width * visualScale),
                new Keyframe(0.3f, width * 0.7f * visualScale),
                new Keyframe(1f, 0f));
            trail.colorGradient = CreateTrailGradient(headColor, hotColor);
            trail.material = CreateRuntimeMaterial(headColor, true, GetSoftParticleTexture());
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private void CreateOffsetTrail(string objectName, Vector3 localOffset, Color color, float time, float width)
        {
            var child = CreateChild(objectName);
            child.transform.localPosition = localOffset * visualScale;

            var trail = child.AddComponent<TrailRenderer>();
            trail.time = time;
            trail.minVertexDistance = 0.015f;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, width * visualScale),
                new Keyframe(1f, 0f));
            trail.colorGradient = CreateTrailGradient(color, Color.Lerp(color, Color.white, 0.25f));
            trail.material = CreateRuntimeMaterial(color, true, GetSoftParticleTexture());
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private void CreateIceShard(string objectName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var shardObject = CreateChild(objectName);
            shardObject.transform.localPosition = localPosition * visualScale;
            shardObject.transform.localRotation = localRotation;
            shardObject.transform.localScale = Vector3.Scale(localScale, Vector3.one * 0.26f * visualScale);

            var filter = shardObject.AddComponent<MeshFilter>();
            filter.sharedMesh = GetShardMesh();

            var renderer = shardObject.AddComponent<MeshRenderer>();
            renderer.material = CreateRuntimeMaterial(new Color(0.58f, 0.94f, 1f, 0.64f), true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private LineRenderer CreateLightningLine(string objectName, float length, float radius, Color color)
        {
            var lineObject = CreateChild(objectName);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = 9;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.012f * visualScale),
                new Keyframe(0.5f, 0.045f * visualScale),
                new Keyframe(1f, 0.012f * visualScale));
            line.colorGradient = CreateTrailGradient(color, Color.Lerp(color, Color.white, 0.55f));
            line.material = CreateRuntimeMaterial(color, true, GetSoftParticleTexture());
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lineObject.transform.localScale = Vector3.one;
            UpdateLightningLine(line, visualScale, length, radius);
            return line;
        }

        private static void UpdateLightningLine(LineRenderer line, float scale, float length = 0.54f, float radius = 0.1f)
        {
            if (line == null)
                return;

            var count = line.positionCount;
            var halfLength = length * scale * 0.5f;
            var jitterRadius = radius * scale;
            for (var i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0f : (float)i / (count - 1);
                var edgeFade = Mathf.Sin(t * Mathf.PI);
                var offset = Random.insideUnitCircle * jitterRadius * edgeFade;
                line.SetPosition(i, new Vector3(offset.x, offset.y, Mathf.Lerp(-halfLength, halfLength, t)));
            }
        }

        private void CreateGlowLight(Color color, float range, float intensity)
        {
            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range * visualScale;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void CreateImpactLight(Color color, float range, float intensity)
        {
            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range * visualScale;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private GameObject CreateChild(string objectName)
        {
            var child = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            foreach (var trail in GetComponents<TrailRenderer>())
            {
                if (Application.isPlaying)
                    Destroy(trail);
                else
                    DestroyImmediate(trail);
            }

            foreach (var light in GetComponents<Light>())
            {
                if (Application.isPlaying)
                    Destroy(light);
                else
                    DestroyImmediate(light);
            }
        }

        private static Material CreateRuntimeMaterial(Color color, bool transparent, Texture texture = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Transparent") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Standard");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "ArcaneRuntimeSpellVfxMaterial",
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

        private static Gradient CreateFadeGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.25f), 0f),
                    new GradientColorKey(color, 0.35f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(color.a * 0.65f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateTrailGradient(Color headColor, Color hotColor)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(hotColor, Color.white, 0.25f), 0f),
                    new GradientColorKey(headColor, 0.36f),
                    new GradientColorKey(headColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(Mathf.Clamp01(hotColor.a), 0f),
                    new GradientAlphaKey(Mathf.Clamp01(headColor.a * 0.72f), 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Texture2D GetSoftParticleTexture()
        {
            if (softParticleTexture != null)
                return softParticleTexture;

            const int size = 32;
            softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ArcaneRuntimeSoftParticle",
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
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            softParticleTexture.SetPixels32(pixels);
            softParticleTexture.Apply(false, true);
            return softParticleTexture;
        }

        private static Mesh GetShardMesh()
        {
            if (shardMesh != null)
                return shardMesh;

            shardMesh = new Mesh
            {
                name = "ArcaneRuntimeIceShardMesh",
                hideFlags = HideFlags.DontSave
            };

            shardMesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0.58f),
                new Vector3(0f, 0f, -0.42f),
                new Vector3(-0.17f, 0f, -0.05f),
                new Vector3(0.17f, 0f, -0.05f),
                new Vector3(0f, 0.17f, -0.05f),
                new Vector3(0f, -0.17f, -0.05f)
            };

            shardMesh.triangles = new[]
            {
                0, 4, 2,
                0, 3, 4,
                0, 5, 3,
                0, 2, 5,
                1, 2, 4,
                1, 4, 3,
                1, 3, 5,
                1, 5, 2
            };
            shardMesh.RecalculateNormals();
            shardMesh.RecalculateBounds();
            return shardMesh;
        }

        private static Color GetElementColor(ElementType value)
        {
            return value switch
            {
                ElementType.Fire => new Color(1f, 0.22f, 0.04f, 1f),
                ElementType.Ice => new Color(0.32f, 0.84f, 1f, 1f),
                ElementType.Thunder => new Color(1f, 0.9f, 0.12f, 1f),
                _ => new Color(0.75f, 0.75f, 0.85f, 1f)
            };
        }

        private static ElementType ResolveElementFromSpell(SpellId value)
        {
            return value switch
            {
                SpellId.Single_Pointer => ElementType.Fire,
                SpellId.Single_Wave => ElementType.Ice,
                SpellId.Single_Strike => ElementType.Thunder,
                SpellId.Combo_FireIce => ElementType.Fire,
                SpellId.Combo_IceThunder => ElementType.Ice,
                SpellId.Combo_ThunderFire => ElementType.Thunder,
                _ => ElementType.None
            };
        }
    }
}
