using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace ArcaneVR.Editor
{
    public static class ElectricColoseumSceneAligner
    {
        private const string ScenePath = "Assets/Scenes/ElectricColoseum.unity";
        private const string RigPrefabPath = "Assets/Prefabs/Player/ArcanePlayerRig.prefab";
        private const string LeftHandTrackingPrefabPath = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Prefabs/Left Hand Tracking.prefab";
        private const string RightHandTrackingPrefabPath = "Assets/Samples/XR Hands/1.7.3/HandVisualizer/Prefabs/Right Hand Tracking.prefab";

        [MenuItem("ArcaneVR/Align ElectricColoseum To Main Runtime")]
        public static void AlignElectricColoseum()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AlignScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void AlignScene(UnityScene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new System.InvalidOperationException("ElectricColoseum scene must be loaded before alignment.");

            var rigRoot = FindExistingRigRoot(scene);
            if (rigRoot == null)
                rigRoot = InstantiateRigPrefab(scene);

            AlignRigPosition(rigRoot.transform);
            DisableLegacyAuthority(scene, "XR Origin");
            DisableLegacyAuthority(scene, "MagicionPlayer");
            DisableDuplicateGrimoireSystems(scene, rigRoot.transform);
            EnsureHandTrackingHierarchy(scene, rigRoot.transform);
            WireRuntimeReferences(rigRoot.transform);
            EnsureBossTargetExists(scene);
            EnsureBinderExists(scene);
        }

        private static GameObject FindExistingRigRoot(UnityScene scene)
        {
            foreach (var rig in Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rig == null || rig.gameObject.scene != scene)
                    continue;

                if (rig.transform.root != null && rig.transform.root.name == "ArcanePlayerRig")
                    return rig.transform.root.gameObject;
            }

            var directRoot = GameObject.Find("ArcanePlayerRig");
            return directRoot != null && directRoot.scene == scene ? directRoot : null;
        }

        private static GameObject InstantiateRigPrefab(UnityScene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"Missing prefab: {RigPrefabPath}");

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException($"Failed to instantiate prefab: {RigPrefabPath}");

            instance.name = "ArcanePlayerRig";
            return instance;
        }

        private static void AlignRigPosition(Transform rigRoot)
        {
            var legacyRoot = GameObject.Find("MagicionPlayer")?.transform ?? GameObject.Find("XR Origin")?.transform;
            if (legacyRoot == null)
                return;

            rigRoot.position = legacyRoot.position;
            rigRoot.rotation = legacyRoot.rotation;
        }

        private static void DisableLegacyAuthority(UnityScene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null || root.name != objectName)
                    continue;

                root.SetActive(false);
                return;
            }
        }

        private static void DisableDuplicateGrimoireSystems(UnityScene scene, Transform rigRoot)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null ||
                    root.transform == rigRoot ||
                    root.name != "GrimoireSystem")
                {
                    continue;
                }

                if (root.GetComponentInChildren<GrimoireManager>(true) == null)
                    continue;

                root.SetActive(false);
            }
        }

        private static void EnsureHandTrackingHierarchy(UnityScene scene, Transform rigRoot)
        {
            if (rigRoot == null)
                return;

            var inputRoot = FindOrCreateChild(rigRoot, "00_InputCollection_PlayerXR");
            var xrOrigin = FindOrCreateChild(inputRoot, "XR Origin");
            xrOrigin.localPosition = Vector3.zero;
            xrOrigin.localRotation = Quaternion.identity;
            xrOrigin.localScale = Vector3.one;

            EnsureTrackingPrefab(scene, xrOrigin, LeftHandTrackingPrefabPath, "Left Hand Tracking");
            EnsureTrackingPrefab(scene, xrOrigin, RightHandTrackingPrefabPath, "Right Hand Tracking");
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
                return existing;

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static GameObject EnsureTrackingPrefab(UnityScene scene, Transform parent, string prefabPath, string objectName)
        {
            var existing = parent.Find(objectName);
            if (existing != null)
                return existing.gameObject;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"Missing hand tracking prefab: {prefabPath}");

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException($"Failed to instantiate hand tracking prefab: {prefabPath}");

            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void WireRuntimeReferences(Transform rigRoot)
        {
            if (rigRoot == null)
                return;

            var gestureDetector = rigRoot.GetComponentInChildren<GestureDetector>(true);
            var gestureRouter = rigRoot.GetComponentInChildren<GestureEventRouter>(true);
            var spellCaster = rigRoot.GetComponentInChildren<SpellCaster>(true);
            var combinationChecker = rigRoot.GetComponentInChildren<CombinationChecker>(true);
            var grimoireManager = rigRoot.GetComponentInChildren<GrimoireManager>(true);
            var feedbackManager = rigRoot.GetComponentInChildren<FeedbackManager>(true);
            var combatManager = rigRoot.GetComponentInChildren<CombatManager>(true);
            var focusController = rigRoot.GetComponentInChildren<CombinationFocusModeController>(true);
            var playerCamera = ArcanePlayerRigResolver.FindHeadTransform(rigRoot);
            var leftHandAnchor = ArcanePlayerRigResolver.FindHandTransform(true, playerCamera);
            var leftHandTrackingEvents = FindTrackingEvents(rigRoot, "Left Hand Tracking");
            var rightHandTrackingEvents = FindTrackingEvents(rigRoot, "Right Hand Tracking");

            WireObjectReference(gestureRouter, "gestureDetector", gestureDetector);
            WireObjectReference(combinationChecker, "gestureDetector", gestureDetector);
            WireObjectReference(combinationChecker, "grimoireManager", grimoireManager);

            WireObjectReference(grimoireManager, "gestureDetector", gestureDetector);
            WireObjectReference(grimoireManager, "gestureRouter", gestureRouter);
            WireObjectReference(grimoireManager, "playerCamera", playerCamera);
            WireObjectReference(grimoireManager, "leftHandBookAnchor", leftHandAnchor);
            WireObjectReference(grimoireManager, "spellCasters", spellCaster != null ? new Object[] { spellCaster } : null);

            WireObjectReference(spellCaster, "gestureDetector", gestureDetector);
            WireObjectReference(spellCaster, "grimoireManager", grimoireManager);
            WireObjectReference(spellCaster, "combinationChecker", combinationChecker);
            WireObjectReference(spellCaster, "feedbackManager", feedbackManager);
            WireObjectReference(spellCaster, "combatManager", combatManager);
            WireObjectReference(spellCaster, "focusModeController", focusController);
            WireObjectReference(spellCaster, "headTransform", playerCamera);
            WireObjectReference(spellCaster, "leftHandSpawnPoint", leftHandAnchor);
            WireObjectReference(spellCaster, "rightHandSpawnPoint", ArcanePlayerRigResolver.FindHandTransform(false, playerCamera));
            WireObjectReference(gestureDetector, "leftHandTrackingEvents", leftHandTrackingEvents);
            WireObjectReference(gestureDetector, "rightHandTrackingEvents", rightHandTrackingEvents);
            WireBooleanProperty(gestureDetector, "showPlayModeDebugOverlay", true);
            WireBooleanProperty(gestureDetector, "showDebugLog", true);
            WireBooleanProperty(gestureDetector, "useSubsystemPollingFallback", true);
        }

        private static XRHandTrackingEvents FindTrackingEvents(Transform rigRoot, string objectName)
        {
            if (rigRoot == null)
                return null;

            foreach (var trackingEvents in rigRoot.GetComponentsInChildren<XRHandTrackingEvents>(true))
            {
                if (trackingEvents != null && trackingEvents.name == objectName)
                    return trackingEvents;
            }

            return null;
        }

        private static void WireObjectReference(Object target, string propertyName, Object value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                return;

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void WireObjectReference(Object target, string propertyName, Object[] values)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return;

            property.arraySize = values?.Length ?? 0;
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void WireBooleanProperty(Object target, string propertyName, bool value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
                return;

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureBinderExists(UnityScene scene)
        {
            if (Object.FindAnyObjectByType<BossBattleRuntimeBinder>() != null)
                return;

            var battleManager = FindSceneRoot(scene, "BattleManager");
            if (battleManager == null)
                battleManager = new GameObject("BattleManager");

            if (battleManager.GetComponent<BossBattleRuntimeBinder>() == null)
                battleManager.AddComponent<BossBattleRuntimeBinder>();
        }

        private static void EnsureBossTargetExists(UnityScene scene)
        {
            if (Object.FindAnyObjectByType<GolemCombatTarget>() != null)
                return;

            var bossRoot = FindSceneRoot(scene, "ThunderGolemn") ??
                           FindSceneRoot(scene, "attack_golemn") ??
                           FindSceneRoot(scene, "Golem") ??
                           FindSceneRoot(scene, "Boss");
            if (bossRoot == null)
                return;

            if (bossRoot.GetComponent<GolemCombatTarget>() == null)
                bossRoot.AddComponent<GolemCombatTarget>();
        }

        private static GameObject FindSceneRoot(UnityScene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == objectName)
                    return root;
            }

            return null;
        }
    }
}
