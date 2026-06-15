using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CodexGenerated.GrimoirePages
{
    public class GrimoirePageTurner : MonoBehaviour
    {
        [SerializeField] private Renderer leftPageRenderer;
        [SerializeField] private Renderer leftPageBackRenderer;
        [SerializeField] private Renderer rightPageRenderer;
        [SerializeField] private Renderer rightPageBackRenderer;
        [SerializeField] private Transform turningPagePivot;
        [SerializeField] private Renderer turningPageRenderer;
        [SerializeField] private Renderer turningPageBackRenderer;
        [SerializeField] private Renderer[] turningPageRenderers;
        [SerializeField] private Transform[] turningPageStrips;
        [SerializeField] private Material[] pageMaterials;
        [SerializeField] private float turnDuration = 1.8f;
        [SerializeField] private bool useCurledStrips = false;
        [SerializeField] private float curlAngle = 34f;
        [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Page Stack Visuals")]
        [SerializeField] private Transform leftPageStack;
        [SerializeField] private Transform rightPageStack;
        [SerializeField] private float maxPageStackWidth = 0.018f;
        [SerializeField] private float manualRevealAngleThreshold = 8f;

        private int spreadIndex;
        private bool isTurning;
        private Quaternion pivotBaseRotation;
        private Vector3[] stripBasePositions;
        private Quaternion[] stripBaseRotations;
        private readonly Dictionary<Material, Material> runtimePageMaterialCache = new();

        private float currentTurnAngle;
        private int manualTargetSpread;
        private bool manualTurnForward;
        private bool manualUnderlyingPageRevealed;

        private struct PageFaceMaterials
        {
            public Material front;
            public Material back;
        }

#if UNITY_EDITOR
        private double editorTurnStartTime;
        private int editorTargetSpread;
        private bool editorTurnForward;
#endif

        public bool IsTurning => isTurning;

        public bool CanManualTurn => !isTurning && pageMaterials != null && pageMaterials.Length > 2;

        public bool BeginManualTurn(bool forward)
        {
            if (!CanManualTurn)
            {
                Debug.LogWarning($"[PageTurn] BeginManualTurn blocked | isTurning={isTurning} | pageMaterials={(pageMaterials == null ? "null" : pageMaterials.Length.ToString())}");
                return false;
            }

            int targetSpread = forward
                ? Mathf.Min(spreadIndex + 1, GetLastSpreadIndex())
                : Mathf.Max(spreadIndex - 1, 0);

            if (targetSpread == spreadIndex)
            {
                Debug.Log($"[PageTurn] BeginManualTurn blocked: no more pages | forward={forward} | spreadIndex={spreadIndex} | last={GetLastSpreadIndex()}");
                return false;
            }

            EnsureCachedTransforms();
            EnsureBackRenderers();

            manualTargetSpread = targetSpread;
            manualTurnForward = forward;
            currentTurnAngle = forward ? 0f : 180f;
            manualUnderlyingPageRevealed = false;

            SetTurningAngle(currentTurnAngle);
            PageFaceMaterials turningMaterials = PrepareTurningPage(targetSpread, forward);
            SetTurningPageVisible(true, turningMaterials);
            UpdateManualUnderlyingPageVisibility();

            isTurning = true;
            return true;
        }

        public void SetManualTurnAngle(float angle)
        {
            if (!isTurning)
                return;

            currentTurnAngle = Mathf.Clamp(angle, 0f, 180f);
            SetTurningAngle(currentTurnAngle);
            UpdateManualUnderlyingPageVisibility();
        }

        public void CommitManualTurn()
        {
            if (!isTurning)
            {
                Debug.LogWarning("[PageTurn] CommitManualTurn called while not turning.");
                return;
            }

            bool commit = manualTurnForward
                ? currentTurnAngle >= 90f
                : currentTurnAngle <= 90f;

            float targetAngle = commit
                ? (manualTurnForward ? 180f : 0f)
                : (manualTurnForward ? 0f : 180f);

            int finalSpread = commit ? manualTargetSpread : spreadIndex;

            if (!gameObject.activeInHierarchy)
            {
                SetTurningAngle(targetAngle);
                spreadIndex = finalSpread;
                ApplySpread(spreadIndex);
                HideTurningPage();
                isTurning = false;
                manualUnderlyingPageRevealed = false;
                return;
            }

            StartCoroutine(SnapAndFinishTurn(targetAngle, finalSpread));
        }

        private void OnDisable()
        {
            if (isTurning)
            {
                isTurning = false;
                HideTurningPage();
            }
        }

        private void OnDestroy()
        {
            foreach (Material cachedMaterial in runtimePageMaterialCache.Values)
            {
                if (cachedMaterial == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(cachedMaterial);
                else
                    DestroyImmediate(cachedMaterial);
            }

            runtimePageMaterialCache.Clear();
        }

        private IEnumerator SnapAndFinishTurn(float targetAngle, int finalSpread)
        {
            float startAngle = currentTurnAngle;
            const float snapDuration = 0.18f;
            float elapsed = 0f;

            while (elapsed < snapDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / snapDuration));
                SetTurningAngle(Mathf.Lerp(startAngle, targetAngle, t));
                yield return null;
            }

            SetTurningAngle(targetAngle);
            spreadIndex = finalSpread;
            ApplySpread(spreadIndex);
            HideTurningPage();
            isTurning = false;
            manualUnderlyingPageRevealed = false;
        }

        [ContextMenu("Next Page")]
        public void NextPage()
        {
            if (isTurning || pageMaterials == null || pageMaterials.Length <= 2)
                return;

            int nextSpread = Mathf.Min(spreadIndex + 1, GetLastSpreadIndex());
            if (nextSpread == spreadIndex)
                return;

            BeginTurn(nextSpread, true);
        }

        [ContextMenu("Previous Page")]
        public void PreviousPage()
        {
            if (isTurning || pageMaterials == null || pageMaterials.Length <= 2)
                return;

            int nextSpread = Mathf.Max(spreadIndex - 1, 0);
            if (nextSpread == spreadIndex)
                return;

            BeginTurn(nextSpread, false);
        }

        public void SetSpread(int index)
        {
            EnsureBackRenderers();
            spreadIndex = Mathf.Clamp(index, 0, GetLastSpreadIndex());
            ApplySpread(spreadIndex);
            HideTurningPage();
        }

        [ContextMenu("Preview Mid Turn")]
        public void PreviewMidTurn()
        {
            EnsureCachedTransforms();
            EnsureBackRenderers();
            SetTurningPageVisible(true, GetRightPageFaceMaterials(spreadIndex));
            SetTurningAngle(65f);
        }

        [ContextMenu("Hide Turn Preview")]
        public void HideTurnPreview()
        {
            EnsureCachedTransforms();
            EnsureBackRenderers();
            HideTurningPage();
        }

        private void Awake()
        {
            EnsureCachedTransforms();
            EnsureBackRenderers();
            EnsurePageStacks();
            ApplySpread(spreadIndex);
            HideTurningPage();
        }

        private void BeginTurn(int targetSpread, bool forward)
        {
            EnsureCachedTransforms();
            EnsureBackRenderers();

            if (Application.isPlaying)
            {
                StartCoroutine(TurnToSpread(targetSpread, forward));
                return;
            }

#if UNITY_EDITOR
            StartEditorTurn(targetSpread, forward);
#else
            spreadIndex = targetSpread;
            ApplySpread(spreadIndex);
            HideTurningPage();
#endif
        }

        private IEnumerator TurnToSpread(int targetSpread, bool forward)
        {
            isTurning = true;

            float from = forward ? 0f : 180f;
            float to = forward ? 180f : 0f;

            SetTurningAngle(from);
            PageFaceMaterials turningMaterials = PrepareTurningPage(targetSpread, forward);
            SetTurningPageVisible(true, turningMaterials);

            float elapsed = 0f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = turnDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / turnDuration);
                float curved = turnCurve != null ? turnCurve.Evaluate(t) : t;
                SetTurningAngle(EvaluateBookTurnAngle(from, to, curved));
                yield return null;
            }

            SetTurningAngle(to);
            spreadIndex = targetSpread;
            ApplySpread(spreadIndex);
            HideTurningPage();
            isTurning = false;
            manualUnderlyingPageRevealed = false;
        }

        private PageFaceMaterials PrepareTurningPage(int targetSpread, bool forward)
        {
            PageFaceMaterials turningMaterials = forward
                ? GetRightPageFaceMaterials(spreadIndex)
                : GetLeftPageFaceMaterials(spreadIndex);

            if (forward)
            {
                ApplyFaceMaterials(rightPageRenderer, rightPageBackRenderer, GetRightPageFaceMaterials(targetSpread));
            }
            else
            {
                ApplyFaceMaterials(leftPageRenderer, leftPageBackRenderer, GetLeftPageFaceMaterials(targetSpread));
            }

            return turningMaterials;
        }

