using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Minimal runtime overlay for validating only the newly recorded two-hand pose selectors.
    /// </summary>
    public class RecordedPoseRecognitionTestPanel : MonoBehaviour
    {
        [SerializeField] private string[] targetPoseNames =
        {
            "Left_PalmTogether_OpenPalm",
            "Right_PalmTogether_OpenPalm",
            "Left_BothForward_OpenPalm",
            "Right_BothForward_OpenPalm"
        };

        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool visible = true;
        [SerializeField] private bool logTransitions = true;
        [SerializeField] private float refreshInterval = 0.5f;
        [SerializeField] private int fontSize = 22;
        [SerializeField] private Vector2 panelPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 panelSize = new Vector2(520f, 210f);

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, bool> lastActiveStates = new Dictionary<string, bool>();
        private float nextRefreshTime;
        private GUIStyle headerStyle;
        private GUIStyle activeStyle;
        private GUIStyle inactiveStyle;

        private sealed class Entry
        {
            public string name;
            public string shortName;
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
                LogTransitions();
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();
            GUILayout.BeginArea(new Rect(panelPosition, panelSize), GUI.skin.box);
            GUILayout.Label("RECORDED POSE TEST", headerStyle);

            foreach (var entry in entries)
            {
                var active = IsRecognized(entry);
                var style = active ? activeStyle : inactiveStyle;
                GUILayout.Label($"{Mark(active)} {entry.shortName,-20} Hand:{Mark(IsHandValid(entry.hand))} Shape:{Mark(IsActive(entry.shape))} Transform:{TransformMark(entry)} Cond:{ConditionMark(entry)}", style);
            }

            GUILayout.EndArea();
        }

        public void RefreshEntries()
        {
            entries.Clear();
            foreach (var poseName in targetPoseNames)
            {
                var root = GameObject.Find(poseName);
                entries.Add(new Entry
                {
                    name = poseName,
                    shortName = RecordedPoseConditionUtility.GetShortPoseName(poseName),
                    hand = root != null ? root.GetComponentInChildren<HandRef>(true) : null,
                    group = root != null ? root.GetComponentInChildren<ActiveStateGroup>(true) : null,
                    shape = root != null ? root.GetComponentInChildren<ShapeRecognizerActiveState>(true) : null,
                    transform = root != null ? root.GetComponentInChildren<TransformRecognizerActiveState>(true) : null
                });
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
        }

        private void LogTransitions()
        {
            foreach (var entry in entries)
            {
                var active = IsRecognized(entry);
                if (!lastActiveStates.TryGetValue(entry.name, out var wasActive))
                {
                    lastActiveStates[entry.name] = active;
                    continue;
                }

                if (active == wasActive)
                    continue;

                lastActiveStates[entry.name] = active;
                Debug.Log(active
                    ? $"[RecordedPoseTest] ACTIVE {entry.name}"
                    : $"[RecordedPoseTest] inactive {entry.name}", this);
            }
        }

        private static bool IsRecognized(Entry entry)
        {
            return IsHandValid(entry.hand) && IsActive(entry.group) && IsConditionSatisfied(entry);
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
                return hand.IsConnected && hand.IsTrackedDataValid && hand.IsHighConfidence;
            }
            catch (System.NullReferenceException)
            {
                return false;
            }
        }

        private static string TransformMark(Entry entry)
        {
            if (entry.transform == null)
                return "MISSING";

            return entry.transform.isActiveAndEnabled ? Mark(IsActive(entry.transform)) : "OFF";
        }

        private static string ConditionMark(Entry entry)
        {
            var conditionKind = RecordedPoseConditionUtility.GetConditionKind(entry.name);
            if (conditionKind == RecordedPoseConditionKind.None)
                return "n/a";

            return Mark(IsConditionSatisfied(entry));
        }

        private static bool IsConditionSatisfied(Entry entry)
        {
            return RecordedPoseConditionUtility.IsConditionSatisfied(
                RecordedPoseConditionUtility.GetConditionKind(entry.name),
                0.32f,
                0.14f);
        }

        private static string Mark(bool value)
        {
            return value ? "OK" : "--";
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
                return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };
            activeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 1f, 0.35f) }
            };
            inactiveStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = new Color(0.82f, 0.82f, 0.82f) }
            };
        }
    }
}
