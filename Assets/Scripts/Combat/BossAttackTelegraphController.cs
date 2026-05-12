using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Runtime world-space warning for boss attack response windows.
    /// It intentionally uses simple primitives so battle scenes do not need extra prefab wiring.
    /// </summary>
    public class BossAttackTelegraphController : MonoBehaviour
    {
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private Transform headTransform;
        [SerializeField] private bool showTextWarning = true;
        [SerializeField] private bool showShapeWarning = true;
        [SerializeField] private Vector3 warningLocalPosition = new Vector3(0f, -0.28f, 2.35f);
        [SerializeField] private float textScale = 0.035f;
        [SerializeField] private float minDisplayDuration = 0.75f;
        [SerializeField] private Color highAttackColor = new Color(1f, 0.18f, 0.06f, 1f);
        [SerializeField] private Color middleAttackColor = new Color(1f, 0.72f, 0.08f, 1f);
        [SerializeField] private Color lowAttackColor = new Color(0.12f, 0.65f, 1f, 1f);

        private BossPatternCombatBridge subscribedBridge;
        private Transform overlayRoot;
        private TextMesh warningText;
        private GameObject highBand;
        private GameObject middleBand;
        private GameObject lowBand;
        private float hideAtTime;

        public string LastTelegraphStatus { get; private set; } = "Telegraph: idle";

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

            if (hideAtTime > 0f && Time.time >= hideAtTime)
                Hide();
        }

        private void ResolveReferences()
        {
            if (patternBridge == null)
                patternBridge = FindAnyObjectByType<BossPatternCombatBridge>();

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
        }

        private void Subscribe()
        {
            if (patternBridge == null || subscribedBridge == patternBridge)
                return;

            Unsubscribe();
            subscribedBridge = patternBridge;
            subscribedBridge.OnAttackResponseWindowStarted += HandleAttackResponseWindowStarted;
        }

        private void Unsubscribe()
        {
            if (subscribedBridge == null)
                return;

            subscribedBridge.OnAttackResponseWindowStarted -= HandleAttackResponseWindowStarted;
            subscribedBridge = null;
        }

        private void HandleAttackResponseWindowStarted(BossAttackType attackType, float duration)
        {
            ShowAttackWarning(attackType, duration);
        }

        public void ShowAttackWarning(BossAttackType attackType, float duration)
        {
            EnsureVisuals();

            var color = ResolveColor(attackType);
            var message = ResolveMessage(attackType);
            var displayDuration = Mathf.Max(minDisplayDuration, duration);

            if (warningText != null)
            {
                warningText.text = message;
                warningText.color = color;
                warningText.gameObject.SetActive(showTextWarning);
            }

            SetBandActive(highBand, showShapeWarning && attackType == BossAttackType.High, color);
            SetBandActive(middleBand, showShapeWarning && attackType == BossAttackType.Middle, color);
            SetBandActive(lowBand, showShapeWarning && attackType == BossAttackType.Low, color);

            hideAtTime = Time.time + displayDuration;
            LastTelegraphStatus = $"Telegraph: {attackType}";
        }

        private void EnsureVisuals()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            if (headTransform == null)
                return;

            if (overlayRoot == null)
            {
                var rootObject = new GameObject("Arcane Boss Attack Telegraph");
                overlayRoot = rootObject.transform;
                overlayRoot.SetParent(headTransform, false);
                overlayRoot.localPosition = warningLocalPosition;
                overlayRoot.localRotation = Quaternion.identity;
                overlayRoot.localScale = Vector3.one;
            }

            if (warningText == null)
            {
                var textObject = new GameObject("Attack Warning Text");
                textObject.transform.SetParent(overlayRoot, false);
                textObject.transform.localPosition = Vector3.zero;
                textObject.transform.localRotation = Quaternion.identity;
                textObject.transform.localScale = Vector3.one * textScale;

                warningText = textObject.AddComponent<TextMesh>();
                warningText.anchor = TextAnchor.MiddleCenter;
                warningText.alignment = TextAlignment.Center;
                warningText.fontSize = 96;
                warningText.characterSize = 0.1f;
            }

            if (highBand == null)
                highBand = CreateBand("High Dodge Band", new Vector3(0f, 0.26f, 0.08f), new Vector3(1.35f, 0.05f, 0.02f));

            if (middleBand == null)
                middleBand = CreateBand("Middle Dodge Band", new Vector3(0f, -0.03f, 0.08f), new Vector3(1.35f, 0.18f, 0.02f));

            if (lowBand == null)
                lowBand = CreateBand("Low Barrier Band", new Vector3(0f, -0.33f, 0.08f), new Vector3(1.35f, 0.05f, 0.02f));
        }

        private GameObject CreateBand(string objectName, Vector3 localPosition, Vector3 localScale)
        {
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = objectName;
            band.transform.SetParent(overlayRoot, false);
            band.transform.localPosition = localPosition;
            band.transform.localRotation = Quaternion.identity;
            band.transform.localScale = localScale;

            var collider = band.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            band.SetActive(false);
            return band;
        }

        private void SetBandActive(GameObject band, bool active, Color color)
        {
            if (band == null)
                return;

            band.SetActive(active);
            if (!active)
                return;

            var renderer = band.GetComponent<Renderer>();
            if (renderer == null)
                return;

            if (renderer.sharedMaterial == null || !renderer.sharedMaterial.name.StartsWith("ArcaneRuntimeTelegraph"))
                renderer.sharedMaterial = CreateRuntimeMaterial(color);
            else
                renderer.sharedMaterial.color = color;
        }

        private Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = "ArcaneRuntimeTelegraph",
                color = color
            };
            return material;
        }

        private Color ResolveColor(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return highAttackColor;
                case BossAttackType.Middle:
                    return middleAttackColor;
                case BossAttackType.Low:
                    return lowAttackColor;
                default:
                    return Color.white;
            }
        }

        private static string ResolveMessage(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return "HIGH ATTACK\nDUCK";
                case BossAttackType.Middle:
                    return "MIDDLE ATTACK\nMOVE SIDE";
                case BossAttackType.Low:
                    return "LOW ATTACK\nBARRIER";
                default:
                    return "INCOMING";
            }
        }

        private void Hide()
        {
            hideAtTime = 0f;

            if (warningText != null)
                warningText.gameObject.SetActive(false);

            if (highBand != null)
                highBand.SetActive(false);
            if (middleBand != null)
                middleBand.SetActive(false);
            if (lowBand != null)
                lowBand.SetActive(false);

            LastTelegraphStatus = "Telegraph: idle";
        }
    }
}
