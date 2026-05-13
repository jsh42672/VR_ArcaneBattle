using ArcaneVR.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    [DefaultExecutionOrder(145)]
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private GolemCombatTarget golemTarget;

        [Header("Layout")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.65f, 0f);
        [SerializeField] private float barWidth = 1.8f;
        [SerializeField] private float barHeight = 0.075f;
        [SerializeField] private float barDepth = 0.018f;
        [SerializeField] private float textScale = 0.03f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.01f, 0.01f, 0.88f);
        [SerializeField] private Color borderColor = new Color(0.28f, 0.04f, 0.04f, 0.96f);
        [SerializeField] private Color healthColor = new Color(1f, 0.05f, 0.03f, 0.96f);
        [SerializeField] private Color textColor = new Color(1f, 0.86f, 0.78f, 1f);

        private GolemCombatTarget subscribedTarget;
        private Transform uiRoot;
        private Transform fillTransform;
        private TextMesh labelText;
        private Camera playerCamera;
        private float currentHealth;
        private float maxHealth = 1f;

        public string LastHealthBarStatus { get; private set; } = "BossHP: idle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForBattleScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsBattleScene(sceneName) || FindAnyObjectByType<BossHealthBarUI>() != null)
                return;

            var host = GameObject.Find("BattleManager") ??
                       GameObject.Find("Arcane Test Hub") ??
                       new GameObject("Boss Health Bar UI");
            host.AddComponent<BossHealthBarUI>();
        }

        private static bool IsBattleScene(string sceneName)
        {
            return sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

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
            EnsureVisuals();
            UpdateVisibility();
        }

        private void ResolveReferences()
        {
            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();

            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        private void Subscribe()
        {
            if (subscribedTarget == golemTarget)
                return;

            Unsubscribe();
            subscribedTarget = golemTarget;
            if (subscribedTarget == null)
                return;

            subscribedTarget.OnHealthChanged += HandleBossHealthChanged;
            HandleBossHealthChanged(subscribedTarget.CurrentHealth, subscribedTarget.MaxHealth);
        }

        private void Unsubscribe()
        {
            if (subscribedTarget == null)
                return;

            subscribedTarget.OnHealthChanged -= HandleBossHealthChanged;
            subscribedTarget = null;
        }

        private void HandleBossHealthChanged(float current, float max)
        {
            currentHealth = Mathf.Max(0f, current);
            maxHealth = Mathf.Max(1f, max);
            RefreshBar();
        }

        private void EnsureVisuals()
        {
            if (uiRoot != null || golemTarget == null)
                return;

            var root = new GameObject("Arcane Boss Health Bar");
            uiRoot = root.transform;
            uiRoot.gameObject.hideFlags = HideFlags.DontSave;
            uiRoot.position = ResolveBossBarPosition();
            uiRoot.rotation = Quaternion.identity;
            uiRoot.localScale = Vector3.one;

            CreateBarPart("Boss HP Border", uiRoot, new Vector3(0f, 0f, 0.01f), new Vector3(barWidth + 0.045f, barHeight + 0.026f, barDepth), borderColor);
            CreateBarPart("Boss HP Background", uiRoot, Vector3.zero, new Vector3(barWidth, barHeight, barDepth), backgroundColor);
            fillTransform = CreateBarPart("Boss HP Fill", uiRoot, Vector3.zero, new Vector3(barWidth, barHeight, barDepth * 1.2f), healthColor);

            var textObject = new GameObject("Boss HP Label");
            textObject.transform.SetParent(uiRoot, false);
            textObject.transform.localPosition = new Vector3(0f, 0.058f, 0f);
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * textScale;

            labelText = textObject.AddComponent<TextMesh>();
            labelText.anchor = TextAnchor.MiddleCenter;
            labelText.alignment = TextAlignment.Center;
            labelText.fontSize = 76;
            labelText.characterSize = 0.1f;
            labelText.color = textColor;

            RefreshBar();
        }

        private Transform CreateBarPart(string objectName, Transform parent, Vector3 localPositionValue, Vector3 localScaleValue, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPositionValue;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScaleValue;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateRuntimeMaterial(color);

            return part.transform;
        }

        private void RefreshBar()
        {
            if (fillTransform == null)
                return;

            var ratio = Mathf.Clamp01(maxHealth <= 0f ? 0f : currentHealth / maxHealth);
            fillTransform.localScale = new Vector3(Mathf.Max(0.001f, barWidth * ratio), barHeight, barDepth * 1.2f);
            fillTransform.localPosition = new Vector3((ratio - 1f) * barWidth * 0.5f, 0f, -0.012f);

            if (labelText != null)
                labelText.text = $"BOSS HP  {currentHealth:0}/{maxHealth:0}";

            LastHealthBarStatus = $"BossHP: {ratio * 100f:0}%";
        }

        private void UpdateVisibility()
        {
            if (uiRoot == null)
                return;

            uiRoot.gameObject.SetActive(golemTarget != null && golemTarget.CurrentHealth > 0f);
            if (!uiRoot.gameObject.activeSelf)
                return;

            uiRoot.position = ResolveBossBarPosition();
            var cameraTransform = playerCamera != null ? playerCamera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null)
                return;

            var lookDirection = uiRoot.position - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
                uiRoot.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private Vector3 ResolveBossBarPosition()
        {
            if (golemTarget == null)
                return transform.position;

            if (TryGetTargetBounds(out var bounds))
                return bounds.center + Vector3.up * (bounds.extents.y + worldOffset.y);

            return golemTarget.transform.position + Vector3.up * 2.4f + worldOffset;
        }

        private bool TryGetTargetBounds(out Bounds bounds)
        {
            bounds = new Bounds(golemTarget.transform.position, Vector3.zero);
            var initialized = false;
            foreach (var renderer in golemTarget.GetComponentsInChildren<Renderer>(true))
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

        private static Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = "ArcaneRuntimeBossHealth",
                hideFlags = HideFlags.DontSave,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }
    }
}
