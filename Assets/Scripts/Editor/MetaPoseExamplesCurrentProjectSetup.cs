using ArcaneVR.Input;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Editor
{
    public static class MetaPoseExamplesCurrentProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/MetaPoseExamplesTest.unity";
        private const string SceneRootName = "Arcane Meta PoseExamples Test";
        private const string DebugPanelName = "Arcane Meta Hand Pose Debug Panel";

        private static readonly string[] PosePrefabNames =
        {
            "ThumbsUpPose",
            "ThumbsDownPose",
            "RockPose",
            "PaperPose",
            "ScissorsPose",
            "StopPose"
        };

        [MenuItem("ArcaneVR/Meta Gestures/Create Current Project PoseExamples Scene")]
        public static void CreateCurrentProjectPoseExamplesScene()
        {
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RemoveDefaultMainCamera();

            MetaInteractionHandPoseSetup.AddRecorderProvidersToCurrentScene();

            var root = FindSceneObjectByName(SceneRootName)?.gameObject;
            if (root == null)
            {
                root = new GameObject(SceneRootName);
                Undo.RegisterCreatedObjectUndo(root, $"Create {SceneRootName}");
            }

            var createdOrUpdated = 0;
            createdOrUpdated += AddPoseSet(root.transform, MetaHandPoseGestureBridge.Handedness.Left);
            createdOrUpdated += AddPoseSet(root.transform, MetaHandPoseGestureBridge.Handedness.Right);
            AddGuide(root.transform);
            AddRecognitionDebugPanel();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ArcaneVR] Created {ScenePath} with {createdOrUpdated} Meta pose selector instance(s). Enter Play Mode on Quest and use the Game View overlay to verify Hand, Shape, Transform, Strict, and final OK states.");
        }

        private static int AddPoseSet(Transform parent, MetaHandPoseGestureBridge.Handedness hand)
        {
            var pair = FindProviderPair(hand);
            if (!pair.IsValid)
            {
                Debug.LogError($"[ArcaneVR] Could not find {hand} Meta pose providers. Run ArcaneVR > Meta Gestures > Repair Recorder Scene Wiring, then try again.");
                return 0;
            }

            var count = 0;
            for (var i = 0; i < PosePrefabNames.Length; i++)
            {
                var poseName = PosePrefabNames[i];
                var objectName = $"{hand}_{poseName}";
                var instance = FindSceneObjectByName(objectName)?.gameObject;
                if (instance == null)
                {
                    var prefab = LoadPosePrefab(poseName);
                    if (prefab == null)
                        continue;

                    instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                    if (instance == null)
                        continue;

                    instance.name = objectName;
                    instance.transform.localPosition = new Vector3((i - 2.5f) * 0.35f, hand == MetaHandPoseGestureBridge.Handedness.Left ? 1.45f : 1.1f, 1.4f);
                    Undo.RegisterCreatedObjectUndo(instance, $"Add {objectName}");
                }
                else if (instance.transform.parent != parent)
                {
                    Undo.SetTransformParent(instance.transform, parent, $"Parent {objectName}");
                }

                WirePoseInstance(instance, objectName, pair);
                count++;
            }

            return count;
        }

        private static GameObject LoadPosePrefab(string poseName)
        {
            var path = $"Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/HandPose/{poseName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;

            Debug.LogError($"[ArcaneVR] Meta pose prefab not found: {path}");
            return null;
        }

        private static void WirePoseInstance(GameObject root, string poseName, ProviderPair pair)
        {
            var handObject = GetSerializedObjectReference(pair.FingerProvider, "_hand");
            if (handObject == null && pair.FingerProvider.Hand is Object runtimeHandObject)
                handObject = runtimeHandObject;

            foreach (var handRef in root.GetComponentsInChildren<HandRef>(true))
            {
                if (handObject is IHand hand)
                {
                    Undo.RecordObject(handRef, "Wire Meta hand reference");
                    handRef.InjectHand(hand);
                    EditorUtility.SetDirty(handRef);
                }
            }

            foreach (var fingerRef in root.GetComponentsInChildren<FingerFeatureStateProviderRef>(true))
            {
                Undo.RecordObject(fingerRef, "Wire Meta finger feature provider");
                fingerRef.InjectFingerFeatureStateProvider(pair.FingerProvider);
                EditorUtility.SetDirty(fingerRef);
            }

            foreach (var transformRef in root.GetComponentsInChildren<TransformFeatureStateProviderRef>(true))
            {
                Undo.RecordObject(transformRef, "Wire Meta transform feature provider");
                transformRef.InjectTransformFeatureStateProvider(pair.TransformProvider);
                EditorUtility.SetDirty(transformRef);
            }

            foreach (var shapeRecognizer in root.GetComponentsInChildren<ShapeRecognizerActiveState>(true))
            {
                Undo.RecordObject(shapeRecognizer, "Wire Meta shape recognizer provider");
#pragma warning disable CS0612
                if (handObject is IHand hand)
                    shapeRecognizer.InjectHand(hand);
#pragma warning restore CS0612
                shapeRecognizer.InjectFingerFeatureStateProvider(pair.FingerProvider);
                EditorUtility.SetDirty(shapeRecognizer);
            }

            foreach (var transformRecognizer in root.GetComponentsInChildren<TransformRecognizerActiveState>(true))
            {
                Undo.RecordObject(transformRecognizer, "Wire Meta transform recognizer provider");
#pragma warning disable CS0612
                if (handObject is IHand hand)
                    transformRecognizer.InjectHand(hand);
#pragma warning restore CS0612
                transformRecognizer.InjectTransformFeatureStateProvider(pair.TransformProvider);
                EditorUtility.SetDirty(transformRecognizer);
            }

            var hmd = FindConcreteHmdSource();
            foreach (var hmdOffset in root.GetComponentsInChildren<HmdOffset>(true))
            {
                if (hmd == null)
                {
                    Debug.LogWarning($"[ArcaneVR] Could not wire HmdOffset on {root.name} because no concrete IHmd source was found.");
                    continue;
                }

                Undo.RecordObject(hmdOffset, "Wire Meta HMD offset");
                hmdOffset.InjectHmd(hmd);
                EditorUtility.SetDirty(hmdOffset);
            }

            EnsureTrackingGuard(root);
            EnsureTransformChecks(root);
            EnsureDebugLogger(root, poseName);
        }

        private static void EnsureTrackingGuard(GameObject root)
        {
            var hand = root.GetComponentInChildren<HandRef>(true);
            var group = root.GetComponentInChildren<ActiveStateGroup>(true);
            if (hand == null || group == null)
                return;

            var guard = root.GetComponent<MetaHandTrackingActiveState>();
            if (guard == null)
                guard = Undo.AddComponent<MetaHandTrackingActiveState>(root);

            guard.Configure(hand, true);
            EditorUtility.SetDirty(guard);

            var serializedGroup = new SerializedObject(group);
            var activeStates = serializedGroup.FindProperty("_activeStates");
            if (activeStates == null || ContainsObjectReference(activeStates, guard))
                return;

            activeStates.InsertArrayElementAtIndex(0);
            activeStates.GetArrayElementAtIndex(0).objectReferenceValue = guard;
            serializedGroup.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(group);
        }

        private static void EnsureTransformChecks(GameObject root)
        {
            var group = root.GetComponentInChildren<ActiveStateGroup>(true);
            var transformRecognizer = root.GetComponentInChildren<TransformRecognizerActiveState>(true);
            if (group == null || transformRecognizer == null)
                return;

            if (!transformRecognizer.enabled)
            {
                Undo.RecordObject(transformRecognizer, "Enable Meta transform recognizer");
                transformRecognizer.enabled = true;
                EditorUtility.SetDirty(transformRecognizer);
            }

            var serializedGroup = new SerializedObject(group);
            var activeStates = serializedGroup.FindProperty("_activeStates");
            if (activeStates == null || ContainsObjectReference(activeStates, transformRecognizer))
                return;

            activeStates.InsertArrayElementAtIndex(activeStates.arraySize);
            activeStates.GetArrayElementAtIndex(activeStates.arraySize - 1).objectReferenceValue = transformRecognizer;
            serializedGroup.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(group);
        }

        private static void EnsureDebugLogger(GameObject root, string poseName)
        {
            var wrapper = root.GetComponentInChildren<SelectorUnityEventWrapper>(true);
            if (wrapper == null)
                return;

            var logger = root.GetComponent<MetaHandPoseDebugLogger>();
            if (logger == null)
                logger = Undo.AddComponent<MetaHandPoseDebugLogger>(root);

            SetSerializedString(logger, "poseName", poseName);
            RemovePersistentListener(wrapper.WhenSelected, logger, nameof(MetaHandPoseDebugLogger.LogSelected));
            RemovePersistentListener(wrapper.WhenUnselected, logger, nameof(MetaHandPoseDebugLogger.LogUnselected));
            UnityEventTools.AddPersistentListener(wrapper.WhenSelected, logger.LogSelected);
            UnityEventTools.AddPersistentListener(wrapper.WhenUnselected, logger.LogUnselected);
            EditorUtility.SetDirty(wrapper);
            EditorUtility.SetDirty(logger);
        }

        private static void AddRecognitionDebugPanel()
        {
            var panel = Object.FindFirstObjectByType<MetaHandPoseRecognitionDebugPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                var panelObject = new GameObject(DebugPanelName);
                Undo.RegisterCreatedObjectUndo(panelObject, $"Create {DebugPanelName}");
                panel = panelObject.AddComponent<MetaHandPoseRecognitionDebugPanel>();
            }

            panel.RefreshEntries();
            EditorUtility.SetDirty(panel);
        }

        private static void AddGuide(Transform parent)
        {
            var guide = parent.Find("Guide");
            var guideObject = guide != null ? guide.gameObject : new GameObject("Guide");
            guideObject.transform.SetParent(parent, false);
            guideObject.transform.localPosition = new Vector3(0f, 1.85f, 1.8f);
            guideObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var text = guideObject.GetComponent<TextMesh>();
            if (text == null)
                text = guideObject.AddComponent<TextMesh>();

            text.text =
                "Current Project Meta PoseExamples\n" +
                "Official Meta pose prefabs wired to this project's UnityXR hand providers.\n" +
                "Test: Thumbs Up, Thumbs Down, Rock, Paper, Scissors, Stop.\n" +
                "Use overlay: OK requires live hand + shape + transform.";
            text.characterSize = 0.065f;
            text.fontSize = 56;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            EditorUtility.SetDirty(text);
        }

        private readonly struct ProviderPair
        {
            public ProviderPair(FingerFeatureStateProvider fingerProvider, TransformFeatureStateProvider transformProvider)
            {
                FingerProvider = fingerProvider;
                TransformProvider = transformProvider;
            }

            public FingerFeatureStateProvider FingerProvider { get; }
            public TransformFeatureStateProvider TransformProvider { get; }
            public bool IsValid => FingerProvider != null && TransformProvider != null;
        }

        private static ProviderPair FindProviderPair(MetaHandPoseGestureBridge.Handedness hand)
        {
            var fingers = Object.FindObjectsByType<FingerFeatureStateProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var transforms = Object.FindObjectsByType<TransformFeatureStateProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ProviderPair bestPair = default;
            var bestScore = int.MinValue;

            foreach (var finger in fingers)
            {
                var fingerHand = GetSerializedObjectReference(finger, "_hand");
                foreach (var transformProvider in transforms)
                {
                    var transformHand = GetSerializedObjectReference(transformProvider, "_hand");
                    if (finger.gameObject != transformProvider.gameObject &&
                        fingerHand != null &&
                        transformHand != null &&
                        fingerHand != transformHand)
                    {
                        continue;
                    }

                    var score = ScoreProviderSide(finger, fingerHand, hand) +
                                ScoreProviderSide(transformProvider, transformHand, hand);
                    if (finger.gameObject == transformProvider.gameObject)
                        score += 10;

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestPair = new ProviderPair(finger, transformProvider);
                }
            }

            return bestScore > 0 ? bestPair : default;
        }

        private static int ScoreProviderSide(Component provider, Object handObject, MetaHandPoseGestureBridge.Handedness hand)
        {
            var score = ScoreTextForHand(GetHierarchyPath(provider.transform), hand);
            if (handObject is IHand metaHand)
            {
                try
                {
                    var expected = hand == MetaHandPoseGestureBridge.Handedness.Right
                        ? Handedness.Right
                        : Handedness.Left;
                    score += metaHand.Handedness == expected ? 20 : -20;
                }
                catch
                {
                    score += 0;
                }
            }

            if (handObject is Component handComponent)
                score += ScoreTextForHand(GetHierarchyPath(handComponent.transform), hand);
            else if (handObject is GameObject handGameObject)
                score += ScoreTextForHand(GetHierarchyPath(handGameObject.transform), hand);
            else if (handObject != null)
                score += ScoreTextForHand(handObject.name, hand);

            return score;
        }

        private static int ScoreTextForHand(string text, MetaHandPoseGestureBridge.Handedness hand)
        {
            var lower = text.ToLowerInvariant();
            var wanted = hand == MetaHandPoseGestureBridge.Handedness.Right ? "right" : "left";
            var unwanted = hand == MetaHandPoseGestureBridge.Handedness.Right ? "left" : "right";
            var score = 0;

            if (lower.Contains(wanted))
                score += 5;
            if (lower.Contains(unwanted))
                score -= 5;

            return score;
        }

        private static Object GetSerializedObjectReference(Object target, string propertyName)
        {
            if (target == null)
                return null;

            var serializedObject = new SerializedObject(target);
            return serializedObject.FindProperty(propertyName)?.objectReferenceValue;
        }

        private static IHmd FindConcreteHmdSource()
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(behaviour => behaviour is IHmd && behaviour is not HmdRef)
                .Select(behaviour => behaviour as IHmd)
                .FirstOrDefault();
        }

        private static void SetSerializedString(Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ContainsObjectReference(SerializedProperty arrayProperty, Object value)
        {
            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == value)
                    return true;
            }

            return false;
        }

        private static void RemovePersistentListener(UnityEvent unityEvent, Object target, string methodName)
        {
            for (var i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (unityEvent.GetPersistentTarget(i) == target &&
                    unityEvent.GetPersistentMethodName(i) == methodName)
                {
                    UnityEventTools.RemovePersistentListener(unityEvent, i);
                }
            }
        }

        private static Transform FindSceneObjectByName(string objectName)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == objectName)
                    return transform;
            }

            return null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private static void RemoveDefaultMainCamera()
        {
            var camera = Object
                .FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate.transform.parent == null &&
                    candidate.gameObject.name == "Main Camera");

            if (camera != null)
                Undo.DestroyObjectImmediate(camera.gameObject);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
