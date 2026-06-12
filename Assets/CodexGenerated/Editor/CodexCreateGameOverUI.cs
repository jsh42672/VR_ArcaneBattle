using ArcaneVR.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class CodexCreateGameOverUI
{
    private const string PrefabPath = "Assets/Prefabs/UI/GameOverScreenUI.prefab";
    private const string TitleFontPath = "Assets/Art/Font/BlackHanSans-Regular.ttf";
    private const string ButtonFontPath = "Assets/Art/Font/DoHyeon-Regular.ttf";
    private const string WorldSceneName = "OPenWorld2";
    private const string PreviewName = "GameOver Screen UI Preview";

    private static Font s_TitleFont;
    private static Font s_ButtonFont;

    public static void Execute()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        DestroyPreview();

        s_TitleFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
        s_ButtonFont = AssetDatabase.LoadAssetAtPath<Font>(ButtonFontPath);

        if (s_TitleFont == null)
            Debug.LogWarning("[GameOverUI] Title font not found: " + TitleFontPath);
        if (s_ButtonFont == null)
            Debug.LogWarning("[GameOverUI] Button font not found: " + ButtonFontPath);

        GameObject prefabRoot = BuildGameOverUI("GameOver Screen UI");
        prefabRoot.SetActive(false);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        if (savedPrefab == null)
        {
            Debug.LogError("[GameOverUI] Failed to save prefab.");
            return;
        }

        GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);
        preview.name = PreviewName;
        preview.SetActive(true);
        preview.hideFlags = HideFlags.DontSaveInEditor;
        SetChildrenActive(preview.transform, true);
        Selection.activeGameObject = preview;

        Debug.Log("[GameOverUI] Updated prefab with legacy UI Text fonts to avoid TMP atlas/material errors.");
    }

    public static void CleanupPreview()
    {
        DestroyPreview();
        Debug.Log("[GameOverUI] Removed temporary preview.");
    }

    public static void CleanupBrokenTmpFontAssets()
    {
        string[] generatedTmpAssets =
        {
            "Assets/Art/Font/BlackHanSans-GameOverTMP.asset",
            "Assets/Art/Font/DoHyeon-GameOverTMP.asset",
            "Assets/CodexGenerated/UI/GameOverHangulTMP.asset"
        };

        foreach (string assetPath in generatedTmpAssets)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameOverUI] Removed generated TMP font assets that were missing atlas/material data.");
    }

    private static GameObject BuildGameOverUI(string rootName)
    {
        GameObject root = new GameObject(rootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));

        GameOverScreenController controller = root.AddComponent<GameOverScreenController>();

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        GameObject overlay = CreateRect("Dim Overlay", root.transform);
        Stretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = new Color(0.02f, 0.015f, 0.025f, 0.88f);

        Text title = CreateText("GAMEOVER Title", overlay.transform, "GAMEOVER", s_TitleFont, 136, new Color(1f, 0.18f, 0.12f, 1f));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 120f);
        titleRect.sizeDelta = new Vector2(1120f, 180f);

        Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        titleShadow.effectDistance = new Vector2(6f, -6f);

        GameObject buttonRow = CreateRect("Button Row", overlay.transform);
        RectTransform rowRect = buttonRow.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = new Vector2(0f, -185f);
        rowRect.sizeDelta = new Vector2(800f, 92f);

        HorizontalLayoutGroup layout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button restartButton = CreateButton("Restart Button", buttonRow.transform, "\uC7AC\uC2DC\uC791");
        Button returnToWorldButton = CreateButton("Return To World Button", buttonRow.transform, "\uC6D4\uB4DC\uB85C \uB3CC\uC544\uAC00\uAE30");

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("worldSceneName").stringValue = WorldSceneName;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(restartButton.onClick, controller.RestartCurrentScene);
        UnityEventTools.AddPersistentListener(returnToWorldButton.onClick, controller.ReturnToWorld);

        return root;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetLayerRecursively(go, LayerMask.NameToLayer("UI"));
        return go;
    }

    private static Text CreateText(string name, Transform parent, string value, Font font, int fontSize, Color color)
    {
        GameObject go = CreateRect(name, parent);
        Text text = go.AddComponent<Text>();
        text.text = value;
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(18, fontSize / 2);
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject go = CreateRect(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 86f);

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 86f;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.26f, 0.98f);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.8f, 0.88f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.26f, 0.98f);
        colors.highlightedColor = new Color(0.34f, 0.34f, 0.44f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.14f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.targetGraphic = image;

        Text text = CreateText("Label", go.transform, label, s_ButtonFont, 38, Color.white);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);

        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetChildrenActive(Transform transform, bool active)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(active);
            SetChildrenActive(child, active);
        }
    }

    private static void DestroyPreview()
    {
        GameObject existing = GameObject.Find(PreviewName);
        if (existing != null)
            Object.DestroyImmediate(existing);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer >= 0)
            go.layer = layer;

        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
