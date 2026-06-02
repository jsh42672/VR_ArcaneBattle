using System;
using System.IO;
using System.Linq;
using CodexGenerated.GrimoirePages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexGrimoirePageSceneSetup
{
    private const string BookName = "A_BookGrimoireFinished";
    private const string BookSourcePath = "Assets/Art/Models/Book/Grimore/source/A_BookGrimoireFinished.fbx";
    private const string GeneratedFolder = "Assets/CodexGenerated/GrimoirePages";
    private const string MeshPath = GeneratedFolder + "/GeneratedMagicPageQuad.asset";
    private const string LeftTexturePath = GeneratedFolder + "/Page_Left_Temp.png";
    private const string RightTexturePath = GeneratedFolder + "/Page_Right_Temp.png";
    private const string LeftTexturePath02 = GeneratedFolder + "/Page_Left_Temp_02.png";
    private const string RightTexturePath02 = GeneratedFolder + "/Page_Right_Temp_02.png";
    private const string LeftMaterialPath = GeneratedFolder + "/M_Page_Left_Temp.mat";
    private const string RightMaterialPath = GeneratedFolder + "/M_Page_Right_Temp.mat";
    private const string LeftMaterialPath02 = GeneratedFolder + "/M_Page_Left_Temp_02.mat";
    private const string RightMaterialPath02 = GeneratedFolder + "/M_Page_Right_Temp_02.mat";
    private const int TurningPageStripCount = 6;

    public static void Execute()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Stop Play Mode before creating saved scene pages.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Active scene is not a saved scene.");
            return;
        }

        GameObject book = FindBookObject();
        if (book == null)
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Could not find the grimoire book in the active scene.");
            return;
        }

        EnsureFolder(GeneratedFolder);
        Mesh pageMesh = EnsurePageMesh();
        Texture2D leftTexture = EnsurePageTexture(LeftTexturePath, true);
        Texture2D rightTexture = EnsurePageTexture(RightTexturePath, false);
        Texture2D leftTexture02 = EnsurePageTexture(LeftTexturePath02, false);
        Texture2D rightTexture02 = EnsurePageTexture(RightTexturePath02, true);
        Material leftMaterial = EnsurePageMaterial(LeftMaterialPath, leftTexture);
        Material rightMaterial = EnsurePageMaterial(RightMaterialPath, rightTexture);
        Material leftMaterial02 = EnsurePageMaterial(LeftMaterialPath02, leftTexture02);
        Material rightMaterial02 = EnsurePageMaterial(RightMaterialPath02, rightTexture02);
        Material[] pageMaterials = EnsureContentPageMaterials(new[] { leftMaterial, rightMaterial, leftMaterial02, rightMaterial02 });
        leftMaterial = pageMaterials[0];
        rightMaterial = pageMaterials[Mathf.Min(1, pageMaterials.Length - 1)];

        Transform customPages = FindOrCreateChild(book.transform, "CustomPages");
        customPages.localPosition = Vector3.zero;
        customPages.localRotation = Quaternion.identity;
        customPages.localScale = Vector3.one;

        Bounds localBounds;
        if (!TryGetLocalBookBounds(book.transform, out localBounds))
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Book has no usable renderers for page placement.");
            return;
        }

        float width = localBounds.size.x;
        float depth = localBounds.size.z;
        float topY = localBounds.max.y + 0.008f;
        float pageWidth = width * 0.43f;
        float pageDepth = depth * 0.82f;
        float xOffset = width * 0.225f;
        float zOffset = -depth * 0.005f;

        GameObject leftPage = CreateOrUpdatePage(
            customPages,
            "LeftMagicPage",
            pageMesh,
            leftMaterial,
            new Vector3(localBounds.center.x - xOffset, topY, localBounds.center.z + zOffset),
            new Vector3(pageWidth, pageDepth, 1f)
        );

        GameObject rightPage = CreateOrUpdatePage(
            customPages,
            "RightMagicPage",
            pageMesh,
            rightMaterial,
            new Vector3(localBounds.center.x + xOffset, topY, localBounds.center.z + zOffset),
            new Vector3(pageWidth, pageDepth, 1f)
        );

        TurningPageObjects turningPage = CreateOrUpdateTurningPage(
            customPages,
            pageMesh,
            rightMaterial,
            new Vector3(localBounds.center.x, topY + 0.006f, localBounds.center.z + zOffset),
            new Vector3(pageWidth, pageDepth, 1f)
        );

        ConfigurePageTurner(
            customPages.gameObject,
            leftPage.GetComponent<Renderer>(),
            rightPage.GetComponent<Renderer>(),
            turningPage.pivot,
            turningPage.renderer,
            Array.Empty<Renderer>(),
            Array.Empty<Transform>(),
            pageMaterials
        );

        leftPage.transform.SetSiblingIndex(0);
        rightPage.transform.SetSiblingIndex(1);
        turningPage.pivot.SetSiblingIndex(2);

        EditorUtility.SetDirty(book);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = customPages.gameObject;
        Debug.Log(
            "[CodexGrimoirePageSceneSetup] Saved grimoire custom pages. " +
            $"scene={scene.path}, saved={saved}, book={GetPath(book.transform)}, " +
            $"left={GetPath(leftPage.transform)}, right={GetPath(rightPage.transform)}, " +
            $"assets={GeneratedFolder}"
        );
    }

    public static void TriggerNextPageForVerification()
    {
        GrimoirePageTurner turner = UnityEngine.Object.FindFirstObjectByType<GrimoirePageTurner>();
        if (turner == null)
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Could not find GrimoirePageTurner for verification.");
            return;
        }

        turner.NextPage();
        Debug.Log("[CodexGrimoirePageSceneSetup] Triggered NextPage for verification.");
    }

    public static void PreviewMidTurnForVerification()
    {
        GrimoirePageTurner turner = UnityEngine.Object.FindFirstObjectByType<GrimoirePageTurner>(FindObjectsInactive.Include);
        if (turner == null)
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Could not find GrimoirePageTurner for mid-turn preview.");
            return;
        }

        turner.PreviewMidTurn();
        Debug.Log("[CodexGrimoirePageSceneSetup] Previewed mid-turn page curl.");
        LogTurningPageState(turner);
    }

    public static void HideTurnPreviewForVerification()
    {
        GrimoirePageTurner turner = UnityEngine.Object.FindFirstObjectByType<GrimoirePageTurner>(FindObjectsInactive.Include);
        if (turner == null)
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Could not find GrimoirePageTurner for hiding preview.");
            return;
        }

        turner.HideTurnPreview();
        Debug.Log("[CodexGrimoirePageSceneSetup] Hid mid-turn page curl preview.");
    }

    private static void LogTurningPageState(GrimoirePageTurner turner)
    {
        SerializedObject serializedTurner = new SerializedObject(turner);
        Transform pivot = serializedTurner.FindProperty("turningPagePivot").objectReferenceValue as Transform;
        Renderer renderer = serializedTurner.FindProperty("turningPageRenderer").objectReferenceValue as Renderer;
        Debug.Log(
            "[CodexGrimoirePageSceneSetup] Turning state " +
            $"pivotActive={(pivot != null && pivot.gameObject.activeInHierarchy)}, " +
            $"pivotLocalRot={(pivot != null ? pivot.localEulerAngles.ToString() : "null")}, " +
            $"rendererActive={(renderer != null && renderer.gameObject.activeInHierarchy)}, " +
            $"renderer={(renderer != null ? renderer.name : "null")}"
        );
    }

    public static void ForceSlowTurnDuration()
    {
        GrimoirePageTurner turner = UnityEngine.Object.FindFirstObjectByType<GrimoirePageTurner>(FindObjectsInactive.Include);
        if (turner == null)
        {
            Debug.LogError("[CodexGrimoirePageSceneSetup] Could not find GrimoirePageTurner for duration update.");
            return;
        }

        SerializedObject serializedTurner = new SerializedObject(turner);
        serializedTurner.FindProperty("turnDuration").floatValue = 1.8f;
        serializedTurner.FindProperty("useCurledStrips").boolValue = false;
        serializedTurner.FindProperty("curlAngle").floatValue = 34f;
        serializedTurner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(turner);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[CodexGrimoirePageSceneSetup] Forced turnDuration=1.8 and saved scene.");
    }

    private static GameObject FindBookObject()
    {
        GameObject exact = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => IsSceneObject(go) && go.name == BookName);
        if (exact != null) return exact;

        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(IsSceneObject)
            .FirstOrDefault(go =>
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                string path = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
                return path == BookSourcePath || go.name.Contains("BookGrimoire") || go.name.Contains("Grimoire") || go.name.Contains("Grimore");
            });
    }

    private static bool IsSceneObject(GameObject go)
    {
        return go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go);
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create grimoire custom pages root");
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static GameObject CreateOrUpdatePage(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = parent.Find(name);
        GameObject page = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(page, "Create grimoire magic page");
            page.transform.SetParent(parent, false);
        }

        page.transform.localPosition = localPosition;
        page.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        page.transform.localScale = localScale;

        MeshFilter meshFilter = page.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = page.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = page.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = page.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        EditorUtility.SetDirty(page);
        return page;
    }

    private struct TurningPageObjects
    {
        public Transform pivot;
        public Renderer renderer;
        public Renderer[] renderers;
        public Transform[] strips;
    }

    private static TurningPageObjects CreateOrUpdateTurningPage(Transform parent, Mesh mesh, Material material, Vector3 localPosition, Vector3 visualScale)
    {
        Transform existingPivot = parent.Find("TurningPagePivot");
        GameObject pivot = existingPivot != null ? existingPivot.gameObject : new GameObject("TurningPagePivot");
        if (existingPivot == null)
        {
            Undo.RegisterCreatedObjectUndo(pivot, "Create turning page pivot");
            pivot.transform.SetParent(parent, false);
        }

        pivot.transform.localPosition = localPosition;
        pivot.transform.localRotation = Quaternion.identity;
        pivot.transform.localScale = Vector3.one;

        Transform existingVisual = pivot.transform.Find("TurningPageVisual");
        GameObject visual = existingVisual != null ? existingVisual.gameObject : new GameObject("TurningPageVisual");
        if (existingVisual == null)
        {
            Undo.RegisterCreatedObjectUndo(visual, "Create turning page visual");
            visual.transform.SetParent(pivot.transform, false);
        }

        visual.transform.localPosition = new Vector3(visualScale.x * 0.5f, 0f, 0f);
        visual.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        visual.transform.localScale = visualScale;

        MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = visual.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        for (int i = visual.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = visual.transform.GetChild(i);
            if (child.name.StartsWith("TurningPageStrip_"))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        pivot.SetActive(false);
        EditorUtility.SetDirty(pivot);
        EditorUtility.SetDirty(visual);
        return new TurningPageObjects
        {
            pivot = pivot.transform,
            renderer = meshRenderer,
            renderers = Array.Empty<Renderer>(),
            strips = Array.Empty<Transform>()
        };
    }

    private static void ConfigurePageTurner(GameObject target, Renderer leftRenderer, Renderer rightRenderer, Transform turningPivot, Renderer turningRenderer, Renderer[] turningRenderers, Transform[] turningStrips, Material[] materials)
    {
        GrimoirePageTurner turner = target.GetComponent<GrimoirePageTurner>();
        if (turner == null)
        {
            turner = target.AddComponent<GrimoirePageTurner>();
        }

        SerializedObject serializedTurner = new SerializedObject(turner);
        serializedTurner.FindProperty("leftPageRenderer").objectReferenceValue = leftRenderer;
        serializedTurner.FindProperty("rightPageRenderer").objectReferenceValue = rightRenderer;
        serializedTurner.FindProperty("turningPagePivot").objectReferenceValue = turningPivot;
        serializedTurner.FindProperty("turningPageRenderer").objectReferenceValue = turningRenderer;
        SetObjectArray(serializedTurner.FindProperty("turningPageRenderers"), turningRenderers);
        SetObjectArray(serializedTurner.FindProperty("turningPageStrips"), turningStrips);
        serializedTurner.FindProperty("turnDuration").floatValue = 1.8f;
        serializedTurner.FindProperty("useCurledStrips").boolValue = false;
        serializedTurner.FindProperty("curlAngle").floatValue = 34f;

        SerializedProperty pageMaterials = serializedTurner.FindProperty("pageMaterials");
        pageMaterials.arraySize = materials.Length;
        for (int i = 0; i < materials.Length; i++)
        {
            pageMaterials.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        }

        serializedTurner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(turner);
    }

    private static void SetObjectArray<T>(SerializedProperty property, T[] objects) where T : UnityEngine.Object
    {
        property.arraySize = objects != null ? objects.Length : 0;
        for (int i = 0; i < property.arraySize; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }
    }

    private static Mesh EnsurePageMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool createAsset = mesh == null;
        if (mesh == null)
        {
            mesh = new Mesh { name = "GeneratedMagicPageQuad" };
        }

        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (createAsset)
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    private static Mesh EnsurePageStripMesh(int index, int count)
    {
        string path = $"{GeneratedFolder}/GeneratedMagicPageStrip_{index + 1:00}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool createAsset = mesh == null;
        if (mesh == null)
        {
            mesh = new Mesh { name = $"GeneratedMagicPageStrip_{index + 1:00}" };
        }

        float leftU = 1f - index / (float)count;
        float rightU = 1f - (index + 1) / (float)count;

        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(leftU, 0f),
            new Vector2(rightU, 0f),
            new Vector2(leftU, 1f),
            new Vector2(rightU, 1f)
        };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (createAsset)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    private static Texture2D EnsurePageTexture(string path, bool left)
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        Texture2D texture = CreatePageTexture(left);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material EnsurePageMaterial(string path, Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material[] EnsureContentPageMaterials(Material[] fallback)
    {
        var materials = new System.Collections.Generic.List<Material>();
        for (int i = 1; i <= 8; i++)
        {
            string texturePath = $"{GeneratedFolder}/Page_Content_{i:00}.png";
            if (!File.Exists(texturePath))
            {
                continue;
            }

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                continue;
            }

            string materialPath = $"{GeneratedFolder}/M_Page_Content_{i:00}.mat";
            materials.Add(EnsurePageMaterial(materialPath, texture));
        }

        return materials.Count >= 2 ? materials.ToArray() : fallback;
    }

    private static bool TryGetLocalBookBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => !IsUnderTransformNamed(renderer.transform, "CustomPages"))
            .ToArray();
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 local = root.InverseTransformPoint(corner);
                if (!hasBounds)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return hasBounds;
    }

    private static bool IsUnderTransformNamed(Transform transform, string name)
    {
        while (transform != null)
        {
            if (transform.name == name) return true;
            transform = transform.parent;
        }

        return false;
    }

    private static Texture2D CreatePageTexture(bool left)
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color parchment = left ? new Color(0.74f, 0.66f, 0.40f, 1f) : new Color(0.70f, 0.62f, 0.36f, 1f);
        Color ink = left ? new Color(0.07f, 0.15f, 0.12f, 1f) : new Color(0.13f, 0.08f, 0.18f, 1f);
        Color glow = left ? new Color(0.11f, 0.95f, 0.55f, 1f) : new Color(0.55f, 0.28f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.035f, y * 0.035f) * 0.12f;
                texture.SetPixel(x, y, parchment + new Color(noise, noise * 0.8f, noise * 0.3f, 0f));
            }
        }

        DrawRect(texture, 34, 34, 444, 444, ink, 5);
        DrawRect(texture, 52, 52, 408, 408, ink * 0.85f, 2);
        DrawCircle(texture, 256, 262, 118, glow, 4);
        DrawCircle(texture, 256, 262, 78, ink, 3);
        DrawLine(texture, 150, 262, 362, 262, ink, 3);
        DrawLine(texture, 256, 154, 256, 370, ink, 3);
        DrawLine(texture, 178, 184, 334, 340, glow, 3);
        DrawLine(texture, 334, 184, 178, 340, glow, 3);
        DrawSmallTextLines(texture, ink, left);

        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.PI * 2f * i / 12f;
            int cx = 256 + Mathf.RoundToInt(Mathf.Cos(angle) * 154f);
            int cy = 262 + Mathf.RoundToInt(Mathf.Sin(angle) * 154f);
            DrawGlyph(texture, cx, cy, ink, i + (left ? 0 : 3));
        }

        texture.Apply();
        return texture;
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color, int thickness)
    {
        for (int t = 0; t < thickness; t++)
        {
            DrawLine(texture, x + t, y + t, x + width - t, y + t, color, 1);
            DrawLine(texture, x + t, y + height - t, x + width - t, y + height - t, color, 1);
            DrawLine(texture, x + t, y + t, x + t, y + height - t, color, 1);
            DrawLine(texture, x + width - t, y + t, x + width - t, y + height - t, color, 1);
        }
    }

    private static void DrawCircle(Texture2D texture, int cx, int cy, int radius, Color color, int thickness)
    {
        int r2 = radius * radius;
        int inner = (radius - thickness) * (radius - thickness);
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                int d = dx * dx + dy * dy;
                if (d <= r2 && d >= inner) SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void DrawGlyph(Texture2D texture, int cx, int cy, Color color, int seed)
    {
        int r = 12 + seed % 5;
        DrawCircle(texture, cx, cy, r, color, 2);
        DrawLine(texture, cx - r, cy, cx + r, cy, color, 1);
        if (seed % 2 == 0) DrawLine(texture, cx, cy - r, cx, cy + r, color, 1);
        if (seed % 3 == 0) DrawLine(texture, cx - r / 2, cy - r / 2, cx + r / 2, cy + r / 2, color, 1);
    }

    private static void DrawSmallTextLines(Texture2D texture, Color color, bool left)
    {
        int startX = left ? 70 : 76;
        int startY = left ? 84 : 392;
        for (int i = 0; i < 8; i++)
        {
            int y = left ? startY + i * 14 : startY - i * 14;
            int width = 70 + (i % 4) * 28;
            DrawLine(texture, startX, y, startX + width, y, color * 0.75f, 1);
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            for (int ox = -thickness; ox <= thickness; ox++)
            {
                for (int oy = -thickness; oy <= thickness; oy++)
                {
                    SetPixelSafe(texture, x0 + ox, y0 + oy, color);
                }
            }

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
        texture.SetPixel(x, y, color);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
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
}
