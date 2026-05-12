using System.Collections;
using UnityEngine;

public class RuneGlowPulse : MonoBehaviour
{
    [Header("Rune Renderers")]
    [Tooltip("Renderers that receive the rune glow emission pulse.")]
    public Renderer[] runeRenderers;

    [Header("Emission")]
    [ColorUsage(true, true)]
    public Color minEmission = new(0f, 0.3f, 1.2f, 1f);

    [ColorUsage(true, true)]
    public Color maxEmission = new(0f, 1.8f, 6f, 1f);

    [Header("Pulse")]
    [Range(0.1f, 5f)]
    public float pulseFrequency = 0.8f;

    [Tooltip("Adds phase offsets so rune glow can move like a wave.")]
    public bool wavePropagation = true;

    [Range(0f, 2f)]
    public float waveSpread = 1.2f;

    [Range(0f, 1f)]
    public float flickerStrength = 0.15f;

    [Header("Floor Glow")]
    public Renderer floorGlowRenderer;

    [ColorUsage(true, true)]
    public Color floorMinEmission = new(0f, 0.05f, 0.3f, 1f);

    [ColorUsage(true, true)]
    public Color floorMaxEmission = new(0f, 0.4f, 1.5f, 1f);

    private MaterialPropertyBlock[] propertyBlocks;
    private MaterialPropertyBlock floorBlock;
    private float[] phaseOffsets;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        if (runeRenderers == null || runeRenderers.Length == 0)
        {
            Debug.LogWarning("[RuneGlowPulse] runeRenderers is empty. Assign rune renderers in the Inspector.");
            return;
        }

        propertyBlocks = new MaterialPropertyBlock[runeRenderers.Length];
        phaseOffsets = new float[runeRenderers.Length];

        for (var i = 0; i < runeRenderers.Length; i++)
        {
            propertyBlocks[i] = new MaterialPropertyBlock();
            if (wavePropagation && runeRenderers[i] != null)
            {
                var position = runeRenderers[i].transform.position;
                var angle = Mathf.Atan2(position.z - transform.position.z, position.x - transform.position.x);
                phaseOffsets[i] = angle * waveSpread;
                EnableEmission(runeRenderers[i]);
            }
        }

        if (floorGlowRenderer != null)
        {
            floorBlock = new MaterialPropertyBlock();
            EnableEmission(floorGlowRenderer);
        }
    }

    private void Update()
    {
        if (propertyBlocks == null)
            return;

        var time = Time.time * pulseFrequency * Mathf.PI * 2f;
        for (var i = 0; i < runeRenderers.Length; i++)
        {
            if (runeRenderers[i] == null)
                continue;

            var raw = Mathf.Sin(time + phaseOffsets[i]);
            var t = (raw + 1f) * 0.5f;
            var flicker = 1f - flickerStrength * Mathf.PerlinNoise(Time.time * 8f + i * 3.7f, 0f);
            var emission = Color.LerpUnclamped(minEmission, maxEmission, t) * flicker;

            runeRenderers[i].GetPropertyBlock(propertyBlocks[i]);
            propertyBlocks[i].SetColor(EmissionId, emission);
            runeRenderers[i].SetPropertyBlock(propertyBlocks[i]);
        }

        if (floorGlowRenderer != null && floorBlock != null)
        {
            var t = (Mathf.Sin(time * 0.5f) + 1f) * 0.5f;
            var emission = Color.LerpUnclamped(floorMinEmission, floorMaxEmission, t);

            floorGlowRenderer.GetPropertyBlock(floorBlock);
            floorBlock.SetColor(EmissionId, emission);
            floorGlowRenderer.SetPropertyBlock(floorBlock);
        }
    }

    public void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp(intensity, 0f, 2f);
        pulseFrequency = Mathf.Lerp(0.3f, 3f, intensity / 2f);
    }

    public void HighlightRune(int index, Color highlightColor, float duration)
    {
        if (runeRenderers == null || index < 0 || index >= runeRenderers.Length)
            return;

        StartCoroutine(HighlightRoutine(highlightColor, duration));
    }

    private IEnumerator HighlightRoutine(Color color, float duration)
    {
        var original = maxEmission;
        maxEmission = color;
        yield return new WaitForSeconds(duration);
        maxEmission = original;
    }

    private static void EnableEmission(Renderer targetRenderer)
    {
        foreach (var material in targetRenderer.materials)
            material.EnableKeyword("_EMISSION");
    }
}
