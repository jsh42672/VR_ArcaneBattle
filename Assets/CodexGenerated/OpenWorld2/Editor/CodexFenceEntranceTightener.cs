using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexFenceEntranceTightener
{
    public static void Execute()
    {
        Terrain terrain = Terrain.activeTerrain;
        GameObject village = GameObject.Find("OW2_Hills_Village_Generated/Village_Object");
        if (terrain == null || village == null)
        {
            Debug.LogWarning("Terrain or Village_Object not found.");
            return;
        }

        Transform oldFence = village.transform.Find("Fence");
        if (oldFence != null)
        {
            Object.DestroyImmediate(oldFence.gameObject);
        }

        GameObject fenceRoot = new GameObject("Fence");
        fenceRoot.transform.SetParent(village.transform, false);

        GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Props/Wood Props/TSI_Wood_Fence_01A.prefab");
        Material fallback = MakeMat("Fence_Fallback", new Color(0.34f, 0.18f, 0.1f));

        Vector3 center = new Vector3(52f, 0f, -42f);
        float radius = 42f;
        int count = 88;

        for (int i = 0; i < count; i++)
        {
            float deg = i * 360f / count;
            if (IsEntranceGap(deg))
            {
                continue;
            }

            float rad = deg * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            pos.y = GroundY(terrain, pos.x, pos.z) + 0.25f;

            GameObject obj = fencePrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Village_Fence_" + i.ToString("00");
            obj.transform.SetParent(fenceRoot.transform, true);
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(0f, -deg + 90f, 0f);

            if (fencePrefab == null)
            {
                obj.transform.localScale = new Vector3(3.4f, 1.2f, 0.2f);
                obj.GetComponent<MeshRenderer>().sharedMaterial = fallback;
            }
            else
            {
                obj.transform.localScale = Vector3.one * 1.02f;
            }
        }

        AddGatePosts(fenceRoot.transform, fencePrefab, fallback, terrain, center, radius, 0f, "Front");
        AddGatePosts(fenceRoot.transform, fencePrefab, fallback, terrain, center, radius, 180f, "Back");

        Selection.activeGameObject = fenceRoot;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Tightened village fence entrances while keeping two openings.");
    }

    private static bool IsEntranceGap(float deg)
    {
        return Mathf.Abs(Mathf.DeltaAngle(deg, 0f)) < 7.2f
            || Mathf.Abs(Mathf.DeltaAngle(deg, 180f)) < 7.2f;
    }

    private static void AddGatePosts(Transform parent, GameObject fencePrefab, Material fallback, Terrain terrain, Vector3 center, float radius, float entryDeg, string label)
    {
        float[] offsets = { -8.8f, 8.8f };
        for (int i = 0; i < offsets.Length; i++)
        {
            float deg = entryDeg + offsets[i];
            float rad = deg * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            pos.y = GroundY(terrain, pos.x, pos.z) + 0.25f;

            GameObject obj = fencePrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Village_Fence_" + label + "_Gate_Edge_" + i.ToString("00");
            obj.transform.SetParent(parent, true);
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(0f, -deg + 90f, 0f);

            if (fencePrefab == null)
            {
                obj.transform.localScale = new Vector3(3.4f, 1.2f, 0.2f);
                obj.GetComponent<MeshRenderer>().sharedMaterial = fallback;
            }
            else
            {
                obj.transform.localScale = Vector3.one * 1.06f;
            }
        }
    }

    private static float GroundY(Terrain terrain, float x, float z)
    {
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
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
}
