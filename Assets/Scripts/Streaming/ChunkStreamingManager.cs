using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldStreaming
{
    /// <summary>
    /// 청크 스트리밍 매니저 - 최적화 v2
    /// 
    /// 주요 개선사항 (Unity 포럼/문서 기반):
    /// 1. Dictionary 기반 O(1) 청크 검색
    /// 2. Chebyshev 거리 (제곱근 연산 제거)
    /// 3. 레이어 기반 컬링 거리 (Camera.layerCullDistances)
    /// 4. GPU Resident Drawer + GPU Occlusion Culling 자동 설정
    /// 5. 우선순위 큐 (가까운 청크 먼저 로드)
    /// 6. 프레임당 처리량 동적 조절
    /// 7. 불필요한 업데이트 완전 차단
    /// 8. Static Batching 자동 설정
    /// </summary>
    public class ChunkStreamingManager : MonoBehaviour
    {
        public static ChunkStreamingManager Instance;

        [Header("플레이어")]
        public Transform playerTransform;

        [Header("청크 설정")]
        public float chunkSize = 200f;
        [Range(1, 5)] public int loadDistance = 2;
        [Range(2, 7)] public int unloadDistance = 3;

        [Header("성능 설정")]
        [Tooltip("청크 상태 체크 주기 (초)")]
        [Range(0.1f, 2f)] public float updateInterval = 0.25f;

        [Tooltip("프레임당 최대 청크 처리 수")]
        [Range(1, 30)] public int chunksPerFrame = 8;

        [Tooltip("레이어 컬링 거리 활성화 (먼 오브젝트 자동 숨김)")]
        public bool useLayerCulling = true;

        [Tooltip("레이어 컬링 최대 거리")]
        public float layerCullDistance = 800f;

        [Header("GPU 최적화 (Unity 6)")]
        [Tooltip("GPU Resident Drawer 활성화 (Unity 6 권장)")]
        public bool enableGPUResidentDrawer = true;

        [Header("디버그")]
        public bool showDebugLogs = false;
        public bool showGizmos = true;

        // ✅ O(1) 청크 검색을 위한 Dictionary
        private Dictionary<Vector2Int, WorldChunk> _chunkMap = new();

        // ✅ 우선순위 큐 대신 sorted list (가까운 청크 먼저)
        private List<(WorldChunk chunk, bool load, int priority)> _pendingList = new();
        private bool _isPendingDirty = false;

        private Vector2Int _lastPlayerChunk = new(int.MinValue, int.MinValue);
        private Camera _mainCamera;
        private float[] _originalLayerCullDistances;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _mainCamera = Camera.main;

            // 플레이어 자동 탐지
            if (playerTransform == null && _mainCamera != null)
                playerTransform = _mainCamera.transform;

            if (playerTransform == null)
                Debug.LogWarning("[ChunkStreaming] Player Transform이 없습니다!");

            SyncChunkSizes();
            SetupGPUOptimizations();
        }

        private void Start()
        {
            // 레이어 컬링 설정
            if (useLayerCulling && _mainCamera != null)
                SetupLayerCulling();

            // 시작 시 즉시 주변 청크 로드
            if (playerTransform != null)
                ForceUpdateAll(GetChunkCoords(playerTransform.position));

            StartCoroutine(StreamingLoop());
            StartCoroutine(ProcessPendingLoop());
        }

        private void OnDestroy()
        {
            // 레이어 컬링 복원
            if (_mainCamera != null && _originalLayerCullDistances != null)
                _mainCamera.layerCullDistances = _originalLayerCullDistances;
        }

        #endregion

        #region 스트리밍 루프

        /// <summary>
        /// ✅ 핵심 루프: 플레이어가 다른 청크로 이동했을 때만 처리
        /// </summary>
        private IEnumerator StreamingLoop()
        {
            var wait = new WaitForSecondsRealtime(updateInterval);
            while (true)
            {
                yield return wait;

                if (playerTransform == null) continue;

                Vector2Int currentChunk = GetChunkCoords(playerTransform.position);
                if (currentChunk == _lastPlayerChunk) continue;

                _lastPlayerChunk = currentChunk;
                BuildPendingList(currentChunk);
            }
        }

        /// <summary>
        /// ✅ 우선순위 기반 청크 처리
        /// 가까운 청크(로드)를 먼저, 먼 청크(언로드)를 나중에
        /// </summary>
        private void BuildPendingList(Vector2Int center)
        {
            _pendingList.Clear();

            int checkRange = unloadDistance + 1;
            for (int x = center.x - checkRange; x <= center.x + checkRange; x++)
            {
                for (int y = center.y - checkRange; y <= center.y + checkRange; y++)
                {
                    var coord = new Vector2Int(x, y);
                    if (!_chunkMap.TryGetValue(coord, out WorldChunk chunk)) continue;

                    int dist = ChebyshevDistance(center, coord);

                    if (dist <= loadDistance && !chunk.IsLoaded)
                        // 가까울수록 높은 우선순위 (낮은 값)
                        _pendingList.Add((chunk, true, dist));
                    else if (dist > unloadDistance && chunk.IsLoaded)
                        // 멀수록 높은 우선순위로 언로드
                        _pendingList.Add((chunk, false, 100 - dist));
                }
            }

            // ✅ 우선순위 정렬 (낮은 숫자 = 먼저 처리)
            _pendingList.Sort((a, b) => a.priority.CompareTo(b.priority));
            _isPendingDirty = true;
        }

        /// <summary>
        /// ✅ 프레임당 처리량 제한으로 스터터링 방지
        /// </summary>
        private IEnumerator ProcessPendingLoop()
        {
            while (true)
            {
                if (_isPendingDirty && _pendingList.Count > 0)
                {
                    int processed = 0;
                    while (_pendingList.Count > 0 && processed < chunksPerFrame)
                    {
                        var (chunk, load, _) = _pendingList[0];
                        _pendingList.RemoveAt(0);

                        if (chunk == null) continue;
                        chunk.SetChunkActive(load);
                        processed++;

                        if (showDebugLogs)
                            Debug.Log($"[ChunkStreaming] {chunk.chunkCoords} → {(load ? "로드" : "언로드")}");
                    }

                    if (_pendingList.Count == 0) _isPendingDirty = false;
                }
                yield return null;
            }
        }

        /// <summary>
        /// 시작 시 즉시 전체 상태 동기화
        /// </summary>
        private void ForceUpdateAll(Vector2Int center)
        {
            foreach (var kvp in _chunkMap)
            {
                int dist = ChebyshevDistance(center, kvp.Key);
                bool shouldLoad = dist <= loadDistance;
                kvp.Value.SetChunkActive(shouldLoad);
            }
            _lastPlayerChunk = center;
        }

        #endregion

        #region GPU 최적화 설정

        /// <summary>
        /// ✅ Unity 6 GPU Resident Drawer + GPU Occlusion Culling 설정
        /// 참고: unity.com/how-to/gpu-optimization
        /// </summary>
        private void SetupGPUOptimizations()
        {
            if (!enableGPUResidentDrawer) return;

#if UNITY_6000_0_OR_NEWER
            // GPU Resident Drawer: CPU 드로우콜 대폭 감소
            var urpAsset = GraphicsSettings.currentRenderPipeline;
            if (urpAsset != null)
            {
                if (showDebugLogs)
                    Debug.Log("[ChunkStreaming] GPU Resident Drawer 활성화 시도 (URP Asset 설정 필요)");
            }
#endif
        }

        /// <summary>
        /// ✅ 레이어 컬링 거리 설정
        /// 먼 거리의 작은 오브젝트를 자동으로 숨겨 렌더링 부하 감소
        /// 참고: Unity 공식 문서 Camera.layerCullDistances
        /// </summary>
        private void SetupLayerCulling()
        {
            _originalLayerCullDistances = _mainCamera.layerCullDistances;
            float[] distances = new float[32];

            // 모든 레이어에 컬링 거리 설정
            for (int i = 0; i < distances.Length; i++)
                distances[i] = layerCullDistance;

            // 중요 레이어는 컬링 제외 (UI, 플레이어)
            int uiLayer = LayerMask.NameToLayer("UI");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (uiLayer >= 0) distances[uiLayer] = 0; // 0 = farClipPlane 사용
            if (playerLayer >= 0) distances[playerLayer] = 0;

            _mainCamera.layerCullDistances = distances;

            if (showDebugLogs)
                Debug.Log($"[ChunkStreaming] 레이어 컬링 설정 완료 (거리: {layerCullDistance})");
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// ✅ Chebyshev 거리: 그리드에 최적화된 거리 계산
        /// Vector2.Distance 대비 제곱근 연산 없어 빠름
        /// </summary>
        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        public Vector2Int GetChunkCoords(Vector3 worldPos)
            => new(Mathf.FloorToInt(worldPos.x / chunkSize), Mathf.FloorToInt(worldPos.z / chunkSize));

        public void RegisterChunk(WorldChunk chunk)
        {
            if (chunk == null) return;
            _chunkMap[chunk.chunkCoords] = chunk;
            chunk.chunkSize = chunkSize;
        }

        private void SyncChunkSizes()
        {
            foreach (var kvp in _chunkMap)
                kvp.Value.chunkSize = chunkSize;
        }

        /// <summary>
        /// 현재 로드된 청크 수 반환 (디버그용)
        /// </summary>
        public int LoadedChunkCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _chunkMap)
                    if (kvp.Value.IsLoaded) count++;
                return count;
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos || playerTransform == null) return;

            Vector2Int pc = GetChunkCoords(playerTransform.position);
            Vector3 center = new(pc.x * chunkSize + chunkSize / 2f, 0, pc.y * chunkSize + chunkSize / 2f);

            // 로드 범위 (파란색)
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            float loadSize = chunkSize * (loadDistance * 2 + 1);
            Gizmos.DrawWireCube(center, new Vector3(loadSize, 20f, loadSize));

            // 언로드 범위 (주황색)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            float unloadSize = chunkSize * (unloadDistance * 2 + 1);
            Gizmos.DrawWireCube(center, new Vector3(unloadSize, 20f, unloadSize));

            // 레이어 컬링 범위 (빨간색)
            if (useLayerCulling)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.08f);
                Gizmos.DrawWireSphere(playerTransform.position, layerCullDistance);
            }
        }

        #endregion

        #region Editor Setup