#if UNITY_EDITOR
        private void StartEditorTurn(int targetSpread, bool forward)
        {
            if (isTurning)
                return;

            isTurning = true;
            editorTargetSpread = targetSpread;
            editorTurnForward = forward;
            editorTurnStartTime = EditorApplication.timeSinceStartup;
            SetTurningPageVisible(true, PrepareTurningPage(targetSpread, forward));
            EditorApplication.update -= UpdateEditorTurn;
            EditorApplication.update += UpdateEditorTurn;
        }

        private void UpdateEditorTurn()
        {
            if (!isTurning)
            {
                EditorApplication.update -= UpdateEditorTurn;
                return;
            }

            float t = turnDuration <= 0f
                ? 1f
                : Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - editorTurnStartTime) / turnDuration));
            float curved = turnCurve != null ? turnCurve.Evaluate(t) : t;
            float from = editorTurnForward ? 0f : 180f;
            float to = editorTurnForward ? 180f : 0f;
            SetTurningAngle(EvaluateBookTurnAngle(from, to, curved));

            if (t < 1f)
            {
                SceneView.RepaintAll();
                return;
            }

            SetTurningAngle(to);
            spreadIndex = editorTargetSpread;
            ApplySpread(spreadIndex);
            HideTurningPage();
            isTurning = false;
            EditorApplication.update -= UpdateEditorTurn;
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
        }
