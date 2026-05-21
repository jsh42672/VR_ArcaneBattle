using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using Object = UnityEngine.Object;

namespace ArcaneVR.Editor
{
    public static class XRHandsStaticGestureSceneSetup
    {
        private const string StaticHandGestureTypeName = "UnityEngine.XR.Hands.Samples.GestureSample.StaticHandGesture";
        private const string DataPath = "Assets/Data/HandShapes";
        private const string InputManagerName = "InputManager";

        private enum PrototypeHandShape
        {
            OpenPalm,
            Fist,
            ThumbsUp
        }

        [MenuItem("ArcaneVR/Prototype/Setup XR Hands Static Gestures")]
        public static void Setup()
        {
            var openPalm = GetOrCreateHandShape("HandShape_OpenPalm", PrototypeHandShape.OpenPalm);
            var fist = GetOrCreateHandShape("HandShape_Fist", PrototypeHandShape.Fist);
            var thumbsUp = GetOrCreateHandShape("HandShape_ThumbsUp", PrototypeHandShape.ThumbsUp);
            AssetDatabase.SaveAssets();

            var staticGestureType = FindType(StaticHandGestureTypeName);
            if (staticGestureType == null)
            {
                const string message =
                    "XR Hands Gestures sample is not imported yet.\n\n" +
                    "Open Package Manager > XR Hands > Samples, import Gestures, then run this menu again.";
                Debug.LogWarning(message);
                EditorUtility.DisplayDialog("XR Hands Gestures Sample Missing", message, "OK");
                return;
            }

            var inputManager = FindOrCreateSceneObject(InputManagerName);
            var detector = inputManager.GetComponent<GestureDetector>();
            if (detector == null)
                detector = Undo.AddComponent<GestureDetector>(inputManager);

            var router = inputManager.GetComponent<GestureEventRouter>();
            if (router == null)
                router = Undo.AddComponent<GestureEventRouter>(inputManager);

            detector.BindGestureEventRouter(router);
            SetSerializedBool(detector, "useXrHandsStaticGestureRouter", true);
            SetSerializedBool(detector, "openPalmDetectionEnabled", true);
            SetSerializedBool(detector, "showBoneAngleDebug", false);
            SetSerializedObjectReference(detector, "gestureEventRouter", router);

            var rightEvents = SetupHandTrackingEvents(inputManager.transform, "XRHandTrackingEvents_Right", Handedness.Right);
            var leftEvents = SetupHandTrackingEvents(inputManager.transform, "XRHandTrackingEvents_Left", Handedness.Left);

            ConfigureGesture(
                inputManager.transform,
                staticGestureType,
                "Gesture_OpenPalm_R",
                rightEvents,
                openPalm,
                router.OnOpenPalmStart,
                router.OnOpenPalmEnd);

            ConfigureGesture(
                inputManager.transform,
                staticGestureType,
                "Gesture_Fist_R",
                rightEvents,
                fist,
                router.OnFistRightStart,
                router.OnFistRightEnd);

            ConfigureGesture(
                inputManager.transform,
                staticGestureType,
                "Gesture_ThumbsUp_R",
                rightEvents,
                thumbsUp,
                router.OnThumbsUpStart,
                router.OnThumbsUpEnd);

            ConfigureGesture(
                inputManager.transform,
                staticGestureType,
                "Gesture_OpenPalm_L",
                leftEvents,
                openPalm,
                router.OnOpenPalmLeftStart,
                router.OnOpenPalmLeftEnd);

            ConfigureGesture(
                inputManager.transform,
                staticGestureType,
                "Gesture_Fist_L",
                leftEvents,
                fist,
                router.OnLeftFistDetected,
                router.OnLeftFistLost);

            ConfigureGesture(
                inputManager.transform,
                staticGestureType,
                "Gesture_ThumbsUp_L",
                leftEvents,
                thumbsUp,
                router.OnThumbsUpLeftStart,
                router.OnThumbsUpLeftEnd);

            ConfigureRuntimeReferences(detector, router);

            EditorUtility.SetDirty(inputManager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            const string done =
                "XR Hand Gesture setup done.\n\n" +
                "Created/updated HandShape assets under Assets/Data/HandShapes.\n" +
                "Configured InputManager, StaticHandGesture components, and UnityEvents.\n" +
                "SpellCaster and MovementController references were assigned when present in the scene.\n\n" +
                "Save the scene, then Build & Run to Quest.";

            Debug.Log("[ArcaneVR Setup] XR Hand Gestures setup complete.");
            EditorUtility.DisplayDialog("Setup Complete", done, "OK");
        }

        private static XRHandShape GetOrCreateHandShape(string name, PrototypeHandShape shapeKind)
        {
            Directory.CreateDirectory(DataPath);
            var path = $"{DataPath}/{name}.asset";
            var shape = AssetDatabase.LoadAssetAtPath<XRHandShape>(path);
            var created = false;

            if (shape == null)
            {
                shape = ScriptableObject.CreateInstance<XRHandShape>();
                AssetDatabase.CreateAsset(shape, path);
                created = true;
            }

            shape.fingerShapeConditions = shapeKind switch
            {
                PrototypeHandShape.OpenPalm => BuildOpenPalmConditions(),
                PrototypeHandShape.Fist => BuildFistConditions(),
                PrototypeHandShape.ThumbsUp => BuildThumbsUpConditions(),
                _ => BuildOpenPalmConditions()
            };

            EditorUtility.SetDirty(shape);
            Debug.Log(created
                ? $"[ArcaneVR Setup] Created {path}"
                : $"[ArcaneVR Setup] Updated {path}");
            return shape;
        }

        private static List<XRFingerShapeCondition> BuildOpenPalmConditions()
        {
            return new List<XRFingerShapeCondition>
            {
                CreateFullCurlCondition(XRHandFingerID.Thumb, 0f, 0.35f, 0.25f),
                CreateFullCurlCondition(XRHandFingerID.Index, 0f, 0.35f, 0.25f),
                CreateFullCurlCondition(XRHandFingerID.Middle, 0f, 0.35f, 0.25f),
                CreateFullCurlCondition(XRHandFingerID.Ring, 0f, 0.35f, 0.25f),
                CreateFullCurlCondition(XRHandFingerID.Little, 0f, 0.25f, 0.25f)
            };
        }

        private static List<XRFingerShapeCondition> BuildFistConditions()
        {
            return new List<XRFingerShapeCondition>
            {
                CreateFullCurlCondition(XRHandFingerID.Thumb, 1f, 0.375f, 0.375f),
                CreateFullCurlCondition(XRHandFingerID.Index, 1f, 0.15f, 0.15f),
                CreateFullCurlCondition(XRHandFingerID.Middle, 1f, 0.15f, 0.15f),
                CreateFullCurlCondition(XRHandFingerID.Ring, 1f, 0.15f, 0.15f),
                CreateFullCurlCondition(XRHandFingerID.Little, 1f, 0.25f, 0.25f)
            };
        }

        private static List<XRFingerShapeCondition> BuildThumbsUpConditions()
        {
            return new List<XRFingerShapeCondition>
            {
                CreateFullCurlCondition(XRHandFingerID.Thumb, 0f, 0.25f, 0.25f),
                CreateFullCurlCondition(XRHandFingerID.Index, 1f, 0.15f, 0.15f),
                CreateFullCurlCondition(XRHandFingerID.Middle, 1f, 0.15f, 0.15f),
                CreateFullCurlCondition(XRHandFingerID.Ring, 1f, 0.15f, 0.15f),
                CreateFullCurlCondition(XRHandFingerID.Little, 1f, 0.15f, 0.15f)
            };
        }

        private static XRFingerShapeCondition CreateFullCurlCondition(
            XRHandFingerID finger,
            float desired,
            float upperTolerance,
            float lowerTolerance)
        {
            return new XRFingerShapeCondition
            {
                fingerID = finger,
                targets = new[]
                {
                    new XRFingerShapeCondition.Target
                    {
                        shapeType = XRFingerShapeType.FullCurl,
                        desired = desired,
                        upperTolerance = upperTolerance,
                        lowerTolerance = lowerTolerance
                    }
                }
            };
        }

        private static XRHandTrackingEvents SetupHandTrackingEvents(Transform parent, string objectName, Handedness handedness)
        {
            var go = GetOrCreateChild(parent, objectName);
            var events = go.GetComponent<XRHandTrackingEvents>();
            if (events == null)
                events = Undo.AddComponent<XRHandTrackingEvents>(go);

            events.handedness = handedness;
            events.updateType = XRHandTrackingEvents.UpdateTypes.Dynamic;
            EditorUtility.SetDirty(events);
            return events;
        }

        private static void ConfigureGesture(
            Transform parent,
            Type staticGestureType,
            string objectName,
            XRHandTrackingEvents trackingEvents,
            ScriptableObject handShape,
            UnityAction onPerformed,
            UnityAction onEnded)
        {
            var go = GetOrCreateChild(parent, objectName);
            var component = go.GetComponent(staticGestureType);
            if (component == null)
                component = Undo.AddComponent(go, staticGestureType);

            SetProperty(staticGestureType, component, "handTrackingEvents", trackingEvents);
            SetProperty(staticGestureType, component, "handShapeOrPose", handShape);
            SetProperty(staticGestureType, component, "minimumHoldTime", 0.2f);
            SetProperty(staticGestureType, component, "gestureDetectionInterval", 0.05f);
            SetProperty(staticGestureType, component, "background", GetOrCreateImage(go.transform, "GestureBackground"));
            SetProperty(staticGestureType, component, "highlight", GetOrCreateImage(go.transform, "GestureHighlight"));

            ClearObjectArray(component, "m_StaticGestures");
            ClearPersistentCalls(component, "m_GesturePerformed");
            ClearPersistentCalls(component, "m_GestureEnded");

            var performedEvent = GetOrCreateUnityEvent(staticGestureType, component, "gesturePerformed");
            var endedEvent = GetOrCreateUnityEvent(staticGestureType, component, "gestureEnded");
            UnityEventTools.AddPersistentListener(performedEvent, onPerformed);
            UnityEventTools.AddPersistentListener(endedEvent, onEnded);

            EditorUtility.SetDirty(component);
            Debug.Log($"[ArcaneVR Setup] Configured {objectName}");
        }

        private static void ConfigureRuntimeReferences(GestureDetector detector, GestureEventRouter router)
        {
            foreach (var spellCaster in Object.FindObjectsByType<SpellCaster>(FindObjectsInactive.Include))
            {
                SetSerializedObjectReference(spellCaster, "gestureDetector", detector);
                SetSerializedObjectReference(spellCaster, "gestureRouter", router);
            }

            foreach (var movementController in Object.FindObjectsByType<MovementController>(FindObjectsInactive.Include))
            {
                SetSerializedObjectReference(movementController, "gestureDetector", detector);
                SetSerializedObjectReference(movementController, "gestureRouter", router);
            }
        }

        private static Image GetOrCreateImage(Transform parent, string name)
        {
            var go = GetOrCreateChild(parent, name);
            var image = go.GetComponent<Image>();
            if (image == null)
                image = Undo.AddComponent<Image>(go);

            image.color = new Color(0f, 0f, 0f, 0f);
            EditorUtility.SetDirty(image);
            return image;
        }

        private static GameObject FindOrCreateSceneObject(string name)
        {
            var found = GameObject.Find(name);
            if (found != null)
                return found;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var sceneObject = GameObject.Find(name);
            if (sceneObject != null)
            {
                Undo.SetTransformParent(sceneObject.transform, parent, $"Parent {name}");
                sceneObject.transform.SetParent(parent, false);
                return sceneObject;
            }

            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void SetProperty(Type type, Component component, string propertyName, object value)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                Debug.LogWarning($"[ArcaneVR Setup] Property not found: {type.Name}.{propertyName}");
                return;
            }

            property.SetValue(component, value);
        }

        private static UnityEvent GetOrCreateUnityEvent(Type type, Component component, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                Debug.LogWarning($"[ArcaneVR Setup] Event property not found: {type.Name}.{propertyName}");
                return new UnityEvent();
            }

            var unityEvent = property.GetValue(component) as UnityEvent;
            if (unityEvent != null)
                return unityEvent;

            unityEvent = new UnityEvent();
            property.SetValue(component, unityEvent);
            return unityEvent;
        }

        private static void ClearPersistentCalls(Component component, string eventFieldName)
        {
            var serializedObject = new SerializedObject(component);
            var calls = serializedObject.FindProperty($"{eventFieldName}.m_PersistentCalls.m_Calls");
            if (calls == null)
                return;

            calls.ClearArray();
            serializedObject.ApplyModifiedProperties();
        }

        private static void ClearObjectArray(Component component, string fieldName)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null || !property.isArray)
                return;

            property.ClearArray();
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetSerializedObjectReference(Object target, string propertyName, Object value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void SetSerializedBool(Object target, string propertyName, bool value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.boolValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
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
    }
}
