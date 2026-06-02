using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexSmallWaterwayFix
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

        DeleteIfExists(root.transform, "Water_And_Bridges");
        DeleteIfExists(root.transform, "Small_Waterway");

        Transform dressing = root.transform.Find("ToonScapes_Environment_Dressing");
        if (dressing != null)
        {
            DeleteIfExists(dressing, "Water_Particles_And_Plants");
        }

        Vector3[] creek =
        {
            new Vector3(-112f, 0f, 62f),
            new Vector3(-98f, 0f, 74f),
            new Vector3(-80f, 0f, 72f),
            new Vector3(-64f, 0f, 84f),
            new Vector3(-50f, 0f, 80f)
        };

        CreateSmallCreekObjects(root.transform, terrain, creek);

        Selection.activeGameObject = root.transform.Find("Small_Waterway").gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Fast removed old water objects and created a compact small creek.");
    }

    public static void FillOldLongStreamFast()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float stepX = size.x / (res - 1);

        int xBase = WorldXToHeightIndex(18f, origin, size, res);
        int xEnd = WorldXToHeightIndex(82f, origin, size, res);
        int width = Mathf.Max(1, xEnd - xBase + 1);
        float[,] heights = data.GetHeights(xBase, 0, width, res);
        int bankOffset = Mathf.RoundToInt(22f / stepX);

        for (int z = 0; z < res; z++)
        {
            float nz = z / (float)(res - 1);
            float wz = origin.z + nz * size.z;
            float oldStreamX = 48f + Mathf.Sin(wz * 0.035f) * 8f;

            for (int localX = 0; localX < width; localX++)
            {
                int globalX = xBase + localX;
                float nx = globalX / (float)(res - 1);
                float wx = origin.x + nx * size.x;
                float d = Mathf.Abs(wx - oldStreamX);
                if (d > 13f)
                {
                    continue;
                }

                int lx = Mathf.Clamp(localX - bankOffset, 0, width - 1);
                int rx = Mathf.Clamp(localX + bankOffset, 0, width - 1);
                float bank = Mathf.Max(heights[z, lx], heights[z, rx]) - 0.004f;
                float blend = 1f - Mathf.SmoothStep(0f, 1f, d / 13f);
                heights[z, localX] = Mathf.Lerp(heights[z, localX], Mathf.Max(heights[z, localX], bank), blend * 0.9f);
            }
        }

        data.SetHeights(xBase, 0, heights);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Filled old long stream trench in a narrow terrain band.");
    }

    public static void RemoveAllWaterwaysAndFlattenVillage()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            DeleteIfExists(root.transform, "Water_And_Bridges");
            DeleteIfExists(root.transform, "Small_Waterway");

            Transform dressing = root.transform.Find("ToonScapes_Environment_Dressing");
            if (dressing != null)
            {
                DeleteIfExists(dressing, "Water_Particles_And_Plants");
            }
        }

        FlattenOldVillageCrossing(terrain);

        Selection.activeGameObject = terrain.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Removed all generated waterway objects and flattened the village-crossing trench.");
    }

    public static void RemoveUglyVillageRiverWide()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        RemoveSceneWaterRenderers();
        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            DeleteIfExists(root.transform, "Water_And_Bridges");
            DeleteIfExists(root.transform, "Small_Waterway");
        }

        FlattenWideVillageRiver(terrain);

        Selection.activeGameObject = terrain.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Removed visible water renderers and filled the wide river trench through the village.");
    }

    public static void ResetTerrainWithoutWaterTrench()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        RemoveSceneWaterRenderers();
        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            DeleteIfExists(root.transform, "Water_And_Bridges");
            DeleteIfExists(root.transform, "Small_Waterway");
            Transform dressing = root.transform.Find("ToonScapes_Environment_Dressing");
            if (dressing != null)
            {
                DeleteIfExists(dressing, "Water_Particles_And_Plants");
            }
        }

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        data.size = new Vector3(300f, 45f, 300f);

        float[,] heights = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1);
                float nz = z / (float)(res - 1);
                float wx = Mathf.Lerp(-150f, 150f, nx);
                float wz = Mathf.Lerp(-150f, 150f, nz);

                float height = 0.46f;
                height += Mathf.PerlinNoise(nx * 3.5f + 2f, nz * 3.5f + 9f) * 0.05f;
                height += Mathf.PerlinNoise(nx * 13f + 8f, nz * 13f + 1f) * 0.012f;
                height += Hill(wx, wz, -112f, 82f, 64f, 0.20f);
                height += Hill(wx, wz, 118f, 88f, 70f, 0.22f);
                height += Hill(wx, wz, -118f, -92f, 54f, 0.13f);
                height += Hill(wx, wz, 112f, -104f, 50f, 0.15f);

                float village = SmoothCircle(wx, wz, 52f, -42f, 48f);
                height = Mathf.Lerp(height, 0.535f, village * 0.94f);

                heights[z, x] = Mathf.Clamp01(height);
            }
        }

        data.SetHeights(0, 0, heights);
        ResetTerrainAlphamapToBase(data);
        terrain.Flush();

        Selection.activeGameObject = terrain.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Reset terrain heightmap without the old water trench.");
    }

    public static void DiagnoseLargeRenderersNearVillage()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            Bounds b = r.bounds;
            bool nearVillage = b.center.x > -20f && b.center.x < 130f && b.center.z > -150f && b.center.z < 80f;
            bool large = b.size.x > 18f || b.size.z > 18f;
            if (!nearVillage || !large)
            {
                continue;
            }

            string matNames = "";
            Material[] mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                {
                    matNames += mats[m].name + " ";
                }
            }

            Debug.Log("Large renderer near village: " + GetPath(r.transform) + " bounds=" + b.size + " center=" + b.center + " mats=" + matNames);
            count++;
            if (count >= 40)
            {
                break;
            }
        }

        Debug.Log("Diagnosed " + count + " large renderers near village.");
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

        DeleteIfExists(root.transform, "Water_And_Bridges");
        DeleteIfExists(root.transform, "Small_Waterway");

        Transform dressing = root.transform.Find("ToonScapes_Environment_Dressing");
        if (dressing != null)
        {
            DeleteIfExists(dressing, "Water_Particles_And_Plants");
        }

        Vector3[] creek =
        {
            new Vector3(-112f, 0f, 62f),
            new Vector3(-98f, 0f, 74f),
            new Vector3(-80f, 0f, 72f),
            new Vector3(-64f, 0f, 84f),
            new Vector3(-50f, 0f, 80f)
        };

        RestoreOldLongStream(terrain);
        CarveSmallCreek(terrain, creek);
        CreateSmallCreekObjects(root.transform, terrain, creek);

        Selection.activeGameObject = root.transform.Find("Small_Waterway").gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Removed old long waterway and created a compact small creek.");
    }

    private static void RestoreOldLongStream(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float stepX = size.x / (res - 1);
        int bankOffset = Mathf.RoundToInt(20f / stepX);

        for (int z = 0; z < res; z++)
        {
            float nz = z / (float)(res - 1);
            float wz = origin.z + nz * size.z;
            float oldStreamX = 48f + Mathf.Sin(wz * 0.035f) * 8f;

            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1);
                float wx = origin.x + nx * size.x;
                float d = Mathf.Abs(wx - oldStreamX);
                if (d > 13f)
                {
                    continue;
                }

                int lx = Mathf.Clamp(x - bankOffset, 0, res - 1);
                int rx = Mathf.Clamp(x + bankOffset, 0, res - 1);
                float bank = Mathf.Max(heights[z, lx], heights[z, rx]) - 0.004f;
                float blend = 1f - Mathf.SmoothStep(0f, 1f, d / 13f);
                heights[z, x] = Mathf.Lerp(heights[z, x], Mathf.Max(heights[z, x], bank), blend * 0.92f);
            }
        }

        data.SetHeights(0, 0, heights);
    }

    private static void CarveSmallCreek(Terrain terrain, Vector3[] creek)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        for (int z = 0; z < res; z++)
        {
            float nz = z / (float)(res - 1);
            float wz = origin.z + nz * size.z;
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1);
                float wx = origin.x + nx * size.x;
                float d = DistanceToPath(new Vector2(wx, wz), creek);
                if (d > 7.5f)
                {
                    continue;
                }

                float blend = 1f - Mathf.SmoothStep(0f, 1f, d / 7.5f);
                float shallowDrop = 0.021f * blend;
                heights[z, x] = Mathf.Clamp01(heights[z, x] - shallowDrop);
            }
        }

        data.SetHeights(0, 0, heights);
    }

    private static void CreateSmallCreekObjects(Transform parent, Terrain terrain, Vector3[] creek)
    {
        GameObject group = new GameObject("Small_Waterway");
        group.transform.SetParent(parent, false);

        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Packages/ToonScapes/Spring Isles/Models/Materials/TSI_Water_1A.mat");
        GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Rocks/TSI_Rock_Small_03A.prefab");
        GameObject lily = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Water Vegetation/TSI_Water_Lily_03A.prefab");
        GameObject splash = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Particles/TSI_Water_Particles_01A.prefab");

        CreateCreekRibbon(group.transform, terrain, creek, water);

        for (int i = 0; i < creek.Length; i++)
        {
            Vector3 p = creek[i];
            PlacePrefab(rock, "Creek_Bank_Rock_" + i.ToString("00"), p + new Vector3(i % 2 == 0 ? -3.8f : 3.4f, 0f, 1.6f), i * 31f, group.transform, terrain, 0.85f);
        }

        PlacePrefab(lily, "Small_Creek_Lily_00", creek[2] + new Vector3(-1.3f, 0f, 0.7f), 16f, group.transform, terrain, 0.75f);
        PlacePrefab(lily, "Small_Creek_Lily_01", creek[3] + new Vector3(1.1f, 0f, -0.9f), -18f, group.transform, terrain, 0.68f);
        PlacePrefab(splash, "Small_Creek_Water_Particles", creek[1], 0f, group.transform, terrain, 0.5f);
    }

    private static void FlattenOldVillageCrossing(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        int xBase = WorldXToHeightIndex(12f, origin, size, res);
        int xEnd = WorldXToHeightIndex(92f, origin, size, res);
        int zBase = WorldZToHeightIndex(-132f, origin, size, res);
        int zEnd = WorldZToHeightIndex(46f, origin, size, res);
        int width = Mathf.Max(1, xEnd - xBase + 1);
        int height = Mathf.Max(1, zEnd - zBase + 1);
        float[,] heights = data.GetHeights(xBase, zBase, width, height);

        for (int z = 0; z < height; z++)
        {
            int globalZ = zBase + z;
            float nz = globalZ / (float)(res - 1);
            float wz = origin.z + nz * size.z;
            float oldStreamX = 48f + Mathf.Sin(wz * 0.035f) * 8f;

            for (int x = 0; x < width; x++)
            {
                int globalX = xBase + x;
                float nx = globalX / (float)(res - 1);
                float wx = origin.x + nx * size.x;
                float d = Mathf.Abs(wx - oldStreamX);
                if (d > 18f)
                {
                    continue;
                }

                float villageBlend = SmoothCircle(wx, wz, 52f, -42f, 48f);
                float target = Mathf.Lerp(heights[z, x], 0.535f, villageBlend);

                int left = Mathf.Clamp(x - 20, 0, width - 1);
                int right = Mathf.Clamp(x + 20, 0, width - 1);
                float bankTarget = Mathf.Max(heights[z, left], heights[z, right]) - 0.002f;
                target = Mathf.Max(target, bankTarget);

                float streamBlend = 1f - Mathf.SmoothStep(0f, 1f, d / 18f);
                heights[z, x] = Mathf.Lerp(heights[z, x], target, streamBlend);
            }
        }

        data.SetHeightsDelayLOD(xBase, zBase, heights);
        terrain.ApplyDelayedHeightmapModification();
        terrain.Flush();
    }

    private static void FlattenWideVillageRiver(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        int xBase = WorldXToHeightIndex(-6f, origin, size, res);
        int xEnd = WorldXToHeightIndex(112f, origin, size, res);
        int zBase = WorldZToHeightIndex(-146f, origin, size, res);
        int zEnd = WorldZToHeightIndex(62f, origin, size, res);
        int width = Mathf.Max(1, xEnd - xBase + 1);
        int height = Mathf.Max(1, zEnd - zBase + 1);
        float[,] heights = data.GetHeights(xBase, zBase, width, height);

        for (int z = 0; z < height; z++)
        {
            int globalZ = zBase + z;
            float nz = globalZ / (float)(res - 1);
            float wz = origin.z + nz * size.z;
            float oldStreamX = 48f + Mathf.Sin(wz * 0.035f) * 8f;

            for (int x = 0; x < width; x++)
            {
                int globalX = xBase + x;
                float nx = globalX / (float)(res - 1);
                float wx = origin.x + nx * size.x;
                float d = Mathf.Abs(wx - oldStreamX);
                if (d > 34f)
                {
                    continue;
                }

                float villageBlend = SmoothCircle(wx, wz, 52f, -42f, 58f);
                float corridorBlend = 1f - Mathf.SmoothStep(0f, 1f, d / 34f);

                float target = Mathf.Lerp(heights[z, x], 0.535f, villageBlend);
                if (villageBlend < 0.15f)
                {
                    int left = Mathf.Clamp(x - 36, 0, width - 1);
                    int right = Mathf.Clamp(x + 36, 0, width - 1);
                    target = Mathf.Max(target, Mathf.Max(heights[z, left], heights[z, right]) - 0.001f);
                }

                heights[z, x] = Mathf.Lerp(heights[z, x], target, corridorBlend * 0.98f);
            }
        }

        SmoothHeightPatch(heights, 2);
        data.SetHeightsDelayLOD(xBase, zBase, heights);
        data.SyncHeightmap();
        terrain.Flush();
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
                    heights[y, x] = copy[y, x] * 0.5f
                        + (copy[y - 1, x] + copy[y + 1, x] + copy[y, x - 1] + copy[y, x + 1]) * 0.125f;
                }
            }
        }
    }

    private static void RemoveSceneWaterRenderers()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.gameObject == null)
            {
                continue;
            }

            bool isWater = renderer.gameObject.name.ToLowerInvariant().Contains("water");
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                if (materials[m] != null && materials[m].name.ToLowerInvariant().Contains("water"))
                {
                    isWater = true;
                    break;
                }
            }

            if (isWater && renderer.transform.GetComponentInParent<Terrain>() == null)
            {
                Object.DestroyImmediate(renderer.gameObject);
            }
        }
    }

    private static void ResetTerrainAlphamapToBase(TerrainData data)
    {
        int layers = data.alphamapLayers;
        if (layers <= 0)
        {
            return;
        }

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        float[,,] alpha = new float[height, width, layers];
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                alpha[z, x, 0] = 1f;
                for (int l = 1; l < layers; l++)
                {
                    alpha[z, x, l] = 0f;
                }
            }
        }

        data.SetAlphamaps(0, 0, alpha);
    }

    private static float Hill(float x, float z, float cx, float cz, float radius, float amount)
    {
        float t = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz)) / radius);
        return t * t * (3f - 2f * t) * amount;
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static void CreateCreekRibbon(Transform parent, Terrain terrain, Vector3[] creek, Material water)
    {
        const float width = 4.8f;
        Vector3[] vertices = new Vector3[creek.Length * 2];
        Vector2[] uv = new Vector2[creek.Length * 2];
        int[] triangles = new int[(creek.Length - 1) * 6];

        float distance = 0f;
        for (int i = 0; i < creek.Length; i++)
        {
            Vector3 prev = creek[Mathf.Max(0, i - 1)];
            Vector3 next = creek[Mathf.Min(creek.Length - 1, i + 1)];
            Vector3 tangent = new Vector3(next.x - prev.x, 0f, next.z - prev.z).normalized;
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            Vector3 center = creek[i];
            center.y = GroundY(terrain, center.x, center.z) + 0.28f;

            if (i > 0)
            {
                distance += Vector3.Distance(creek[i - 1], creek[i]);
            }

            vertices[i * 2] = center - side * (width * 0.5f);
            vertices[i * 2 + 1] = center + side * (width * 0.5f);
            uv[i * 2] = new Vector2(0f, distance * 0.15f);
            uv[i * 2 + 1] = new Vector2(1f, distance * 0.15f);
        }

        for (int i = 0; i < creek.Length - 1; i++)
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
        mesh.name = "Small_Creek_Water_Ribbon_Mesh";
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject obj = new GameObject("Small_Creek_Water_Ribbon");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        if (water != null)
        {
            renderer.sharedMaterial = water;
        }
    }

    private static void DeleteIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
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
        obj.transform.position = new Vector3(pos.x, GroundY(terrain, pos.x, pos.z) + 0.12f, pos.z);
        obj.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        obj.transform.localScale = Vector3.one * scale;
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

    private static float SmoothCircle(float x, float z, float cx, float cz, float radius)
    {
        float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
        return Mathf.Clamp01(1f - Mathf.InverseLerp(radius * 0.65f, radius, d));
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
}
