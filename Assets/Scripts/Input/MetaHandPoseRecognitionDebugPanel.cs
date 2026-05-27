using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Runtime overlay for validating recorded Meta Interaction SDK hand pose selectors.
    /// Shows whether each selector's shape, transform, and combined active state are currently recognized.
    /// </summary>
    public class MetaHandPoseRecognitionDebugPanel : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool logTransitions = true;
        [SerializeField] private float refreshInterval = 1f;
        [SerializeField] private int fontSize = 16;
        [SerializeField] private Vector2 panelPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 panelSize = new Vector2(560f, 420f);

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<GameObject, bool> lastActiveStates = new Dictionary<GameObject, bool>();
        private Vector2 scroll;
        private float nextRefreshTime;
        private GUIStyle labelStyle;
        private GUIStyle activeStyle;
        private GUIStyle inactiveStyle;
        private GUIStyle headerStyle;

        private sealed class Entry
        {
            public GameObject root;
            public HandRef hand;
            public ActiveStateGroup group;
            public ShapeRecognizerActiveState shape;
            public TransformRecognizerActiveState transform;
        }

        private void Awake()
        {
            RefreshEntries();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                visible = !visible;

            if (Time.unscaledTime >= nextRefreshTime)
                RefreshEntries();

            if (logTransitions)
                LogActiveTransitions();
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            GUILayout.BeginArea(new Rect(panelPosition, panelSize), GUI.skin.box);
            GUILayout.Label($"Recorded Hand Pose Debug ({entries.Count})  |  Toggle: {toggleKey}", headerStyle);
            GUILayout.Label("VALID requires live hand tracking. Raw Shape can remain OK briefly from cached feature state, so trust VALID/SDK more than raw Shape alone.", labelStyle);

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var entry in entries)
            {
                if (entry.root == null)
                    continue;

                var handValid = IsHandValid(entry.hand);
                var groupActive = IsActive(entry.group);
                var shapeActive = IsActive(entry.shape);
                var transformActive = IsActive(entry.transform);
                var strictActive = handValid && shapeActive && (!HasTransformRecognizer(entry) || transformActive);
                var sdkActive = handValid && groupActive;
                var style = sdkActive ? activeStyle : inactiveStyle;
                GUILayout.Label(
                    $"{Mark(sdkActive)} {entry.root.name,-30} Hand:{Mark(handValid)}  Shape:{Mark(shapeActive)}  Transform:{TransformMark(entry, transformActive)}  Strict:{Mark(strictActive)}",
                    style);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public void RefreshEntries()
        {
            entries.Clear();
            var seenRoots = new HashSet<GameObject>();
            var bridges = FindObjectsByType<MetaHandPoseGestureBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var bridge in bridges)
            {
                var root = bridge.gameObject;
                AddEntry(root, seenRoots);
            }

            var selectors = FindObjectsByType<ActiveStateSelector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var selector in selectors)
            {
                var root = selector.gameObject;
                if (root.GetComponentInChildren<ShapeRecognizerActiveState>(true) == null)
                    continue;

                AddEntry(root, seenRoots);
            }

            entries.Sort((a, b) => string.Compare(a.root.name, b.root.name, System.StringComparison.OrdinalIgnoreCase));
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.25f, refreshInterval);
        }

        private void AddEntry(GameObject root, HashSet<GameObject> seenRoots)
        {
            if (root == null || !seenRoots.Add(root))
                return;

            entries.Add(new Entry
            {
                root = root,
                hand = root.GetComponentInChildren<HandRef>(true),
                group = root.GetComponentInChildren<ActiveStateGroup>(true),
                shape = root.GetComponentInChildren<ShapeRecognizerActiveState>(true),
                transform = root.GetComponentInChildren<TransformRecognizerActiveState>(true)
            });
        }

        private void LogActiveTransitions()
        {
            foreach (var entry in entries)
            {
                if (entry.root == null)
                    continue;

                var active = IsHandValid(entry.hand) && IsActive(entry.group);
                if (!lastActiveStates.TryGetValue(entry.root, out var wasActive))
                {
                    lastActiveStates[entry.root] = active;
                    continue;
                }

                if (active == wasActive)
                    continue;

                lastActiveStates[entry.root] = active;
                Debug.Log(active
                    ? $"[MetaHandPoseDebug] ACTIVE {entry.root.name}"
                    : $"[MetaHandPoseDebug] inactive {entry.root.name}", entry.root);
            }
        }

        private static bool IsActive(IActiveState activeState)
        {
            return activeState != null && activeState.Active;
        }

        private static bool IsHandValid(HandRef hand)
        {
            if (hand == null)
                return false;

            try
            {
                return hand.IsConnected &&
                       hand.IsTrackedDataValid &&
                       hand.IsHighConfidence;
            }
            catch (System.NullReferenceException)
            {
                return false;
            }
        }

        private static bool HasTransformRecognizer(Entry entry)
        {
            return entry.transform != null;
        }

        private static string TransformMark(Entry entry, bool active)
        {
            if (entry.transform == null)
                return "MISSING";

            return entry.transform.isActiveAndEnabled ? Mark(active) : "DISABLED";
        }

        private static string Mark(bool value)
        {
            return value ? "OK" : "--";
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            headerStyle = new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
            activeStyle = new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 1f, 0.35f) }
            };
            inactiveStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };
        }
    }
}
