using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexToonTerrainPainter
{
    public static void Execute()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("No active terrain found.");
            return;
        }

        TerrainData data = terrain.terrainData;
        TerrainLayer grass = LoadLayer("Layer_TSI_Terrain_Grass_01A.terrainlayer");
        TerrainLayer earth = LoadLayer("Layer_TSI_Terrain_Earth_01A.terrainlayer");
        TerrainLayer stone = LoadLayer("Layer_TSI_Terrain_Stone_01A.terrainlayer");
        TerrainLayer riverRock = LoadLayer("Layer_TSI_Terrain_River_Rocks_01A.terrainlayer");
        TerrainLayer sand = LoadLayer("Layer_TSI_Terrain_Sand_01A.terrainlayer");

        data.terrainLayers = new[] { grass, earth, stone, riverRock, sand };
        if (data.alphamapResolution > 256)
        {
            data.alphamapResolution = 256;
        }

        Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Packages/ToonScapes/Spring Isles/Models/Materials/TSI_Terrain.mat");
        if (terrainMat != null)
        {
            terrain.materialTemplate = terrainMat;
        }

        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layers = data.alphamapLayers;
        float[,,] alpha = new float[h, w, layers];

        Vector3 terrainPos = terrain.transform.position;
        Vector3 size = data.size;
        for (int z = 0; z < h; z++)
        {
            float nz = z / (float)(h - 1);
            float worldZ = terrainPos.z + nz * size.z;
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)(w - 1);
                float worldX = terrainPos.x + nx * size.x;
                float height = data.GetInterpolatedHeight(nx, nz) / size.y;
                float steep = data.GetSteepness(nx, nz);
                float noiseA = Mathf.PerlinNoise(nx * 18f + 4f, nz * 18f + 12f);
                float noiseB = Mathf.PerlinNoise(nx * 46f + 2f, nz * 46f + 9f);

                float grassW = 1.0f;
                float earthW = Mathf.SmoothStep(0.44f, 0.68f, noiseA) * 0.34f;
                float stoneW = Mathf.SmoothStep(18f, 38f, steep) * 1.35f;
                float riverW = Mathf.SmoothStep(0.50f, 0.83f, noiseB) * 0.16f;
                float sandW = 0.0f;

                float village = SmoothCircle(worldX, worldZ, 52f, -42f, 50f);
                earthW += village * 0.55f;
                grassW += (1f - village) * 0.25f;

                float creek = DistanceToSideCreek(new Vector2(worldX, worldZ));
                if (creek < 9f)
                {
                    float creekBlend = 1f - Mathf.SmoothStep(0f, 1f, creek / 9f);
                    riverW += creekBlend * 1.15f;
                    sandW += creekBlend * 0.18f;
                    grassW *= 1f - creekBlend * 0.35f;
                }

                if (height > 0.67f)
                {
                    stoneW += (height - 0.67f) * 2.4f;
                }

                float total = grassW + earthW + stoneW + riverW + sandW;
                alpha[z, x, 0] = grassW / total;
                alpha[z, x, 1] = earthW / total;
                alpha[z, x, 2] = stoneW / total;
                alpha[z, x, 3] = riverW / total;
                alpha[z, x, 4] = sandW / total;
            }
        }

        data.SetAlphamaps(0, 0, alpha);
        terrain.Flush();
        Selection.activeGameObject = terrain.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Applied ToonScapes terrain layers: grass, earth, stone, river rocks, sand.");
    }

    private static TerrainLayer LoadLayer(string name)
    {
        string path = "Assets/Packages/ToonScapes/Spring Isles/Terrain/Terrain Data/" + name;
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            Debug.LogWarning("Missing terrain layer: " + path);
        }

        return layer;
    }

    private static float DistanceToSideCreek(Vector2 point)
    {
        Vector3 anchor = new Vector3(82f, 0f, 38f);
        Vector3[] path =
        {
            anchor + new Vector3(-42f, 0f, 10f),
            anchor + new Vector3(-24f, 0f, 5f),
            anchor + new Vector3(-7f, 0f, 8f),
            anchor + new Vector3(12f, 0f, 2f),
            anchor + new Vector3(31f, 0f, -5f),
            anchor + new Vector3(48f, 0f, -3f)
        };

        float best = float.MaxValue;
        for (int i = 0; i < path.Length - 1; i++)
        {
            best = Mathf.Min(best, DistanceToSegment(point, new Vector2(path[i].x, path[i].z), new Vector2(path[i + 1].x, path[i + 1].z)));
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

    private static float SmoothCircle(float x, float z, float cx, float cz, float radius)
    {
        float d = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
        return Mathf.Clamp01(1f - Mathf.InverseLerp(radius * 0.65f, radius, d));
    }
}
