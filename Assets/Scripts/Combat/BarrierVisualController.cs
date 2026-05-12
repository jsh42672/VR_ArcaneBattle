using UnityEngine;
using UnityEngine.Rendering;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Runtime golden shield shown in front of the player during low-attack barrier responses.
    /// </summary>
    public class BarrierVisualController : MonoBehaviour
    {
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private Transform headTransform;
        [SerializeField] private bool showLowAttackPreview = true;
        [SerializeField] private Vector3 localPosition = new Vector3(0f, -0.04f, 1.18f);
        [SerializeField] private Vector2 shieldSize = new Vector2(1.45f, 1.05f);
        [SerializeField, Range(0.05f, 1f)] private float activeOpacity = 0.38f;
        [SerializeField, Range(0.02f, 0.65f)] private float previewOpacity = 0.16f;
        [SerializeField] private Color goldColor = new Color(1f, 0.72f, 0.08f, 1f);
        [SerializeField] private float minPreviewDuration = 0.45f;

        private BarrierController subscribedBarrierController;
        private Transform shieldRoot;
        private MeshRenderer shieldRenderer;
        private Material shieldMaterial;
        private Material lineMaterial;
        private float previewHideAtTime;
        private bool isBarrierActive;

        public string LastVisualStatus { get; private set; } = "BarrierVisual: idle";

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();

            if (!isBarrierActive && previewHideAtTime > 0f && Time.time >= previewHideAtTime)
                Hide();
        }

        private void ResolveReferences()
        {
            if (barrierController == null)
                barrierController = GetComponent<BarrierController>() ?? FindAnyObjectByType<BarrierController>();

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
        }

        private void Subscribe()
        {
            if (barrierController == null || subscribedBarrierController == barrierController)
                return;

            Unsubscribe();
            subscribedBarrierController = barrierController;
            subscribedBarrierController.OnResponseWindowStarted += HandleResponseWindowStarted;
            subscribedBarrierController.OnResponseWindowResolved += HandleResponseWindowResolved;
            subscribedBarrierController.OnBarrierActiveChanged += HandleBarrierActiveChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedBarrierController == null)
                return;

            subscribedBarrierController.OnResponseWindowStarted -= HandleResponseWindowStarted;
            subscribedBarrierController.OnResponseWindowResolved -= HandleResponseWindowResolved;
            subscribedBarrierController.OnBarrierActiveChanged -= HandleBarrierActiveChanged;
            subscribedBarrierController = null;
        }

        private void HandleResponseWindowStarted(BossAttackType attackType)
        {
            if (!showLowAttackPreview || attackType != BossAttackType.Low)
                return;

            Show(previewOpacity);
            previewHideAtTime = Time.time + Mathf.Max(minPreviewDuration, barrierController != null ? barrierController.ResponseWindowRemaining : 0f);
            LastVisualStatus = "BarrierVisual: low preview";
        }

        private void HandleResponseWindowResolved(bool success, string result)
        {
            if (!success && !isBarrierActive)
                Hide();
        }

        private void HandleBarrierActiveChanged(bool active)
        {
            isBarrierActive = active;

            if (active)
            {
                Show(activeOpacity);
                previewHideAtTime = 0f;
                LastVisualStatus = "BarrierVisual: active";
                return;
            }

            Hide();
        }

        private void Show(float opacity)
        {
            EnsureVisuals();

            if (shieldRoot == null)
                return;

            shieldRoot.gameObject.SetActive(true);
            ApplyOpacity(opacity);
        }

        private void Hide()
        {
            previewHideAtTime = 0f;
            isBarrierActive = false;

            if (shieldRoot != null)
                shieldRoot.gameObject.SetActive(false);

            LastVisualStatus = "BarrierVisual: idle";
        }

        private void EnsureVisuals()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            if (headTransform == null)
                return;

            if (shieldRoot != null)
                return;

            var rootObject = new GameObject("Arcane Golden Barrier Visual");
            shieldRoot = rootObject.transform;
            shieldRoot.SetParent(headTransform, false);
            shieldRoot.localPosition = localPosition;
            shieldRoot.localRotation = Quaternion.identity;
            shieldRoot.localScale = Vector3.one;

            var shieldObject = new GameObject("Golden Barrier Surface");
            shieldObject.transform.SetParent(shieldRoot, false);
            shieldObject.transform.localPosition = Vector3.zero;
            shieldObject.transform.localRotation = Quaternion.identity;
            shieldObject.transform.localScale = Vector3.one;

            var filter = shieldObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateShieldMesh();

            shieldRenderer = shieldObject.AddComponent<MeshRenderer>();
            shieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shieldRenderer.receiveShadows = false;
            shieldMaterial = CreateTransparentMaterial("ArcaneRuntimeGoldenBarrierSurface", goldColor, activeOpacity);
            shieldRenderer.sharedMaterial = shieldMaterial;

            lineMaterial = CreateTransparentMaterial("ArcaneRuntimeGoldenBarrierLines", goldColor, 0.78f);
            CreateOvalLine("Golden Barrier Rim", 0f, 1f, 1f, 0.025f);
            CreateOvalLine("Golden Barrier Inner Rim", 0.012f, 0.82f, 0.78f, 0.012f);
            CreateArcLine("Golden Barrier Sweep A", 28f, 0.92f, 0.58f, 0.025f);
            CreateArcLine("Golden Barrier Sweep B", -34f, 0.78f, 0.44f, 0.018f);

            shieldRoot.gameObject.SetActive(false);
        }

        private Mesh CreateShieldMesh()
        {
            const int columns = 24;
            const int rows = 16;

            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 12];

            var width = shieldSize.x;
            var height = shieldSize.y;
            var index = 0;

            for (var y = 0; y <= rows; y++)
            {
                var v = y / (float)rows;
                var normalizedY = (v - 0.5f) * 2f;

                for (var x = 0; x <= columns; x++)
                {
                    var u = x / (float)columns;
                    var normalizedX = (u - 0.5f) * 2f;
                    var edgeFactor = Mathf.Clamp01(normalizedX * normalizedX * 0.55f + normalizedY * normalizedY * 0.85f);
                    var domeDepth = -0.16f * (1f - edgeFactor);

                    vertices[index] = new Vector3(normalizedX * width * 0.5f, normalizedY * height * 0.5f, domeDepth);
                    uvs[index] = new Vector2(u, v);
                    index++;
                }
            }

            var triangleIndex = 0;
            var stride = columns + 1;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    var a = y * stride + x;
                    var b = a + 1;
                    var c = a + stride;
                    var d = c + 1;

                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;

                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = d;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                }
            }

            var mesh = new Mesh
            {
                name = "ArcaneRuntimeGoldenBarrierMesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateOvalLine(string objectName, float zOffset, float widthMultiplier, float heightMultiplier, float lineWidth)
        {
            const int points = 96;
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(shieldRoot, false);
            lineObject.transform.localPosition = new Vector3(0f, 0f, zOffset);
            lineObject.transform.localRotation = Quaternion.identity;

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = points;
            line.widthMultiplier = lineWidth;
            line.material = lineMaterial;

            for (var i = 0; i < points; i++)
            {
                var angle = Mathf.PI * 2f * i / points;
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * shieldSize.x * 0.5f * widthMultiplier,
                    Mathf.Sin(angle) * shieldSize.y * 0.5f * heightMultiplier,
                    zOffset));
            }
        }

        private void CreateArcLine(string objectName, float rotationZ, float widthMultiplier, float heightMultiplier, float lineWidth)
        {
            const int points = 72;
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(shieldRoot, false);
            lineObject.transform.localPosition = new Vector3(0f, 0f, 0.035f);
            lineObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = points;
            line.widthMultiplier = lineWidth;
            line.material = lineMaterial;

            for (var i = 0; i < points; i++)
            {
                var t = i / (float)(points - 1);
                var angle = Mathf.Lerp(-130f, 130f, t) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * shieldSize.x * 0.5f * widthMultiplier,
                    Mathf.Sin(angle) * shieldSize.y * 0.5f * heightMultiplier,
                    0f));
            }
        }

        private void ApplyOpacity(float surfaceOpacity)
        {
            var surfaceColor = goldColor;
            surfaceColor.a = Mathf.Clamp01(surfaceOpacity);
            ApplyColor(shieldMaterial, surfaceColor);

            var lineColor = goldColor;
            lineColor.a = Mathf.Clamp01(Mathf.Max(surfaceOpacity + 0.22f, 0.42f));
            ApplyColor(lineMaterial, lineColor);
        }

        private static Material CreateTransparentMaterial(string materialName, Color color, float alpha)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = materialName
            };

            ConfigureTransparentMaterial(material);
            color.a = alpha;
            ApplyColor(material, color);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            material.color = color;
        }
    }
}
