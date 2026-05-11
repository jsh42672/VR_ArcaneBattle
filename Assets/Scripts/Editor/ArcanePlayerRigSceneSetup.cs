using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityScene = UnityEngine.SceneManagement.Scene;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ArcaneVR.Editor
{
    public static class ArcanePlayerRigSceneSetup
    {
        private const string OvrCameraRigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string OvrHandPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab";
        private static readonly string[] RuntimeScenePaths =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/World.unity",
            "Assets/Scenes/World_main.unity",
            "Assets/Scenes/Tutorial.unity",
            "Assets/Scenes/BattleSceen2.unity",
            "Assets/Scenes/FireColoseum.unity",
            "Assets/Scenes/IceColoseum.unity",
            "Assets/Scenes/ElectricColoseum.unity"
        };

        [MenuItem("ArcaneVR/Scenes/Install Main OVR Player Rig In Active Scene")]
        public static void InstallInActiveScene()
        {
            var scene = UnitySceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Arcane Player Rig", "No active scene is loaded.", "OK");
                return;
            }

            var changed = InstallInScene(scene, true);
            if (changed)
                EditorUtility.DisplayDialog("Arcane Player Rig", $"OVR player rig prepared in {scene.name}. Save the scene before Build & Run.", "OK");
        }

        [MenuItem("ArcaneVR/Scenes/Install Main OVR Player Rig In World And Battle")]
        public static void InstallInWorldAndBattleScenes()
        {
            InstallInRuntimeScenes();
        }

        [MenuItem("ArcaneVR/Scenes/Install Main OVR Player Rig In Runtime Scenes")]
        public static void InstallInRuntimeScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!EditorUtility.DisplayDialog(
                    "Arcane Player Rig",
                    "This will open all runtime scenes, add the Main-style OVRCameraRig with left/right OVR hands when missing, and disable the old XR Origin object instead of deleting it.",
                    "Proceed",
                    "Cancel"))
            {
                return;
            }

            foreach (var scenePath in RuntimeScenePaths)
                InstallAndSaveScene(scenePath);

            EditorUtility.DisplayDialog("Arcane Player Rig", "Runtime scenes were prepared. Reopen your working scene if needed.", "OK");
        }

        public static void InstallInWorldAndBattleScenesBatch()
        {
            InstallInRuntimeScenesBatch();
        }

        public static void InstallInRuntimeScenesBatch()
        {
            foreach (var scenePath in RuntimeScenePaths)
                InstallAndSaveScene(scenePath, true);
        }

        private static void InstallAndSaveScene(string scenePath)
        {
            InstallAndSaveScene(scenePath, false);
        }

        private static void InstallAndSaveScene(string scenePath, bool batchMode)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[ArcanePlayerRigSceneSetup] Could not open scene: {scenePath}");
                return;
            }

            if (InstallInScene(scene, false, batchMode))
                EditorSceneManager.SaveScene(scene);
        }

        private static bool InstallInScene(UnityScene scene, bool showDialog)
        {
            return InstallInScene(scene, showDialog, false);
        }

        private static bool InstallInScene(UnityScene scene, bool showDialog, bool batchMode)
        {
            var cameraRig = FindOvrCameraRig(scene);
            var legacyXrOrigin = FindSceneRoot(scene, "XR Origin") ?? FindSceneRoot(scene, "XROriginCameraRig");
            var changed = false;

            if (cameraRig == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OvrCameraRigPrefabPath);
                if (prefab == null)
                {
                    ReportSetupIssue($"Missing prefab: {OvrCameraRigPrefabPath}", batchMode);
                    return false;
                }

                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    ReportSetupIssue("Failed to instantiate OVRCameraRig prefab.", batchMode);
                    return false;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Create Arcane OVR Player Rig");
                instance.name = "OVRCameraRig";
                CopyTransformOrReset(instance.transform, legacyXrOrigin != null ? legacyXrOrigin.transform : null);
                TryAssignPlayerTag(instance);
                cameraRig = instance.GetComponent<OVRCameraRig>();
                changed = true;
            }
            else
            {
                TryAssignPlayerTag(cameraRig.gameObject);
            }

            if (cameraRig == null)
            {
                ReportSetupIssue("OVRCameraRig component was not found after setup.", batchMode);
                return changed;
            }

            changed |= ConfigureOvrCameraRig(cameraRig);
            changed |= EnsureHand(cameraRig, true, batchMode);
            changed |= EnsureHand(cameraRig, false, batchMode);

            if (legacyXrOrigin != null && legacyXrOrigin.activeSelf)
            {
                var shouldDisable = !showDialog || EditorUtility.DisplayDialog(
                    "Arcane Player Rig",
                    $"Disable legacy rig '{legacyXrOrigin.name}' to avoid duplicate camera/input roots?",
                    "Disable",
                    "Keep Active");

                if (shouldDisable)
                {
                    Undo.RecordObject(legacyXrOrigin, "Disable Legacy XR Origin");
                    legacyXrOrigin.name = "XR Origin (Legacy Disabled)";
                    legacyXrOrigin.SetActive(false);
                    changed = true;
                }
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);

            return changed;
        }

        private static bool ConfigureOvrCameraRig(OVRCameraRig cameraRig)
        {
            if (cameraRig == null)
                return false;

            var changed = false;

            if (cameraRig.useFixedUpdateForTracking)
            {
                Undo.RecordObject(cameraRig, "Configure Arcane OVR Camera Rig");
                cameraRig.useFixedUpdateForTracking = false;
                EditorUtility.SetDirty(cameraRig);
                changed = true;
            }

            var manager = cameraRig.GetComponent<OVRManager>();
            if (manager == null)
                return changed;

            var managerChanged = false;
            Undo.RecordObject(manager, "Configure Arcane OVR Manager");

            if (manager.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel)
            {
                manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                managerChanged = true;
            }

            if (!manager.usePositionTracking)
            {
                manager.usePositionTracking = true;
                managerChanged = true;
            }

            if (!manager.enableDynamicResolution)
            {
                manager.enableDynamicResolution = true;
                managerChanged = true;
            }

            if (!managerChanged)
                return changed;

            EditorUtility.SetDirty(manager);
            return true;
        }

        private static bool EnsureHand(OVRCameraRig cameraRig, bool isLeft, bool batchMode)
        {
            var anchor = FindChildByName(cameraRig.transform, isLeft ? "LeftHandAnchor" : "RightHandAnchor");
            if (anchor == null)
            {
                Debug.LogWarning($"[ArcanePlayerRigSceneSetup] Missing {(isLeft ? "left" : "right")} hand anchor on {cameraRig.name}.");
                return false;
            }

            var existingHandObject = FindExistingHandObject(anchor, isLeft);
            if (existingHandObject != null)
            {
                Undo.RecordObject(existingHandObject.transform, "Repair Arcane OVR Hand");
                existingHandObject.name = isLeft ? "Left OVRHandPrefab" : "Right OVRHandPrefab";
                existingHandObject.transform.SetParent(anchor, false);
                existingHandObject.transform.localPosition = Vector3.zero;
                existingHandObject.transform.localRotation = Quaternion.identity;
                existingHandObject.transform.localScale = Vector3.one;
                ConfigureHandSerializedFields(existingHandObject, isLeft);
                EditorUtility.SetDirty(existingHandObject);
                return true;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OvrHandPrefabPath);
            if (prefab == null)
            {
                ReportSetupIssue($"Missing prefab: {OvrHandPrefabPath}", batchMode);
                return false;
            }

            var handObject = PrefabUtility.InstantiatePrefab(prefab, cameraRig.gameObject.scene) as GameObject;
            if (handObject == null)
                return false;

            Undo.RegisterCreatedObjectUndo(handObject, "Create Arcane OVR Hand");
            handObject.name = isLeft ? "Left OVRHandPrefab" : "Right OVRHandPrefab";
            handObject.transform.SetParent(anchor, false);
            handObject.transform.localPosition = Vector3.zero;
            handObject.transform.localRotation = Quaternion.identity;
            handObject.transform.localScale = Vector3.one;
            ConfigureHandSerializedFields(handObject, isLeft);
            EditorUtility.SetDirty(handObject);
            return true;
        }

        private static GameObject FindExistingHandObject(Transform anchor, bool isLeft)
        {
            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            GameObject fallback = null;

            foreach (var hand in anchor.GetComponentsInChildren<OVRHand>(true))
            {
                if (hand != null && hand.GetHand() == expected)
                    return hand.gameObject;

                if (hand != null && fallback == null)
                    fallback = hand.gameObject;
            }

            foreach (var child in anchor.GetComponentsInChildren<Transform>(true))
            {
                if (child == anchor || child == null)
                    continue;

                if (child.name.Contains(isLeft ? "Left OVRHand" : "Right OVRHand"))
                    return child.gameObject;

                if (fallback == null && child.name.Contains("OVRHand"))
                    fallback = child.gameObject;
            }

            return fallback;
        }

        private static void ConfigureHandSerializedFields(GameObject handObject, bool isLeft)
        {
            var handIndex = isLeft ? 0 : 1;
            foreach (var component in handObject.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;

                var serializedObject = new SerializedObject(component);
                var changed = false;

                changed |= TrySetInt(serializedObject, "HandType", handIndex);
                changed |= TrySetInt(serializedObject, "_skeletonType", handIndex);
                changed |= TrySetInt(serializedObject, "_meshType", handIndex);
                changed |= TrySetBool(serializedObject, "_updateRootPose", false);

                if (!changed)
                    continue;

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
            }
        }

        private static bool TrySetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return false;

            if (property.propertyType == SerializedPropertyType.Integer ||
                property.propertyType == SerializedPropertyType.Enum)
            {
                property.intValue = value;
                return true;
            }

            return false;
        }

        private static bool TrySetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
                return false;

            property.boolValue = value;
            return true;
        }

        private static OVRCameraRig FindOvrCameraRig(UnityScene scene)
        {
            foreach (var cameraRig in Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include))
            {
                if (cameraRig != null && cameraRig.gameObject.scene == scene)
                    return cameraRig;
            }

            return null;
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

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static void CopyTransformOrReset(Transform target, Transform source)
        {
            if (source == null)
            {
                target.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return;
            }

            target.SetPositionAndRotation(source.position, source.rotation);
        }

        private static void TryAssignPlayerTag(GameObject rigObject)
        {
            if (rigObject == null || rigObject.tag == "Player")
                return;

            try
            {
                rigObject.tag = "Player";
            }
            catch (UnityException)
            {
                Debug.LogWarning("[ArcanePlayerRigSceneSetup] Player tag is not available. Rig detection will still use OVRCameraRig.");
            }
        }

        private static void ReportSetupIssue(string message, bool batchMode)
        {
            Debug.LogWarning($"[ArcanePlayerRigSceneSetup] {message}");
            if (!batchMode)
                EditorUtility.DisplayDialog("Arcane Player Rig", message, "OK");
        }
    }
}
