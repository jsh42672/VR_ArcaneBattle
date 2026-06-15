using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        private const string BackFaceName = "BackFace";
        private const string RuntimeSheetsRootName = "RuntimeSheets";

        private int spreadIndex;
        private bool isTurning;
        private int activeTurningSheetIndex = -1;
        private Quaternion pivotBaseRotation;
        private Vector3[] stripBasePositions;
        private Quaternion[] stripBaseRotations;
        private readonly Dictionary<Material, Material> runtimePageMaterialCache = new();
        private readonly List<RuntimeSheet> runtimeSheets = new();

        private float currentTurnAngle;
        private int manualTargetSpread;
        private bool manualTurnForward;
        private bool manualUnderlyingPageRevealed;
        private bool runtimeInitialized;

        private Vector3 leftPageLocalPosition;
        private Quaternion leftPageLocalRotation;
        private Vector3 leftPageLocalScale;
        private Vector3 rightPageLocalPosition;
        private Quaternion rightPageLocalRotation;
        private Vector3 rightPageLocalScale;

        private Transform runtimeSheetsRoot;

        private struct PageFaceMaterials
        {
            public Material front;
            public Material back;
        }

        private sealed class RuntimeSheet
        {
            public Transform transform;
            public Renderer frontRenderer;
            public Renderer backRenderer;
            public int frontPageIndex;
            public int backPageIndex;
        }

        public bool IsTurning => isTurning;

        public bool CanManualTurn => !isTurning && pageMaterials != null && pageMaterials.Length > 1;

        private void Awake()
        {
            EnsureInitialized();
            ApplySpread(Mathf.Clamp(spreadIndex, 0, GetLastSpreadIndex()));
        }

        private void OnDisable()
        {
            if (!runtimeInitialized)
                return;

            if (isTurning)
            {
                isTurning = false;
                activeTurningSheetIndex = -1;
                manualUnderlyingPageRevealed = false;
                ApplySpread(spreadIndex);
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

        public bool BeginManualTurn(bool forward)
        {
            if (!CanManualTurn)
                return false;

            EnsureInitialized();

            int targetSpread = forward
                ? Mathf.Min(spreadIndex + 1, GetLastSpreadIndex())
                : Mathf.Max(spreadIndex - 1, 0);

            if (targetSpread == spreadIndex)
                return false;

            if (!PrepareTurn(targetSpread, forward))
                return false;

            isTurning = true;
            currentTurnAngle = forward ? 0f : 180f;
            SetTurningAngle(currentTurnAngle);
            UpdateManualUnderlyingPageVisibility();
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
                return;

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
                isTurning = false;
                activeTurningSheetIndex = -1;
                manualUnderlyingPageRevealed = false;
                ApplySpread(spreadIndex);
                return;
            }

            StartCoroutine(SnapAndFinishTurn(targetAngle, finalSpread));
        }

        [ContextMenu("Next Page")]
        public void NextPage()
        {
            if (isTurning || pageMaterials == null || pageMaterials.Length <= 1)
                return;

            int targetSpread = Mathf.Min(spreadIndex + 1, GetLastSpreadIndex());
            if (targetSpread == spreadIndex)
                return;

            BeginTurn(targetSpread, true);
        }

        [ContextMenu("Previous Page")]
        public void PreviousPage()
        {
            if (isTurning || pageMaterials == null || pageMaterials.Length <= 1)
                return;

            int targetSpread = Mathf.Max(spreadIndex - 1, 0);
            if (targetSpread == spreadIndex)
                return;

            BeginTurn(targetSpread, false);
        }

        public void SetSpread(int index)
        {
            EnsureInitialized();
            spreadIndex = Mathf.Clamp(index, 0, GetLastSpreadIndex());
            isTurning = false;
            activeTurningSheetIndex = -1;
            manualUnderlyingPageRevealed = false;
            ApplySpread(spreadIndex);
        }

        [ContextMenu("Preview Mid Turn")]
        public void PreviewMidTurn()
        {
            EnsureInitialized();

            int previewSpread = Mathf.Min(spreadIndex + 1, GetLastSpreadIndex());
            if (previewSpread == spreadIndex)
                return;

            if (!PrepareTurn(previewSpread, true))
                return;

            isTurning = true;
            currentTurnAngle = 65f;
            SetTurningAngle(currentTurnAngle);
            UpdateManualUnderlyingPageVisibility();
        }

        [ContextMenu("Hide Turn Preview")]
        public void HideTurnPreview()
        {
            if (!runtimeInitialized)
                return;

            isTurning = false;
            activeTurningSheetIndex = -1;
            manualUnderlyingPageRevealed = false;
            ApplySpread(spreadIndex);
        }

        private void EnsureInitialized()
        {
            if (runtimeInitialized)
                return;

            EnsureCachedTransforms();
            EnsureBackRenderers();
            CacheRestingAnchors();
            EnsureRuntimeSheetsRoot();
            EnsurePageStacks();
            BuildRuntimeSheets();
            runtimeInitialized = true;
        }

        private void BeginTurn(int targetSpread, bool forward)
        {
            EnsureInitialized();

            if (!PrepareTurn(targetSpread, forward))
                return;

            if (!Application.isPlaying)
            {
                spreadIndex = targetSpread;
                isTurning = false;
                activeTurningSheetIndex = -1;
                manualUnderlyingPageRevealed = false;
                ApplySpread(spreadIndex);
                return;
            }

            StartCoroutine(TurnToSpread(targetSpread, forward));
        }

        private bool PrepareTurn(int targetSpread, bool forward)
        {
            int turningSheetIndex = forward ? spreadIndex : spreadIndex - 1;
            if (turningSheetIndex < 0 || turningSheetIndex >= runtimeSheets.Count)
                return false;

            ApplySpread(spreadIndex);

            RuntimeSheet turningSheet = runtimeSheets[turningSheetIndex];
            activeTurningSheetIndex = turningSheetIndex;
            manualTargetSpread = targetSpread;
            manualTurnForward = forward;
            manualUnderlyingPageRevealed = false;

            AttachSheetToPivot(turningSheet, forward ? rightPageLocalPosition : leftPageLocalPosition,
                forward ? rightPageLocalRotation : leftPageLocalRotation,
                forward ? rightPageLocalScale : leftPageLocalScale);

            if (forward)
            {
                SetSheetActive(turningSheetIndex, true);
                SetForwardUnderlyingVisible(false);
            }
            else
            {
                SetBackwardUnderlyingVisible(false);
            }

            return true;
        }

        private IEnumerator TurnToSpread(int targetSpread, bool forward)
        {
            isTurning = true;

            float from = forward ? 0f : 180f;
            float to = forward ? 180f : 0f;
            currentTurnAngle = from;
            SetTurningAngle(currentTurnAngle);
            UpdateManualUnderlyingPageVisibility();

            float elapsed = 0f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = turnDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / turnDuration);
                float curved = turnCurve != null ? turnCurve.Evaluate(t) : t;
                currentTurnAngle = EvaluateBookTurnAngle(from, to, curved);
                SetTurningAngle(currentTurnAngle);
                UpdateManualUnderlyingPageVisibility();
                yield return null;
            }

            SetTurningAngle(to);
            spreadIndex = targetSpread;
            isTurning = false;
            activeTurningSheetIndex = -1;
            manualUnderlyingPageRevealed = false;
            ApplySpread(spreadIndex);
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
                currentTurnAngle = Mathf.Lerp(startAngle, targetAngle, t);
                SetTurningAngle(currentTurnAngle);
                UpdateManualUnderlyingPageVisibility();
                yield return null;
            }

            SetTurningAngle(targetAngle);
            spreadIndex = finalSpread;
            isTurning = false;
            activeTurningSheetIndex = -1;
            manualUnderlyingPageRevealed = false;
            ApplySpread(spreadIndex);
        }

        private void ApplySpread(int index)
        {
            spreadIndex = Mathf.Clamp(index, 0, GetLastSpreadIndex());

            if (leftPageRenderer != null)
                ApplyFaceMaterials(leftPageRenderer, leftPageBackRenderer, GetLeftBaseFaceMaterials());

            SetLeftBaseVisible(spreadIndex == 0);
            HideTurningTemplate();

            for (int i = 0; i < runtimeSheets.Count; i++)
            {
                if (i == spreadIndex - 1)
                {
                    PositionSheetOnLeft(runtimeSheets[i]);
                    SetSheetActive(i, true);
                }
                else if (i == spreadIndex)
                {
                    PositionSheetOnRight(runtimeSheets[i]);
                    SetSheetActive(i, true);
                }
                else
                {
                    SetSheetActive(i, false);
                }
            }

            UpdatePageStacks(spreadIndex);
        }

        private void BuildRuntimeSheets()
        {
            runtimeSheets.Clear();

            if (rightPageRenderer == null)
                return;

            int sheetCount = GetTurnableSheetCount();
            if (sheetCount <= 0)
            {
                rightPageRenderer.gameObject.SetActive(false);
                if (rightPageBackRenderer != null)
                    rightPageBackRenderer.gameObject.SetActive(false);
                return;
            }

            RuntimeSheet firstSheet = CreateRuntimeSheetFromExisting(rightPageRenderer, rightPageBackRenderer, 0);
            runtimeSheets.Add(firstSheet);

            for (int i = 1; i < sheetCount; i++)
            {
                RuntimeSheet clone = CreateRuntimeSheetClone(firstSheet, i);
                runtimeSheets.Add(clone);
            }

            for (int i = 0; i < runtimeSheets.Count; i++)
            {
                ApplyFaceMaterials(runtimeSheets[i].frontRenderer, runtimeSheets[i].backRenderer, GetTurnableSheetFaceMaterials(i));
                SetSheetActive(i, false);
            }
        }

        private RuntimeSheet CreateRuntimeSheetFromExisting(Renderer frontRenderer, Renderer backRenderer, int sheetIndex)
        {
            frontRenderer.transform.SetParent(runtimeSheetsRoot, false);
            frontRenderer.transform.localPosition = rightPageLocalPosition;
            frontRenderer.transform.localRotation = rightPageLocalRotation;
            frontRenderer.transform.localScale = rightPageLocalScale;
            frontRenderer.gameObject.name = $"PageSheet_{sheetIndex:00}";

            return new RuntimeSheet
            {
                transform = frontRenderer.transform,
                frontRenderer = frontRenderer,
                backRenderer = backRenderer,
                frontPageIndex = GetTurnableFrontPageIndex(sheetIndex),
                backPageIndex = GetTurnableBackPageIndex(sheetIndex)
            };
        }

        private RuntimeSheet CreateRuntimeSheetClone(RuntimeSheet template, int sheetIndex)
        {
            GameObject clone = Instantiate(template.transform.gameObject, runtimeSheetsRoot, false);
            clone.name = $"PageSheet_{sheetIndex:00}";

            Renderer frontRenderer = clone.GetComponent<Renderer>();
            Renderer backRenderer = null;
            Transform backFace = clone.transform.Find(BackFaceName);
            if (backFace != null)
                backRenderer = backFace.GetComponent<Renderer>();
            if (backRenderer == null)
                backRenderer = EnsureBackRenderer(frontRenderer, null, BackFaceName);

            return new RuntimeSheet
            {
                transform = clone.transform,
                frontRenderer = frontRenderer,
                backRenderer = backRenderer,
                frontPageIndex = GetTurnableFrontPageIndex(sheetIndex),
                backPageIndex = GetTurnableBackPageIndex(sheetIndex)
            };
        }

        private void EnsureRuntimeSheetsRoot()
        {
            runtimeSheetsRoot = transform.Find(RuntimeSheetsRootName);
            if (runtimeSheetsRoot != null)
                return;

            GameObject runtimeRoot = new GameObject(RuntimeSheetsRootName);
            runtimeRoot.transform.SetParent(transform, false);
            runtimeSheetsRoot = runtimeRoot.transform;
        }

        private void CacheRestingAnchors()
        {
            if (leftPageRenderer != null)
            {
                leftPageLocalPosition = leftPageRenderer.transform.localPosition;
                leftPageLocalRotation = leftPageRenderer.transform.localRotation;
                leftPageLocalScale = leftPageRenderer.transform.localScale;
            }

            if (rightPageRenderer != null)
            {
                rightPageLocalPosition = rightPageRenderer.transform.localPosition;
                rightPageLocalRotation = rightPageRenderer.transform.localRotation;
                rightPageLocalScale = rightPageRenderer.transform.localScale;
            }
        }

        private void PositionSheetOnLeft(RuntimeSheet sheet)
        {
            sheet.transform.SetParent(runtimeSheetsRoot, false);
            sheet.transform.localPosition = leftPageLocalPosition;
            sheet.transform.localRotation = leftPageLocalRotation;
            sheet.transform.localScale = leftPageLocalScale;
        }

        private void PositionSheetOnRight(RuntimeSheet sheet)
        {
            sheet.transform.SetParent(runtimeSheetsRoot, false);
            sheet.transform.localPosition = rightPageLocalPosition;
            sheet.transform.localRotation = rightPageLocalRotation;
            sheet.transform.localScale = rightPageLocalScale;
        }

        private void AttachSheetToPivot(RuntimeSheet sheet, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            sheet.transform.SetParent(runtimeSheetsRoot, false);
            sheet.transform.localPosition = localPosition;
            sheet.transform.localRotation = localRotation;
            sheet.transform.localScale = localScale;

            if (turningPagePivot != null)
                turningPagePivot.localRotation = pivotBaseRotation;

            sheet.transform.SetParent(turningPagePivot, true);
        }

        private void HideTurningTemplate()
        {
            if (turningPageRenderer != null)
                turningPageRenderer.gameObject.SetActive(false);
            if (turningPageBackRenderer != null)
                turningPageBackRenderer.gameObject.SetActive(false);
            if (turningPageRenderers != null)
            {
                foreach (Renderer renderer in turningPageRenderers)
                {
                    if (renderer != null)
                        renderer.gameObject.SetActive(false);
                }
            }

            if (turningPagePivot != null)
                turningPagePivot.localRotation = pivotBaseRotation;
        }

        private void SetTurningAngle(float angle)
        {
            if (turningPagePivot == null)
                return;

            if (useCurledStrips && turningPageStrips != null && turningPageStrips.Length > 0)
            {
                turningPagePivot.localRotation = pivotBaseRotation;
                UpdateCurlStrips(angle);
                return;
            }

            turningPagePivot.localRotation = pivotBaseRotation * Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void UpdateManualUnderlyingPageVisibility()
        {
            if (!isTurning && activeTurningSheetIndex < 0)
                return;

            bool shouldReveal = manualTurnForward
                ? currentTurnAngle >= manualRevealAngleThreshold
                : currentTurnAngle <= 180f - manualRevealAngleThreshold;

            if (shouldReveal == manualUnderlyingPageRevealed)
                return;

            manualUnderlyingPageRevealed = shouldReveal;

            if (manualTurnForward)
                SetForwardUnderlyingVisible(shouldReveal);
            else
                SetBackwardUnderlyingVisible(shouldReveal);
        }

        private void SetForwardUnderlyingVisible(bool visible)
        {
            int nextSheetIndex = spreadIndex + 1;
            if (nextSheetIndex >= 0 && nextSheetIndex < runtimeSheets.Count)
            {
                PositionSheetOnRight(runtimeSheets[nextSheetIndex]);
                SetSheetActive(nextSheetIndex, visible);
            }
        }

        private void SetBackwardUnderlyingVisible(bool visible)
        {
            int previousLeftSheetIndex = spreadIndex - 2;
            if (previousLeftSheetIndex >= 0 && previousLeftSheetIndex < runtimeSheets.Count)
            {
                PositionSheetOnLeft(runtimeSheets[previousLeftSheetIndex]);
                SetSheetActive(previousLeftSheetIndex, visible);
                SetLeftBaseVisible(false);
                return;
            }

            SetLeftBaseVisible(visible);
        }

        private void SetLeftBaseVisible(bool visible)
        {
            if (leftPageRenderer != null)
                leftPageRenderer.gameObject.SetActive(visible);
            if (leftPageBackRenderer != null)
                leftPageBackRenderer.gameObject.SetActive(visible);
        }

        private void SetSheetActive(int index, bool active)
        {
            if (index < 0 || index >= runtimeSheets.Count)
                return;

            if (runtimeSheets[index].frontRenderer != null)
                runtimeSheets[index].frontRenderer.gameObject.SetActive(active);
            if (runtimeSheets[index].backRenderer != null)
                runtimeSheets[index].backRenderer.gameObject.SetActive(active);
        }

        private void EnsureCachedTransforms()
        {
            if (turningPagePivot != null)
                pivotBaseRotation = turningPagePivot.localRotation;

            CacheStripTransforms();
        }

        private void EnsureBackRenderers()
        {
            leftPageBackRenderer = EnsureBackRenderer(leftPageRenderer, leftPageBackRenderer, BackFaceName);
            rightPageBackRenderer = EnsureBackRenderer(rightPageRenderer, rightPageBackRenderer, BackFaceName);
            turningPageBackRenderer = EnsureBackRenderer(turningPageRenderer, turningPageBackRenderer, BackFaceName);
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
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }

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
            int last = Mathf.Max(1, GetLastSpreadIndex());
            float readFraction = Mathf.Clamp01(index / (float)last);
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

        private PageFaceMaterials GetLeftBaseFaceMaterials()
        {
            return new PageFaceMaterials
            {
                front = GetMaterialForPageIndex(0),
                back = GetMaterialForPageIndex(0)
            };
        }

        private PageFaceMaterials GetTurnableSheetFaceMaterials(int sheetIndex)
        {
            return new PageFaceMaterials
            {
                front = GetMaterialForPageIndex(GetTurnableFrontPageIndex(sheetIndex)),
                back = GetMaterialForPageIndex(GetTurnableBackPageIndex(sheetIndex))
            };
        }

        private int GetTurnableFrontPageIndex(int sheetIndex)
        {
            return sheetIndex * 2 + 1;
        }

        private int GetTurnableBackPageIndex(int sheetIndex)
        {
            return sheetIndex * 2 + 2;
        }

        private Material GetMaterialForPageIndex(int pageIndex)
        {
            if (pageMaterials == null || pageMaterials.Length == 0)
                return null;

            int clampedIndex = Mathf.Clamp(pageIndex, 0, pageMaterials.Length - 1);
            return pageMaterials[clampedIndex];
        }

        private int GetTurnableSheetCount()
        {
            if (pageMaterials == null || pageMaterials.Length <= 1)
                return 0;

            return GetLastSpreadIndex() + 1;
        }

        private int GetLastSpreadIndex()
        {
            if (pageMaterials == null || pageMaterials.Length <= 1)
                return 0;

            return Mathf.Max(0, (pageMaterials.Length - 1) / 2);
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

        private static float EvaluateBookTurnAngle(float from, float to, float t)
        {
            if (t < 0.35f)
                return Mathf.Lerp(from, 70f, t / 0.35f);

            if (t < 0.48f)
                return Mathf.Lerp(70f, 110f, (t - 0.35f) / 0.13f);

            return Mathf.Lerp(110f, to, (t - 0.48f) / 0.52f);
        }
    }
}
