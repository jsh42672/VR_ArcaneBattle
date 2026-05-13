using System.Collections;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(80)]
    public class AttackResponseDemoController : MonoBehaviour
    {
        [SerializeField] private bool autoRun = true;
        [SerializeField] private float readyDuration = 1.0f;
        [SerializeField] private float responseDuration = 1.6f;
        [SerializeField] private float resultDuration = 1.1f;
        [SerializeField] private float loopDelay = 0.45f;
        [SerializeField] private Vector3 overlayLocalPosition = new Vector3(0f, -0.05f, 1.95f);
        [SerializeField] private float textScale = 0.034f;
        [SerializeField] private Color slashColor = new Color(1f, 0.02f, 0.01f, 1f);
        [SerializeField] private Color successColor = new Color(0.2f, 1f, 0.32f, 1f);
        [SerializeField] private Color failColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private float slashStartLocalZ = 4.0f;
        [SerializeField] private float slashEndLocalZ = 0.7f;

        private DodgeDetector dodgeDetector;
        private BarrierController barrierController;
        private GestureDetector gestureDetector;
        private OVRHand leftOvrHand;
        private OVRHand rightOvrHand;
        private Transform headTransform;
        private Transform overlayRoot;
        private TextMesh resultText;
        private GameObject activeSlash;
        private LineRenderer activeSlashLine;
        private GameObject demoBarrierRoot;
        private Material slashMaterial;
        private Material barrierMaterial;
        private Vector3 slashAnchorPosition;
        private Vector3 slashForward = Vector3.forward;
        private Vector3 slashRight = Vector3.right;
        private Vector3 slashUp = Vector3.up;
        private bool awaitingResult;
        private bool lastResultSuccess;
        private string lastResultLabel = "WAIT";
        private Coroutine demoRoutine;
        private float nextSuppressionRefreshTime;
        private float barrierVisibleUntilTime;

        private const string DemoSuppressionReason = "DogeTest Attack Response Demo";

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

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene.name);
        }

        private static void EnsureForScene(string sceneName)
        {
            if (!IsDemoScene(sceneName) || FindAnyObjectByType<AttackResponseDemoController>() != null)
                return;

            var host = new GameObject("Attack Response Demo");
            host.AddComponent<AttackResponseDemoController>();
        }

        private static bool IsDemoScene(string sceneName)
        {
            return sceneName == "DogeTest" ||
                   sceneName == "DodgeTest";
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();

            if (autoRun && demoRoutine == null)
                demoRoutine = StartCoroutine(DemoLoop());
        }

        private void OnDisable()
        {
            Unsubscribe();
            ApplyDemoSuppression(false);
            if (demoRoutine != null)
                StopCoroutine(demoRoutine);
            demoRoutine = null;
        }

        private void Update()
        {
            ApplyDemoSuppression(true);
        }

        private void LateUpdate()
        {
            EnsureOverlay();
            UpdateDemoBarrierVisual();
        }

        private void ResolveReferences()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            if (dodgeDetector == null)
                dodgeDetector = GetComponent<DodgeDetector>() ??
                                FindAnyObjectByType<DodgeDetector>() ??
                                gameObject.AddComponent<DodgeDetector>();

            if (barrierController == null)
                barrierController = GetComponent<BarrierController>() ??
                                    FindAnyObjectByType<BarrierController>() ??
                                    gameObject.AddComponent<BarrierController>();

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            ResolveHands();
        }

        private void ResolveHands()
        {
            if (leftOvrHand != null && rightOvrHand != null)
                return;

            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (hand.GetHand() == OVRPlugin.Hand.HandLeft)
                    leftOvrHand = hand;
                else if (hand.GetHand() == OVRPlugin.Hand.HandRight)
                    rightOvrHand = hand;
            }
        }

        private void ApplyDemoSuppression(bool suppress)
        {
            if (suppress && Time.time < nextSuppressionRefreshTime)
                return;

            nextSuppressionRefreshTime = Time.time + (suppress ? 0.35f : 0f);

            foreach (var movement in FindObjectsByType<HandPullMovementController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                movement.SetMovementSuppressed(suppress, DemoSuppressionReason);

            foreach (var legacyMovement in FindObjectsByType<MovementController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                legacyMovement.IsEnabled = !suppress;

            foreach (var caster in FindObjectsByType<SpellCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                caster.SetCastingSuppressed(suppress, DemoSuppressionReason);

            foreach (var barrierVisual in FindObjectsByType<BarrierVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                barrierVisual.enabled = !suppress;

            SetNamedObjectActive("Arcane Gesture Text Overlay", !suppress);
            SetNamedObjectActive("Arcane Feedback Text", !suppress);
            SetNamedObjectActive("Right Wrist Mana Display", !suppress);
            SetNamedObjectActive("XR Input Diagnostics", !suppress);
        }

        private static void SetNamedObjectActive(string objectName, bool active)
        {
            var target = GameObject.Find(objectName);
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private void Subscribe()
        {
            if (dodgeDetector != null)
            {
                dodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
                dodgeDetector.OnDodgeFail -= HandleDodgeFail;
                dodgeDetector.OnDodgeSuccess += HandleDodgeSuccess;
                dodgeDetector.OnDodgeFail += HandleDodgeFail;
            }

            if (barrierController != null)
            {
                barrierController.OnResponseWindowResolved -= HandleBarrierResolved;
                barrierController.OnResponseWindowResolved += HandleBarrierResolved;
            }
        }

        private void Unsubscribe()
        {
            if (dodgeDetector != null)
            {
                dodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
                dodgeDetector.OnDodgeFail -= HandleDodgeFail;
            }

            if (barrierController != null)
                barrierController.OnResponseWindowResolved -= HandleBarrierResolved;
        }

        private IEnumerator DemoLoop()
        {
            yield return new WaitForSeconds(0.75f);

            var attacks = new[]
            {
                BossAttackType.High,
                BossAttackType.Middle,
                BossAttackType.Low
            };

            var index = 0;
            while (isActiveAndEnabled)
            {
                yield return RunAttack(attacks[index]);
                index = (index + 1) % attacks.Length;
                yield return new WaitForSeconds(loopDelay);
            }
        }

        private IEnumerator RunAttack(BossAttackType attackType)
        {
            ResolveReferences();
            EnsureOverlay();

            awaitingResult = false;
            lastResultSuccess = false;
            lastResultLabel = "WAIT";
            SetResult(string.Empty, Color.white);
            HideSlash();

            yield return new WaitForSeconds(readyDuration);

            awaitingResult = true;
            lastResultLabel = "HIT!";
            SetResult(string.Empty, slashColor);
            ShowSlash(attackType);

            if (attackType == BossAttackType.Low)
            {
                var combatManager = FindAnyObjectByType<CombatManager>();
                if (combatManager != null)
                    combatManager.RefundMana(4f);

                barrierController?.BeginResponseWindow(attackType, responseDuration);
            }
            else
            {
                dodgeDetector?.BeginDodgeWindow(attackType, responseDuration);
            }

            yield return AnimateSlash(attackType, responseDuration);

            if (awaitingResult)
            {
                awaitingResult = false;
                lastResultSuccess = false;
                lastResultLabel = "HIT!";
            }

            HideSlash();
            SetResult(lastResultLabel, lastResultSuccess ? successColor : failColor);
            yield return new WaitForSeconds(resultDuration);
        }

        private IEnumerator AnimateSlash(BossAttackType attackType, float duration)
        {
            var startTime = Time.time;
            var safeDuration = Mathf.Max(0.1f, duration);

            while (Time.time - startTime < safeDuration)
            {
                if (activeSlash != null)
                {
                    var t = Mathf.Clamp01((Time.time - startTime) / safeDuration);
                    var z = Mathf.Lerp(slashStartLocalZ, slashEndLocalZ, t);
                    var pulse = 1f + Mathf.Sin(t * Mathf.PI * 5f) * 0.05f;
                    activeSlash.transform.position = ResolveSlashWorldCenter(attackType, z);
                    ApplySlashLineShape(attackType, pulse);
                }

                if (attackType == BossAttackType.Low && awaitingResult && IsDemoGuardPoseActive())
                {
                    awaitingResult = false;
                    lastResultSuccess = true;
                    lastResultLabel = "BLOCKED!";
                    ShowDemoBarrier(1.1f);
                }

                yield return null;
            }
        }

        private void HandleDodgeSuccess()
        {
            if (!awaitingResult)
                return;

            awaitingResult = false;
            lastResultSuccess = true;
            lastResultLabel = "DODGED!";
        }

        private void HandleDodgeFail()
        {
            if (!awaitingResult)
                return;

            awaitingResult = false;
            lastResultSuccess = false;
            lastResultLabel = "HIT!";
        }

        private void HandleBarrierResolved(bool success, string result)
        {
            if (!awaitingResult)
                return;

            awaitingResult = false;
            lastResultSuccess = success;
            lastResultLabel = success ? "BLOCKED!" : "HIT!";
            if (success)
                ShowDemoBarrier(1.1f);
        }

        private void EnsureOverlay()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            if (headTransform == null)
                return;

            if (overlayRoot == null)
            {
                var root = new GameObject("Attack Response Demo Overlay");
                overlayRoot = root.transform;
                overlayRoot.SetParent(headTransform, false);
                overlayRoot.localPosition = overlayLocalPosition;
                overlayRoot.localRotation = Quaternion.identity;
                overlayRoot.localScale = Vector3.one;
            }

            if (resultText == null)
                resultText = CreateText("Demo Result Text", new Vector3(0f, -0.46f, 0f), textScale * 1.25f, 92);
        }

        private TextMesh CreateText(string objectName, Vector3 localPosition, float scale, int fontSize)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(overlayRoot, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * scale;

            var text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = fontSize;
            text.characterSize = 0.1f;
            text.color = Color.white;
            return text;
        }

        private void SetResult(string text, Color color)
        {
            EnsureOverlay();
            if (resultText == null)
                return;

            resultText.text = text;
            resultText.color = color;
        }

        private void ShowSlash(BossAttackType attackType)
        {
            EnsureOverlay();
            HideSlash();

            if (headTransform == null)
                return;

            activeSlash = new GameObject(ResolveSlashObjectName(attackType));
            activeSlash.name = ResolveSlashObjectName(attackType);
            activeSlash.hideFlags = HideFlags.DontSave;
            activeSlash.transform.SetParent(null, false);
            CaptureSlashAxes();
            activeSlash.transform.position = ResolveSlashWorldCenter(attackType, slashStartLocalZ);
            activeSlash.transform.rotation = Quaternion.identity;
            activeSlash.transform.localScale = Vector3.one;

            activeSlashLine = activeSlash.AddComponent<LineRenderer>();
            activeSlashLine.useWorldSpace = true;
            activeSlashLine.alignment = LineAlignment.View;
            activeSlashLine.positionCount = 2;
            activeSlashLine.startWidth = 0.075f;
            activeSlashLine.endWidth = 0.075f;
            activeSlashLine.startColor = slashColor;
            activeSlashLine.endColor = slashColor;
            activeSlashLine.numCapVertices = 8;
            activeSlashLine.numCornerVertices = 4;
            activeSlashLine.sharedMaterial = ResolveSlashMaterial();
            ApplySlashLineShape(attackType, 1f);
        }

        private void HideSlash()
        {
            if (activeSlash == null)
                return;

            Destroy(activeSlash);
            activeSlash = null;
            activeSlashLine = null;
        }

        private Material ResolveSlashMaterial()
        {
            if (slashMaterial != null)
                return slashMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Lit");
            slashMaterial = new Material(shader)
            {
                name = "ArcaneRuntimeDemoSlash",
                color = slashColor
            };

            if (slashMaterial.HasProperty("_BaseColor"))
                slashMaterial.SetColor("_BaseColor", slashColor);
            if (slashMaterial.HasProperty("_Color"))
                slashMaterial.SetColor("_Color", slashColor);
            if (slashMaterial.HasProperty("_EmissionColor"))
                slashMaterial.SetColor("_EmissionColor", slashColor * 2.25f);
            slashMaterial.EnableKeyword("_EMISSION");

            return slashMaterial;
        }

        private void CaptureSlashAxes()
        {
            slashAnchorPosition = headTransform.position;

            slashForward = Vector3.ProjectOnPlane(headTransform.forward, Vector3.up);
            if (slashForward.sqrMagnitude < 0.001f)
                slashForward = headTransform.forward;
            slashForward.Normalize();

            slashRight = Vector3.ProjectOnPlane(headTransform.right, Vector3.up);
            if (slashRight.sqrMagnitude < 0.001f)
                slashRight = Vector3.Cross(Vector3.up, slashForward);
            slashRight.Normalize();

            slashUp = Vector3.up;
        }

        private Vector3 ResolveSlashWorldCenter(BossAttackType attackType, float forwardDistance)
        {
            var local = ResolveSlashPosition(attackType);
            return slashAnchorPosition +
                   slashRight * local.x +
                   slashUp * local.y +
                   slashForward * forwardDistance;
        }

        private void ApplySlashLineShape(BossAttackType attackType, float pulse)
        {
            if (activeSlashLine == null)
                return;

            var center = activeSlash.transform.position;

            if (attackType == BossAttackType.Middle)
            {
                var halfHeight = 0.72f * pulse;
                activeSlashLine.SetPosition(0, center - slashUp * halfHeight);
                activeSlashLine.SetPosition(1, center + slashUp * halfHeight);
            }
            else
            {
                var halfWidth = 1.35f * pulse;
                activeSlashLine.SetPosition(0, center - slashRight * halfWidth);
                activeSlashLine.SetPosition(1, center + slashRight * halfWidth);
            }
        }

        private bool IsDemoGuardPoseActive()
        {
            ResolveReferences();

            if (gestureDetector != null)
            {
                var leftFist = gestureDetector.CurrentLeftPrototypePose == PoseType.Fist ||
                               gestureDetector.CurrentLeftPose == PoseId.Fist ||
                               gestureDetector.CurrentLeftPose == PoseId.FistPush;
                var rightFist = gestureDetector.CurrentRightPrototypePose == PoseType.Fist ||
                                gestureDetector.CurrentRightPose == PoseId.Fist ||
                                gestureDetector.CurrentRightPose == PoseId.FistPush;
                if (leftFist && rightFist)
                    return true;
            }

            return IsOvrFist(leftOvrHand) && IsOvrFist(rightOvrHand);
        }

        private static bool IsOvrFist(OVRHand hand)
        {
            if (hand == null || !hand.isActiveAndEnabled || !hand.IsTracked)
                return false;

            return hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) > 0.5f &&
                   hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) > 0.5f &&
                   hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) > 0.35f &&
                   hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) > 0.35f;
        }

        private void ShowDemoBarrier(float visibleSeconds)
        {
            EnsureDemoBarrier();
            barrierVisibleUntilTime = Time.time + Mathf.Max(0.2f, visibleSeconds);
            if (demoBarrierRoot != null)
                demoBarrierRoot.SetActive(true);
        }

        private void UpdateDemoBarrierVisual()
        {
            if (demoBarrierRoot == null)
                return;

            if (headTransform != null && demoBarrierRoot.transform.parent != headTransform)
                demoBarrierRoot.transform.SetParent(headTransform, false);

            demoBarrierRoot.SetActive(Time.time < barrierVisibleUntilTime);
        }

        private void EnsureDemoBarrier()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;
            if (headTransform == null)
                return;

            if (demoBarrierRoot != null)
                return;

            demoBarrierRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            demoBarrierRoot.name = "Demo Golden Guard Barrier";
            demoBarrierRoot.transform.SetParent(headTransform, false);
            demoBarrierRoot.transform.localPosition = new Vector3(0f, -0.2f, 1.05f);
            demoBarrierRoot.transform.localRotation = Quaternion.identity;
            demoBarrierRoot.transform.localScale = new Vector3(1.5f, 1.0f, 0.035f);
            demoBarrierRoot.hideFlags = HideFlags.DontSave;

            var collider = demoBarrierRoot.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = demoBarrierRoot.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ResolveBarrierMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            demoBarrierRoot.SetActive(false);
        }

        private Material ResolveBarrierMaterial()
        {
            if (barrierMaterial != null)
                return barrierMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default");
            barrierMaterial = new Material(shader)
            {
                name = "ArcaneRuntimeDemoBarrier"
            };

            var color = new Color(1f, 0.72f, 0.08f, 0.58f);
            barrierMaterial.color = color;
            if (barrierMaterial.HasProperty("_BaseColor"))
                barrierMaterial.SetColor("_BaseColor", color);
            if (barrierMaterial.HasProperty("_EmissionColor"))
                barrierMaterial.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.08f, 1f) * 1.6f);
            if (barrierMaterial.HasProperty("_Surface"))
                barrierMaterial.SetFloat("_Surface", 1f);
            if (barrierMaterial.HasProperty("_AlphaClip"))
                barrierMaterial.SetFloat("_AlphaClip", 0f);
            if (barrierMaterial.HasProperty("_SrcBlend"))
                barrierMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (barrierMaterial.HasProperty("_DstBlend"))
                barrierMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (barrierMaterial.HasProperty("_ZWrite"))
                barrierMaterial.SetFloat("_ZWrite", 0f);
            barrierMaterial.EnableKeyword("_EMISSION");
            barrierMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            barrierMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return barrierMaterial;
        }

        private static string ResolveSlashObjectName(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return "High Horizontal Head Slash";
                case BossAttackType.Middle:
                    return "Middle Vertical Body Slash";
                case BossAttackType.Low:
                    return "Low Horizontal Leg Slash";
                default:
                    return "Demo Slash";
            }
        }

        private static Vector3 ResolveSlashPosition(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return new Vector3(0f, 0.12f, 4.0f);
                case BossAttackType.Middle:
                    return new Vector3(0f, -0.42f, 4.0f);
                case BossAttackType.Low:
                    return new Vector3(0f, -0.95f, 4.0f);
                default:
                    return new Vector3(0f, 0f, 4.0f);
            }
        }
    }
}