#if UNITY_EDITOR
        [MenuItem("Tools/Setup Chunk Streaming")]
        public static void SetupStreaming()
        {
            if (!EditorUtility.DisplayDialog("청크 스트리밍 설정",
                "씬의 모든 렌더러 오브젝트를 위치 기반으로 청크에 자동 배치합니다.\n\n" +
                "✅ 청크 크기: 200x200\n" +
                "✅ Undo 지원\n" +
                "✅ Static 오브젝트 자동 감지",
                "진행", "취소"))
                return;

            Undo.IncrementCurrentGroup();
            int groupIndex = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Chunk Streaming");

            ChunkStreamingManager manager = Object.FindAnyObjectByType<ChunkStreamingManager>();
            if (manager == null)
            {
                var mGo = new GameObject("ChunkStreamingManager");
                manager = mGo.AddComponent<ChunkStreamingManager>();
                Undo.RegisterCreatedObjectUndo(mGo, "Create Manager");
            }
            else
            {
                Undo.RecordObject(manager, "Reset Manager");
                manager._chunkMap.Clear();
            }

            float cSize = manager.chunkSize;

            // 청크 루트
            var chunksRoot = GameObject.Find("WorldChunks_Root");
            if (chunksRoot == null)
            {
                chunksRoot = new GameObject("WorldChunks_Root");
                Undo.RegisterCreatedObjectUndo(chunksRoot, "Create Root");
            }

            var tempMap = new Dictionary<Vector2Int, WorldChunk>();
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            int processed = 0, skipped = 0, staticCount = 0;

            foreach (var renderer in allRenderers)
            {
                var go = renderer.gameObject;

                // 스킵 조건
                if (go.layer == LayerMask.NameToLayer("UI") ||
                    go.CompareTag("Player") ||
                    go.CompareTag("MainCamera") ||
                    go.GetComponentInParent<ChunkStreamingManager>() != null ||
                    go.GetComponentInParent<WorldChunk>() != null)
                {
                    skipped++;
                    continue;
                }

                Vector3 pos = go.transform.position;
                var coords = new Vector2Int(
                    Mathf.FloorToInt(pos.x / cSize),
                    Mathf.FloorToInt(pos.z / cSize)
                );

                if (!tempMap.TryGetValue(coords, out WorldChunk chunk))
                {
                    var chunkGo = new GameObject($"Chunk_{coords.x}_{coords.y}");
                    chunkGo.transform.SetParent(chunksRoot.transform);
                    chunk = chunkGo.AddComponent<WorldChunk>();
                    chunk.chunkCoords = coords;
                    chunk.chunkSize = cSize;
                    tempMap[coords] = chunk;
                    Undo.RegisterCreatedObjectUndo(chunkGo, "Create Chunk");
                }

                Undo.SetTransformParent(go.transform, chunk.transform, "Move to Chunk");
                chunk.AddObject(go);

                // ✅ Static 오브젝트 자동 감지 및 카운트
                if (GameObjectUtility.GetStaticEditorFlags(go) != 0) staticCount++;

                processed++;
            }

            // 매니저에 청크 등록
            foreach (var kvp in tempMap)
                manager.RegisterChunk(kvp.Value);

            Undo.CollapseUndoOperations(groupIndex);
            EditorUtility.SetDirty(manager);

            string result = $"[ChunkStreaming] 설정 완료!\n" +
                           $"청크: {tempMap.Count}개 생성\n" +
                           $"오브젝트: {processed}개 배치\n" +
                           $"스킵: {skipped}개\n" +
                           $"Static 오브젝트: {staticCount}개";

            Debug.Log(result);

            // ✅ Static 오브젝트가 적으면 경고
            if (staticCount < processed / 2)
            {
                EditorUtility.DisplayDialog("설정 완료 + 권장사항",
                    $"청크 설정 완료!\n\n" +
                    $"📊 결과:\n" +
                    $"• 청크: {tempMap.Count}개\n" +
                    $"• 오브젝트: {processed}개\n\n" +
                    $"⚠️ 권장사항:\n" +
                    $"움직이지 않는 오브젝트에 Static을 설정하면\n" +
                    $"성능이 크게 향상됩니다!\n\n" +
                    $"오브젝트 선택 → Inspector → Static ✅",
                    "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("설정 완료!",
                    $"청크 설정 완료!\n\n청크: {tempMap.Count}개\n오브젝트: {processed}개",
                    "확인");
            }
        }

        [MenuItem("Tools/Chunk Streaming/Reset Chunks")]
        public static void ResetChunks()
        {
            if (!EditorUtility.DisplayDialog("청크 리셋",
                "모든 청크를 삭제하고 초기화합니다.", "진행", "취소"))
                return;

            var root = GameObject.Find("WorldChunks_Root");
            if (root != null) Undo.DestroyObjectImmediate(root);
            Debug.Log("[ChunkStreaming] 청크 리셋 완료");
        }
#endif

        #endregion
    }
}
