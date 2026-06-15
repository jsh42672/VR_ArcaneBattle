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

        [Header("── 페이지 두께 표현 ──")]
        [Tooltip("왼쪽 페이지 스택 Transform (없으면 자동 생성)")]
        [SerializeField] private Transform leftPageStack;
        [Tooltip("오른쪽 페이지 스택 Transform (없으면 자동 생성)")]
        [SerializeField] private Transform rightPageStack;
        [Tooltip("읽지 않은 전체 페이지의 최대 두께 (m)")]
        [SerializeField] private float maxPageStackWidth = 0.018f;

        private int spreadIndex;
        private bool isTurning;
        private Quaternion pivotBaseRotation;
        private Vector3[] stripBasePositions;
        private Quaternion[] stripBaseRotations;

        // 수동 드래그 턴 상태
        private float currentTurnAngle;
        private int manualTargetSpread;
        private bool manualTurnForward;
#if UNITY_EDITOR
        private double editorTurnStartTime;
        private int editorTargetSpread;
        private bool editorTurnForward;
        private Material editorTurningMaterial;
#endif

        public bool IsTurning => isTurning;

        /// <summary>손가락 드래그 턴을 시작할 수 있는 상태인지</summary>
        public bool CanManualTurn => !isTurning && pageMaterials != null && pageMaterials.Length > 2;

        /// <summary>
        /// 손가락 드래그 턴 시작. forward=true면 다음 페이지, false면 이전 페이지.
        /// 성공 시 true 반환.
        /// </summary>
        public bool BeginManualTurn(bool forward)
        {
            if (!CanManualTurn)
            {
                Debug.LogWarning($"[PageTurn] BeginManualTurn 거부 | isTurning={isTurning} | pageMaterials={(pageMaterials == null ? "null" : pageMaterials.Length.ToString())}");
                return false;
            }

            int targetSpread = forward
                ? Mathf.Min(spreadIndex + 1, GetLastSpreadIndex())
                : Mathf.Max(spreadIndex - 1, 0);

            if (targetSpread == spreadIndex)
            {
                Debug.Log($"[PageTurn] BeginManualTurn 거부: 더 이상 페이지 없음 | forward={forward} | spreadIndex={spreadIndex} | last={GetLastSpreadIndex()}");
                return false;
            }

            EnsureCachedTransforms();
            manualTargetSpread = targetSpread;
            manualTurnForward = forward;

            // 각도를 먼저 세팅 (pivot이 항상 표시되므로 시각적 점프 방지)
            currentTurnAngle = forward ? 0f : 180f;
            SetTurningAngle(currentTurnAngle);

            var turningMat = PrepareTurningPage(targetSpread, forward);
            SetTurningPageVisible(true, turningMat);

            isTurning = true;

            Debug.Log($"[PageTurn] BeginManualTurn | forward={forward} | spread {spreadIndex}→{targetSpread} | turningMat={(turningMat == null ? "null" : turningMat.name)}");
            return true;
        }

        /// <summary>드래그 중 각도 갱신 (0°=오른쪽, 180°=왼쪽)</summary>
        public void SetManualTurnAngle(float angle)
        {
            if (!isTurning)
                return;

            currentTurnAngle = Mathf.Clamp(angle, 0f, 180f);
            SetTurningAngle(currentTurnAngle);
        }

        /// <summary>
        /// 손가락 뗄 때 호출. 현재 각도 기준으로 커밋/취소 결정 후 스냅 애니메이션.
        /// forward: 90° 이상이면 다음 페이지 확정.
        /// backward: 90° 이하이면 이전 페이지 확정.
        /// </summary>
        public void CommitManualTurn()
        {
            if (!isTurning)
            {
                Debug.LogWarning("[PageTurn] CommitManualTurn 호출됐으나 isTurning=false");
                return;
            }

            bool commit = manualTurnForward
                ? currentTurnAngle >= 90f
                : currentTurnAngle <= 90f;

            float targetAngle = commit
                ? (manualTurnForward ? 180f : 0f)
                : (manualTurnForward ? 0f : 180f);

            int finalSpread = commit ? manualTargetSpread : spreadIndex;

            Debug.Log($"[PageTurn] CommitManualTurn | 각도={currentTurnAngle:F1}° | {(commit ? $"확정 spread→{finalSpread}" : $"취소 spread 유지={spreadIndex}")} | 스냅→{targetAngle:F0}°");

            // 오브젝트가 비활성이면 코루틴 불가 → 즉시 완료 처리
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log("[PageTurn] CommitManualTurn: 오브젝트 비활성, 즉시 완료");
                SetTurningAngle(targetAngle);
                spreadIndex = finalSpread;
                ApplySpread(spreadIndex);
                HideTurningPage();
                isTurning = false;
                return;
            }

            StartCoroutine(SnapAndFinishTurn(targetAngle, finalSpread));
        }

        private void OnDisable()
        {
            // 비활성화 시 isTurning 강제 리셋 (코루틴이 중단되어도 다음 사용 가능하도록)
            if (isTurning)
            {
                isTurning = false;
                HideTurningPage();
                Debug.Log("[PageTurn] OnDisable: isTurning 리셋");
            }
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

            Debug.Log($"[PageTurn] 스냅 완료 | 최종 spread={spreadIndex}");
        }

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
            EnsurePageStacks();
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

            float from = forward ? 0f : 180f;
            float to = forward ? 180f : 0f;

            // 시각적 점프 방지: 각도를 먼저 세팅한 뒤 표시
            SetTurningAngle(from);
            Material turningMaterial = PrepareTurningPage(targetSpread, forward);
            SetTurningPageVisible(true, turningMaterial);

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
                leftPageRenderer.sharedMaterial = GetMaterialForSpread(index, 0);

            if (rightPageRenderer != null)
                rightPageRenderer.sharedMaterial = GetMaterialForSpread(index, 1);

            // turningPagePivot이 항상 오른쪽 페이지 역할을 하므로 재질 동기화
            if (turningPageRenderer != null)
                turningPageRenderer.sharedMaterial = GetMaterialForSpread(index, 1);

            UpdatePageStacks(index);
        }

        private void EnsurePageStacks()
        {
            if (leftPageStack == null && leftPageRenderer != null)
                leftPageStack = CreateStackObject("LeftPageStack", leftPageRenderer.transform, isLeft: true);

            if (rightPageStack == null && rightPageRenderer != null)
                rightPageStack = CreateStackObject("RightPageStack", rightPageRenderer.transform, isLeft: false);
        }

        private Transform CreateStackObject(string objName, Transform pageTransform, bool isLeft)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objName;
            go.transform.SetParent(transform, false);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // 페이지와 동일한 rotation
            go.transform.localRotation = pageTransform.localRotation;

            // 페이지 외부 가장자리에 배치
            var pos = pageTransform.localPosition;
            float halfWidth = pageTransform.localScale.x * 0.5f;
            pos.x += isLeft ? -halfWidth : halfWidth;
            go.transform.localPosition = pos;

            // 초기 scale: 페이지 높이에 맞춤, 두께는 UpdatePageStacks에서 설정
            go.transform.localScale = new Vector3(0f, pageTransform.localScale.y * 0.92f, pageTransform.localScale.z);

            // 페이지 가장자리 색상 (크림색)
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ??
                                   Shader.Find("Unlit/Color") ??
                                   Shader.Find("Standard"))
            {
                color = new Color(0.88f, 0.82f, 0.68f)
            };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            return go.transform;
        }

        private void UpdatePageStacks(int index)
        {
            int last = GetLastSpreadIndex();
            if (last <= 0) return;

            float readFraction = (float)index / last;            // 0=첫페이지, 1=마지막
            float unreadFraction = 1f - readFraction;

            // 오른쪽: 읽지 않은 페이지 (두꺼움 → 얇아짐)
            // 왼쪽: 읽은 페이지 (얇음 → 두꺼워짐)
            SetStackWidth(rightPageStack, unreadFraction * maxPageStackWidth, isLeft: false);
            SetStackWidth(leftPageStack, readFraction * maxPageStackWidth, isLeft: true);
        }

        private void SetStackWidth(Transform stack, float width, bool isLeft)
        {
            if (stack == null) return;

            var s = stack.localScale;
            s.x = Mathf.Max(width, 0.0005f); // 최소 0.5mm (완전히 사라지지 않도록)
            stack.localScale = s;

            // 스택이 페이지 가장자리에서 바깥쪽으로 자라도록 위치 보정
            var pos = stack.localPosition;
            var refRenderer = isLeft ? leftPageRenderer : rightPageRenderer;
            if (refRenderer != null)
            {
                float halfPageWidth = refRenderer.transform.localScale.x * 0.5f;
                float halfStackWidth = s.x * 0.5f;
                pos.x = refRenderer.transform.localPosition.x
                        + (isLeft ? -(halfPageWidth + halfStackWidth) : (halfPageWidth + halfStackWidth));
            }
            stack.localPosition = pos;
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

            // turningPagePivot은 항상 0°에 표시 유지 (오른쪽 페이지 역할)
            // → 다음 드래그 시작 시 갑자기 나타나는 현상 없음
            var restingMat = GetMaterialForSpread(spreadIndex, 1);
            if (turningPageRenderer != null)
            {
                turningPageRenderer.sharedMaterial = restingMat;
                turningPageRenderer.gameObject.SetActive(true);
            }
            if (useCurledStrips && turningPageRenderers != null)
            {
                foreach (Renderer r in turningPageRenderers)
                {
                    if (r == null) continue;
                    r.sharedMaterial = restingMat;
                    r.gameObject.SetActive(true);
                }
            }
            if (turningPagePivot != null)
                turningPagePivot.gameObject.SetActive(true);

            // rightPageRenderer는 숨김: pivot(0°)이 같은 위치에 있으므로 Z-파이팅 방지
            if (rightPageRenderer != null)
                rightPageRenderer.gameObject.SetActive(false);
        }

        private void SetTurningPageVisible(bool turning, Material material)
        {
            // turningPageRenderer/Pivot은 항상 활성 유지
            if (turningPageRenderer != null)
            {
                if (material != null)
                    turningPageRenderer.sharedMaterial = material;
                turningPageRenderer.gameObject.SetActive(true);
            }
            if (useCurledStrips && turningPageRenderers != null)
            {
                foreach (Renderer renderer in turningPageRenderers)
                {
                    if (renderer == null) continue;
                    if (material != null) renderer.sharedMaterial = material;
                    renderer.gameObject.SetActive(true);
                }
            }
            if (turningPagePivot != null)
                turningPagePivot.gameObject.SetActive(true);

            // rightPageRenderer: 드래그 중에만 표시 (pivot 뒤에서 서서히 드러나는 효과)
            if (rightPageRenderer != null)
                rightPageRenderer.gameObject.SetActive(turning);
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