#endif

        private static float EvaluateBookTurnAngle(float from, float to, float t)
        {
            if (t < 0.35f)
                return Mathf.Lerp(from, 70f, t / 0.35f);

            if (t < 0.48f)
                return Mathf.Lerp(70f, 110f, (t - 0.35f) / 0.13f);

            return Mathf.Lerp(110f, to, (t - 0.48f) / 0.52f);
        }

        private void ApplySpread(int index)
        {
            ApplyFaceMaterials(leftPageRenderer, leftPageBackRenderer, GetLeftPageFaceMaterials(index));
            ApplyFaceMaterials(rightPageRenderer, rightPageBackRenderer, GetRightPageFaceMaterials(index));
            ApplyFaceMaterials(turningPageRenderer, turningPageBackRenderer, GetRightPageFaceMaterials(index));
            UpdatePageStacks(index);
        }

        private void EnsurePageStacks()
        {
            if (leftPageStack == null && leftPageRenderer != null)
                leftPageStack = CreateStackObject("LeftPageStack", leftPageRenderer, true);

            if (rightPageStack == null && rightPageRenderer != null)
                rightPageStack = CreateStackObject("RightPageStack", rightPageRenderer, false);
        }

        private Transform CreateStackObject(string objectName, Renderer pageRenderer, bool isLeft)
        {
            GameObject stackObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stackObject.name = objectName;
            stackObject.transform.SetParent(transform, false);

            Collider collider = stackObject.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Bounds worldBounds = pageRenderer.bounds;
            Vector3[] worldCorners =
            {
                new Vector3(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z),
                new Vector3(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z),
                new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z),
                new Vector3(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z),
                new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z),
                new Vector3(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z),
                new Vector3(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z),
                new Vector3(worldBounds.max.x, worldBounds.max.y, worldBounds.max.z)
            };

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (Vector3 worldCorner in worldCorners)
            {
                Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                minX = Mathf.Min(minX, localCorner.x);
                maxX = Mathf.Max(maxX, localCorner.x);
                minY = Mathf.Min(minY, localCorner.y);
                maxY = Mathf.Max(maxY, localCorner.y);
                minZ = Mathf.Min(minZ, localCorner.z);
                maxZ = Mathf.Max(maxZ, localCorner.z);
            }

            float height = maxY - minY;
            float depth = Mathf.Max(maxZ - minZ, 0.004f);
            float edgeX = isLeft ? minX : maxX;

            stackObject.transform.localRotation = Quaternion.identity;
            stackObject.transform.localPosition = new Vector3(edgeX, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            stackObject.transform.localScale = new Vector3(0f, height * 0.92f, depth);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            if (shader != null)
            {
                Material stackMaterial = new Material(shader)
                {
                    color = new Color(0.88f, 0.82f, 0.68f)
                };

                if (stackMaterial.HasProperty("_BaseColor"))
                    stackMaterial.SetColor("_BaseColor", stackMaterial.color);

                stackObject.GetComponent<Renderer>().sharedMaterial = stackMaterial;
            }

            return stackObject.transform;
        }

        private void UpdatePageStacks(int index)
        {
            int last = GetLastSpreadIndex();
            if (last <= 0)
                return;

            float readFraction = (float)index / last;
            float unreadFraction = 1f - readFraction;

            SetStackWidth(rightPageStack, unreadFraction * maxPageStackWidth);
            SetStackWidth(leftPageStack, readFraction * maxPageStackWidth);
        }

        private static void SetStackWidth(Transform stack, float width)
        {
            if (stack == null)
                return;

            Vector3 scale = stack.localScale;
            scale.x = Mathf.Max(width, 0.0005f);
            stack.localScale = scale;
        }

        private Material GetMaterialForSpread(int index, int side)
        {
            if (pageMaterials == null || pageMaterials.Length == 0)
                return null;

            int materialIndex = Mathf.Clamp(index * 2 + side, 0, pageMaterials.Length - 1);
            return pageMaterials[materialIndex];
        }

        private Material GetMaterialForPageIndex(int pageIndex)
        {
            if (pageMaterials == null || pageMaterials.Length == 0)
                return null;

            int clampedIndex = Mathf.Clamp(pageIndex, 0, pageMaterials.Length - 1);
            return pageMaterials[clampedIndex];
        }

        private PageFaceMaterials GetLeftPageFaceMaterials(int spread)
        {
            int frontIndex = spread * 2;
            return new PageFaceMaterials
            {
                front = GetMaterialForPageIndex(frontIndex),
                back = GetMaterialForPageIndex(frontIndex - 1)
            };
        }

        private PageFaceMaterials GetRightPageFaceMaterials(int spread)
        {
            int frontIndex = spread * 2 + 1;
            return new PageFaceMaterials
            {
                front = GetMaterialForPageIndex(frontIndex),
                back = GetMaterialForPageIndex(frontIndex + 1)
            };
        }

        private int GetLastSpreadIndex()
        {
            if (pageMaterials == null || pageMaterials.Length == 0)
                return 0;

            return Mathf.Max(0, (pageMaterials.Length - 1) / 2);
        }

        private void SetTurningAngle(float angle)
        {
            if (turningPagePivot == null)
                return;

            if (useCurledStrips && turningPageStrips != null && turningPageStrips.Length > 0)
            {
                turningPagePivot.localRotation = pivotBaseRotation;
                UpdateCurlStrips(angle);
            }
            else
            {
                turningPagePivot.localRotation = pivotBaseRotation * Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }

        private void HideTurningPage()
        {
            SetTurningAngle(0f);
            manualUnderlyingPageRevealed = false;

            PageFaceMaterials restingMaterials = GetRightPageFaceMaterials(spreadIndex);
            ApplyFaceMaterials(turningPageRenderer, turningPageBackRenderer, restingMaterials);

            if (turningPageRenderer != null)
                turningPageRenderer.gameObject.SetActive(true);
            if (turningPageBackRenderer != null)
                turningPageBackRenderer.gameObject.SetActive(true);

            if (useCurledStrips && turningPageRenderers != null)
            {
                foreach (Renderer renderer in turningPageRenderers)
                {
                    if (renderer == null)
                        continue;

                    SetRendererMaterial(renderer, restingMaterials.front);
                    renderer.gameObject.SetActive(true);
                }
            }

            if (turningPagePivot != null)
                turningPagePivot.gameObject.SetActive(true);

            if (rightPageRenderer != null)
                rightPageRenderer.gameObject.SetActive(false);
            if (rightPageBackRenderer != null)
                rightPageBackRenderer.gameObject.SetActive(false);
        }

        private void SetTurningPageVisible(bool turning, PageFaceMaterials materials)
        {
            ApplyFaceMaterials(turningPageRenderer, turningPageBackRenderer, materials);

            if (turningPageRenderer != null)
                turningPageRenderer.gameObject.SetActive(true);
            if (turningPageBackRenderer != null)
                turningPageBackRenderer.gameObject.SetActive(true);

            if (useCurledStrips && turningPageRenderers != null)
            {
                foreach (Renderer renderer in turningPageRenderers)
                {
                    if (renderer == null)
                        continue;

                    SetRendererMaterial(renderer, materials.front);
                    renderer.gameObject.SetActive(true);
                }
            }

            if (turningPagePivot != null)
                turningPagePivot.gameObject.SetActive(true);

            if (rightPageRenderer != null)
                rightPageRenderer.gameObject.SetActive(turning);
            if (rightPageBackRenderer != null)
                rightPageBackRenderer.gameObject.SetActive(turning);
        }

        private void UpdateManualUnderlyingPageVisibility()
        {
            if (!isTurning)
                return;

            bool shouldReveal = manualTurnForward
                ? currentTurnAngle >= manualRevealAngleThreshold
                : currentTurnAngle <= 180f - manualRevealAngleThreshold;

            if (shouldReveal == manualUnderlyingPageRevealed)
                return;

            manualUnderlyingPageRevealed = shouldReveal;

            if (manualTurnForward)
            {
                if (rightPageRenderer != null)
                    rightPageRenderer.gameObject.SetActive(shouldReveal);
                if (rightPageBackRenderer != null)
                    rightPageBackRenderer.gameObject.SetActive(shouldReveal);
            }
            else
            {
                if (leftPageRenderer != null)
                    leftPageRenderer.gameObject.SetActive(shouldReveal);
                if (leftPageBackRenderer != null)
                    leftPageBackRenderer.gameObject.SetActive(shouldReveal);
            }
        }

        private void EnsureCachedTransforms()
        {
            if (turningPagePivot != null)
                pivotBaseRotation = turningPagePivot.localRotation;

            CacheStripTransforms();
        }

        private void EnsureBackRenderers()
        {
            leftPageBackRenderer = EnsureBackRenderer(leftPageRenderer, leftPageBackRenderer, "BackFace");
            rightPageBackRenderer = EnsureBackRenderer(rightPageRenderer, rightPageBackRenderer, "BackFace");
            turningPageBackRenderer = EnsureBackRenderer(turningPageRenderer, turningPageBackRenderer, "BackFace");
        }

        private Renderer EnsureBackRenderer(Renderer frontRenderer, Renderer backRenderer, string childName)
        {
            if (frontRenderer == null)
                return null;

            if (backRenderer != null)
                return backRenderer;

            Transform existing = frontRenderer.transform.Find(childName);
            if (existing != null)
            {
                Renderer existingRenderer = existing.GetComponent<Renderer>();
                if (existingRenderer != null)
                    return existingRenderer;
            }

            GameObject backFace = new GameObject(childName);
            backFace.layer = frontRenderer.gameObject.layer;
            backFace.transform.SetParent(frontRenderer.transform, false);
            backFace.transform.localPosition = new Vector3(0f, 0f, -0.0002f);
            backFace.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            backFace.transform.localScale = Vector3.one;

            MeshFilter sourceMeshFilter = frontRenderer.GetComponent<MeshFilter>();
            if (sourceMeshFilter != null)
            {
                MeshFilter backMeshFilter = backFace.AddComponent<MeshFilter>();
                backMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
            }

            MeshRenderer backMeshRenderer = backFace.AddComponent<MeshRenderer>();
            if (frontRenderer is MeshRenderer frontMeshRenderer)
            {
                backMeshRenderer.shadowCastingMode = frontMeshRenderer.shadowCastingMode;
                backMeshRenderer.receiveShadows = frontMeshRenderer.receiveShadows;
                backMeshRenderer.lightProbeUsage = frontMeshRenderer.lightProbeUsage;
                backMeshRenderer.reflectionProbeUsage = frontMeshRenderer.reflectionProbeUsage;
                backMeshRenderer.renderingLayerMask = frontMeshRenderer.renderingLayerMask;
                backMeshRenderer.rendererPriority = frontMeshRenderer.rendererPriority;
            }

            return backMeshRenderer;
        }

        private void ApplyFaceMaterials(Renderer frontRenderer, Renderer backRenderer, PageFaceMaterials materials)
        {
            SetRendererMaterial(frontRenderer, materials.front);
            SetRendererMaterial(backRenderer, materials.back);
        }

        private void SetRendererMaterial(Renderer renderer, Material sourceMaterial)
        {
            if (renderer == null)
                return;

            renderer.sharedMaterial = GetRenderablePageMaterial(sourceMaterial);
        }

        private Material GetRenderablePageMaterial(Material sourceMaterial)
        {
            if (sourceMaterial == null)
                return null;

            if (runtimePageMaterialCache.TryGetValue(sourceMaterial, out Material cached) && cached != null)
                return cached;

            Material materialInstance = new Material(sourceMaterial)
            {
                name = sourceMaterial.name + " (PageFace)"
            };

            if (materialInstance.HasProperty("_Cull"))
                materialInstance.SetFloat("_Cull", 2f);

            runtimePageMaterialCache[sourceMaterial] = materialInstance;
            return materialInstance;
        }

        private void CacheStripTransforms()
        {
            if (turningPageStrips == null || turningPageStrips.Length == 0)
            {
                stripBasePositions = null;
                stripBaseRotations = null;
                return;
            }

            stripBasePositions = new Vector3[turningPageStrips.Length];
            stripBaseRotations = new Quaternion[turningPageStrips.Length];
            for (int i = 0; i < turningPageStrips.Length; i++)
            {
                Transform strip = turningPageStrips[i];
                if (strip == null)
                    continue;

                stripBasePositions[i] = strip.localPosition;
                stripBaseRotations[i] = strip.localRotation;
            }
        }

        private void UpdateCurlStrips(float angle)
        {
            if (turningPageStrips == null || stripBasePositions == null || turningPageStrips.Length == 0)
                return;

            float midCurl = Mathf.Sin(Mathf.Clamp01(angle / 180f) * Mathf.PI) * curlAngle;
            int last = Mathf.Max(1, turningPageStrips.Length - 1);

            for (int i = 0; i < turningPageStrips.Length; i++)
            {
                Transform strip = turningPageStrips[i];
                if (strip == null)
                    continue;

                float normalized = i / (float)last;
                float stripAngle = angle + (normalized - 0.5f) * midCurl;
                Quaternion rotation = Quaternion.Euler(0f, stripAngle, 0f);
                strip.localPosition = rotation * stripBasePositions[i];
                strip.localRotation = rotation * stripBaseRotations[i];
            }
        }
    }
}
