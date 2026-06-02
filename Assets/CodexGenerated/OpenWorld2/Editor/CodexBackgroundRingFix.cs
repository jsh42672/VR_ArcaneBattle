using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexBackgroundRingFix
{
    private const string DressingPath = "OW2_Hills_Village_Generated/ToonScapes_Environment_Dressing";

    public static void Execute()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        Transform dressing = FindByPath(DressingPath);
        if (dressing == null)
        {
            GameObject root = GameObject.Find("OW2_Hills_Village_Generated");
            if (root == null)
            {
                root = new GameObject("OW2_Hills_Village_Generated");
            }
            dressing = new GameObject("ToonScapes_Environment_Dressing").transform;
            dressing.SetParent(root.transform, false);
        }

        Transform old = dressing.Find("Background_Ring");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Transform group = new GameObject("Background_Ring").transform;
        group.SetParent(dressing, false);

        string[] prefabs =
        {
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Mountain_01A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Mountain_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Mountain_03A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Hill_01A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Hill_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Cliff_01A.prefab"
        };

        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        Vector3 center = terrainPos + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);

        BackdropPlacement[] placements =
        {
            new BackdropPlacement(new Vector3(-240f, 0f, 230f), 2.55f, 0f),
            new BackdropPlacement(new Vector3(-150f, 0f, 268f), 3.25f, -8f),
            new BackdropPlacement(new Vector3(-40f, 0f, 282f), 3.85f, 4f),
            new BackdropPlacement(new Vector3(86f, 0f, 268f), 3.2f, -5f),
            new BackdropPlacement(new Vector3(214f, 0f, 226f), 2.7f, 8f),

            new BackdropPlacement(new Vector3(-280f, 0f, 62f), 2.2f, 8f),
            new BackdropPlacement(new Vector3(282f, 0f, 44f), 2.25f, -8f),
            new BackdropPlacement(new Vector3(-246f, 0f, -118f), 1.85f, 3f),
            new BackdropPlacement(new Vector3(244f, 0f, -132f), 1.9f, -3f)
        };

        for (int i = 0; i < placements.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i % prefabs.Length]);
            if (prefab == null)
            {
                Debug.LogWarning("Missing background prefab: " + prefabs[i % prefabs.Length]);
                continue;
            }

            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.name = "BG_Backdrop_" + i.ToString("00");
            obj.transform.SetParent(group, true);

            BackdropPlacement placement = placements[i];
            Vector3 pos = placement.position;
            Vector3 terrainEdgePos = ClampToTerrain(pos, terrainPos, terrainSize);
            float edgeHeight = terrain.SampleHeight(terrainEdgePos) + terrainPos.y;

            pos.y = edgeHeight + 10f;
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(0f, LookAtCenterYaw(pos, center) + placement.yawOffset, 0f);
            obj.transform.localScale = Vector3.one * placement.scale;

            Bounds bounds;
            if (TryGetRendererBounds(obj, out bounds))
            {
                float targetBottom = edgeHeight + 2.5f;
                obj.transform.position += Vector3.up * (targetBottom - bounds.min.y);
            }
        }

        Selection.activeGameObject = group.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Fixed background ring as distant terrain backdrop.");
    }

    private struct BackdropPlacement
    {
        public readonly Vector3 position;
        public readonly float scale;
        public readonly float yawOffset;

        public BackdropPlacement(Vector3 position, float scale, float yawOffset)
        {
            this.position = position;
            this.scale = scale;
            this.yawOffset = yawOffset;
        }
    }

    private static Transform FindByPath(string path)
    {
        string[] parts = path.Split('/');
        GameObject root = GameObject.Find(parts[0]);
        if (root == null)
        {
            return null;
        }

        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    private static Vector3 ClampToTerrain(Vector3 position, Vector3 terrainPos, Vector3 terrainSize)
    {
        return new Vector3(
            Mathf.Clamp(position.x, terrainPos.x, terrainPos.x + terrainSize.x),
            position.y,
            Mathf.Clamp(position.z, terrainPos.z, terrainPos.z + terrainSize.z));
    }

    private static bool TryGetRendererBounds(GameObject obj, out Bounds bounds)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        bounds = new Bounds(obj.transform.position, Vector3.zero);
        if (renderers.Length == 0)
        {
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static float LookAtCenterYaw(Vector3 position, Vector3 center)
    {
        Vector3 dir = new Vector3(center.x - position.x, 0f, center.z - position.z);
        if (dir.sqrMagnitude < 0.001f)
        {
            return 0f;
        }
        return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
    }
}
