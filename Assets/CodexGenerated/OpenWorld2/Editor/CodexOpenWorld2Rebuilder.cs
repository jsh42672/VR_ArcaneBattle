using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexOpenWorld2Rebuilder
{
    private const string ScenePath = "Assets/Scenes/OPenWorld2.unity";
    private const string TerrainDataPath = "Assets/CodexGenerated/OpenWorld2/OW2_HillsVillage_300_TerrainData.asset";

    public static void Execute()
    {
        EnsureFolder("Assets/CodexGenerated/OpenWorld2");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "OPenWorld2";

        TerrainData terrainData = BuildTerrain();
        AssetDatabase.DeleteAsset(TerrainDataPath);
        AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        AssetDatabase.SaveAssets();

        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = "OW2_HillsVillage_Terrain_300x300";
        terrainObject.transform.position = new Vector3(-150f, 0f, -150f);
        Terrain terrain = terrainObject.GetComponent<Terrain>();

        GameObject root = new GameObject("OW2_Hills_Village_Generated");
        GameObject village = new GameObject("Village_Object");
        village.transform.SetParent(root.transform, false);
        village.transform.position = new Vector3(52f, GroundY(terrain, 52f, -42f), -42f);

        PlaceHouses(village.transform, terrain);
        PlaceFence(village.transform, terrain);
        PlaceTreesAndRocks(root.transform, terrain);
        PlaceSegmentedWater(root.transform, terrain);
        CreateLightingAndCamera();

        RenderSettings.fog = true;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.58f, 0.72f, 0.74f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Rebuilt OPenWorld2 with terrain, village, houses, fence, rocks, trees, and segmented water.");
    }

    private static TerrainData BuildTerrain()
    {
        const int res = 513;
        TerrainData data = new TerrainData();
        data.heightmapResolution = res;
        data.size = new Vector3(300f, 45f, 300f);
        float[,] h = new float[res, res];

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

                float village = SmoothCircle(wx, wz, 52f, -42f, 42f);
                height = Mathf.Lerp(height, 0.535f, village * 0.9f);

                float streamX = 48f + Mathf.Sin(wz * 0.035f) * 8f;
                float stream = Mathf.Exp(-Mathf.Pow((wx - streamX) / 8f, 2f));
                height -= stream * 0.13f;

                h[z, x] = Mathf.Clamp01(height);
            }
        }

        data.SetHeights(0, 0, h);
        return data;
    }

    private static void PlaceHouses(Transform parent, Terrain terrain)
    {
        PlacePrefab("Assets/Prefabs/house3.prefab", "house3", new Vector3(43f, 0f, -54f), 12f, parent, terrain);
        PlacePrefab("Assets/Prefabs/house4.prefab", "house4", new Vector3(70f, 0f, -26f), -24f, parent, terrain);
        PlacePrefab("Assets/Prefabs/house.prefab", "house", new Vector3(32f, 0f, -22f), 38f, parent, terrain);
    }

    private static void PlaceFence(Transform parent, Terrain terrain)
    {
        GameObject fenceRoot = new GameObject("Fence");
        fenceRoot.transform.SetParent(parent, false);
        GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Props/Wood Props/TSI_Wood_Fence_01A.prefab");
        Material fallback = MakeMat("Fence_Fallback", new Color(0.34f, 0.18f, 0.1f));

        Vector3 center = new Vector3(52f, 0f, -42f);
        float radius = 42f;
        for (int i = 0; i < 64; i++)
        {
            float deg = i * 360f / 64f;
            if (Mathf.Abs(Mathf.DeltaAngle(deg, 0f)) < 15f || Mathf.Abs(Mathf.DeltaAngle(deg, 180f)) < 15f)
            {
                continue;
            }

            float rad = deg * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            pos.y = GroundY(terrain, pos.x, pos.z) + 0.25f;
            GameObject obj = fencePrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Village_Fence_" + i.ToString("00");
            obj.transform.SetParent(fenceRoot.transform, true);
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(0f, -deg + 90f, 0f);
            if (fencePrefab == null)
            {
                obj.transform.localScale = new Vector3(4.2f, 1.2f, 0.2f);
                obj.GetComponent<MeshRenderer>().sharedMaterial = fallback;
            }
        }
    }

    private static void PlaceTreesAndRocks(Transform parent, Terrain terrain)
    {
        GameObject nature = new GameObject("Hills_Forest_Rocks");
        nature.transform.SetParent(parent, false);
        GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Trees/TSI_Tree_Branch_01A.prefab");
        GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/ToonScapes/Spring Isles/Prefabs/Rocks/TSI_Rock_Large_01A.prefab");

        Vector3[] treePos =
        {
            new Vector3(-84f,0f,70f), new Vector3(-44f,0f,94f), new Vector3(96f,0f,72f),
            new Vector3(118f,0f,-68f), new Vector3(-98f,0f,-74f), new Vector3(18f,0f,88f),
            new Vector3(102f,0f,10f), new Vector3(-72f,0f,-18f), new Vector3(12f,0f,-112f)
        };

        for (int i = 0; i < treePos.Length; i++)
        {
            PlacePrefab(tree, "Hill_Tree_" + i.ToString("00"), treePos[i], i * 29f, nature.transform, terrain, 1.4f);
        }

        Vector3[] rockPos =
        {
            new Vector3(-116f,0f,40f), new Vector3(126f,0f,58f), new Vector3(94f,0f,-112f),
            new Vector3(-122f,0f,-104f), new Vector3(8f,0f,58f), new Vector3(78f,0f,28f)
        };

        for (int i = 0; i < rockPos.Length; i++)
        {
            PlacePrefab(rock, "Hill_Rock_" + i.ToString("00"), rockPos[i], i * 41f, nature.transform, terrain, 1.1f);
        }
    }

    private static void PlaceSegmentedWater(Transform parent, Terrain terrain)
    {
        GameObject waterRoot = new GameObject("Water_And_Bridges");
        waterRoot.transform.SetParent(parent, false);
        Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Packages/ToonScapes/Spring Isles/Models/Materials/TSI_Water_1A.mat");
        if (water == null)
        {
            water = MakeMat("Water_Fallback", new Color(0.25f, 0.67f, 0.9f, 0.72f));
        }

        Vector3[] points =
        {
            new Vector3(48f,0f,-126f), new Vector3(51f,0f,-104f), new Vector3(50f,0f,-82f),
            new Vector3(46f,0f,-60f), new Vector3(43f,0f,-38f), new Vector3(46f,0f,-16f),
            new Vector3(53f,0f,8f), new Vector3(56f,0f,32f)
        };

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            Vector3 mid = (a + b) * 0.5f;
            mid.y = GroundY(terrain, mid.x, mid.z) + 0.08f;
            Vector3 dir = b - a;
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Stream_Water_Short_" + i.ToString("00");
            plane.transform.SetParent(waterRoot.transform, true);
            plane.transform.position = mid;
            plane.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z).normalized, Vector3.up);
            plane.transform.localScale = new Vector3(0.36f, 1f, dir.magnitude / 10f * 0.88f);
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.GetComponent<MeshRenderer>().sharedMaterial = water;
        }
    }

    private static void CreateLightingAndCamera()
    {
        GameObject lightObj = new GameObject("Sun_Key_Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera camera = cameraObj.AddComponent<Camera>();
        cameraObj.transform.position = new Vector3(62f, 64f, -128f);
        cameraObj.transform.rotation = Quaternion.Euler(32f, -5f, 0f);
        camera.fieldOfView = 52f;
    }

    private static void PlacePrefab(string path, string name, Vector3 pos, float yRot, Transform parent, Terrain terrain)
    {
        PlacePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), name, pos, yRot, parent, terrain, 1f);
    }

    private static void PlacePrefab(GameObject prefab, string name, Vector3 pos, float yRot, Transform parent, Terrain terrain, float scale)
    {
        GameObject obj = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, true);
        obj.transform.position = new Vector3(pos.x, GroundY(terrain, pos.x, pos.z), pos.z);
        obj.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        obj.transform.localScale = Vector3.one * scale;
    }

    private static float GroundY(Terrain terrain, float x, float z)
    {
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    private static float Hill(float x, float z, float cx, float cz, float radius, float amount)
    {
        float t = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz)) / radius);
        return t * t * (3f - 2f * t) * amount;
    }

    private static float SmoothCircle(float x, float z, float cx, float cz, float radius)
    {
        float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
        return Mathf.Clamp01(1f - Mathf.InverseLerp(radius * 0.65f, radius, d));
    }

    private static Material MakeMat(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        Material material = new Material(shader);
        material.name = name;
        material.color = color;
        return material;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
