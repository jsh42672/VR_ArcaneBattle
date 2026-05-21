using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools > ArcaneVR > Phase4 메뉴에서 실행
/// 3개 Colosseum 씬에 흩어진 공통 오브젝트를 Prefab으로 추출하고 교체한다.
public static class Phase4PrefabExtractor
{
    private static readonly string[] ColosseumScenes =
    {
        "Assets/Scenes/ElectricColoseum.unity",
        "Assets/Scenes/FireColoseum.unity",
        "Assets/Scenes/IceColoseum.unity",
    };

    private const string GrimoirePrefabPath = "Assets/Prefabs/UI/GrimoireSystem.prefab";
    private const string CombatPrefabDir    = "Assets/Prefabs/Combat";
    private const string BattlePrefabPath   = "Assets/Prefabs/Combat/BattleManager.prefab";

    // ---------------------------------------------------------------
    [MenuItem("Tools/ArcaneVR/Phase4 - 1) Extract GrimoireSystem Prefab")]
    public static void ExtractGrimoireSystem()
    {
        EnsureDirectory("Assets/Prefabs/UI");

        bool createdPrefab = false;

        foreach (var scenePath in ColosseumScenes)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[Phase4] 씬 없음: {scenePath}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var grimoire = GameObject.Find("GrimoireSystem");
            if (grimoire == null)
            {
                Debug.Log($"[Phase4] GrimoireSystem 없음: {scenePath}");
                continue;
            }

            // 첫 씬에서 Prefab 생성
            if (!createdPrefab)
            {
                // 이미 Prefab 인스턴스면 원본 Prefab 교체
                string existingGuid = AssetDatabase.AssetPathToGUID(GrimoirePrefabPath);
                if (string.IsNullOrEmpty(existingGuid))
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(
                        grimoire, GrimoirePrefabPath, InteractionMode.AutomatedAction);
                    Debug.Log($"[Phase4] Prefab 생성: {GrimoirePrefabPath}");
                }
                createdPrefab = true;
            }
            else
            {
                // 이후 씬: 기존 오브젝트 제거 후 Prefab 인스턴스 배치
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GrimoirePrefabPath);
                if (prefabAsset == null)
                {
                    Debug.LogError("[Phase4] Prefab 로드 실패");
                    continue;
                }

                var worldPos = grimoire.transform.position;
                var worldRot = grimoire.transform.rotation;

                Object.DestroyImmediate(grimoire);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                instance.transform.SetPositionAndRotation(worldPos, worldRot);
                Debug.Log($"[Phase4] Prefab 인스턴스 교체 완료: {scenePath}");
            }

            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log("[Phase4] GrimoireSystem 작업 완료");
    }

    // ---------------------------------------------------------------
    [MenuItem("Tools/ArcaneVR/Phase4 - 2) Extract BattleManager Prefab")]
    public static void ExtractBattleManager()
    {
        EnsureDirectory(CombatPrefabDir);

        bool createdPrefab = false;

        foreach (var scenePath in ColosseumScenes)
        {
            if (!File.Exists(scenePath))
                continue;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var bm = GameObject.Find("BattleManager");
            if (bm == null)
            {
                Debug.Log($"[Phase4] BattleManager 없음: {scenePath} (스킵)");
                continue;
            }

            if (!createdPrefab)
            {
                string existingGuid = AssetDatabase.AssetPathToGUID(BattlePrefabPath);
                if (string.IsNullOrEmpty(existingGuid))
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(
                        bm, BattlePrefabPath, InteractionMode.AutomatedAction);
                    Debug.Log($"[Phase4] Prefab 생성: {BattlePrefabPath}");
                }
                createdPrefab = true;
            }
            else
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BattlePrefabPath);
                if (prefabAsset == null)
                {
                    Debug.LogError("[Phase4] BattleManager Prefab 로드 실패");
                    continue;
                }

                var worldPos = bm.transform.position;
                var worldRot = bm.transform.rotation;

                Object.DestroyImmediate(bm);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                instance.transform.SetPositionAndRotation(worldPos, worldRot);
                Debug.Log($"[Phase4] BattleManager Prefab 인스턴스 교체: {scenePath}");
            }

            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log("[Phase4] BattleManager 작업 완료");
    }

    // ---------------------------------------------------------------
    [MenuItem("Tools/ArcaneVR/Phase4 - 3) Report Scene Common Objects")]
    public static void ReportSceneCommonObjects()
    {
        string[] targets = { "GrimoireSystem", "BattleManager", "XR Input Diagnostics",
                             "Arcane Test Hub", "PostProcess_Volume", "Portal_Exit" };

        var currentScene = EditorSceneManager.GetActiveScene().path;

        foreach (var scenePath in ColosseumScenes)
        {
            if (!File.Exists(scenePath))
                continue;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"\n=== {Path.GetFileNameWithoutExtension(scenePath)} ===");

            foreach (var name in targets)
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    Debug.Log($"  [ 없음] {name}");
                    continue;
                }

                bool isPrefab = PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.Connected;
                string prefabPath = isPrefab
                    ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go))
                    : "(씬 오브젝트)";

                Debug.Log($"  [  있음] {name}  →  {prefabPath}");
            }
        }

        if (!string.IsNullOrEmpty(currentScene))
            EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);

        Debug.Log("\n[Phase4] 리포트 완료 - 위 로그를 Console에서 확인하세요");
    }

    // ---------------------------------------------------------------
    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
