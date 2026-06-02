using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexSideWaterwayBuilder
{
    private const string RootName = "OW2_Hills_Village_Generated";

    public static void FastExecute()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }

        Transform old = root.transform.Find("Side_Natural_Waterway");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Vector3 anchor = new Vector3(82f, 0f, 38f);
        Vector3[] path = BuildPath(anchor);
        CreateWaterwayObjects(root.transform, terrain, path);

        Transform group = root.transform.Find("Side_Natural_Waterway");
        Selection.activeGameObject = group != null ? group.gameObject : root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Created fast side waterway objects near village side.");
    }

    public static void FinishNaturalSideWaterway()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        Vector3 anchor = new Vector3(82f, 0f, 38f);
        Vector3[] path = BuildPath(anchor);
        CarveShallowCreek(terrain, path);

        GameObject root = GameObject.Find(RootName);
        Transform group = root != null ? root.transform.Find("Side_Natural_Waterway") : null;
        if (group != null)
        {
            AddEndCaps(group, terrain, path);
            Selection.activeGameObject = group.gameObject;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Finished side waterway with shallow terrain carve and natural end caps.");
    }

    public static void Execute()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }

        Transform old = root.transform.Find("Side_Natural_Waterway");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Vector3 anchor = GetAnchor();
        Vector3[] path = BuildPath(anchor);

        CarveShallowCreek(terrain, path);
        CreateWaterwayObjects(root.transform, terrain, path);

        Transform group = root.transform.Find("Side_Natural_Waterway");
        Selection.activeGameObject = group != null ? group.gameObject : root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Created side natural waterway near anchor " + anchor);
    }

    private static Vector3 GetAnchor()
    {
        if (Selection.activeTransform != null && Selection.activeTransform.GetComponent<Terrain>() == null)
        {
            Vector3 p = Selection.activeTransform.position;
            if (p.x > -150f && p.x < 150f && p.z > -150f && p.z < 150f)
            {
                return p;
            }
        }

        return new Vector3(82f, 0f, 38f);
    }

    private static Vector3[] BuildPath(Vector3 anchor)
    {
        anchor.y = 0f;
        return new[]
        {
            anchor + new Vector3(-42f, 0f, 10f),
            anchor + new Vector3(-24f, 0f, 5f),
            anchor + new Vector3(-7f, 0f, 8f),
            anchor + new Vector3(12f, 0f, 2f),
            anchor + new Vector3(31f, 0f, -5f),
            anchor + new Vector3(48f, 0f, -3f)
        };
    }

    private static void CarveShallowCreek(Terrain terrain, Vector3[] path)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        Bounds bounds = PathBounds(path, 12f);
        int xBase = WorldXToHeightIndex(bounds.min.x, origin, size, res);
        int xEnd = WorldXToHeightIndex(bounds.max.x, origin, size, res);
        int zBase = WorldZToHeightIndex(bounds.min.z, origin, size, res);
        int zEnd = WorldZToHeightIndex(bounds.max.z, origin, size, res);
        int width = Mathf.Max(1, xEnd - xBase + 1);
        int height = Mathf.Max(1, zEnd - zBase + 1);
        float[,] heights = data.GetHeights(xBase, zBase, width, height);

        for (int z = 0; z < height; z++)
        {
            int globalZ = zBase + z;
            float wz = origin.z + globalZ / (float)(res - 1) * size.z;

            for (int x = 0; x < width; x++)
            {
                int globalX = xBase + x;
                float wx = origin.x + globalX / (float)(res - 1) * size.x;
                float d = DistanceToPath(new Vector2(wx, wz), path);
                if (d > 7.5f)
                {
                    continue;
                }

                float carve = 1f - Mathf.SmoothStep(0f, 1f, d / 7.5f);
                heights[z, x] = Mathf.Clamp01(heights[z, x] - carve * 0.014f);
            }
        }

        SmoothHeightPatch(heights, 1);
        data.SetHeightsDelayLOD(xBase, zBase, heights);
        data.SyncHeightmap();
        terrain.Flush();
    }

    private static void CreateWaterwayObjects(Transform parent, Terrain terrain, Vector3[] path)
    {
        GameObject group = new GameObject("Side_Natural_Waterway");
        group.transform.SetParent(parent, false);

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Packages/ToonScapes/Spring Isles/Models/Materials/TSI_Water_1A.mat");
        CreateCreekRibbon(group.transform, terrain, path, water);

        GameObject rockA = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Rocks/TSI_Rock_Small_03A.prefab");
        GameObject rockB = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Rocks/TSI_Rock_Small_05B.prefab");
        GameObject lily = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Water Vegetation/TSI_Water_Lily_02A.prefab");
        GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Grass_Patch_03A.prefab");
        GameObject splash = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Particles/TSI_Water_Particles_01A.prefab");

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 tangent = PathTangent(path, i);
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            PlacePrefab(i % 2 == 0 ? rockA : rockB, "Side_Creek_Rock_" + i.ToString("00"), path[i] + side * (i % 2 == 0 ? -3.3f : 3.1f), i * 37f, group.transform, terrain, 0.22f + (i % 3) * 0.04f);
            PlacePrefab(grass, "Side_Creek_Grass_" + i.ToString("00"), path[i] - side * (i % 2 == 0 ? 4.6f : -4.3f), i * 21f, group.transform, terrain, 0.58f);
        }

        PlacePrefab(lily, "Side_Creek_Lily_00", path[2] + new Vector3(0.6f, 0f, -0.4f), 18f, group.transform, terrain, 0.72f);
        PlacePrefab(lily, "Side_Creek_Lily_01", path[4] + new Vector3(-0.8f, 0f, 0.5f), -14f, group.transform, terrain, 0.66f);
        PlacePrefab(splash, "Side_Creek_Water_Particles", path[3], 0f, group.transform, terrain, 0.42f);
        AddEndCaps(group.transform, terrain, path);
    }

    private static void AddEndCaps(Transform group, Terrain terrain, Vector3[] path)
    {
        DeleteChildIfExists(group, "Side_Creek_Start_Cap_Rock");
        DeleteChildIfExists(group, "Side_Creek_End_Cap_Rock");
        DeleteChildIfExists(group, "Side_Creek_Start_Cap_Grass");
        DeleteChildIfExists(group, "Side_Creek_End_Cap_Grass");

        GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Rocks/TSI_Rock_Small_04B.prefab");
        GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Grass_Patch_04A.prefab");

        Vector3 startDir = (path[1] - path[0]).normalized;
        Vector3 endDir = (path[path.Length - 1] - path[path.Length - 2]).normalized;
        PlacePrefab(rock, "Side_Creek_Start_Cap_Rock", path[0] - startDir * 1.2f, 42f, group, terrain, 0.36f);
        PlacePrefab(rock, "Side_Creek_End_Cap_Rock", path[path.Length - 1] + endDir * 1.2f, -28f, group, terrain, 0.34f);
        PlacePrefab(grass, "Side_Creek_Start_Cap_Grass", path[0] - startDir * 2.5f + new Vector3(0f, 0f, 1.5f), 11f, group, terrain, 0.72f);
        PlacePrefab(grass, "Side_Creek_End_Cap_Grass", path[path.Length - 1] + endDir * 2.4f + new Vector3(0f, 0f, -1.4f), -9f, group, terrain, 0.7f);
    }

    private static void CreateCreekRibbon(Transform parent, Terrain terrain, Vector3[] path, Material water)
    {
        const float width = 4.2f;
        Vector3[] vertices = new Vector3[path.Length * 2];
        Vector2[] uv = new Vector2[path.Length * 2];
        int[] triangles = new int[(path.Length - 1) * 6];
        float distance = 0f;

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 tangent = PathTangent(path, i);
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            Vector3 center = path[i];
            center.y = GroundY(terrain, center.x, center.z) + 0.34f;
            if (i > 0)
            {
                distance += Vector3.Distance(path[i - 1], path[i]);
            }

            float localWidth = width * (i == 0 || i == path.Length - 1 ? 0.82f : 1f);
            vertices[i * 2] = center - side * localWidth * 0.5f;
            vertices[i * 2 + 1] = center + side * localWidth * 0.5f;
            uv[i * 2] = new Vector2(0f, distance * 0.16f);
            uv[i * 2 + 1] = new Vector2(1f, distance * 0.16f);
        }

        for (int i = 0; i < path.Length - 1; i++)
        {
            int v = i * 2;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Side_Creek_Water_Ribbon_Mesh";
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject obj = new GameObject("Side_Creek_Water_Ribbon");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        if (water != null)
        {
            renderer.sharedMaterial = water;
        }
    }

    private static void PlacePrefab(GameObject prefab, string name, Vector3 pos, float yRot, Transform parent, Terrain terrain, float scale)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.name = name;
        obj.transform.SetParent(parent, true);
        obj.transform.position = new Vector3(pos.x, GroundY(terrain, pos.x, pos.z) + 0.08f, pos.z);
        obj.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        obj.transform.localScale = Vector3.one * scale;
    }

    private static void DeleteChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Vector3 PathTangent(Vector3[] path, int index)
    {
        Vector3 prev = path[Mathf.Max(0, index - 1)];
        Vector3 next = path[Mathf.Min(path.Length - 1, index + 1)];
        return new Vector3(next.x - prev.x, 0f, next.z - prev.z).normalized;
    }

    private static Bounds PathBounds(Vector3[] path, float padding)
    {
        Bounds bounds = new Bounds(path[0], Vector3.zero);
        for (int i = 1; i < path.Length; i++)
        {
            bounds.Encapsulate(path[i]);
        }

        bounds.Expand(new Vector3(padding * 2f, 0f, padding * 2f));
        return bounds;
    }

    private static void SmoothHeightPatch(float[,] heights, int passes)
    {
        int h = heights.GetLength(0);
        int w = heights.GetLength(1);
        for (int pass = 0; pass < passes; pass++)
        {
            float[,] copy = (float[,])heights.Clone();
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    heights[y, x] = copy[y, x] * 0.62f
                        + (copy[y - 1, x] + copy[y + 1, x] + copy[y, x - 1] + copy[y, x + 1]) * 0.095f;
                }
            }
        }
    }

    private static float DistanceToPath(Vector2 point, Vector3[] path)
    {
        float best = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = new Vector2(path[i].x, path[i].z);
            Vector2 b = new Vector2(path[i + 1].x, path[i + 1].z);
            best = Mathf.Min(best, DistanceToSegment(point, a, b));
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float GroundY(Terrain terrain, float x, float z)
    {
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    private static int WorldXToHeightIndex(float worldX, Vector3 origin, Vector3 size, int res)
    {
        float normalized = Mathf.InverseLerp(origin.x, origin.x + size.x, worldX);
        return Mathf.Clamp(Mathf.RoundToInt(normalized * (res - 1)), 0, res - 1);
    }

    private static int WorldZToHeightIndex(float worldZ, Vector3 origin, Vector3 size, int res)
    {
        float normalized = Mathf.InverseLerp(origin.z, origin.z + size.z, worldZ);
        return Mathf.Clamp(Mathf.RoundToInt(normalized * (res - 1)), 0, res - 1);
    }
}
