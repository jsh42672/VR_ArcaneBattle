using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexToonScapesEnvironmentDresser
{
    private const string RootName = "OW2_Hills_Village_Generated";
    private const string DressingName = "ToonScapes_Environment_Dressing";

    public static void Execute()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found for ToonScapes dressing.");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }

        Transform old = root.transform.Find(DressingName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject dressing = new GameObject(DressingName);
        dressing.transform.SetParent(root.transform, false);

        ApplyEnvironmentSettings(terrain);
        CopyTerrainLayers(terrain);
        PlaceBackground(dressing.transform);
        PlaceForestAndPlants(dressing.transform, terrain);
        PlaceWaterDetails(dressing.transform, terrain);
        PlaceVillageAtmosphere(dressing.transform, terrain);

        Selection.activeGameObject = dressing;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Applied ToonScapes environment dressing to OPenWorld2.");
    }

    private static void ApplyEnvironmentSettings(Terrain terrain)
    {
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Packages/ToonScapes/Spring Isles/Skybox/Materials/TSI_Skybox_01A.mat");
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.55f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.21f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.56f, 0.72f, 0.76f);
        RenderSettings.fogDensity = 0.0022f;

        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.name = "Sun_Key_Light";
            sun.type = LightType.Directional;
            sun.intensity = 1.45f;
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 4f);
        }

        DynamicGI.UpdateEnvironment();
    }

    private static void CopyTerrainLayers(Terrain terrain)
    {
        TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Packages/ToonScapes/Spring Isles/Terrain/Terrain Data/TSI_Terrain_01.asset");
        if (source == null || source.terrainLayers == null || source.terrainLayers.Length == 0)
        {
            return;
        }

        terrain.terrainData.terrainLayers = source.terrainLayers;
        int width = terrain.terrainData.alphamapWidth;
        int height = terrain.terrainData.alphamapHeight;
        int layers = source.terrainLayers.Length;
        float[,,] alpha = new float[height, width, layers];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = x / (float)(width - 1);
                float nz = y / (float)(height - 1);
                float wx = Mathf.Lerp(-150f, 150f, nx);
                float wz = Mathf.Lerp(-150f, 150f, nz);
                float h = terrain.terrainData.GetInterpolatedHeight(nx, nz) / terrain.terrainData.size.y;
                int layer = 0;
                if (layers > 1 && h > 0.58f)
                {
                    layer = 1;
                }
                if (layers > 2 && Mathf.Abs(wx - (48f + Mathf.Sin(wz * 0.035f) * 8f)) < 12f)
                {
                    layer = 2;
                }

                for (int i = 0; i < layers; i++)
                {
                    alpha[y, x, i] = i == layer ? 1f : 0f;
                }
            }
        }

        terrain.terrainData.SetAlphamaps(0, 0, alpha);
    }

    private static void PlaceBackground(Transform parent)
    {
        Transform group = NewGroup("Background_Ring", parent);
        string[] prefabs =
        {
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Mountain_01A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Mountain_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Mountain_03A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Hill_01A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Hill_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Background/TSI_BG_Cliff_01A.prefab"
        };

        Vector3[] positions =
        {
            new Vector3(-135f, 20f, 142f), new Vector3(-55f, 18f, 154f), new Vector3(45f, 19f, 152f),
            new Vector3(130f, 18f, 118f), new Vector3(-148f, 17f, -48f), new Vector3(150f, 18f, -74f),
            new Vector3(-86f, 19f, -148f), new Vector3(76f, 18f, -154f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject obj = InstantiatePrefab(prefabs[i % prefabs.Length], "BG_Setpiece_" + i.ToString("00"), group);
            if (obj == null)
            {
                continue;
            }
            obj.transform.position = positions[i];
            obj.transform.rotation = Quaternion.Euler(0f, LookAtCenterYaw(positions[i]), 0f);
            obj.transform.localScale = Vector3.one * (i % 3 == 0 ? 2.2f : 1.7f);
        }
    }

    private static void PlaceForestAndPlants(Transform parent, Terrain terrain)
    {
        Transform trees = NewGroup("Trees_And_Shrubs", parent);
        string[] treePrefabs =
        {
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Trees/TSI_Broadleaf_Tree_03A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Trees/TSI_Springleaf_Tree_03A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Trees/TSI_Amberleaf_Tree_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Trees/TSI_Blossom_Tree_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Trees/TSI_Broadleaf_Tree_05A.prefab"
        };

        Vector3[] treePositions =
        {
            new Vector3(-116f,0f,88f), new Vector3(-86f,0f,110f), new Vector3(-48f,0f,102f), new Vector3(4f,0f,108f),
            new Vector3(88f,0f,112f), new Vector3(126f,0f,64f), new Vector3(124f,0f,8f), new Vector3(116f,0f,-82f),
            new Vector3(78f,0f,-120f), new Vector3(20f,0f,-126f), new Vector3(-48f,0f,-116f), new Vector3(-112f,0f,-84f),
            new Vector3(-128f,0f,-18f), new Vector3(-96f,0f,28f), new Vector3(-20f,0f,60f), new Vector3(94f,0f,46f),
            new Vector3(88f,0f,-4f), new Vector3(10f,0f,-88f)
        };

        for (int i = 0; i < treePositions.Length; i++)
        {
            GameObject obj = InstantiatePrefab(treePrefabs[i % treePrefabs.Length], "Toon_Tree_" + i.ToString("00"), trees);
            PlaceOnGround(obj, terrain, treePositions[i], i * 37f, 0.95f + (i % 4) * 0.12f);
        }

        string[] plantPrefabs =
        {
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Grass_Patch_03A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Flower_Patch_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Bush_02A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Flower_Bush_01A.prefab",
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Bamboo/TSI_Bamboo_03A.prefab"
        };

        Transform plants = NewGroup("Ground_Plants", parent);
        for (int i = 0; i < 48; i++)
        {
            float angle = i * 137.5f * Mathf.Deg2Rad;
            float radius = 28f + (i % 9) * 9f;
            Vector3 pos = new Vector3(52f + Mathf.Cos(angle) * radius, 0f, -42f + Mathf.Sin(angle) * radius);
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(52f, -42f)) < 25f)
            {
                continue;
            }
            GameObject obj = InstantiatePrefab(plantPrefabs[i % plantPrefabs.Length], "Toon_Plant_" + i.ToString("00"), plants);
            PlaceOnGround(obj, terrain, pos, i * 23f, 0.85f + (i % 3) * 0.18f);
        }
    }

    private static void PlaceWaterDetails(Transform parent, Terrain terrain)
    {
        Transform water = NewGroup("Water_Particles_And_Plants", parent);
        string ripple = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Water/TSI_Water_Ripples_01A.prefab";
        string particle = "Assets/Packages/ToonScapes/Spring Isles/Particles/TSI_Water_Particles_01A.prefab";
        string lily = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Water Vegetation/TSI_Water_Lily_03A.prefab";
        string plant = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Water Vegetation/TSI_Water_Plant_02A.prefab";
        string waterfall = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Water/TSI_Waterfall_01A.prefab";

        Vector3[] stream =
        {
            new Vector3(48f,0f,-126f), new Vector3(51f,0f,-104f), new Vector3(50f,0f,-82f),
            new Vector3(46f,0f,-60f), new Vector3(43f,0f,-38f), new Vector3(46f,0f,-16f),
            new Vector3(53f,0f,8f), new Vector3(56f,0f,32f)
        };

        for (int i = 0; i < stream.Length; i++)
        {
            Vector3 p = stream[i];
            p.y = GroundY(terrain, p.x, p.z) + 0.18f;
            GameObject r = InstantiatePrefab(i % 2 == 0 ? ripple : particle, "Stream_FX_" + i.ToString("00"), water);
            if (r != null)
            {
                r.transform.position = p;
                r.transform.rotation = Quaternion.Euler(0f, i * 31f, 0f);
                r.transform.localScale = Vector3.one * (0.8f + (i % 3) * 0.18f);
            }

            if (i % 2 == 0)
            {
                GameObject lp = InstantiatePrefab(i % 4 == 0 ? lily : plant, "Stream_Plant_" + i.ToString("00"), water);
                if (lp != null)
                {
                    lp.transform.position = p + new Vector3(i % 4 == 0 ? -2.4f : 2.2f, 0.02f, 1.2f);
                    lp.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                }
            }
        }

        GameObject wf = InstantiatePrefab(waterfall, "Upper_Stream_Waterfall", water);
        if (wf != null)
        {
            Vector3 pos = new Vector3(56f, GroundY(terrain, 56f, 32f) + 1.2f, 32f);
            wf.transform.position = pos;
            wf.transform.rotation = Quaternion.Euler(0f, 185f, 0f);
            wf.transform.localScale = Vector3.one * 0.75f;
        }
    }

    private static void PlaceVillageAtmosphere(Transform parent, Terrain terrain)
    {
        Transform atmosphere = NewGroup("Fog_And_Village_Props", parent);
        string fog = "Assets/Packages/ToonScapes/Spring Isles/Particles/TSI_Fog_01A.prefab";
        string lantern = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Building Props/Stone Kit/TSI_Stone_Lantern_01A.prefab";
        string sign = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Props/Wood Props/TSI_Sign_Post_01A.prefab";
        string petals = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Petals_01A.prefab";

        Vector3[] fogPoints =
        {
            new Vector3(34f,0f,-72f), new Vector3(60f,0f,-66f), new Vector3(77f,0f,-42f),
            new Vector3(37f,0f,-18f), new Vector3(50f,0f,4f), new Vector3(-30f,0f,70f),
            new Vector3(105f,0f,86f), new Vector3(-104f,0f,-54f)
        };

        for (int i = 0; i < fogPoints.Length; i++)
        {
            GameObject obj = InstantiatePrefab(fog, "Low_Fog_" + i.ToString("00"), atmosphere);
            PlaceOnGround(obj, terrain, fogPoints[i], i * 13f, i < 5 ? 1.15f : 1.8f);
        }

        Vector3[] props =
        {
            new Vector3(94f,0f,-42f), new Vector3(10f,0f,-42f), new Vector3(52f,0f,0f),
            new Vector3(52f,0f,-84f), new Vector3(36f,0f,-70f), new Vector3(70f,0f,-14f)
        };

        for (int i = 0; i < props.Length; i++)
        {
            string prefab = i < 4 ? lantern : sign;
            GameObject obj = InstantiatePrefab(prefab, "Village_Atmosphere_Prop_" + i.ToString("00"), atmosphere);
            PlaceOnGround(obj, terrain, props[i], i * 90f + 35f, 1f);
        }

        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Vector3 p = new Vector3(52f + Mathf.Cos(a) * 30f, 0f, -42f + Mathf.Sin(a) * 30f);
            GameObject obj = InstantiatePrefab(petals, "Village_Petals_" + i.ToString("00"), atmosphere);
            PlaceOnGround(obj, terrain, p, i * 21f, 0.9f);
        }
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static GameObject InstantiatePrefab(string path, string name, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Missing ToonScapes prefab: " + path);
            return null;
        }

        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.name = name;
        obj.transform.SetParent(parent, true);
        return obj;
    }

    private static void PlaceOnGround(GameObject obj, Terrain terrain, Vector3 pos, float yaw, float scale)
    {
        if (obj == null)
        {
            return;
        }

        obj.transform.position = new Vector3(pos.x, GroundY(terrain, pos.x, pos.z), pos.z);
        obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        obj.transform.localScale = Vector3.one * scale;
    }

    private static float GroundY(Terrain terrain, float x, float z)
    {
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    private static float LookAtCenterYaw(Vector3 position)
    {
        Vector3 dir = -new Vector3(position.x, 0f, position.z);
        return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
    }
}
