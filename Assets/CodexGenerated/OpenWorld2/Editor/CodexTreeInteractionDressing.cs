using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexTreeInteractionDressing
{
    private const string RootName = "OW2_Hills_Village_Generated";
    private const string GroupName = "Tree_Interaction_Dressing";

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

        Transform old = root.transform.Find(GroupName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject group = new GameObject(GroupName);
        group.transform.SetParent(root.transform, false);

        List<Vector3> treePositions = FindTreePositions();
        if (treePositions.Count == 0)
        {
            treePositions.AddRange(new[]
            {
                new Vector3(-84f, 0f, 70f),
                new Vector3(-48f, 0f, 102f),
                new Vector3(88f, 0f, 112f),
                new Vector3(116f, 0f, -82f),
                new Vector3(-112f, 0f, -84f),
                new Vector3(94f, 0f, 46f)
            });
        }

        string petals = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Petals_01A.prefab";
        string flowerPatch = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Flower_Patch_02A.prefab";
        string grassPatch = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Vegetation/Plants & Flowers/TSI_Grass_Patch_04A.prefab";
        string fog = "Assets/Packages/ToonScapes/Spring Isles/Particles/TSI_Fog_01A.prefab";
        string sign = "Assets/Packages/ToonScapes/Spring Isles/Prefabs/Props/Wood Props/TSI_Sign_Post_02A.prefab";

        int count = Mathf.Min(treePositions.Count, 12);
        for (int i = 0; i < count; i++)
        {
            Vector3 center = treePositions[i];
            PlaceAroundTree(group.transform, terrain, petals, "Tree_Petals_" + i.ToString("00"), center, 2.0f, i * 31f, 0.9f);
            PlaceAroundTree(group.transform, terrain, flowerPatch, "Tree_Flowers_" + i.ToString("00"), center, 3.2f, i * 47f + 15f, 0.8f);
            PlaceAroundTree(group.transform, terrain, grassPatch, "Tree_Grass_" + i.ToString("00"), center, 4.4f, i * 73f + 40f, 1.1f);

            if (i % 3 == 0)
            {
                GameObject fogObj = InstantiatePrefab(fog, "Tree_Low_Fog_" + i.ToString("00"), group.transform);
                Place(fogObj, terrain, center + new Vector3(1.4f, 0f, -1.1f), i * 22f, 0.75f);
            }

            if (i % 4 == 0)
            {
                GameObject signObj = InstantiatePrefab(sign, "Tree_Interaction_Sign_" + i.ToString("00"), group.transform);
                Place(signObj, terrain, center + new Vector3(3.0f, 0f, 2.0f), LookYaw(center + new Vector3(3.0f, 0f, 2.0f), center), 0.85f);
            }
        }

        Selection.activeGameObject = group;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Applied tree interaction dressing around " + count + " trees.");
    }

    private static List<Vector3> FindTreePositions()
    {
        List<Vector3> positions = new List<Vector3>();
        Transform root = GameObject.Find(RootName)?.transform;
        if (root == null)
        {
            return positions;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string n = child.name.ToLowerInvariant();
            if ((n.Contains("tree") || n.Contains("shrub")) && !n.Contains("lod") && child.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                positions.Add(child.position);
            }
        }

        return positions;
    }

    private static void PlaceAroundTree(Transform parent, Terrain terrain, string prefabPath, string name, Vector3 center, float radius, float angleDeg, float scale)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
        GameObject obj = InstantiatePrefab(prefabPath, name, parent);
        Place(obj, terrain, pos, angleDeg, scale);
    }

    private static GameObject InstantiatePrefab(string path, string name, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Missing prefab: " + path);
            return null;
        }

        GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        obj.name = name;
        obj.transform.SetParent(parent, true);
        return obj;
    }

    private static void Place(GameObject obj, Terrain terrain, Vector3 pos, float yaw, float scale)
    {
        if (obj == null)
        {
            return;
        }

        obj.transform.position = new Vector3(pos.x, terrain.SampleHeight(pos) + terrain.transform.position.y, pos.z);
        obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        obj.transform.localScale = Vector3.one * scale;
    }

    private static float LookYaw(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
        {
            return 0f;
        }

        return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
    }
}
