using UnityEngine;
using System.Collections.Generic;

namespace WorldStreaming
{
    /// <summary>
    /// 개별 청크 - 최적화 v2
    /// </summary>
    public class WorldChunk : MonoBehaviour
    {
        public Vector2Int chunkCoords;
        public List<GameObject> chunkObjects = new List<GameObject>();

        [HideInInspector] public float chunkSize = 200f;

        [SerializeField] private bool _isLoaded = true;
        public bool IsLoaded => _isLoaded;

        private int _cachedObjectCount = -1;
        public int ObjectCount
        {
            get
            {
                if (_cachedObjectCount < 0)
                    _cachedObjectCount = chunkObjects.Count;
                return _cachedObjectCount;
            }
        }

        private void Awake()
        {
            CleanNullObjects();
        }

        public void SetChunkActive(bool active)
        {
            if (_isLoaded == active) return;
            _isLoaded = active;

            for (int i = 0; i < chunkObjects.Count; i++)
            {
                if (chunkObjects[i] != null)
                    chunkObjects[i].SetActive(active);
            }
        }

        public void AddObject(GameObject obj)
        {
            if (obj != null && !chunkObjects.Contains(obj))
            {
                chunkObjects.Add(obj);
                _cachedObjectCount = -1;
            }
        }

        public void CleanNullObjects()
        {
            chunkObjects.RemoveAll(o => o == null);
            _cachedObjectCount = -1;
        }

        private void OnDrawGizmos()
        {
            Vector3 center = new Vector3(
                chunkCoords.x * chunkSize + chunkSize / 2f, 0f,
                chunkCoords.y * chunkSize + chunkSize / 2f
            );

            Gizmos.color = _isLoaded
                ? new Color(0f, 1f, 0f, 0.12f)
                : new Color(1f, 0f, 0f, 0.06f);
            Gizmos.DrawCube(center, new Vector3(chunkSize, 30f, chunkSize));

            Gizmos.color = _isLoaded ? Color.green : new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(chunkSize, 30f, chunkSize));

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                center + Vector3.up * 10f,
                $"[{chunkCoords.x},{chunkCoords.y}]\n{ObjectCount} objs\n{(_isLoaded ? "LOADED" : "UNLOADED")}"
            );
#endif
        }
    }
}
