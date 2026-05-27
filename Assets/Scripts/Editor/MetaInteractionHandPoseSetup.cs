using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneVR.Input;
using Oculus.Interaction.Input;
using Oculus.Interaction.Input.UnityXR;
using Oculus.Interaction.PoseDetection;
using Oculus.Interaction;
using Oculus.Interaction.UnityXR;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Editor
{
    public static class MetaInteractionHandPoseSetup
    {
        private const string UnityXRInteractionPrefabPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/UnityXR/Prefabs/UnityXRInteractionComprehensive.prefab";
        private const string XROriginCameraRigPrefabPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/UnityXR/Prefabs/XROriginCameraRig.prefab";

        private const string UnityXRInteractionRootName = "Meta Interaction UnityXRInteraction";
        private const string XROriginCameraRigRootName = "Meta Gesture Recorder XROrigin";
        private const string RecorderProvidersRootName = "Arcane Meta Recorder Providers";
        private const string LegacyRecorderProvidersRootName = "Meta Interaction Recorder Providers";
        private const string DefaultThumbThresholdsPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/DefaultSettings/PoseDetection/DefaultThumbFeatureStateThresholds.asset";
        private const string DefaultFingerThresholdsPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/DefaultSettings/PoseDetection/DefaultFingerFeatureStateThresholds.asset";

        private const string RecorderScenePath = "Assets/Scenes/MetaGestureRecordTest.unity";
        private const string UnityXRExampleRecorderScenePath = "Assets/Scenes/UnityXRComprehensiveRigExample_Record.unity";
        private const string RecordedPoseFolder = "Assets/RecordedHandPoseSelectors";

        [MenuItem("ArcaneVR/Meta Gestures/Create Recorder Scene")]
        public static void CreateRecorderScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(RecordedPoseFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RemoveDefaultMainCamera();
            AddRecorderProvidersInternal();
            AddRecorderGuide();

            EditorSceneManager.SaveScene(scene, RecorderScenePath);
            EditorUtility.DisplayDialog(
                "Meta Gesture Recorder Scene",
                "MetaGestureRecordTest scene created.\n\nOpen Meta > Interaction > Hand Pose Selector Recorder, then run ArcaneVR > Meta Gestures > Auto Fill Recorder With Right/Left Hand.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Create UnityXR Example Recorder Scene")]
        public static void CreateUnityXRExampleRecorderScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(RecordedPoseFolder);

            if (!File.Exists(UnityXRExampleRecorderScenePath))
            {
                var sampleScenePath = FindUnityXRComprehensiveRigExampleScene();
                if (string.IsNullOrEmpty(sampleScenePath))
                {
                    EditorUtility.DisplayDialog(
                        "UnityXR sample scene not found",
                        "Could not find UnityXRComprehensiveRigExample.unity in the Meta XR Interaction SDK package cache.",
                        "OK");
                    return;
                }

                File.Copy(sampleScenePath, UnityXRExampleRecorderScenePath);
                AssetDatabase.ImportAsset(UnityXRExampleRecorderScenePath);
            }

            var scene = EditorSceneManager.OpenScene(UnityXRExampleRecorderScenePath, OpenSceneMode.Single);
            AddRecorderProvidersInternal();
            AddAllRecordedPosePrefabsToScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog(
                "UnityXR Example Recorder Scene",
                "UnityXRComprehensiveRigExample_Record was opened and wired for Meta hand pose recording.\n\nUse this scene instead of MetaGestureRecordTest if the custom test scene is too heavy or unstable.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Add UnityXR Interaction Rig To Current Scene")]
        public static void AddUnityXRInteractionToCurrentScene()
        {
            EnsureFolder(RecordedPoseFolder);
            var instance = AddUnityXRInteractionInternal();
            if (instance == null)
                return;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = instance;
        }

        [MenuItem("ArcaneVR/Meta Gestures/Add Recorder Providers To Current Scene")]
        public static void AddRecorderProvidersToCurrentScene()
        {
            var instance = AddRecorderProvidersInternal();
            if (instance == null)
                return;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = instance;
        }

        [MenuItem("ArcaneVR/Meta Gestures/Repair Recorder Scene Wiring")]
        public static void RepairRecorderSceneWiring()
        {
            var instance = AddRecorderProvidersInternal();
            if (instance == null)
                return;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = instance;
            EditorUtility.DisplayDialog(
                "Recorder scene repaired",
                "Recorder scene wiring was repaired.\n\nIf Play Mode still logs UnityEngine.Input.GetKeyDown, temporarily set Active Input Handling to Both while using Meta's recorder window.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Auto Fill Recorder With Right Hand")]
        public static void AutoFillRecorderWithRightHand()
        {
            AutoFillRecorder(MetaHandPoseGestureBridge.Handedness.Right);
        }

        [MenuItem("ArcaneVR/Meta Gestures/Auto Fill Recorder With Left Hand")]
        public static void AutoFillRecorderWithLeftHand()
        {
            AutoFillRecorder(MetaHandPoseGestureBridge.Handedness.Left);
        }

        [MenuItem("ArcaneVR/Meta Gestures/Wire Selected Recorded Pose Prefab")]
        public static void WireSelectedRecordedPosePrefab()
        {
            var wiredCount = 0;
            foreach (var selected in Selection.objects)
            {
                if (selected is not GameObject selectedGameObject)
                    continue;

                var path = AssetDatabase.GetAssetPath(selectedGameObject);
                if (!string.IsNullOrEmpty(path) && PrefabUtility.GetPrefabAssetType(selectedGameObject) != PrefabAssetType.NotAPrefab)
                {
                    var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    var changed = WirePoseRoot(prefabRoot, Path.GetFileNameWithoutExtension(path));
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                        wiredCount++;
                    }

                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    continue;
                }

                if (WirePoseRoot(selectedGameObject, selectedGameObject.name))
                {
                    EditorUtility.SetDirty(selectedGameObject);
                    wiredCount++;
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Meta Pose Wiring",
                wiredCount > 0
                    ? $"Wired {wiredCount} recorded pose prefab/object(s) to Arcane GestureEventRouter."
                    : "No recorded pose prefab/object was wired. Select a generated prefab under Assets/RecordedHandPoseSelectors first.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Wire Selected Pose To Current Recorder Providers")]
        public static void WireSelectedPoseToCurrentRecorderProviders()
        {
            var wiredCount = 0;
            foreach (var selected in Selection.objects)
            {
                if (selected is not GameObject selectedGameObject)
                    continue;

                GameObject sceneRoot;
                var path = AssetDatabase.GetAssetPath(selectedGameObject);
                if (!string.IsNullOrEmpty(path) &&
                    PrefabUtility.GetPrefabAssetType(selectedGameObject) != PrefabAssetType.NotAPrefab)
                {
                    sceneRoot = PrefabUtility.InstantiatePrefab(selectedGameObject) as GameObject;
                    if (sceneRoot == null)
                        continue;

                    sceneRoot.name = selectedGameObject.name;
                    Undo.RegisterCreatedObjectUndo(sceneRoot, "Add recorded Meta pose selector");
                }
                else
                {
                    sceneRoot = selectedGameObject;
                }

                if (WirePoseRootToCurrentRecorderProviders(sceneRoot, sceneRoot.name))
                    wiredCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Meta Pose Provider Wiring",
                wiredCount > 0
                    ? $"Wired {wiredCount} pose object(s) to the current scene recorder providers.\n\nSelect the pose object and confirm Hand/Finger/Transform references are no longer None."
                    : "No pose object was wired. Select a generated pose selector prefab or a pose object in the Hierarchy first.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Add All Recorded Poses To Current Scene")]
        public static void AddAllRecordedPosesToCurrentScene()
        {
            var wiredCount = AddAllRecordedPosePrefabsToScene();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Recorded Poses Added",
                wiredCount > 0
                    ? $"Added/wired {wiredCount} recorded pose selector(s).\n\nEnter Play Mode and watch for [MetaHandPose] SELECTED logs."
                    : $"No recorded pose prefabs found in {RecordedPoseFolder}.",
                "OK");
        }

        [MenuItem("ArcaneVR/Meta Gestures/Relax All Recorded Poses To Shape Only")]
        public static void RelaxAllRecordedPosesToShapeOnly()
        {
            EnsureFolder(RecordedPoseFolder);
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { RecordedPoseFolder });
            var changedCount = 0;

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                var changed = RelaxPoseRootToShapeOnly(prefabRoot);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    changedCount++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Recorded Poses Relaxed",
                changedCount > 0
                    ? $"Relaxed {changedCount} recorded pose selector(s) to shape-only recognition.\n\nRun Add All Recorded Poses To Current Scene again if the scene already has old instances."
                    : "No recorded pose selector needed relaxing.",
                "OK");
        }

        private static int AddAllRecordedPosePrefabsToScene()
        {
            EnsureFolder(RecordedPoseFolder);
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { RecordedPoseFolder });
            var wiredCount = 0;

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var sceneRoot = FindSceneObjectByName(prefab.name)?.gameObject;
                if (sceneRoot == null)
                {
                    sceneRoot = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (sceneRoot == null)
                        continue;

                    sceneRoot.name = prefab.name;
                    Undo.RegisterCreatedObjectUndo(sceneRoot, "Add recorded Meta pose selector");
                }

                if (WirePoseRootToCurrentRecorderProviders(sceneRoot, prefab.name))
                    wiredCount++;
            }

            return wiredCount;
        }

        private static GameObject AddUnityXRInteractionInternal()
        {
            var xrOrigin = AddXROriginCameraRigInternal();
            var trackingSpace = FindTrackingSpace(xrOrigin);
            if (xrOrigin == null || trackingSpace == null)
                return null;

            RemoveBrokenStandaloneHands();

            var existing = FindExistingUnityXRInteractionRig();
            if (existing != null)
            {
                RepairMetaUnityXRReferences(xrOrigin, trackingSpace);
                if (TryFindMetaRigReferences(
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    Selection.activeGameObject = existing;
                    Debug.Log("[ArcaneVR] UnityXRInteraction already exists in this scene.");
                    return existing;
                }

                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[ArcaneVR] Removed incomplete UnityXRInteraction rig because it had HMD only and no hand data sources.");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnityXRInteractionPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "UnityXRInteraction prefab not found",
                    $"Could not load:\n{UnityXRInteractionPrefabPath}\n\nCheck that com.meta.xr.sdk.interaction is installed.",
                    "OK");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return null;

            instance.name = UnityXRInteractionRootName;
            Undo.RegisterCreatedObjectUndo(instance, "Add Meta Interaction UnityXRInteraction");
            RepairMetaUnityXRReferences(xrOrigin, trackingSpace);
            DisableRecorderOnlyLocomotionAndTunneling();
            Selection.activeGameObject = instance;
            Debug.Log("[ArcaneVR] Added Meta Interaction UnityXRInteraction. This rig includes HMD data wired into UnityXR hands.");
            return instance;
        }

        private static XROrigin AddXROriginCameraRigInternal()
        {
            var existing = Object
                .FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();
            if (existing != null)
                return existing;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginCameraRigPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "XROriginCameraRig prefab not found",
                    $"Could not load:\n{XROriginCameraRigPrefabPath}\n\nCheck that com.meta.xr.sdk.interaction is installed.",
                    "OK");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return null;

            instance.name = XROriginCameraRigRootName;
            Undo.RegisterCreatedObjectUndo(instance, "Add Meta Gesture Recorder XROrigin");
            return instance.GetComponent<XROrigin>();
        }

        private static Transform FindTrackingSpace(XROrigin origin)
        {
            if (origin == null)
                return null;

            var trackingSpace = origin
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "TrackingSpace");

            return trackingSpace != null ? trackingSpace : origin.transform;
        }

        private static void RepairMetaUnityXRReferences(XROrigin origin, Transform trackingSpace)
        {
            if (origin == null || trackingSpace == null)
                return;

            var transformers = Object.FindObjectsByType<TransformTrackingToWorldTransformer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var transformer in transformers)
                SetSerializedObjectReference(transformer, "TrackingSpace", trackingSpace);

            var trackingTransformer = transformers.FirstOrDefault();
            if (trackingTransformer == null)
                return;

            var hmdDataSources = Object.FindObjectsByType<FromUnityXRHmdDataSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var hmdDataSource in hmdDataSources)
            {
                SetSerializedObjectReference(hmdDataSource, "_origin", origin);
                SetSerializedObjectReference(hmdDataSource, "_trackingToWorldTransformer", trackingTransformer);
            }

            var hmdSource = FindConcreteHmdSource();

            foreach (var hmdRef in Object.FindObjectsByType<HmdRef>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hmdSource != null)
                {
                    Undo.RecordObject(hmdRef, "Wire Meta HMD reference");
                    SetSerializedObjectReference(hmdRef, "_hmd", hmdSource as Object);
                    hmdRef.InjectHmd(hmdSource);
                    EditorUtility.SetDirty(hmdRef);
                }
            }

            if (hmdSource != null)
            {
                foreach (var transformProvider in Object.FindObjectsByType<TransformFeatureStateProvider>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    Undo.RecordObject(transformProvider, "Wire Meta transform HMD source");
                    transformProvider.InjectHmd(hmdSource);
                    EditorUtility.SetDirty(transformProvider);
                }
            }

            foreach (var handDataSource in Object.FindObjectsByType<FromUnityXRHandDataSource>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                SetSerializedObjectReference(handDataSource, "_trackingToWorldTransformer", trackingTransformer);
                if (hmdSource != null)
                    SetSerializedObjectReference(handDataSource, "_hmdData", hmdSource as Object);
            }

            DisableRecorderOnlyLocomotionAndTunneling();
        }

        private static void DisableRecorderOnlyLocomotionAndTunneling()
        {
            var disabledCount = 0;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null)
                    continue;

                var typeName = behaviour.GetType().Name;
                var objectName = behaviour.gameObject.name;
                var shouldDisable =
                    typeName == "TunnelingEffect" ||
                    typeName == "FirstPersonLocomotor" ||
                    objectName == "WallPenetrationTunneling" ||
                    objectName == "SmoothMovementTunneling" ||
                    objectName == "PlayerController";

                if (!shouldDisable || !behaviour.enabled)
                    continue;

                behaviour.enabled = false;
                EditorUtility.SetDirty(behaviour);
                disabledCount++;
            }

            if (disabledCount > 0)
                Debug.Log($"[ArcaneVR] Disabled {disabledCount} locomotion/tunneling component(s) that are not needed for hand pose recording.");
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

        private static void RemoveBrokenStandaloneHands()
        {
            var standaloneHands = Object
                .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(transform =>
                    (transform.name == "Meta Interaction UnityXRHands" || transform.name == "UnityXRHands") &&
                    transform.parent == null)
                .Select(transform => transform.gameObject)
                .ToList();

            if (standaloneHands.Count == 0)
                return;

            foreach (var handsRoot in standaloneHands)
                Undo.DestroyObjectImmediate(handsRoot);

            Debug.Log("[ArcaneVR] Removed standalone UnityXRHands because it lacks the HMD data wiring required by the recorder scene.");
        }

        private static GameObject FindExistingUnityXRInteractionRig()
        {
            var candidates = Object
                .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(transform =>
                    transform.name == UnityXRInteractionRootName ||
                    transform.name == "UnityXRInteraction" ||
                    transform.name == "UnityXRInteractionComprehensive")
                .Select(transform => transform.root.gameObject)
                .Distinct()
                .ToList();

            if (candidates.Count == 0)
                return null;

            var primary = candidates.FirstOrDefault(candidate => candidate.name == UnityXRInteractionRootName) ??
                          candidates[0];
            primary.name = UnityXRInteractionRootName;

            foreach (var duplicate in candidates.Where(candidate => candidate != primary))
                Undo.DestroyObjectImmediate(duplicate);

            if (candidates.Count > 1)
                Debug.Log($"[ArcaneVR] Removed {candidates.Count - 1} duplicate UnityXRInteraction rig(s).");

            return primary;
        }

        private static GameObject AddRecorderProvidersInternal()
        {
            RemoveDefaultMainCamera();
            RemoveLegacyRecorderProviders();
            var root = FindSceneObjectByName(RecorderProvidersRootName);
            if (root != null)
            {
                var refreshedRig = AddUnityXRInteractionInternal();
                if (refreshedRig == null)
                    return null;

                Selection.activeGameObject = root.gameObject;
                Debug.Log("[ArcaneVR] Arcane Meta Recorder Providers already exists in this scene. Recorder rig wiring was refreshed.");
                return root.gameObject;
            }

            var rig = AddUnityXRInteractionInternal();
            if (rig == null)
                return null;

            if (!TryFindMetaRigReferences(
                    out var leftHand,
                    out var rightHand,
                    out var hmd,
                    out var trackingToWorldTransformer))
            {
                EditorUtility.DisplayDialog(
                    "Meta rig references not found",
                    "Could not find both hands, HMD, and tracking transformer in the scene.\n\nRun Create Recorder Scene again, then try once more.",
                    "OK");
                return null;
            }

            root = new GameObject(RecorderProvidersRootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Add Arcane Meta Recorder Providers");
            CreateRecorderProvider("Left Features", root, leftHand, hmd, trackingToWorldTransformer);
            CreateRecorderProvider("Right Features", root, rightHand, hmd, trackingToWorldTransformer);

            Selection.activeGameObject = root.gameObject;
            Debug.Log("[ArcaneVR] Added lightweight recorder providers for Meta hand pose recording.");
            return root.gameObject;
        }

        private static void AutoFillRecorder(MetaHandPoseGestureBridge.Handedness hand)
        {
            RemoveLegacyRecorderProviders();

            var providerPair = FindProviderPair(hand);
            if (!providerPair.IsValid && AddRecorderProvidersInternal() != null)
                providerPair = FindProviderPair(hand);

            if (!providerPair.IsValid)
            {
                EditorUtility.DisplayDialog(
                    "Provider components not found",
                    $"Could not find both FingerFeatureStateProvider and TransformFeatureStateProvider for {hand} hand.\n\nRun ArcaneVR > Meta Gestures > Add Recorder Providers To Current Scene, then try again.",
                    "OK");
                return;
            }

            var wizardType = FindHandPoseSelectorWizardType();
            if (wizardType == null)
            {
                EditorUtility.DisplayDialog(
                    "Recorder window type not found",
                    "Could not find Meta's HandPoseSelectorWizard type. Open Meta > Interaction > Hand Pose Selector Recorder manually, then try again.",
                    "OK");
                return;
            }

            var window = EditorWindow.GetWindow(wizardType);
            window.titleContent = new GUIContent("Hand Pose Selector Recorder");

            SetPrivateField(wizardType, window, "_fingerFeatureStateProvider", providerPair.FingerProvider);
            SetPrivateField(wizardType, window, "_transformFeatureStateProvider", providerPair.TransformProvider);
            SetPrivateField(wizardType, window, "_fingerFeatureStateProviderInstanceId", providerPair.FingerProvider.GetInstanceID());
            SetPrivateField(wizardType, window, "_transformFeatureStateProviderInstanceId", providerPair.TransformProvider.GetInstanceID());

            var handObject = GetSerializedObjectReference(providerPair.FingerProvider, "_hand");
            if (handObject == null && providerPair.FingerProvider.Hand is Object runtimeHandObject)
                handObject = runtimeHandObject;

            if (handObject != null)
                SetPrivateField(wizardType, window, "_handInstanceId", handObject.GetInstanceID());

            Selection.activeGameObject = providerPair.FingerProvider.gameObject;
            window.Show();
            window.Repaint();

            EditorUtility.DisplayDialog(
                "Recorder auto-filled",
                $"Hand Pose Selector Recorder was filled with {hand} hand providers.\n\nNow set New pose name, enter Play Mode, hold the pose, and press Record/Space.",
                "OK");
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
            var root = FindSceneObjectByName(RecorderProvidersRootName);
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
                    if (root != null && finger.transform.IsChildOf(root))
                        score += 20;

                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestPair = new ProviderPair(finger, transformProvider);
                }
            }

            return bestScore > 0 ? bestPair : default;
        }

        private static void CreateRecorderProvider(
            string objectName,
            Transform parent,
            IHand hand,
            IHmd hmd,
            ITrackingToWorldTransformer trackingToWorldTransformer)
        {
            var providerObject = new GameObject(objectName);
            providerObject.transform.SetParent(parent, false);

            var fingerProvider = providerObject.AddComponent<FingerFeatureStateProvider>();
            fingerProvider.InjectAllFingerFeatureStateProvider(
                hand,
                CreateDefaultFingerThresholds(),
                FingerFeatureStateProvider.DefaultFingerShapes,
                false);

            var transformProvider = providerObject.AddComponent<TransformFeatureStateProvider>();
            transformProvider.InjectAllTransformFeatureStateProvider(hand, hmd, false);
            SetSerializedObjectReference(transformProvider, "_trackingToWorldTransformer", trackingToWorldTransformer as Object);
        }

        private static List<FingerFeatureStateProvider.FingerStateThresholds> CreateDefaultFingerThresholds()
        {
            var thumbThresholds = AssetDatabase.LoadAssetAtPath<FingerFeatureStateThresholds>(DefaultThumbThresholdsPath);
            var fingerThresholds = AssetDatabase.LoadAssetAtPath<FingerFeatureStateThresholds>(DefaultFingerThresholdsPath);

            return new List<FingerFeatureStateProvider.FingerStateThresholds>
            {
                new() { Finger = HandFinger.Thumb, StateThresholds = thumbThresholds },
                new() { Finger = HandFinger.Index, StateThresholds = fingerThresholds },
                new() { Finger = HandFinger.Middle, StateThresholds = fingerThresholds },
                new() { Finger = HandFinger.Ring, StateThresholds = fingerThresholds },
                new() { Finger = HandFinger.Pinky, StateThresholds = fingerThresholds }
            };
        }

        private static bool TryFindMetaRigReferences(
            out IHand leftHand,
            out IHand rightHand,
            out IHmd hmd,
            out ITrackingToWorldTransformer trackingToWorldTransformer)
        {
            leftHand = null;
            rightHand = null;
            hmd = null;
            trackingToWorldTransformer = null;

            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is IHand metaHand)
                {
                    var side = GuessMetaHandSide(metaHand, behaviour);
                    if (side == MetaHandPoseGestureBridge.Handedness.Left)
                        leftHand ??= metaHand;
                    else if (side == MetaHandPoseGestureBridge.Handedness.Right)
                        rightHand ??= metaHand;
                }

                if (behaviour is IHmd metaHmd)
                {
                    if (behaviour is not HmdRef)
                        hmd = metaHmd;
                    else
                        hmd ??= metaHmd;
                }

                if (behaviour is ITrackingToWorldTransformer transformer)
                    trackingToWorldTransformer ??= transformer;
            }

            return leftHand != null &&
                   rightHand != null &&
                   hmd != null &&
                   trackingToWorldTransformer != null;
        }

        private static IHmd FindConcreteHmdSource()
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(behaviour => behaviour is IHmd && behaviour is not HmdRef)
                .Select(behaviour => behaviour as IHmd)
                .FirstOrDefault();
        }

        private static MetaHandPoseGestureBridge.Handedness? GuessMetaHandSide(IHand metaHand, Component component)
        {
            try
            {
                return metaHand.Handedness == Oculus.Interaction.Input.Handedness.Left
                    ? MetaHandPoseGestureBridge.Handedness.Left
                    : MetaHandPoseGestureBridge.Handedness.Right;
            }
            catch
            {
                var path = GetHierarchyPath(component.transform).ToLowerInvariant();
                if (path.Contains("left"))
                    return MetaHandPoseGestureBridge.Handedness.Left;
                if (path.Contains("right"))
                    return MetaHandPoseGestureBridge.Handedness.Right;
                return null;
            }
        }

        private static void RemoveLegacyRecorderProviders()
        {
            var legacy = FindSceneObjectByName(LegacyRecorderProvidersRootName);
            if (legacy == null)
                return;

            Undo.DestroyObjectImmediate(legacy.gameObject);
            Debug.Log("[ArcaneVR] Removed legacy Meta Interaction Recorder Providers because it contains locomotion/controller components that are not wired for the recorder scene.");
        }

        private static int ScoreProviderSide(Component provider, Object handObject, MetaHandPoseGestureBridge.Handedness hand)
        {
            var score = ScoreTextForHand(GetHierarchyPath(provider.transform), hand);
            if (handObject is IHand metaHand)
                score += ScoreMetaHandSide(metaHand, hand);

            if (handObject is Component handComponent)
                score += ScoreTextForHand(GetHierarchyPath(handComponent.transform), hand);
            else if (handObject is GameObject handGameObject)
                score += ScoreTextForHand(GetHierarchyPath(handGameObject.transform), hand);
            else if (handObject != null)
                score += ScoreTextForHand(handObject.name, hand);

            return score;
        }

        private static int ScoreMetaHandSide(IHand metaHand, MetaHandPoseGestureBridge.Handedness hand)
        {
            try
            {
                var expected = hand == MetaHandPoseGestureBridge.Handedness.Right
                    ? Oculus.Interaction.Input.Handedness.Right
                    : Oculus.Interaction.Input.Handedness.Left;
                return metaHand.Handedness == expected ? 20 : -20;
            }
            catch
            {
                return 0;
            }
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

        private static void SetSerializedObjectReference(Object target, string propertyName, Object value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[ArcaneVR] Could not find serialized property {propertyName} on {target.name}.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
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

        private static System.Type FindHandPoseSelectorWizardType()
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).ToArray();
                }

                var type = types.FirstOrDefault(candidate =>
                    candidate.FullName == "Oculus.Interaction.HandGrab.Editor.HandPoseSelectorWizard");
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void SetPrivateField(System.Type type, object instance, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning($"[ArcaneVR] Could not find field {fieldName} on {type.FullName}.");
                return;
            }

            field.SetValue(instance, value);
        }

        private static void AddRecorderGuide()
        {
            var guide = new GameObject("Meta Gesture Recorder Guide");
            var text = guide.AddComponent<TextMesh>();
            text.text =
                "Meta Gesture Recorder Scene\n" +
                "1. Open: Meta > Interaction > Hand Pose Selector Recorder\n" +
                "2. Run: ArcaneVR > Meta Gestures > Auto Fill Recorder With Right/Left Hand\n" +
                "3. Enter Play Mode, hold pose, press Record/Space\n" +
                "4. Generated prefabs are saved in Assets/RecordedHandPoseSelectors";
            text.characterSize = 0.08f;
            text.fontSize = 64;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            guide.transform.position = new Vector3(0f, 1.5f, 2.5f);
            guide.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static bool WirePoseRoot(GameObject root, string sourceName)
        {
            if (!TryInferPose(sourceName, out var hand, out var pose))
            {
                Debug.LogWarning($"[ArcaneVR] Could not infer hand/pose from '{sourceName}'. Rename like Right_Thunder_ThumbsUp, Right_Ice_TwoFinger, Right_Gun, or Left_Grimoire_OpenPalm.");
                return false;
            }

            var wrapper = root.GetComponentInChildren<SelectorUnityEventWrapper>(true);
            if (wrapper == null)
            {
                Debug.LogWarning($"[ArcaneVR] '{sourceName}' has no SelectorUnityEventWrapper. Is this a generated Meta hand pose selector prefab?");
                return false;
            }

            var bridge = root.GetComponent<MetaHandPoseGestureBridge>();
            if (bridge == null)
                bridge = root.AddComponent<MetaHandPoseGestureBridge>();

            bridge.Configure(hand, pose);
            RemovePersistentListener(wrapper.WhenSelected, bridge, nameof(MetaHandPoseGestureBridge.HandleSelected));
            RemovePersistentListener(wrapper.WhenUnselected, bridge, nameof(MetaHandPoseGestureBridge.HandleUnselected));
            UnityEventTools.AddPersistentListener(wrapper.WhenSelected, bridge.HandleSelected);
            UnityEventTools.AddPersistentListener(wrapper.WhenUnselected, bridge.HandleUnselected);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(wrapper);
            EditorUtility.SetDirty(bridge);
            Debug.Log($"[ArcaneVR] Wired {sourceName}: {hand} {pose}");
            return true;
        }

        private static bool WirePoseRootToCurrentRecorderProviders(GameObject root, string sourceName)
        {
            if (!TryInferPose(sourceName, out var hand, out _))
            {
                Debug.LogWarning($"[ArcaneVR] Could not infer hand from '{sourceName}'. Rename like Right_Fire_OpenPalm, Right_Gun, or Left_Grimoire_OpenPalm.");
                return false;
            }

            var providerPair = FindProviderPair(hand);
            if (!providerPair.IsValid && AddRecorderProvidersInternal() != null)
                providerPair = FindProviderPair(hand);

            if (!providerPair.IsValid)
            {
                EditorUtility.DisplayDialog(
                    "Provider components not found",
                    $"Could not find {hand} hand recorder providers.\n\nRun ArcaneVR > Meta Gestures > Repair Recorder Scene Wiring, then try again.",
                    "OK");
                return false;
            }

            var handObject = GetSerializedObjectReference(providerPair.FingerProvider, "_hand");
            if (handObject == null && providerPair.FingerProvider.Hand is Object runtimeHandObject)
                handObject = runtimeHandObject;

            var changed = WirePoseRoot(root, sourceName);
            changed |= WireProviderRefs(root, handObject, providerPair.FingerProvider, providerPair.TransformProvider);
            changed |= WireDebugLogger(root, sourceName);

            if (!changed)
                return false;

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);
            Debug.Log($"[ArcaneVR] Wired {sourceName} to {hand} recorder providers.");
            return true;
        }

        private static bool WireProviderRefs(
            GameObject root,
            Object handObject,
            FingerFeatureStateProvider fingerProvider,
            TransformFeatureStateProvider transformProvider)
        {
            var changed = false;

            foreach (var handRef in root.GetComponentsInChildren<HandRef>(true))
            {
                if (handObject is IHand hand)
                {
                    Undo.RecordObject(handRef, "Wire Meta hand reference");
                    handRef.InjectHand(hand);
                    EditorUtility.SetDirty(handRef);
                    changed = true;
                }
            }

            foreach (var fingerRef in root.GetComponentsInChildren<FingerFeatureStateProviderRef>(true))
            {
                Undo.RecordObject(fingerRef, "Wire Meta finger feature provider");
                fingerRef.InjectFingerFeatureStateProvider(fingerProvider);
                EditorUtility.SetDirty(fingerRef);
                changed = true;
            }

            foreach (var transformRef in root.GetComponentsInChildren<TransformFeatureStateProviderRef>(true))
            {
                Undo.RecordObject(transformRef, "Wire Meta transform feature provider");
                transformRef.InjectTransformFeatureStateProvider(transformProvider);
                EditorUtility.SetDirty(transformRef);
                changed = true;
            }

            foreach (var shapeRecognizer in root.GetComponentsInChildren<ShapeRecognizerActiveState>(true))
            {
                Undo.RecordObject(shapeRecognizer, "Wire Meta shape recognizer provider");
#pragma warning disable CS0612
                if (handObject is IHand hand)
                    shapeRecognizer.InjectHand(hand);
#pragma warning restore CS0612
                shapeRecognizer.InjectFingerFeatureStateProvider(fingerProvider);
                EditorUtility.SetDirty(shapeRecognizer);
                changed = true;
            }

            foreach (var transformRecognizer in root.GetComponentsInChildren<TransformRecognizerActiveState>(true))
            {
                Undo.RecordObject(transformRecognizer, "Wire Meta transform recognizer provider");
#pragma warning disable CS0612
                if (handObject is IHand hand)
                    transformRecognizer.InjectHand(hand);
#pragma warning restore CS0612
                transformRecognizer.InjectTransformFeatureStateProvider(transformProvider);
                transformRecognizer.enabled = true;
                EditorUtility.SetDirty(transformRecognizer);
                changed = true;
            }

            var hmd = FindConcreteHmdSource();
            foreach (var hmdOffset in root.GetComponentsInChildren<HmdOffset>(true))
            {
                if (hmd == null)
                    continue;

                Undo.RecordObject(hmdOffset, "Wire Meta HMD offset");
                hmdOffset.InjectHmd(hmd);
                EditorUtility.SetDirty(hmdOffset);
                changed = true;
            }

            return changed;
        }

        private static bool WireDebugLogger(GameObject root, string sourceName)
        {
            var wrapper = root.GetComponentInChildren<SelectorUnityEventWrapper>(true);
            if (wrapper == null)
                return false;

            var logger = root.GetComponent<MetaHandPoseDebugLogger>();
            if (logger == null)
            {
                Undo.RecordObject(root, "Add Meta hand pose debug logger");
                logger = root.AddComponent<MetaHandPoseDebugLogger>();
            }

            SetSerializedString(logger, "poseName", sourceName);
            RemovePersistentListener(wrapper.WhenSelected, logger, nameof(MetaHandPoseDebugLogger.LogSelected));
            RemovePersistentListener(wrapper.WhenUnselected, logger, nameof(MetaHandPoseDebugLogger.LogUnselected));
            UnityEventTools.AddPersistentListener(wrapper.WhenSelected, logger.LogSelected);
            UnityEventTools.AddPersistentListener(wrapper.WhenUnselected, logger.LogUnselected);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(wrapper);
            EditorUtility.SetDirty(logger);
            return true;
        }

        private static bool RelaxPoseRootToShapeOnly(GameObject root)
        {
            var changed = false;
            foreach (var group in root.GetComponentsInChildren<ActiveStateGroup>(true))
            {
                var serializedGroup = new SerializedObject(group);
                var activeStates = serializedGroup.FindProperty("_activeStates");
                if (activeStates == null || !activeStates.isArray)
                    continue;

                for (var i = activeStates.arraySize - 1; i >= 0; i--)
                {
                    var element = activeStates.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue is not TransformRecognizerActiveState)
                        continue;

                    activeStates.DeleteArrayElementAtIndex(i);
                    changed = true;
                }

                serializedGroup.ApplyModifiedPropertiesWithoutUndo();
                if (changed)
                    EditorUtility.SetDirty(group);
            }

            foreach (var transformRecognizer in root.GetComponentsInChildren<TransformRecognizerActiveState>(true))
            {
                if (!transformRecognizer.enabled)
                    continue;

                transformRecognizer.enabled = false;
                EditorUtility.SetDirty(transformRecognizer);
                changed = true;
            }

            return changed;
        }

        private static void SetSerializedString(Object target, string propertyName, string value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryInferPose(string sourceName, out MetaHandPoseGestureBridge.Handedness hand, out PoseType pose)
        {
            var lower = sourceName.ToLowerInvariant();
            hand = lower.Contains("left")
                ? MetaHandPoseGestureBridge.Handedness.Left
                : MetaHandPoseGestureBridge.Handedness.Right;

            if (lower.Contains("thumb"))
            {
                pose = PoseType.ThumbsUp;
                return true;
            }

            if (lower.Contains("two") || lower.Contains("vsign") || lower.Contains("victory") || lower.Contains("scissors"))
            {
                pose = PoseType.TwoFinger;
                return true;
            }

            if (lower.Contains("gun") || lower.Contains("index") || lower.Contains("point"))
            {
                // There is no dedicated Gun pose in the gameplay enum yet.
                // Route recorded finger-gun selectors through the TwoFinger/Ice slot for now.
                pose = PoseType.TwoFinger;
                return true;
            }

            if (lower.Contains("fist") || lower.Contains("guard") || lower.Contains("barrier") || lower.Contains("rock"))
            {
                pose = PoseType.Fist;
                return true;
            }

            if (lower.Contains("open") || lower.Contains("palm") || lower.Contains("grimoire") || lower.Contains("paper") || lower.Contains("stop"))
            {
                pose = PoseType.OpenPalm;
                return true;
            }

            pose = PoseType.None;
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

        private static string FindUnityXRComprehensiveRigExampleScene()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return null;

            var packageCacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");
            if (!Directory.Exists(packageCacheRoot))
                return null;

            foreach (var packageRoot in Directory.GetDirectories(packageCacheRoot, "com.meta.xr.sdk.interaction@*"))
            {
                var scenePath = Path.Combine(
                    packageRoot,
                    "Samples~",
                    "UnityXR",
                    "Scenes",
                    "UnityXRComprehensiveRigExample.unity");

                if (File.Exists(scenePath))
                    return scenePath;
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var folderName = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, folderName);
        }
    }
}
