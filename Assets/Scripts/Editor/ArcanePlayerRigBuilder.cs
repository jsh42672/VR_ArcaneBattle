using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Editor
{
    /// <summary>
    /// Tools > ArcaneVR > Build ArcanePlayerRig Prefab
    ///
    /// ArcanePlayerRig.prefab 구조:
    ///   ArcanePlayerRig (root)
    ///     XR Hands Gesture System
    ///       Left Hand Gestures   [XRHandTrackingEvents, Left]
    ///       Right Hand Gestures  [XRHandTrackingEvents, Right]
    ///     OVRCameraRig           (Meta XR SDK prefab)
    ///     GameSystems            (Assets/Prefabs/Core/GameSystems.prefab)
    ///     GrimoireSystem         (Assets/Prefabs/UI/GrimoireSystem.prefab)
    ///     SpellSpawnRoot         (마법 발사체 부모 빈 오브젝트)
    ///
    /// 인스펙터 참조 연결 목록:
    ///   GestureDetector.leftHandTrackingEvents  → Left Hand Gestures
    ///   GestureDetector.rightHandTrackingEvents → Right Hand Gestures
    ///   SpellCaster.leftHandSpawnPoint          → OVRCameraRig/LeftHandAnchor
    ///   SpellCaster.rightHandSpawnPoint         → OVRCameraRig/RightHandAnchor
    ///   SpellCaster.headTransform               → OVRCameraRig/CenterEyeAnchor
    ///   SpellCaster.spellSpawnRoot              → SpellSpawnRoot
    ///   SpellCaster.spellDatabase               → Assets/Data/SpellDatabase.asset
    ///   SpellCaster.grimoireManager             → GrimoireSystem/GrimoireManager
    ///   FeedbackManager.playerCamera            → OVRCameraRig/CenterEyeAnchor Camera
    /// </summary>
    public static class ArcanePlayerRigBuilder
    {
        private const string OvrCameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string GameSystemsPrefabPath  = "Assets/Prefabs/Core/GameSystems.prefab";
        private const string GrimoirePrefabPath     = "Assets/Prefabs/UI/GrimoireSystem.prefab";
        private const string SpellDatabasePath      = "Assets/Data/SpellDatabase.asset";
        private const string OutputPrefabPath       = "Assets/Prefabs/Player/ArcanePlayerRig.prefab";

        [MenuItem("ArcaneVR/Build ArcanePlayerRig Prefab")]
        public static void Build()
        {
            var ovrPrefab      = AssetDatabase.LoadAssetAtPath<GameObject>(OvrCameraRigPrefabPath);
            var gamesPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(GameSystemsPrefabPath);
            var grimoirePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrimoirePrefabPath);
            var spellDb        = AssetDatabase.LoadAssetAtPath<SpellDatabase>(SpellDatabasePath);

            if (!Validate(ovrPrefab,      OvrCameraRigPrefabPath)) return;
            if (!Validate(gamesPrefab,    GameSystemsPrefabPath))  return;
            if (!Validate(grimoirePrefab, GrimoirePrefabPath))     return;

            if (spellDb == null)
                Debug.LogWarning($"[ArcanePlayerRig] SpellDatabase 없음: {SpellDatabasePath} — SpellCaster.spellDatabase 는 수동 연결 필요");

            var root = new GameObject("ArcanePlayerRig");

            // ── 1. XR Hands Gesture System ──────────────────────────────────
            BuildXRHandsGestureSystem(root.transform,
                out var leftEvents, out var rightEvents);

            // ── 2. OVRCameraRig ─────────────────────────────────────────────
            var ovrGO = InstantiateChild(ovrPrefab, root.transform);

            var leftAnchor  = FindChild(ovrGO.transform, "LeftHandAnchor");
            var rightAnchor = FindChild(ovrGO.transform, "RightHandAnchor");
            var centerEye   = FindChild(ovrGO.transform, "CenterEyeAnchor");
            var eyeCamera   = centerEye != null ? centerEye.GetComponent<Camera>() : null;

            WarnIfNull(leftAnchor,  "OVRCameraRig/LeftHandAnchor");
            WarnIfNull(rightAnchor, "OVRCameraRig/RightHandAnchor");
            WarnIfNull(centerEye,   "OVRCameraRig/CenterEyeAnchor");

            // ── 3. GameSystems ──────────────────────────────────────────────
            var gamesGO = InstantiateChild(gamesPrefab, root.transform);

            // ── 4. GrimoireSystem ───────────────────────────────────────────
            var grimoireGO = InstantiateChild(grimoirePrefab, root.transform);

            // ── 5. SpellSpawnRoot ───────────────────────────────────────────
            var spellSpawnRoot = new GameObject("SpellSpawnRoot");
            spellSpawnRoot.transform.SetParent(root.transform, false);

            // ── 인스펙터 참조 연결 ───────────────────────────────────────────

            WireGestureDetector(gamesGO, leftEvents, rightEvents);
            WireSpellCaster(gamesGO, grimoireGO, leftAnchor, rightAnchor,
                centerEye, spellSpawnRoot.transform, spellDb);
            WireFeedbackManager(gamesGO, eyeCamera);

            // ── 저장 ─────────────────────────────────────────────────────────
            EnsureDirectory("Assets/Prefabs/Player");
            PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();

            Debug.Log($"[ArcanePlayerRig] 생성 완료 → {OutputPrefabPath}");
            EditorUtility.DisplayDialog(
                "ArcaneVR",
                $"ArcanePlayerRig.prefab 생성 완료\n\n{OutputPrefabPath}\n\n" +
                "※ Unity 인스펙터에서 누락 참조가 있는지 확인하세요.",
                "OK");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static void BuildXRHandsGestureSystem(
            Transform parent,
            out XRHandTrackingEvents leftEvents,
            out XRHandTrackingEvents rightEvents)
        {
            var system = new GameObject("XR Hands Gesture System");
            system.transform.SetParent(parent, false);

            leftEvents  = CreateHandGestureChild(system.transform, "Left Hand Gestures",  Handedness.Left);
            rightEvents = CreateHandGestureChild(system.transform, "Right Hand Gestures", Handedness.Right);
        }

        private static XRHandTrackingEvents CreateHandGestureChild(
            Transform parent, string goName, Handedness handedness)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            var events = go.AddComponent<XRHandTrackingEvents>();
            var so = new SerializedObject(events);
            var prop = so.FindProperty("m_Handedness");
            if (prop != null)
            {
                prop.intValue = (int)handedness;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[ArcanePlayerRig] XRHandTrackingEvents.m_Handedness 프로퍼티를 찾지 못함 — {goName} 수동 설정 필요");
            }

            return events;
        }

        private static void WireGestureDetector(
            GameObject gamesGO,
            XRHandTrackingEvents leftEvents,
            XRHandTrackingEvents rightEvents)
        {
            var detector = gamesGO.GetComponentInChildren<GestureDetector>(true);
            if (detector == null) { WarnMissing("GestureDetector"); return; }

            var so = new SerializedObject(detector);
            SetRef(so, "leftHandTrackingEvents",  leftEvents);
            SetRef(so, "rightHandTrackingEvents", rightEvents);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[ArcanePlayerRig] GestureDetector 참조 연결 완료");
        }

        private static void WireSpellCaster(
            GameObject gamesGO,
            GameObject grimoireGO,
            Transform leftAnchor,
            Transform rightAnchor,
            Transform centerEye,
            Transform spellSpawnRoot,
            SpellDatabase spellDb)
        {
            var caster = gamesGO.GetComponentInChildren<SpellCaster>(true);
            if (caster == null) { WarnMissing("SpellCaster"); return; }

            var grimoireMgr = grimoireGO != null
                ? grimoireGO.GetComponentInChildren<GrimoireManager>(true)
                : null;

            var so = new SerializedObject(caster);
            SetRef(so, "leftHandSpawnPoint",  leftAnchor);
            SetRef(so, "rightHandSpawnPoint", rightAnchor);
            SetRef(so, "headTransform",       centerEye);
            SetRef(so, "spellSpawnRoot",      spellSpawnRoot);
            if (spellDb    != null) SetRef(so, "spellDatabase",   spellDb);
            if (grimoireMgr != null) SetRef(so, "grimoireManager", grimoireMgr);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[ArcanePlayerRig] SpellCaster 참조 연결 완료");
        }

        private static void WireFeedbackManager(GameObject gamesGO, Camera eyeCamera)
        {
            var feedback = gamesGO.GetComponentInChildren<FeedbackManager>(true);
            if (feedback == null) { WarnMissing("FeedbackManager"); return; }
            if (eyeCamera == null) { Debug.LogWarning("[ArcanePlayerRig] CenterEyeAnchor Camera 없음 — FeedbackManager.playerCamera 수동 연결 필요"); return; }

            var so = new SerializedObject(feedback);
            SetRef(so, "playerCamera", eyeCamera);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[ArcanePlayerRig] FeedbackManager 참조 연결 완료");
        }

        // ─────────────────────────────────────────────────────────────────────

        private static GameObject InstantiateChild(GameObject prefab, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static void SetRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = value;
            else
                Debug.LogWarning($"[ArcanePlayerRig] 프로퍼티 없음: {propName}");
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }

        private static bool Validate(Object obj, string path)
        {
            if (obj != null) return true;
            var msg = $"에셋 없음: {path}";
            Debug.LogError($"[ArcanePlayerRig] {msg}");
            EditorUtility.DisplayDialog("ArcaneVR", msg, "OK");
            return false;
        }

        private static void WarnIfNull(Object obj, string label)
        {
            if (obj == null)
                Debug.LogWarning($"[ArcanePlayerRig] {label} 를 찾지 못함 — 수동 연결 필요");
        }

        private static void WarnMissing(string componentName)
        {
            Debug.LogWarning($"[ArcanePlayerRig] {componentName} 컴포넌트 없음 — 확인 필요");
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
