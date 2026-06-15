using CodexGenerated.GrimoirePages;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// 오른손 검지 끝(XR Hands R_IndexTip)이 마도서 페이지 영역에 근접하면 드래그 턴을 시작.
    /// 손을 왼쪽으로 움직이면 다음 페이지, 오른쪽으로 움직이면 이전 페이지.
    /// OVRSkeleton 없이 XR Hands의 R_IndexTip Transform을 직접 참조.
    /// </summary>
    public class GrimoireFingerPageTurner : MonoBehaviour
    {
        [Header("── 참조 ──")]
        [SerializeField] private GrimoirePageTurner pageTurner;
        [SerializeField] private GrimoireManager grimoireManager;
        [Tooltip("XR Hands의 R_IndexTip 오브젝트. 비워두면 Awake에서 자동 탐색.")]
        [SerializeField] private Transform rightIndexTip;

        [Header("── 터치 감지 (책 로컬 좌표 기준) ──")]
        [Tooltip("페이지 안쪽 X 경계 (척추 쪽, m)")]
        [SerializeField] private float pageInnerEdgeX = 0.02f;
        [Tooltip("페이지 바깥 X 경계 (m)")]
        [SerializeField] private float pageOuterEdgeX = 0.19f;
        [Tooltip("페이지 세로 절반 범위 (m)")]
        [SerializeField] private float pageHalfHeight = 0.12f;
        [Tooltip("페이지 앞면 Z 위치 (m, 책 로컬)")]
        [SerializeField] private float pageSurfaceZ = -0.024f;
        [Tooltip("페이지 표면 깊이 허용 범위 (m)")]
        [SerializeField] private float touchDepthTolerance = 0.025f;
        [Tooltip("터치 감지 X 여유 범위 (m)")]
        [SerializeField] private float touchXTolerance = 0.02f;

        [Header("── 드래그 설정 ──")]
        [Tooltip("페이지 완료 기준 각도 (이 이상이면 다음 페이지 확정)")]
        [SerializeField] private float commitAngleThreshold = 60f;
        [Tooltip("드래그 도중 Z 이탈 허용 범위 (m). 책에서 앞뒤로 손이 너무 멀어지면 해제.")]
        [SerializeField] private float releaseZTolerance = 0.12f;
        [Tooltip("커밋 후 새 드래그를 시작할 수 없는 쿨다운 (초). 역방향 즉시 재시작 방지.")]
        [SerializeField] private float postCommitCooldown = 0.35f;

        [Header("── 디버그 ──")]
        [Tooltip("활성화 시 매 프레임 손가락 위치를 콘솔에 출력 (성능 주의)")]
        [SerializeField] private bool verboseFingerPosition = false;

        private bool isDragging;
        private bool draggingForward;
        private float currentAngle;
        private float nextDiagnosticTime;
        private float lastCommitTime = -999f;

        private void Awake()
        {
            ResolveReferences();
            Debug.Log($"[PageTurn] GrimoireFingerPageTurner Awake | pageTurner={(pageTurner == null ? "NULL" : "OK")} | grimoireManager={(grimoireManager == null ? "NULL" : "OK")} | rightIndexTip={(rightIndexTip == null ? "NULL" : rightIndexTip.name)}");
        }

        private void ResolveReferences()
        {
            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>(FindObjectsInactive.Include);

            if (pageTurner == null)
                pageTurner = FindAnyObjectByType<GrimoirePageTurner>(FindObjectsInactive.Include);

            if (rightIndexTip == null)
            {
                // XR Hands 손가락 끝 오브젝트 자동 탐색 (씬에 R_IndexTip이 두 개일 수 있으므로 "Right Hand Tracking" 하위 우선)
                var rightHandTracking = FindRightHandTracking();
                if (rightHandTracking != null)
                {
                    rightIndexTip = FindDeepChild(rightHandTracking, "R_IndexTip");
                    if (rightIndexTip != null)
                        Debug.Log($"[PageTurn] R_IndexTip 자동 탐색 성공: {GetPath(rightIndexTip)}");
                }

                if (rightIndexTip == null)
                {
                    var tipObj = GameObject.Find("R_IndexTip");
                    if (tipObj != null)
                    {
                        rightIndexTip = tipObj.transform;
                        Debug.Log($"[PageTurn] R_IndexTip fallback 탐색: {GetPath(rightIndexTip)}");
                    }
                    else
                    {
                        Debug.LogWarning("[PageTurn] R_IndexTip을 씬에서 찾을 수 없음 — 인스펙터에서 직접 연결 필요");
                    }
                }
            }
        }

        private static Transform FindRightHandTracking()
        {
            // 1순위: OVRCameraRig/TrackingSpace 하위 (Meta Quest 실제 트래킹)
            var ovrRig = FindAnyObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (ovrRig != null)
            {
                var found = FindDeepChild(ovrRig.transform, "Right Hand Tracking");
                if (found != null) return found;
            }

            // 2순위: XRHandSkeletonDriver 중 "Right Hand Tracking" 이름인 것
            var candidates = FindObjectsByType<UnityEngine.XR.Hands.XRHandSkeletonDriver>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in candidates)
            {
                if (c.name == "Right Hand Tracking")
                    return c.transform;
            }
            return null;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void Update()
        {
            // 2초마다 현재 상태 진단 로그
            if (Time.time >= nextDiagnosticTime)
            {
                nextDiagnosticTime = Time.time + 2f;
                var tipPos = rightIndexTip != null ? rightIndexTip.position.ToString("F3") : "null";
                var bookLocalDiag = (rightIndexTip != null && pageTurner != null)
                    ? pageTurner.transform.InverseTransformPoint(rightIndexTip.position).ToString("F3")
                    : "N/A";
                Debug.Log($"[PageTurn] 상태 진단 | IsOpen={grimoireManager?.IsOpen} | tipWorld={tipPos} | tipBookLocal={bookLocalDiag} | isDragging={isDragging} | pageTurnerPath={(pageTurner != null ? GetPath(pageTurner.transform) : "null")}");
            }

            if (pageTurner == null || grimoireManager == null)
            {
                if (isDragging) ReleaseDrag("참조 null");
                return;
            }

            if (!grimoireManager.IsOpen)
            {
                if (isDragging) ReleaseDrag("마도서 닫힘");
                return;
            }

            if (rightIndexTip == null)
            {
                ResolveReferences();
                return;
            }

            var bookLocal = pageTurner.transform.InverseTransformPoint(rightIndexTip.position);

            if (verboseFingerPosition)
                Debug.Log($"[PageTurn] 손가락 bookLocal=({bookLocal.x:F3}, {bookLocal.y:F3}, {bookLocal.z:F3}) | isDragging={isDragging} | angle={currentAngle:F1}°");

            if (!isDragging)
                TryBeginDrag(bookLocal);
            else
                UpdateDrag(bookLocal);
        }

        private void TryBeginDrag(Vector3 bookLocal)
        {
            bool onRight = IsOnPage(bookLocal, rightSide: true);
            bool onLeft = IsOnPage(bookLocal, rightSide: false);

            if (!onRight && !onLeft)
                return;

            if (Time.unscaledTime - lastCommitTime < postCommitCooldown)
                return;

            if (!pageTurner.CanManualTurn)
            {
                Debug.Log($"[PageTurn] 터치 감지됐으나 CanManualTurn=false | side={(onRight ? "오른쪽" : "왼쪽")}");
                return;
            }

            bool forward = onRight;
            Debug.Log($"[PageTurn] 터치 감지 | side={(forward ? "오른쪽→앞으로" : "왼쪽→뒤로")} | bookLocal=({bookLocal.x:F3}, {bookLocal.y:F3}, {bookLocal.z:F3})");

            if (!pageTurner.BeginManualTurn(forward))
            {
                Debug.LogWarning($"[PageTurn] BeginManualTurn({forward}) 실패 — 더 이상 페이지 없음");
                return;
            }

            isDragging = true;
            draggingForward = forward;
            currentAngle = forward ? 0f : 180f;
            grimoireManager.SetPageDragLocked(true);
            Debug.Log($"[PageTurn] 드래그 시작 | forward={forward} | 초기각도={currentAngle:F1}° | 닫힘 잠금 ON");
        }

        private void UpdateDrag(Vector3 bookLocal)
        {
            float normalizedX = Mathf.InverseLerp(pageOuterEdgeX, -pageOuterEdgeX, bookLocal.x);
            currentAngle = Mathf.Clamp(normalizedX * 180f, 0f, 180f);
            pageTurner.SetManualTurnAngle(currentAngle);

            // 드래그 중 자연스러운 손 이동으로 Y가 많이 변하므로 Z 이탈만 체크
            bool zOut = Mathf.Abs(bookLocal.z - pageSurfaceZ) > touchDepthTolerance + releaseZTolerance;

            if (zOut)
            {
                Debug.Log($"[PageTurn] Z 이탈로 해제 | z={bookLocal.z:F3} | 각도={currentAngle:F1}°");
                ReleaseDrag("Z 범위 이탈");
            }
        }

        private void ReleaseDrag(string reason = "")
        {
            if (!isDragging) return;

            bool willCommit = draggingForward ? currentAngle >= commitAngleThreshold : currentAngle <= commitAngleThreshold;
            Debug.Log($"[PageTurn] 드래그 해제 | 이유={reason} | 각도={currentAngle:F1}° | {(willCommit ? "✓ 페이지 확정" : "✗ 원위치")} | 닫힘 잠금 OFF");

            isDragging = false;
            if (willCommit)
                lastCommitTime = Time.unscaledTime;
            grimoireManager?.SetPageDragLocked(false);
            pageTurner.CommitManualTurn();
        }

        private bool IsOnPage(Vector3 bookLocal, bool rightSide)
        {
            float xMin = rightSide ? pageInnerEdgeX : -(pageOuterEdgeX + touchXTolerance);
            float xMax = rightSide ? pageOuterEdgeX + touchXTolerance : -pageInnerEdgeX;

            return bookLocal.x >= xMin &&
                   bookLocal.x <= xMax &&
                   Mathf.Abs(bookLocal.y) <= pageHalfHeight &&
                   Mathf.Abs(bookLocal.z - pageSurfaceZ) <= touchDepthTolerance;
        }

        private void OnDisable()
        {
            if (isDragging)
                ReleaseDrag("컴포넌트 비활성화");
        }

        private static string GetPath(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts);
        }
    }
}
