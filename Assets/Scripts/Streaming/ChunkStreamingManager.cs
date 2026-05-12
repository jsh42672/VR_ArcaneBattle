using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WorldStreaming
{
    public class ChunkStreamingManager : MonoBehaviour
    {
        public static ChunkStreamingManager Instance;

        [Header("Player")]
        public Transform playerTransform;

        [Header("Chunk Settings")]
        public float chunkSize = 200f;
        [Range(1, 5)] public int loadDistance = 2;
        [Range(2, 7)] public int unloadDistance = 3;

        [Header("Performance")]
        [Tooltip("How often chunk states are checked.")]
        [Range(0.1f, 2f)] public float updateInterval = 0.25f;

        [Tooltip("Maximum chunks processed per frame.")]
        [Range(1, 30)] public int chunksPerFrame = 8;

        [Tooltip("Apply camera layer culling distances for far objects.")]
        public bool useLayerCulling = true;

        [Tooltip("Maximum culling distance for non-critical layers.")]
        public float layerCullDistance = 800f;

        [Header("GPU Optimization")]
        [Tooltip("Attempt to use Unity 6 GPU-friendly rendering settings where available.")]
        public bool enableGPUResidentDrawer = true;

        [Header("Debug")]
        public bool showDebugLogs = false;
        public bool showGizmos = true;

        private readonly Dictionary<Vector2Int, WorldChunk> chunkMap = new();
        private readonly List<(WorldChunk chunk, bool load, int priority)> pendingList = new();
        private bool isPendingDirty;

        private Vector2Int lastPlayerChunk = new(int.MinValue, int.MinValue);
        private Camera mainCamera;
        private float[] originalLayerCullDistances;

        public int LoadedChunkCount
        {
            get
            {
                var count = 0;
                foreach (var kvp in chunkMap)
                {
                    if (kvp.Value != null && kvp.Value.IsLoaded)
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            mainCamera = Camera.main;

            if (playerTransform == null && mainCamera != null)
                playerTransform = mainCamera.transform;

            if (playerTransform == null)
                Debug.LogWarning("[ChunkStreaming] Player Transform is not assigned.");

            SyncChunkSizes();
            SetupGPUOptimizations();
        }

        private void Start()
        {
            if (useLayerCulling && mainCamera != null)
                SetupLayerCulling();

            if (playerTransform != null)
                ForceUpdateAll(GetChunkCoords(playerTransform.position));

            StartCoroutine(StreamingLoop());
            StartCoroutine(ProcessPendingLoop());
        }

        private void OnDestroy()
        {
            if (mainCamera != null && originalLayerCullDistances != null)
                mainCamera.layerCullDistances = originalLayerCullDistances;
        }

        private IEnumerator StreamingLoop()
        {
            var wait = new WaitForSecondsRealtime(updateInterval);
            while (true)
            {
                yield return wait;

                if (playerTransform == null)
                    continue;

                var currentChunk = GetChunkCoords(playerTransform.position);
                if (currentChunk == lastPlayerChunk)
                    continue;

                lastPlayerChunk = currentChunk;
                BuildPendingList(currentChunk);
            }
        }

        private void BuildPendingList(Vector2Int center)
        {
            pendingList.Clear();

            var checkRange = unloadDistance + 1;
            for (var x = center.x - checkRange; x <= center.x + checkRange; x++)
            {
                for (var y = center.y - checkRange; y <= center.y + checkRange; y++)
                {
                    var coord = new Vector2Int(x, y);
                    if (!chunkMap.TryGetValue(coord, out var chunk) || chunk == null)
                        continue;

                    var distance = ChebyshevDistance(center, coord);
                    if (distance <= loadDistance && !chunk.IsLoaded)
                        pendingList.Add((chunk, true, distance));
                    else if (distance > unloadDistance && chunk.IsLoaded)
                        pendingList.Add((chunk, false, 100 - distance));
                }
            }

            pendingList.Sort((a, b) => a.priority.CompareTo(b.priority));
            isPendingDirty = true;
        }

        private IEnumerator ProcessPendingLoop()
        {
            while (true)
            {
                if (isPendingDirty && pendingList.Count > 0)
                {
                    var processed = 0;
                    while (pendingList.Count > 0 && processed < chunksPerFrame)
                    {
                        var (chunk, load, _) = pendingList[0];
                        pendingList.RemoveAt(0);

                        if (chunk == null)
                            continue;

                        chunk.SetChunkActive(load);
                        processed++;

                        if (showDebugLogs)
                        {
                            var actionName = load ? "Load" : "Unload";
                            Debug.Log($"[ChunkStreaming] {chunk.chunkCoords} {actionName}");
                        }
                    }

                    if (pendingList.Count == 0)
                        isPendingDirty = false;
                }

                yield return null;
            }
        }

        private void ForceUpdateAll(Vector2Int center)
        {
            foreach (var kvp in chunkMap)
            {
                if (kvp.Value == null)
                    continue;

                var distance = ChebyshevDistance(center, kvp.Key);
                kvp.Value.SetChunkActive(distance <= loadDistance);
            }

            lastPlayerChunk = center;
        }

        private void SetupGPUOptimizations()
        {
            if (!enableGPUResidentDrawer)
                return;

#if UNITY_6000_0_OR_NEWER
            var urpAsset = GraphicsSettings.currentRenderPipeline;
            if (urpAsset != null && showDebugLogs)
                Debug.Log("[ChunkStreaming] GPU-friendly render pipeline settings detected.");
#endif
        }

        private void SetupLayerCulling()
        {
            originalLayerCullDistances = mainCamera.layerCullDistances;
            var distances = new float[32];

            for (var i = 0; i < distances.Length; i++)
                distances[i] = layerCullDistance;

            var uiLayer = LayerMask.NameToLayer("UI");
            var playerLayer = LayerMask.NameToLayer("Player");
            if (uiLayer >= 0)
                distances[uiLayer] = 0f;
            if (playerLayer >= 0)
                distances[playerLayer] = 0f;

            mainCamera.layerCullDistances = distances;

            if (showDebugLogs)
                Debug.Log($"[ChunkStreaming] Layer culling configured at {layerCullDistance}m.");
        }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        public Vector2Int GetChunkCoords(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / chunkSize),
                Mathf.FloorToInt(worldPosition.z / chunkSize));
        }

        public void RegisterChunk(WorldChunk chunk)
        {
            if (chunk == null)
                return;

            chunkMap[chunk.chunkCoords] = chunk;
            chunk.chunkSize = chunkSize;
        }

        private void SyncChunkSizes()
        {
            foreach (var kvp in chunkMap)
            {
                if (kvp.Value != null)
                    kvp.Value.chunkSize = chunkSize;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos || playerTransform == null)
                return;

            var playerChunk = GetChunkCoords(playerTransform.position);
            var center = new Vector3(
                playerChunk.x * chunkSize + chunkSize / 2f,
                0f,
                playerChunk.y * chunkSize + chunkSize / 2f);

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            var loadSize = chunkSize * (loadDistance * 2 + 1);
            Gizmos.DrawWireCube(center, new Vector3(loadSize, 20f, loadSize));

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            var unloadSize = chunkSize * (unloadDistance * 2 + 1);
            Gizmos.DrawWireCube(center, new Vector3(unloadSize, 20f, unloadSize));

            if (useLayerCulling)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.08f);
                Gizmos.DrawWireSphere(playerTransform.position, layerCullDistance);
            }
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Setup Chunk Streaming")]
        public static void SetupStreaming()
        {
            if (!EditorUtility.DisplayDialog(
                    "Setup Chunk Streaming",
                    "This will organize scene renderers into streaming chunks based on position.\n\nChunk size: 200x200\nUndo is supported.",
                    "Continue",
                    "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            var groupIndex = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Chunk Streaming");

            var manager = Object.FindAnyObjectByType<ChunkStreamingManager>();
            if (manager == null)
            {
                var managerObject = new GameObject("ChunkStreamingManager");
                manager = managerObject.AddComponent<ChunkStreamingManager>();
                Undo.RegisterCreatedObjectUndo(managerObject, "Create Manager");
            }
            else
            {
                Undo.RecordObject(manager, "Reset Manager");
                manager.chunkMap.Clear();
            }

            var chunksRoot = GameObject.Find("WorldChunks_Root");
            if (chunksRoot == null)
            {
                chunksRoot = new GameObject("WorldChunks_Root");
                Undo.RegisterCreatedObjectUndo(chunksRoot, "Create Root");
            }

            var tempMap = new Dictionary<Vector2Int, WorldChunk>();
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var processed = 0;
            var skipped = 0;
            var staticCount = 0;

            foreach (var renderer in allRenderers)
            {
                var target = renderer.gameObject;
                if (target.layer == LayerMask.NameToLayer("UI") ||
                    target.CompareTag("Player") ||
                    target.CompareTag("MainCamera") ||
                    target.GetComponentInParent<ChunkStreamingManager>() != null ||
                    target.GetComponentInParent<WorldChunk>() != null)
                {
                    skipped++;
                    continue;
                }

                var position = target.transform.position;
                var coords = new Vector2Int(
                    Mathf.FloorToInt(position.x / manager.chunkSize),
                    Mathf.FloorToInt(position.z / manager.chunkSize));

                if (!tempMap.TryGetValue(coords, out var chunk))
                {
                    var chunkObject = new GameObject($"Chunk_{coords.x}_{coords.y}");
                    chunkObject.transform.SetParent(chunksRoot.transform);
                    chunk = chunkObject.AddComponent<WorldChunk>();
                    chunk.chunkCoords = coords;
                    chunk.chunkSize = manager.chunkSize;
                    tempMap[coords] = chunk;
                    Undo.RegisterCreatedObjectUndo(chunkObject, "Create Chunk");
                }

                Undo.SetTransformParent(target.transform, chunk.transform, "Move to Chunk");
                chunk.AddObject(target);

                if (GameObjectUtility.GetStaticEditorFlags(target) != 0)
                    staticCount++;

                processed++;
            }

            foreach (var kvp in tempMap)
                manager.RegisterChunk(kvp.Value);

            Undo.CollapseUndoOperations(groupIndex);
            EditorUtility.SetDirty(manager);

            Debug.Log(
                $"[ChunkStreaming] Setup complete. Chunks: {tempMap.Count}, Objects: {processed}, Skipped: {skipped}, Static: {staticCount}");

            EditorUtility.DisplayDialog(
                "Setup complete",
                $"Chunks: {tempMap.Count}\nObjects: {processed}\nSkipped: {skipped}\nStatic objects: {staticCount}",
                "OK");
        }

        [MenuItem("Tools/Chunk Streaming/Reset Chunks")]
        public static void ResetChunks()
        {
            if (!EditorUtility.DisplayDialog("Reset Chunks", "This will remove generated chunk objects.", "Continue", "Cancel"))
                return;

            var root = GameObject.Find("WorldChunks_Root");
            if (root != null)
                Undo.DestroyObjectImmediate(root);

            Debug.Log("[ChunkStreaming] Chunk reset complete");
        }
#endif
    }
}
