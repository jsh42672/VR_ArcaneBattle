using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CodexGenerated.GrimoirePages
{
    public class GrimoirePageTurner : MonoBehaviour
    {
        [SerializeField] private Renderer leftPageRenderer;
        [SerializeField] private Renderer rightPageRenderer;
        [SerializeField] private Transform turningPagePivot;
        [SerializeField] private Renderer turningPageRenderer;
        [SerializeField] private Renderer[] turningPageRenderers;
        [SerializeField] private Transform[] turningPageStrips;
        [SerializeField] private Material[] pageMaterials;
        [SerializeField] private float turnDuration = 1.8f;
        [SerializeField] private bool useCurledStrips = false;
        [SerializeField] private float curlAngle = 34f;
        [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private int spreadIndex;
        private bool isTurning;
        private Quaternion pivotBaseRotation;
        private Vector3[] stripBasePositions;
        private Quaternion[] stripBaseRotations;
#if UNITY_EDITOR
        private double editorTurnStartTime;
        private int editorTargetSpread;
        private bool editorTurnForward;
        private Material editorTurningMaterial;
#endif

        public bool IsTurning => isTurning;

        [ContextMenu("Next Page")]
        public void NextPage()
        {
            if (isTurning || pageMaterials == null || pageMaterials.Length <= 2)
            {
                return;
            }

            int nextSpread = Mathf.Min(spreadIndex + 1, GetLastSpreadIndex());
            if (nextSpread == spreadIndex)
            {
                return;
            }

            BeginTurn(nextSpread, true);
        }

        [ContextMenu("Previous Page")]
        public void PreviousPage()
        {
            if (isTurning || pageMaterials == null || pageMaterials.Length <= 2)
            {
                return;
            }

            int nextSpread = Mathf.Max(spreadIndex - 1, 0);
            if (nextSpread == spreadIndex)
            {
                return;
            }

            BeginTurn(nextSpread, false);
        }

        public void SetSpread(int index)
        {
            spreadIndex = Mathf.Clamp(index, 0, GetLastSpreadIndex());
            ApplySpread(spreadIndex);
            HideTurningPage();
        }

        [ContextMenu("Preview Mid Turn")]
        public void PreviewMidTurn()
        {
            EnsureCachedTransforms();
            Material turningMaterial = GetMaterialForSpread(spreadIndex, 1);
            SetTurningPageVisible(true, turningMaterial);
            SetTurningAngle(65f);
        }

        [ContextMenu("Hide Turn Preview")]
        public void HideTurnPreview()
        {
            EnsureCachedTransforms();
            HideTurningPage();
        }

        private void Awake()
        {
            EnsureCachedTransforms();
            ApplySpread(spreadIndex);
            HideTurningPage();
        }

        private void BeginTurn(int targetSpread, bool forward)
        {
            EnsureCachedTransforms();
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

            Material turningMaterial = PrepareTurningPage(targetSpread, forward);
            SetTurningPageVisible(true, turningMaterial);

            float from = forward ? 0f : 180f;
            float to = forward ? 180f : 0f;
            float elapsed = 0f;

            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
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
        }

        private Material PrepareTurningPage(int targetSpread, bool forward)
        {
            Material turningMaterial = GetMaterialForSpread(spreadIndex, forward ? 1 : 0);
            if (forward && rightPageRenderer != null)
            {
                rightPageRenderer.sharedMaterial = GetMaterialForSpread(targetSpread, 1);
            }
            else if (!forward && leftPageRenderer != null)
            {
                leftPageRenderer.sharedMaterial = GetMaterialForSpread(targetSpread, 0);
            }

            return turningMaterial;
        }

#if UNITY_EDITOR
        private void StartEditorTurn(int targetSpread, bool forward)
        {
            if (isTurning)
            {
                return;
            }

            isTurning = true;
            editorTargetSpread = targetSpread;
            editorTurnForward = forward;
            editorTurnStartTime = EditorApplication.timeSinceStartup;
            editorTurningMaterial = PrepareTurningPage(targetSpread, forward);
            SetTurningPageVisible(true, editorTurningMaterial);
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
            {
                return Mathf.Lerp(from, 70f, t / 0.35f);
            }

            if (t < 0.48f)
            {
                return Mathf.Lerp(70f, 110f, (t - 0.35f) / 0.13f);
            }

            return Mathf.Lerp(110f, to, (t - 0.48f) / 0.52f);
        }

        private void ApplySpread(int index)
        {
            if (leftPageRenderer != null)
            {
                leftPageRenderer.sharedMaterial = GetMaterialForSpread(index, 0);
            }

            if (rightPageRenderer != null)
            {
                rightPageRenderer.sharedMaterial = GetMaterialForSpread(index, 1);
            }
        }

        private Material GetMaterialForSpread(int index, int side)
        {
            if (pageMaterials == null || pageMaterials.Length == 0)
            {
                return null;
            }

            int materialIndex = Mathf.Clamp(index * 2 + side, 0, pageMaterials.Length - 1);
            return pageMaterials[materialIndex];
        }

        private int GetLastSpreadIndex()
        {
            if (pageMaterials == null || pageMaterials.Length == 0)
            {
                return 0;
            }

            return Mathf.Max(0, (pageMaterials.Length - 1) / 2);
        }

        private void SetTurningAngle(float angle)
        {
            if (turningPagePivot == null)
            {
                return;
            }

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
            SetTurningPageVisible(false, null);
        }

        private void SetTurningPageVisible(bool visible, Material material)
        {
            if (turningPageRenderer != null)
            {
                if (material != null)
                {
                    turningPageRenderer.sharedMaterial = material;
                }

                turningPageRenderer.gameObject.SetActive(visible);
            }

            if (useCurledStrips && turningPageRenderers != null)
            {
                foreach (Renderer renderer in turningPageRenderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (material != null)
                    {
                        renderer.sharedMaterial = material;
                    }

                    renderer.gameObject.SetActive(visible);
                }
            }

            if (turningPagePivot != null)
            {
                turningPagePivot.gameObject.SetActive(visible);
            }
        }

        private void EnsureCachedTransforms()
        {
            if (turningPagePivot != null)
            {
                pivotBaseRotation = turningPagePivot.localRotation;
            }

            CacheStripTransforms();
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
                {
                    continue;
                }

                stripBasePositions[i] = strip.localPosition;
                stripBaseRotations[i] = strip.localRotation;
            }
        }

        private void UpdateCurlStrips(float angle)
        {
            if (turningPageStrips == null || stripBasePositions == null || turningPageStrips.Length == 0)
            {
                return;
            }

            float midCurl = Mathf.Sin(Mathf.Clamp01(angle / 180f) * Mathf.PI) * curlAngle;
            int last = Mathf.Max(1, turningPageStrips.Length - 1);

            for (int i = 0; i < turningPageStrips.Length; i++)
            {
                Transform strip = turningPageStrips[i];
                if (strip == null)
                {
                    continue;
                }

                float normalized = i / (float)last;
                float stripAngle = angle + (normalized - 0.5f) * midCurl;
                Quaternion rotation = Quaternion.Euler(0f, stripAngle, 0f);
                strip.localPosition = rotation * stripBasePositions[i];
                strip.localRotation = rotation * stripBaseRotations[i];
            }
        }
    }
}
