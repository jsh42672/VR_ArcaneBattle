using ArcaneVR.UI.Quest;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OpenWorldMissionHUDSceneSetup
{
    [MenuItem("ArcaneVR/Open World/Setup Mission HUD")]
    public static void Execute()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("[OpenWorldMissionHUD] Main Camera를 찾을 수 없습니다.");
            return;
        }

        GameObject hudObject = GameObject.Find("OpenWorld Mission HUD");
        if (hudObject == null)
        {
            hudObject = new GameObject("OpenWorld Mission HUD", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(hudObject, "Create OpenWorld Mission HUD");
        }

        OpenWorldMissionHUD hud = hudObject.GetComponent<OpenWorldMissionHUD>();
        if (hud == null)
            hud = Undo.AddComponent<OpenWorldMissionHUD>(hudObject);

        SerializedObject serializedHud = new(hud);
        serializedHud.FindProperty("headerText").stringValue = "임무";
        serializedHud.FindProperty("vrCamera").objectReferenceValue = camera.transform;
        serializedHud.FindProperty("followDistance").floatValue = 1.85f;
        serializedHud.FindProperty("viewportOffset").vector2Value = new Vector2(-0.72f, 0.44f);
        serializedHud.FindProperty("followSpeed").floatValue = 8f;
        serializedHud.FindProperty("panelSize").vector2Value = new Vector2(560f, 210f);
        serializedHud.FindProperty("backgroundAlpha").floatValue = 0.55f;
        serializedHud.FindProperty("canvasScale").floatValue = 0.0015f;

        SerializedProperty missions = serializedHud.FindProperty("missions");
        missions.arraySize = 2;
        SetMission(missions.GetArrayElementAtIndex(0), "마법을 더미에 맞추세요");
        SetMission(missions.GetArrayElementAtIndex(1), "포탈을 타세요");

        serializedHud.ApplyModifiedProperties();

        hud.RebuildHudLayout();
        Selection.activeGameObject = hudObject;

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[OpenWorldMissionHUD] OPenWorld2 씬에 VR 임무 HUD를 배치했습니다. 씬을 저장하세요.");
    }

    public static void SaveActiveScene()
    {
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[OpenWorldMissionHUD] 현재 씬을 저장했습니다.");
    }

    public static string CapturePreview()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return "Main Camera를 찾을 수 없습니다.";

        OpenWorldMissionHUD hud = Object.FindFirstObjectByType<OpenWorldMissionHUD>();
        if (hud == null)
            return "OpenWorldMissionHUD를 찾을 수 없습니다.";

        hud.RebuildHudLayout();
        Canvas.ForceUpdateCanvases();

        const int width = 1280;
        const int height = 720;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new(width, height, TextureFormat.RGBA32, false);

        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture.active = renderTexture;
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();

        camera.targetTexture = previousTarget;
        RenderTexture.active = null;

        string directory = "Assets/CodexGenerated/OpenWorld2";
        string path = Path.Combine(directory, "MissionHUDPreview.png");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, image.EncodeToPNG());

        Object.DestroyImmediate(image);
        Object.DestroyImmediate(renderTexture);

        AssetDatabase.ImportAsset(path);
        Debug.Log("[OpenWorldMissionHUD] Preview saved: " + path);
        return path;
    }

    private static void SetMission(SerializedProperty mission, string text)
    {
        mission.FindPropertyRelative("text").stringValue = text;
        mission.FindPropertyRelative("visible").boolValue = true;
        mission.FindPropertyRelative("completed").boolValue = false;
    }
}
