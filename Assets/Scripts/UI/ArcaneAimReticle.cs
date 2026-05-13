using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    [DefaultExecutionOrder(135)]
    public class ArcaneAimReticle : MonoBehaviour
    {
        [SerializeField] private float reticleDistance = 1.9f;
        [SerializeField] private Vector2 localOffset = new Vector2(0.15f, -0.4f);
        [SerializeField] private float ringRadius = 0.034f;
        [SerializeField] private float dotDiameter = 0.009f;
        [SerializeField] private float idleLineWidth = 0.0018f;
        [SerializeField] private float armedLineWidth = 0.0028f;
        [SerializeField] private int ringSegments = 72;
        [SerializeField] private Color idleColor = new Color(0.72f, 0.95f, 1f, 0.48f);
        [SerializeField] private Color lockedColor = new Color(0.45f, 0.55f, 0.62f, 0.18f);
        [SerializeField] private Color fireColor = new Color(1f, 0.28f, 0.12f, 0.95f);
        [SerializeField] private Color iceColor = new Color(0.28f, 0.72f, 1f, 0.95f);
        [SerializeField] private Color thunderColor = new Color(1f, 0.86f, 0.08f, 0.95f);

        private LineRenderer ringRenderer;
        private Renderer dotRenderer;
        private Material reticleMaterial;
        private SpellCaster spellCaster;
        private GrimoireManager grimoireManager;
        private Transform cachedCamera;
        private float nextReferenceRefreshTime;

        public static ArcaneAimReticle ActiveReticle { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForActiveScene()
        {
            CreateForScene(SceneManager.GetActiveScene().name);
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            CreateForScene(scene.name);
        }

        private static void CreateForScene(string sceneName)
        {
            if (!IsReticleScene(sceneName) || FindAnyObjectByType<ArcaneAimReticle>() != null)
                return;

            new GameObject("Arcane Aim Reticle").AddComponent<ArcaneAimReticle>();
        }

        private static bool IsReticleScene(string sceneName)
        {
            return sceneName == "Main" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum" ||
                   sceneName == "ElectricColoseum";
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            ActiveReticle = this;
        }

        private void OnDisable()
        {
            if (ActiveReticle == this)
                ActiveReticle = null;
        }

        public static bool TryGetAimPoint(float distance, out Vector3 aimPoint)
        {
            aimPoint = Vector3.zero;

            var reticle = ActiveReticle;
            if (reticle == null)
                reticle = FindAnyObjectByType<ArcaneAimReticle>();

            if (reticle == null || reticle.cachedCamera == null)
                return false;

            var rayDirection = reticle.ResolveAimRayDirection();
            if (rayDirection.sqrMagnitude <= 0.001f)
                return false;

            aimPoint = reticle.cachedCamera.position + rayDirection.normalized * Mathf.Max(1f, distance);
            return true;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextReferenceRefreshTime)
            {
                nextReferenceRefreshTime = Time.unscaledTime + 0.35f;
                ResolveReferences();
            }

            AttachToCamera();
            EnsureVisuals();
            RefreshVisualState();
        }

        private void ResolveReferences()
        {
            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();

            if (cachedCamera == null && Camera.main != null)
                cachedCamera = Camera.main.transform;
        }

        private void AttachToCamera()
        {
            if (cachedCamera == null)
                return;

            if (transform.parent != cachedCamera)
                transform.SetParent(cachedCamera, false);

            transform.localPosition = new Vector3(localOffset.x, localOffset.y, reticleDistance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private Vector3 ResolveAimRayDirection()
        {
            if (cachedCamera == null)
                return Vector3.zero;

            var localPoint = new Vector3(localOffset.x, localOffset.y, reticleDistance);
            return cachedCamera.TransformDirection(localPoint.normalized);
        }

        private void EnsureVisuals()
        {
            if (reticleMaterial == null)
                reticleMaterial = CreateReticleMaterial();

            if (ringRenderer == null)
                ringRenderer = CreateRing();

            if (dotRenderer == null)
                dotRenderer = CreateDot();
        }

        private LineRenderer CreateRing()
        {
            var ringObject = new GameObject("Reticle Ring");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = Vector3.zero;
            ringObject.transform.localRotation = Quaternion.identity;

            var line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Mathf.Max(16, ringSegments);
            line.widthMultiplier = idleLineWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = reticleMaterial;

            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius, 0f));
            }

            return line;
        }

        private Renderer CreateDot()
        {
            var dotObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dotObject.name = "Reticle Dot";
            dotObject.transform.SetParent(transform, false);
            dotObject.transform.localPosition = Vector3.zero;
            dotObject.transform.localRotation = Quaternion.identity;
            dotObject.transform.localScale = Vector3.one * dotDiameter;

            var collider = dotObject.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = dotObject.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = reticleMaterial;

            return renderer;
        }

        private Material CreateReticleMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = "Arcane Aim Reticle Material",
                renderQueue = 3000
            };
            SetMaterialColor(material, idleColor);
            return material;
        }

        private void RefreshVisualState()
        {
            var locked = grimoireManager != null && grimoireManager.IsOpen ||
                         spellCaster != null && spellCaster.IsCastingSuppressed;
            var color = locked ? lockedColor : ResolveReticleColor();
            var width = spellCaster != null && spellCaster.IsPrototypeArmed && !locked
                ? armedLineWidth
                : idleLineWidth;

            SetMaterialColor(reticleMaterial, color);

            if (ringRenderer != null)
            {
                ringRenderer.widthMultiplier = width;
                ringRenderer.startColor = color;
                ringRenderer.endColor = color;
            }

            if (dotRenderer != null)
                dotRenderer.enabled = true;
        }

        private Color ResolveReticleColor()
        {
            if (spellCaster == null || !spellCaster.IsPrototypeArmed)
                return idleColor;

            return spellCaster.PrototypeArmedElement switch
            {
                ElementType.Fire => fireColor,
                ElementType.Ice => iceColor,
                ElementType.Thunder => thunderColor,
                _ => idleColor
            };
        }

        private static void SetMaterialColor(Material material, Color color)
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
