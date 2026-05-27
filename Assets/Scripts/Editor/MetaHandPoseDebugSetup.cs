using ArcaneVR.Input;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Editor
{
    public static class MetaHandPoseDebugSetup
    {
        private const string DebugPanelName = "Arcane Meta Hand Pose Debug Panel";

        [MenuItem("ArcaneVR/Meta Gestures/Prepare Recognition Debug Scene")]
        public static void PrepareRecognitionDebugScene()
        {
            MetaInteractionHandPoseSetup.AddAllRecordedPosesToCurrentScene();
            AddTrackingGuardsToRecordedPoses();
            RestoreTransformChecksOnRecordedPoses();
            AddRecognitionDebugPanel();
        }

        [MenuItem("ArcaneVR/Meta Gestures/Restore Transform Checks On Recorded Poses")]
        public static void RestoreTransformChecksOnRecordedPoses()
        {
            var changedCount = 0;
            var bridges = UnityEngine.Object.FindObjectsByType<MetaHandPoseGestureBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var bridge in bridges)
            {
                if (RestoreTransformCheck(bridge.gameObject))
                    changedCount++;
            }

            if (changedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog(
                "Meta Hand Pose Transform Checks",
                changedCount > 0
                    ? $"Restored transform checks on {changedCount} recorded pose selector(s).\n\nThe debug panel will now show Transform:OK/-- instead of DISABLED when wrist/palm direction is evaluated."
                    : "No recorded pose selector transform checks needed changes.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Add Tracking Guards To Recorded Poses")]
        public static void AddTrackingGuardsToRecordedPoses()
        {
            var changedCount = 0;
            var bridges = UnityEngine.Object.FindObjectsByType<MetaHandPoseGestureBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var bridge in bridges)
            {
                if (EnsureTrackingGuard(bridge.gameObject))
                    changedCount++;
            }

            if (changedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog(
                "Meta Hand Pose Tracking Guards",
                changedCount > 0
                    ? $"Added/updated tracking guards on {changedCount} recorded pose selector(s).\n\nThese guards prevent cached shape states from selecting poses when the hand is not currently tracked."
                    : "No recorded pose selectors needed changes.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Add Recognition Debug Panel")]
        public static void AddRecognitionDebugPanel()
        {
            var panel = UnityEngine.Object.FindFirstObjectByType<MetaHandPoseRecognitionDebugPanel>(FindObjectsInactive.Include);
            GameObject panelObject;
            if (panel == null)
            {
                panelObject = new GameObject(DebugPanelName);
                Undo.RegisterCreatedObjectUndo(panelObject, $"Create {DebugPanelName}");
                panel = panelObject.AddComponent<MetaHandPoseRecognitionDebugPanel>();
            }
            else
            {
                panelObject = panel.gameObject;
            }

            panel.RefreshEntries();
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = panelObject;

            EditorUtility.DisplayDialog(
                "Meta Hand Pose Debug",
                "Recognition debug panel is ready.\n\nEnter Play Mode and perform each recorded pose. The Game View overlay will show Shape, Transform, and final ACTIVE state for every wired pose.\n\nPress the backquote (`) key to toggle the overlay.",
                "OK");
        }

        private static bool EnsureTrackingGuard(GameObject root)
        {
            if (root == null)
                return false;

            var hand = root.GetComponentInChildren<HandRef>(true);
            var group = root.GetComponentInChildren<ActiveStateGroup>(true);
            if (hand == null || group == null)
                return false;

            var guardType = FindType("ArcaneVR.Input.MetaHandTrackingActiveState");
            if (guardType == null)
            {
                Debug.LogError("[ArcaneVR] MetaHandTrackingActiveState type was not found. Wait for scripts to finish compiling, then run this menu again.");
                return false;
            }

            var changed = false;
            var guard = root.GetComponent(guardType);
            if (guard == null)
            {
                guard = Undo.AddComponent(root, guardType);
                changed = true;
            }

            var serializedGuard = new SerializedObject(guard);
            var handProperty = serializedGuard.FindProperty("hand");
            if (handProperty != null && handProperty.objectReferenceValue != hand)
            {
                handProperty.objectReferenceValue = hand;
                changed = true;
            }

            var confidenceProperty = serializedGuard.FindProperty("requireHighConfidence");
            if (confidenceProperty != null && !confidenceProperty.boolValue)
            {
                confidenceProperty.boolValue = true;
                changed = true;
            }

            serializedGuard.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(guard);

            var serializedGroup = new SerializedObject(group);
            var activeStates = serializedGroup.FindProperty("_activeStates");
            if (activeStates == null)
                return changed;

            if (!ContainsObjectReference(activeStates, guard))
            {
                activeStates.InsertArrayElementAtIndex(0);
                activeStates.GetArrayElementAtIndex(0).objectReferenceValue = guard;
                serializedGroup.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(group);
                changed = true;
            }

            return changed;
        }

        private static bool RestoreTransformCheck(GameObject root)
        {
            if (root == null)
                return false;

            var group = root.GetComponentInChildren<ActiveStateGroup>(true);
            var transformRecognizer = root.GetComponentInChildren<TransformRecognizerActiveState>(true);
            if (group == null || transformRecognizer == null)
                return false;

            var changed = false;
            if (!transformRecognizer.enabled)
            {
                Undo.RecordObject(transformRecognizer, "Enable Meta transform recognizer");
                transformRecognizer.enabled = true;
                EditorUtility.SetDirty(transformRecognizer);
                changed = true;
            }

            var serializedGroup = new SerializedObject(group);
            var activeStates = serializedGroup.FindProperty("_activeStates");
            if (activeStates == null)
                return changed;

            if (!ContainsObjectReference(activeStates, transformRecognizer))
            {
                activeStates.InsertArrayElementAtIndex(activeStates.arraySize);
                activeStates.GetArrayElementAtIndex(activeStates.arraySize - 1).objectReferenceValue = transformRecognizer;
                serializedGroup.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(group);
                changed = true;
            }

            return changed;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static bool ContainsObjectReference(SerializedProperty arrayProperty, UnityEngine.Object value)
        {
            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == value)
                    return true;
            }

            return false;
        }
    }
}
