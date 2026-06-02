using UnityEngine;

[ExecuteAlways]
public class InteractiveGrassManager : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform playerTransform;

    [Header("Grass Materials")]
    [SerializeField] private Material[] grassMaterials;
    [SerializeField] private bool autoFindGrassMaterials = true;

    [Header("Interaction")]
    [SerializeField, Min(0f)] private float interactionRadius = 1.5f;
    [SerializeField, Min(0f)] private float influenceRadius = 5.5f;
    [SerializeField, Min(0f)] private float interactionStrength = 3.3f;

    [Header("Wind")]
    [SerializeField, Min(0f)] private float windSpeed = 1.6f;
    [SerializeField, Min(0f)] private float windStrength = 0.2f;
    [SerializeField] private Vector3 windDirection = new Vector3(2.7f, 0.5f, 4.6f);
    [SerializeField, Range(0f, 1f)] private float wiggleOffset = 0.43f;

    [Header("Color")]
    [SerializeField] private Color bottomColor = new Color(0.13f, 0.58f, 0f, 1f);
    [SerializeField] private Color topColor = new Color(0f, 0.69f, 0.36f, 1f);

    private static readonly int InteractorPositionId = Shader.PropertyToID("_InteractorPosition");
    private static readonly int InteractionRadiusId = Shader.PropertyToID("_Interaction_Radius");
    private static readonly int InfluenceRadiusId = Shader.PropertyToID("_Influence_Radius");
    private static readonly int InteractionStrengthId = Shader.PropertyToID("_Interaction_Strength");
    private static readonly int WindSpeedId = Shader.PropertyToID("_Wind_Speed");
    private static readonly int WindStrengthId = Shader.PropertyToID("_Wind_Strength");
    private static readonly int WindDirectionId = Shader.PropertyToID("_Wind_Direction");
    private static readonly int WiggleOffsetId = Shader.PropertyToID("_Wiggle_Offset");
    private static readonly int BottomColorId = Shader.PropertyToID("_Bottom_Color");
    private static readonly int TopColorId = Shader.PropertyToID("_Top_Color");

    private void OnEnable()
    {
        ResolvePlayer();
        FindGrassMaterialsIfNeeded();
        ApplySettings();
    }

    private void OnValidate()
    {
        FindGrassMaterialsIfNeeded();
        ApplySettings();
    }

    private void Update()
    {
        ResolvePlayer();
        ApplySettings();
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void FindGrassMaterialsIfNeeded()
    {
        if (!autoFindGrassMaterials || grassMaterials != null && grassMaterials.Length > 0)
        {
            return;
        }

        Renderer[] renderers = FindSceneRenderers();
        var found = new System.Collections.Generic.List<Material>();

        foreach (Renderer grassRenderer in renderers)
        {
            foreach (Material material in grassRenderer.sharedMaterials)
            {
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (material.shader.name.Contains("Grass") && !found.Contains(material))
                {
                    found.Add(material);
                }
            }
        }

        grassMaterials = found.ToArray();
    }

    private void ApplySettings()
    {
        if (grassMaterials == null)
        {
            return;
        }

        Vector3 interactorPosition = playerTransform != null ? playerTransform.position : transform.position;

        foreach (Material material in grassMaterials)
        {
            if (material == null)
            {
                continue;
            }

            SetVectorIfPresent(material, InteractorPositionId, interactorPosition);
            SetFloatIfPresent(material, InteractionRadiusId, interactionRadius);
            SetFloatIfPresent(material, InfluenceRadiusId, influenceRadius);
            SetFloatIfPresent(material, InteractionStrengthId, interactionStrength);
            SetFloatIfPresent(material, WindSpeedId, windSpeed);
            SetFloatIfPresent(material, WindStrengthId, windStrength);
            SetVectorIfPresent(material, WindDirectionId, windDirection);
            SetFloatIfPresent(material, WiggleOffsetId, wiggleOffset);
            SetColorIfPresent(material, BottomColorId, bottomColor);
            SetColorIfPresent(material, TopColorId, topColor);
        }
    }

    private static void SetFloatIfPresent(Material material, int id, float value)
    {
        if (material.HasProperty(id))
        {
            material.SetFloat(id, value);
        }
    }

    private static void SetVectorIfPresent(Material material, int id, Vector4 value)
    {
        if (material.HasProperty(id))
        {
            material.SetVector(id, value);
        }
    }

    private static void SetColorIfPresent(Material material, int id, Color value)
    {
        if (material.HasProperty(id))
        {
            material.SetColor(id, value);
        }
    }

    private static Renderer[] FindSceneRenderers()
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
#else
        return FindObjectsOfType<Renderer>();
#endif
    }
}
