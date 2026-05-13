using ArcaneVR.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    [DefaultExecutionOrder(95)]
    public class ManaWristDisplay : MonoBehaviour
    {
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private OVRHand rightHand;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Vector3 wristLocalOffset = new Vector3(0f, -0.075f, 0f);
        [SerializeField] private float wristTowardPlayerOffset = 0.065f;
        [SerializeField] private Vector3 wristLocalEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private float segmentSpacing = 0.027f;
        [SerializeField] private Vector3 segmentScale = new Vector3(0.022f, 0.005f, 0.008f);
        [SerializeField] private Color filledColor = new Color(0.2f, 0.78f, 1f, 0.95f);
        [SerializeField] private Color partialColor = new Color(0.65f, 0.92f, 1f, 0.85f);
        [SerializeField] private Color emptyColor = new Color(0.04f, 0.09f, 0.14f, 0.65f);
        [SerializeField] private Color disruptedColor = new Color(0.95f, 0.35f, 1f, 0.95f);

        private const int SegmentCount = 4;
        private readonly MeshRenderer[] segmentRenderers = new MeshRenderer[SegmentCount];
        private Transform visualRoot;
        private Transform currentAnchor;
        private Material[] segmentMaterials;
        private float currentMana = 4f;
        private float maxMana = 4f;
        private bool subscribed;
        private float nextReferenceRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForArcaneScenes()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsArcaneScene(sceneName) || FindAnyObjectByType<ManaWristDisplay>() != null)
                return;

            var host = GameObject.Find("FeedbackManager") ??
                       GameObject.Find("BattleManager") ??
                       new GameObject("ManaWristDisplay");
            host.AddComponent<ManaWristDisplay>();
        }

        private static bool IsArcaneScene(string sceneName)
        {
            return sceneName == "Main" ||
                   sceneName == "World" ||
                   sceneName == "Tutorial" ||
                   sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureVisuals();
            Subscribe();
            RefreshMana();
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

        private void LateUpdate()
        {
            if (Time.time >= nextReferenceRefreshTime)
            {
                nextReferenceRefreshTime = Time.time + 0.5f;
                ResolveReferences();
                Subscribe();
            }

            EnsureVisuals();
            PositionVisuals();
            RefreshMana();
        }

        private void ResolveReferences()
        {
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (rightHand == null ||
                rightHand.GetHand() != OVRPlugin.Hand.HandRight ||
                !rightHand.gameObject.activeInHierarchy ||
                !rightHand.enabled ||
                !rightHand.IsTracked)
            {
                rightHand = FindRightHand();
            }

            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        private void Subscribe()
        {
            if (subscribed || combatManager == null)
                return;

            combatManager.OnManaChanged += HandleManaChanged;
            subscribed = true;
            HandleManaChanged(combatManager.CurrentMana, combatManager.MaxMana);
        }

        private void Unsubscribe()
        {
            if (!subscribed || combatManager == null)
                return;

            combatManager.OnManaChanged -= HandleManaChanged;
            subscribed = false;
        }

        private void HandleManaChanged(float current, float max)
        {
            currentMana = Mathf.Max(0f, current);
            maxMana = Mathf.Max(0.01f, max);
            RefreshMana();
        }

        private void EnsureVisuals()
        {
            if (visualRoot != null)
                return;

            visualRoot = new GameObject("Right Wrist Mana Display").transform;
            visualRoot.gameObject.hideFlags = HideFlags.DontSave;
            ParkVisualRoot();
            visualRoot.gameObject.SetActive(false);

            var backplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backplate.name = "Mana Bar Backplate";
            backplate.transform.SetParent(visualRoot, false);
            backplate.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            backplate.transform.localRotation = Quaternion.identity;
            backplate.transform.localScale = new Vector3(segmentSpacing * 4.25f, segmentScale.y * 0.65f, segmentScale.z * 1.2f);
            var backplateCollider = backplate.GetComponent<Collider>();
            if (backplateCollider != null)
                Destroy(backplateCollider);

            var backplateRenderer = backplate.GetComponent<MeshRenderer>();
            if (backplateRenderer != null)
                backplateRenderer.sharedMaterial = CreateRuntimeMaterial(emptyColor);

            segmentMaterials = new Material[SegmentCount];
            for (var i = 0; i < SegmentCount; i++)
            {
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Mana Segment {i + 1}";
                segment.transform.SetParent(visualRoot, false);
                segment.transform.localPosition = new Vector3((i - 1.5f) * segmentSpacing, 0f, 0f);
                segment.transform.localRotation = Quaternion.identity;
                segment.transform.localScale = segmentScale;

                var collider = segment.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                var renderer = segment.GetComponent<MeshRenderer>();
                segmentRenderers[i] = renderer;
                segmentMaterials[i] = CreateRuntimeMaterial(emptyColor);
                if (renderer != null)
                    renderer.sharedMaterial = segmentMaterials[i];
            }
        }

        private void PositionVisuals()
        {
            if (visualRoot == null)
                return;

            var anchor = ResolveAnchorTransform();
            if (anchor == null)
            {
                currentAnchor = null;
                ParkVisualRoot();
                visualRoot.gameObject.SetActive(true);
                return;
            }

            if (!visualRoot.gameObject.activeSelf)
                visualRoot.gameObject.SetActive(true);

            if (currentAnchor != anchor || visualRoot.parent != null)
            {
                currentAnchor = anchor;
                visualRoot.SetParent(null, true);
            }

            visualRoot.position = anchor.position + ResolveWristOffset(anchor.position);
            visualRoot.rotation = Quaternion.Euler(wristLocalEuler);
            visualRoot.localScale = Vector3.one;
        }

        private Vector3 ResolveWristOffset(Vector3 anchorPosition)
        {
            var offset = wristLocalOffset;
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerCamera == null)
                return offset;

            var towardPlayer = playerCamera.transform.position - anchorPosition;
            if (towardPlayer.sqrMagnitude > 0.0001f)
                offset += towardPlayer.normalized * Mathf.Max(0f, wristTowardPlayerOffset);

            return offset;
        }

        private void ParkVisualRoot()
        {
            if (visualRoot == null)
                return;

            visualRoot.SetParent(null, true);
            visualRoot.SetPositionAndRotation(ResolveParkingPosition(), Quaternion.identity);
            visualRoot.localScale = Vector3.one;
        }

        private Vector3 ResolveParkingPosition()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerCamera != null)
                return playerCamera.transform.position + playerCamera.transform.forward * 0.55f + Vector3.down * 0.12f;

            var spawnPoint = GameObject.Find("PlayerSpawnPoint");
            if (spawnPoint != null)
                return spawnPoint.transform.position + Vector3.up * 1.1f;

            return Vector3.up * 1.6f;
        }

        private Transform ResolveAnchorTransform()
        {
            if (rightHand != null && rightHand.gameObject.activeInHierarchy && rightHand.enabled && rightHand.IsTracked)
            {
                var wrist = ResolveWristBone(rightHand);
                if (wrist != null)
                    return wrist;

                return rightHand.transform;
            }

            return null;
        }

        private static Transform ResolveWristBone(OVRHand hand)
        {
            if (hand == null)
                return null;

            foreach (var skeleton in hand.GetComponentsInChildren<OVRSkeleton>(true))
            {
                var wrist = FindWristBoneTransform(skeleton);
                if (wrist != null)
                    return wrist;
            }

            var expectedHand = hand.GetHand();
            foreach (var skeleton in FindObjectsByType<OVRSkeleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (skeleton == null ||
                    skeleton.GetComponentInParent<OVRHand>()?.GetHand() != expectedHand)
                {
                    continue;
                }

                var wrist = FindWristBoneTransform(skeleton);
                if (wrist != null)
                    return wrist;
            }

            return null;
        }

        private static Transform FindWristBoneTransform(OVRSkeleton skeleton)
        {
            if (skeleton == null || skeleton.Bones == null)
                return null;

            foreach (var bone in skeleton.Bones)
            {
                if (bone == null || bone.Transform == null)
                    continue;

                if (bone.Id == OVRSkeleton.BoneId.XRHand_Wrist ||
                    bone.Id == OVRSkeleton.BoneId.Hand_WristRoot)
                {
                    return bone.Transform;
                }
            }

            return null;
        }

        private void RefreshMana()
        {
            if (segmentRenderers[0] == null)
                return;

            var manaPerSegment = maxMana / SegmentCount;
            var disrupted = combatManager != null && combatManager.IsManaDisrupted;
            var clampedMana = Mathf.Clamp(currentMana, 0f, maxMana);

            for (var i = 0; i < SegmentCount; i++)
            {
                var visualDrainIndex = SegmentCount - 1 - i;
                var segmentStart = visualDrainIndex * manaPerSegment;
                var fill = Mathf.Clamp01((clampedMana - segmentStart) / manaPerSegment);
                var transform = segmentRenderers[i].transform;
                var baseX = (i - 1.5f) * segmentSpacing;
                var filledWidth = segmentScale.x * fill;

                segmentRenderers[i].enabled = fill > 0.01f;
                transform.localScale = new Vector3(Mathf.Max(0.001f, filledWidth), segmentScale.y, segmentScale.z);
                transform.localPosition = new Vector3(baseX + (segmentScale.x - filledWidth) * 0.5f, 0f, 0f);

                var color = fill >= 0.98f
                    ? filledColor
                    : fill > 0.05f ? Color.Lerp(emptyColor, partialColor, fill) : emptyColor;
                if (disrupted && fill > 0.05f)
                    color = Color.Lerp(color, disruptedColor, 0.55f);

                ApplyColor(segmentMaterials[i], color);
            }

        }

        private static OVRHand FindRightHand()
        {
            OVRHand best = null;
            var bestScore = int.MinValue;
            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hand == null || hand.GetHand() != OVRPlugin.Hand.HandRight)
                    continue;

                var score = 0;
                if (hand.gameObject.activeInHierarchy)
                    score += 20;
                if (hand.enabled)
                    score += 20;
                if (hand.IsTracked)
                    score += 40;
                if (hand.IsPointerPoseValid)
                    score += 10;

                if (score <= bestScore)
                    continue;

                best = hand;
                bestScore = score;
            }

            return best;
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = "ArcaneManaWristMaterial",
                hideFlags = HideFlags.DontSave,
                renderQueue = 3000
            };

            ApplyColor(material, color);
            return material;
        }

        private static void ApplyColor(Material material, Color color)
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
