using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// Small runtime-only aura used for prototype elemental feedback.
    /// It intentionally avoids authored VFX so every element shares the same silhouette.
    /// </summary>
    public class ElementAuraDummy : MonoBehaviour
    {
        [SerializeField] private Color auraColor = Color.white;
        [SerializeField] private float pulseSpeed = 2.4f;
        [SerializeField] private float pulseAmount = 0.08f;

        private Transform core;
        private Transform outerRing;
        private Transform innerRing;
        private Light auraLight;
        private float baseScale = 1f;

        public static ElementAuraDummy Create(string name, Color color, float scale, string exemptLayerName)
        {
            var root = new GameObject(name);
            var aura = root.AddComponent<ElementAuraDummy>();
            aura.Build(color, Mathf.Max(0.01f, scale));
            aura.ApplyLayerRecursively(exemptLayerName);
            return aura;
        }

        public void Configure(Color color, float scale, string exemptLayerName)
        {
            auraColor = color;
            baseScale = Mathf.Max(0.01f, scale);
            if (core == null)
                Build(auraColor, baseScale);

            ApplyColor(auraColor);
            ApplyLayerRecursively(exemptLayerName);
        }

        public void ApplyLayerRecursively(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
                return;

            foreach (var child in GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private void Update()
        {
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            if (outerRing != null)
                outerRing.localScale = new Vector3(baseScale * 1.35f * pulse, baseScale * 0.035f, baseScale * 1.35f * pulse);
            if (innerRing != null)
                innerRing.localScale = new Vector3(baseScale * 0.85f / pulse, baseScale * 0.025f, baseScale * 0.85f / pulse);
            if (core != null)
                core.localScale = Vector3.one * (baseScale * 0.32f * pulse);
        }

        private void Build(Color color, float scale)
        {
            auraColor = color;
            baseScale = scale;

            core = CreatePrimitive("Aura Core", PrimitiveType.Sphere, transform);
            outerRing = CreatePrimitive("Aura Outer Ring", PrimitiveType.Cylinder, transform);
            innerRing = CreatePrimitive("Aura Inner Ring", PrimitiveType.Cylinder, transform);

            core.localPosition = Vector3.zero;
            outerRing.localPosition = Vector3.zero;
            innerRing.localPosition = Vector3.zero;
            outerRing.localRotation = Quaternion.Euler(90f, 0f, 0f);
            innerRing.localRotation = Quaternion.Euler(90f, 0f, 0f);

            auraLight = gameObject.AddComponent<Light>();
            auraLight.type = LightType.Point;
            auraLight.range = 0.65f;
            auraLight.intensity = 1.25f;

            ApplyColor(auraColor);
        }

        private static Transform CreatePrimitive(string name, PrimitiveType primitiveType, Transform parent)
        {
            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            return go.transform;
        }

        private void ApplyColor(Color color)
        {
            var bright = new Color(color.r, color.g, color.b, 0.72f);
            var soft = new Color(color.r, color.g, color.b, 0.38f);

            ApplyMaterial(core, bright);
            ApplyMaterial(outerRing, soft);
            ApplyMaterial(innerRing, bright);

            if (auraLight != null)
                auraLight.color = color;
        }

        private static void ApplyMaterial(Transform target, Color color)
        {
            if (target == null)
                return;

            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.material = CreateAuraMaterial(color);
        }

        private static Material CreateAuraMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                color = color
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }

            return material;
        }
    }
}
