using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(135)]
    public class BossAttackEffectController : MonoBehaviour
    {
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private DodgeDetector dodgeDetector;
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private Transform bossRoot;
        [SerializeField] private Transform headTransform;

        [Header("Attack VFX")]
        [SerializeField] private bool enableAttackEffects = true;
        [SerializeField] private float effectDurationMultiplier = 1.05f;
        [SerializeField] private float bossForwardOffset = 0.35f;
        [SerializeField] private float highAttackHeightOffset = 0.18f;
        [SerializeField] private float middleAttackHeightOffset = -0.32f;
        [SerializeField] private float lowAttackGroundOffset = 0.08f;
        [SerializeField] private Color highAttackColor = new Color(1f, 0.08f, 0.02f, 0.92f);
        [SerializeField] private Color middleAttackColor = new Color(1f, 0.58f, 0.04f, 0.9f);
        [SerializeField] private Color lowAttackColor = new Color(0.16f, 0.74f, 1f, 0.88f);
        [SerializeField] private Color successColor = new Color(0.18f, 1f, 0.52f, 0.95f);
        [SerializeField] private Color failColor = new Color(1f, 0.12f, 0.06f, 0.95f);

        private BossPatternCombatBridge subscribedPatternBridge;
        private DodgeDetector subscribedDodgeDetector;
        private BarrierController subscribedBarrierController;
        private CombatManager subscribedCombatManager;
        private Coroutine activeAttackRoutine;
        private float lastResolveEffectTime = -999f;

        public string LastEffectStatus { get; private set; } = "AttackFx: idle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForBattleScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsBattleScene(sceneName) || FindAnyObjectByType<BossAttackEffectController>() != null)
                return;

            var host = GameObject.Find("BattleManager") ??
                       GameObject.Find("Arcane Test Hub") ??
                       new GameObject("Boss Attack Effects");
            host.AddComponent<BossAttackEffectController>();
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
        }

        private void ResolveReferences()
        {
            if (patternBridge == null)
                patternBridge = FindAnyObjectByType<BossPatternCombatBridge>();

            if (dodgeDetector == null)
                dodgeDetector = FindAnyObjectByType<DodgeDetector>();

            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();

            if (bossRoot == null)
                bossRoot = ResolveBossRoot();

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
        }

        private Transform ResolveBossRoot()
        {
            if (golemTarget != null)
                return golemTarget.transform;

            var candidate = GameObject.Find("attack_golemn") ??
                            GameObject.Find("Golem_Placeholder") ??
                            GameObject.Find("GolemPlaceholder") ??
                            GameObject.Find("Golem");

            return candidate != null ? candidate.transform : null;
        }

        private void Subscribe()
        {
            if (subscribedPatternBridge != patternBridge)
            {
                if (subscribedPatternBridge != null)
                    subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;

                subscribedPatternBridge = patternBridge;
                if (subscribedPatternBridge != null)
                    subscribedPatternBridge.OnAttackResponseWindowStarted += HandleAttackStarted;
            }

            if (subscribedDodgeDetector != dodgeDetector)
            {
                if (subscribedDodgeDetector != null)
                {
                    subscribedDodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
                    subscribedDodgeDetector.OnDodgeFail -= HandleDodgeFail;
                }

                subscribedDodgeDetector = dodgeDetector;
                if (subscribedDodgeDetector != null)
                {
                    subscribedDodgeDetector.OnDodgeSuccess += HandleDodgeSuccess;
                    subscribedDodgeDetector.OnDodgeFail += HandleDodgeFail;
                }
            }

            if (subscribedBarrierController != barrierController)
            {
                if (subscribedBarrierController != null)
                    subscribedBarrierController.OnResponseWindowResolved -= HandleBarrierResolved;

                subscribedBarrierController = barrierController;
                if (subscribedBarrierController != null)
                    subscribedBarrierController.OnResponseWindowResolved += HandleBarrierResolved;
            }

            if (subscribedCombatManager != combatManager)
            {
                if (subscribedCombatManager != null)
                    subscribedCombatManager.OnPlayerHit -= HandlePlayerHit;

                subscribedCombatManager = combatManager;
                if (subscribedCombatManager != null)
                    subscribedCombatManager.OnPlayerHit += HandlePlayerHit;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedPatternBridge != null)
                subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;

            if (subscribedDodgeDetector != null)
            {
                subscribedDodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
                subscribedDodgeDetector.OnDodgeFail -= HandleDodgeFail;
            }

            if (subscribedBarrierController != null)
                subscribedBarrierController.OnResponseWindowResolved -= HandleBarrierResolved;

            if (subscribedCombatManager != null)
                subscribedCombatManager.OnPlayerHit -= HandlePlayerHit;

            subscribedPatternBridge = null;
            subscribedDodgeDetector = null;
            subscribedBarrierController = null;
            subscribedCombatManager = null;
        }

        private void HandleAttackStarted(BossAttackType attackType, float duration)
        {
            if (!enableAttackEffects)
                return;

            if (activeAttackRoutine != null)
                StopCoroutine(activeAttackRoutine);

            activeAttackRoutine = StartCoroutine(AttackEffectRoutine(attackType, Mathf.Max(0.2f, duration)));
        }

        private void HandleDodgeSuccess()
        {
            SpawnResolveEffect(true, "Evade");
        }

        private void HandleDodgeFail()
        {
            SpawnResolveEffect(false, "Hit");
        }

        private void HandleBarrierResolved(bool success, string result)
        {
            if (success)
            {
                SpawnResolveEffect(true, "Block");
                return;
            }

            if (string.IsNullOrEmpty(result) || result.Contains("Fail") || result.Contains("timeout"))
                SpawnResolveEffect(false, "Break");
        }

        private void HandlePlayerHit(float damage)
        {
            SpawnResolveEffect(false, $"Damage {damage:0}");
        }

        private IEnumerator AttackEffectRoutine(BossAttackType attackType, float duration)
        {
            ResolveReferences();

            var root = new GameObject($"Arcane Boss {attackType} Attack FX")
            {
                hideFlags = HideFlags.DontSave
            };

            var color = ResolveAttackColor(attackType);
            var lifetime = duration * Mathf.Max(0.2f, effectDurationMultiplier);
            BuildAttackVisual(root.transform, attackType, color);
            LastEffectStatus = $"AttackFx: {attackType}";

            var started = Time.time;
            while (root != null && Time.time - started < lifetime)
            {
                var t = Mathf.Clamp01((Time.time - started) / Mathf.Max(0.01f, lifetime));
                UpdateAttackVisual(root.transform, attackType, color, t);
                yield return null;
            }

            if (root != null)
                Destroy(root);

            activeAttackRoutine = null;
            LastEffectStatus = "AttackFx: idle";
        }

        private void BuildAttackVisual(Transform root, BossAttackType attackType, Color color)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    CreateBeam(root, "High Red Slash", color, 0.09f, 0.34f);
                    CreateBeam(root, "High White Core", Color.Lerp(color, Color.white, 0.55f), 0.025f, 0.18f);
                    CreatePlayerArc(root, "High Head Arc", color, 1.1f, 0.16f, 18);
                    break;

                case BossAttackType.Middle:
                    CreateBeam(root, "Middle Sweep", color, 0.12f, 0.34f);
                    CreateBeam(root, "Middle Core", Color.Lerp(color, Color.white, 0.42f), 0.035f, 0.16f);
                    CreatePlayerArc(root, "Middle Body Arc", color, 1.25f, 0.18f, 22);
                    break;

                case BossAttackType.Low:
                    CreateBeam(root, "Low Ground Streak", color, 0.14f, 0.32f);
                    CreateGroundRing(root, "Low Shockwave Ring", color, 0.45f, 40);
                    CreateGroundRing(root, "Low Shockwave Core", Color.Lerp(color, Color.white, 0.45f), 0.28f, 40);
                    break;
            }
        }

        private void UpdateAttackVisual(Transform root, BossAttackType attackType, Color color, float t)
        {
            var origin = ResolveBossAttackOrigin(attackType);
            var targetPoint = ResolvePlayerAttackPoint(attackType);

            foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
            {
                if (line == null)
                    continue;

                if (line.name.Contains("Arc"))
                {
                    UpdatePlayerArc(line, targetPoint, attackType, t);
                    continue;
                }

                if (line.name.Contains("Ring"))
                {
                    UpdateGroundRing(line, targetPoint, t);
                    continue;
                }

                UpdateBeam(line, origin, targetPoint, attackType, t);
            }
        }

        private Vector3 ResolveBossAttackOrigin(BossAttackType attackType)
        {
            var fallbackForward = bossRoot != null ? bossRoot.forward : Vector3.forward;
            var position = bossRoot != null ? bossRoot.position : ResolvePlayerAttackPoint(attackType) - fallbackForward * 4f;
            var height = attackType switch
            {
                BossAttackType.High => 1.75f,
                BossAttackType.Middle => 1.15f,
                BossAttackType.Low => 0.35f,
                _ => 1.15f
            };

            return position + Vector3.up * height + fallbackForward * bossForwardOffset;
        }

        private Vector3 ResolvePlayerAttackPoint(BossAttackType attackType)
        {
            var head = headTransform != null ? headTransform : Camera.main != null ? Camera.main.transform : null;
            var position = head != null ? head.position : transform.position + transform.forward * 2f;

            switch (attackType)
            {
                case BossAttackType.High:
                    return position + Vector3.up * highAttackHeightOffset;
                case BossAttackType.Middle:
                    return position + Vector3.up * middleAttackHeightOffset;
                case BossAttackType.Low:
                    return ResolveGroundPoint(position) + Vector3.up * lowAttackGroundOffset;
                default:
                    return position;
            }
        }

        private Vector3 ResolveGroundPoint(Vector3 position)
        {
            var origin = position + Vector3.up * 1.5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore) &&
                !ArcaneVR.Core.ArcanePlayerRigResolver.IsPlayerCollider(hit.collider))
            {
                return hit.point;
            }

            return new Vector3(position.x, position.y - 1.35f, position.z);
        }

        private void UpdateBeam(LineRenderer line, Vector3 origin, Vector3 targetPoint, BossAttackType attackType, float t)
        {
            var direction = targetPoint - origin;
            var right = Vector3.Cross(Vector3.up, direction.normalized);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            var sway = Mathf.Sin(t * Mathf.PI) * 0.32f;
            var offset = attackType == BossAttackType.Middle ? right.normalized * sway : Vector3.up * sway;
            var tip = Vector3.Lerp(origin, targetPoint, Mathf.SmoothStep(0.15f, 1f, t));
            var mid = Vector3.Lerp(origin, tip, 0.58f) + offset;

            line.positionCount = 4;
            line.SetPosition(0, origin);
            line.SetPosition(1, mid);
            line.SetPosition(2, tip);
            line.SetPosition(3, targetPoint);
            SetLineAlpha(line, Mathf.Sin(t * Mathf.PI));
        }

        private void UpdatePlayerArc(LineRenderer line, Vector3 center, BossAttackType attackType, float t)
        {
            var radius = attackType == BossAttackType.High ? 0.52f : 0.64f;
            var verticalScale = attackType == BossAttackType.High ? 0.2f : 0.42f;
            var progress = Mathf.SmoothStep(0.2f, 1f, t);
            var count = Mathf.Max(3, line.positionCount);
            var start = -110f;
            var end = Mathf.Lerp(start, 110f, progress);

            for (var i = 0; i < count; i++)
            {
                var p = count <= 1 ? 0f : (float)i / (count - 1);
                var angle = Mathf.Lerp(start, end, p) * Mathf.Deg2Rad;
                var local = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius * verticalScale, 0f);
                line.SetPosition(i, center + local);
            }

            SetLineAlpha(line, Mathf.Sin(t * Mathf.PI));
        }

        private void UpdateGroundRing(LineRenderer line, Vector3 center, float t)
        {
            var radius = Mathf.Lerp(0.35f, 1.45f, Mathf.SmoothStep(0f, 1f, t));
            var count = Mathf.Max(8, line.positionCount);
            for (var i = 0; i < count; i++)
            {
                var p = (float)i / (count - 1);
                var angle = p * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            SetLineAlpha(line, 1f - Mathf.SmoothStep(0.45f, 1f, t));
        }

        private LineRenderer CreateBeam(Transform parent, string objectName, Color color, float width, float alpha)
        {
            var lineObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
            lineObject.transform.SetParent(parent, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width;
            line.material = CreateRuntimeMaterial(WithAlpha(color, alpha));
            line.colorGradient = CreateGradient(WithAlpha(color, alpha));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private LineRenderer CreatePlayerArc(Transform parent, string objectName, Color color, float width, float alpha, int segments)
        {
            var line = CreateBeam(parent, objectName, color, width, alpha);
            line.positionCount = Mathf.Max(3, segments);
            line.loop = false;
            line.widthMultiplier = width * 0.08f;
            return line;
        }

        private LineRenderer CreateGroundRing(Transform parent, string objectName, Color color, float alpha, int segments)
        {
            var line = CreateBeam(parent, objectName, color, 0.045f, alpha);
            line.positionCount = Mathf.Max(12, segments + 1);
            line.loop = true;
            return line;
        }

        private void SpawnResolveEffect(bool success, string label)
        {
            if (!enableAttackEffects || Time.time - lastResolveEffectTime < 0.12f)
                return;

            lastResolveEffectTime = Time.time;
            var center = ResolvePlayerAttackPoint(BossAttackType.Middle);
            var color = success ? successColor : failColor;
            var root = new GameObject(success ? "Arcane Defense Success FX" : "Arcane Player Hit FX")
            {
                hideFlags = HideFlags.DontSave
            };

            var ring = CreateGroundRing(root.transform, label, color, success ? 0.65f : 0.82f, 36);
            var burst = CreateBeam(root.transform, "Resolve Burst", color, success ? 0.06f : 0.09f, 0.5f);
            StartCoroutine(ResolveEffectRoutine(root, ring, burst, center, success));
            LastEffectStatus = success ? $"AttackFx: {label}" : $"AttackFx: {label}";
        }

        private IEnumerator ResolveEffectRoutine(GameObject root, LineRenderer ring, LineRenderer burst, Vector3 center, bool success)
        {
            var duration = success ? 0.55f : 0.42f;
            var started = Time.time;
            while (root != null && Time.time - started < duration)
            {
                var t = Mathf.Clamp01((Time.time - started) / duration);
                if (ring != null)
                    UpdateResolveRing(ring, center, t, success);
                if (burst != null)
                    UpdateResolveBurst(burst, center, t);

                yield return null;
            }

            if (root != null)
                Destroy(root);
        }

        private void UpdateResolveRing(LineRenderer line, Vector3 center, float t, bool success)
        {
            var radius = Mathf.Lerp(success ? 0.25f : 0.18f, success ? 0.9f : 0.65f, t);
            var count = Mathf.Max(8, line.positionCount);
            for (var i = 0; i < count; i++)
            {
                var p = (float)i / (count - 1);
                var angle = p * Mathf.PI * 2f;
                var vertical = success ? Mathf.Sin(angle * 2f + t * Mathf.PI * 3f) * 0.03f : 0f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, vertical, Mathf.Sin(angle) * radius));
            }

            SetLineAlpha(line, 1f - t);
        }

        private void UpdateResolveBurst(LineRenderer line, Vector3 center, float t)
        {
            var up = Vector3.up * Mathf.Lerp(0.1f, 0.72f, t);
            var right = headTransform != null ? headTransform.right : Vector3.right;
            line.positionCount = 4;
            line.SetPosition(0, center - right * 0.34f);
            line.SetPosition(1, center + up);
            line.SetPosition(2, center + right * 0.34f);
            line.SetPosition(3, center - up * 0.35f);
            SetLineAlpha(line, 1f - t);
        }

        private Color ResolveAttackColor(BossAttackType attackType)
        {
            return attackType switch
            {
                BossAttackType.High => highAttackColor,
                BossAttackType.Middle => middleAttackColor,
                BossAttackType.Low => lowAttackColor,
                _ => Color.white
            };
        }

        private static void SetLineAlpha(LineRenderer line, float alpha)
        {
            if (line == null)
                return;

            var gradient = line.colorGradient;
            var colorKeys = gradient.colorKeys;
            var baseAlpha = 1f;
            var material = line.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                    baseAlpha = material.GetColor("_BaseColor").a;
                else if (material.HasProperty("_Color"))
                    baseAlpha = material.GetColor("_Color").a;
            }

            var resolvedAlpha = Mathf.Clamp01(baseAlpha * alpha);
            gradient.SetKeys(
                colorKeys,
                new[]
                {
                    new GradientAlphaKey(resolvedAlpha * 0.2f, 0f),
                    new GradientAlphaKey(resolvedAlpha, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            line.colorGradient = gradient;
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Transparent") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored");

            var material = new Material(shader)
            {
                name = "ArcaneRuntimeBossAttackFx",
                hideFlags = HideFlags.DontSave,
                renderQueue = 3000
            };

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            return material;
        }

        private static Gradient CreateGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.3f), 0f),
                    new GradientColorKey(color, 0.5f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(color.a * 0.2f, 0f),
                    new GradientAlphaKey(color.a, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
