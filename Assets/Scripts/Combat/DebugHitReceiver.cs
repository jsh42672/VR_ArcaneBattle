using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class DebugHitReceiver : MonoBehaviour, ISpellTarget
    {
        private Renderer targetRenderer;
        private Color originalColor = Color.white;
        private SpellHitWorldText hitWorldText;

        private void Awake()
        {
            targetRenderer = FindTargetRenderer();
            if (targetRenderer != null)
            {
                originalColor = ReadMaterialColor(targetRenderer, new Color(0.2f, 0.95f, 0.65f, 1f));
                targetRenderer.material = CreateRuntimeMaterial(originalColor);
            }

            hitWorldText = GetComponent<SpellHitWorldText>();
            if (hitWorldText == null)
                hitWorldText = gameObject.AddComponent<SpellHitWorldText>();
        }

        public void ConfigureBaseColor(Color color)
        {
            originalColor = color;
            if (targetRenderer == null)
                targetRenderer = FindTargetRenderer();

            if (targetRenderer != null)
                targetRenderer.material = CreateRuntimeMaterial(originalColor);
        }

        public void OnHit(SpellHitData hitData)
        {
            if (hitData == null)
                return;

            Debug.Log(
                $"[TARGET] Hit: {hitData.element} | {hitData.statusEffect} | DMG:{hitData.damage} | DUR:{hitData.statusDuration}s | MAG:{hitData.statusMagnitude} | TICK:{hitData.statusTickInterval}");
            hitWorldText?.ShowHit(hitData);

            if (targetRenderer == null)
                return;

            StopAllCoroutines();
            StartCoroutine(FlashColor(GetElementColor(hitData.element)));
        }

        private System.Collections.IEnumerator FlashColor(Color flashColor)
        {
            ApplyMaterialColor(targetRenderer.material, flashColor);
            yield return new WaitForSeconds(0.3f);
            ApplyMaterialColor(targetRenderer.material, originalColor);
        }

        private static Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => new Color(1f, 0.3f, 0.1f, 1f),
                ElementType.Ice => new Color(0.3f, 0.6f, 1f, 1f),
                ElementType.Thunder => new Color(1f, 0.9f, 0.1f, 1f),
                _ => Color.white
            };
        }

        private Renderer FindTargetRenderer()
        {
            var ownRenderer = GetComponent<Renderer>();
            if (ownRenderer != null)
                return ownRenderer;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<TextMesh>() == null)
                    return renderer;
            }

            return null;
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Standard");

            var material = new Material(shader);
            ApplyMaterialColor(material, color);
            return material;
        }

        private static Color ReadMaterialColor(Renderer renderer, Color fallback)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return fallback;

            var material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");

            return fallback;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }
    }
}
