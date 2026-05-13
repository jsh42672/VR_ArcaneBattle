using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(50)]
    public class ArenaBoundaryRuntimeWall : MonoBehaviour
    {
        [SerializeField] private float fallbackRadius = 18f;
        [SerializeField] private float radiusScaleFromArenaFloor = 0.94f;
        [SerializeField] private float playerClampMargin = 0.65f;
        [SerializeField] private float wallHeight = 5.5f;
        [SerializeField] private float wallThickness = 0.35f;
        [SerializeField] private int wallSegments = 72;
        [SerializeField] private bool showTransparentWall = true;
        [SerializeField, Range(0f, 1f)] private float wallAlpha = 0.10f;
        [SerializeField] private Color wallColor = new Color(0.45f, 0.85f, 1f, 0.10f);

        private static ArenaBoundaryRuntimeWall activeBoundary;

        private Vector3 center;
        private float radius;
        private bool built;
        private Material wallMaterial;

        public Vector3 Center => center;
        public float Radius => radius;
        public float ClampRadius => Mathf.Max(0.1f, radius - Mathf.Max(0f, playerClampMargin));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForActiveScene()
        {
            EnsureForScene(SceneManager.GetActiveScene().name);
        }

        public static bool TryClampInside(Vector3 worldPosition, out Vector3 clampedPosition)
        {
            clampedPosition = worldPosition;

            if (activeBoundary == null)
                EnsureForScene(SceneManager.GetActiveScene().name);

            if (activeBoundary == null)
                return false;

            activeBoundary.EnsureBuilt();
            return activeBoundary.TryClamp(worldPosition, out clampedPosition);
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            activeBoundary = null;
            EnsureForScene(scene.name);
        }

        private static void EnsureForScene(string sceneName)
        {
            if (!IsBattleScene(sceneName) || activeBoundary != null)
                return;

            var host = GameObject.Find("Arena Runtime Boundary") ?? new GameObject("Arena Runtime Boundary");
            activeBoundary = host.GetComponent<ArenaBoundaryRuntimeWall>();
            if (activeBoundary == null)
                activeBoundary = host.AddComponent<ArenaBoundaryRuntimeWall>();
            activeBoundary.EnsureBuilt();
        }

        private static bool IsBattleScene(string sceneName)
        {
            return sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

        private void Awake()
        {
            activeBoundary = this;
        }

        private void Start()
        {
            EnsureBuilt();
        }

        private void OnEnable()
        {
            activeBoundary = this;
        }

        private void OnDisable()
        {
            if (activeBoundary == this)
                activeBoundary = null;
        }

        private void EnsureBuilt()
        {
            if (built)
                return;

            ResolveArenaShape();
            BuildWall();
            built = true;
        }

        private bool TryClamp(Vector3 worldPosition, out Vector3 clampedPosition)
        {
            clampedPosition = worldPosition;
            var flatOffset = new Vector3(worldPosition.x - center.x, 0f, worldPosition.z - center.z);
            var maxRadius = ClampRadius;
            if (flatOffset.sqrMagnitude <= maxRadius * maxRadius)
                return false;

            var direction = flatOffset.sqrMagnitude > 0.0001f ? flatOffset.normalized : Vector3.forward;
            clampedPosition = new Vector3(
                center.x + direction.x * maxRadius,
                worldPosition.y,
                center.z + direction.z * maxRadius);
            return true;
        }

        private void ResolveArenaShape()
        {
            radius = Mathf.Max(4f, fallbackRadius);
            center = Vector3.zero;

            var arenaFloor = FindSceneObject("ArenaFloor");
            if (arenaFloor != null && TryGetRendererBounds(arenaFloor.transform, out var floorBounds))
            {
                center = floorBounds.center;
                radius = Mathf.Max(radius, Mathf.Max(floorBounds.extents.x, floorBounds.extents.z) * radiusScaleFromArenaFloor);
                center.y = floorBounds.min.y;
                return;
            }

            var marker = FindSceneObject("CombatZone_Marker") ?? FindSceneObject("PlayerSpawnPoint");
            if (marker != null)
                center = marker.transform.position;
        }

        private void BuildWall()
        {
            var safeSegments = Mathf.Clamp(wallSegments, 16, 160);
            var segmentLength = (2f * Mathf.PI * radius) / safeSegments * 1.08f;
            var baseY = center.y + wallHeight * 0.5f;

            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("Arena Boundary Wall Segment"))
                    Destroy(child.gameObject);
            }

            var material = ResolveWallMaterial();
            for (var i = 0; i < safeSegments; i++)
            {
                var angle = i * Mathf.PI * 2f / safeSegments;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Arena Boundary Wall Segment {i:00}";
                segment.transform.SetParent(transform, false);
                segment.transform.position = center + radial * radius + Vector3.up * (baseY - center.y);
                segment.transform.rotation = Quaternion.LookRotation(radial, Vector3.up);
                segment.transform.localScale = new Vector3(segmentLength, wallHeight, wallThickness);

                var renderer = segment.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = showTransparentWall;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sharedMaterial = material;
                }

                var collider = segment.GetComponent<BoxCollider>();
                if (collider != null)
                    collider.isTrigger = false;
            }
        }

        private Material ResolveWallMaterial()
        {
            if (wallMaterial != null)
                return wallMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            wallMaterial = new Material(shader)
            {
                name = "ArcaneRuntimeTransparentArenaWall"
            };

            var color = wallColor;
            color.a = wallAlpha;
            wallMaterial.color = color;
            if (wallMaterial.HasProperty("_BaseColor"))
                wallMaterial.SetColor("_BaseColor", color);
            if (wallMaterial.HasProperty("_Surface"))
                wallMaterial.SetFloat("_Surface", 1f);
            if (wallMaterial.HasProperty("_Blend"))
                wallMaterial.SetFloat("_Blend", 0f);
            if (wallMaterial.HasProperty("_SrcBlend"))
                wallMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (wallMaterial.HasProperty("_DstBlend"))
                wallMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (wallMaterial.HasProperty("_ZWrite"))
                wallMaterial.SetFloat("_ZWrite", 0f);
            if (wallMaterial.HasProperty("_AlphaClip"))
                wallMaterial.SetFloat("_AlphaClip", 0f);
            wallMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            wallMaterial.renderQueue = (int)RenderQueue.Transparent;
            return wallMaterial;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            var direct = GameObject.Find(objectName);
            if (direct != null)
                return direct;

            var activeScene = SceneManager.GetActiveScene();
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null ||
                    transform.gameObject.scene != activeScene ||
                    transform.hideFlags != HideFlags.None ||
                    transform.name != objectName)
                {
                    continue;
                }

                return transform.gameObject;
            }

            return null;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(root.position, Vector3.zero);
            var initialized = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }
    }
}
