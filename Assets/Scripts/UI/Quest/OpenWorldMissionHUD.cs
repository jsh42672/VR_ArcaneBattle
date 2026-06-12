using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneVR.UI.Quest
{
    [DisallowMultipleComponent]
    public class OpenWorldMissionHUD : MonoBehaviour
    {
        [Serializable]
        public class MissionLine
        {
            public string text;
            public bool visible = true;
            public bool completed;
        }

        [Header("Mission Text")]
        [SerializeField] private string headerText = "임무";
        [SerializeField] private List<MissionLine> missions = new()
        {
            new MissionLine { text = "마법을 더미에 맞추세요" },
            new MissionLine { text = "포탈을 타세요" }
        };

        [Header("VR Placement")]
        [SerializeField] private Transform vrCamera;
        [SerializeField] private float followDistance = 1.85f;
        [SerializeField] private Vector2 viewportOffset = new(-0.72f, 0.44f);
        [SerializeField] private float followSpeed = 8f;

        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new(560f, 210f);
        [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.55f;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color headerColor = new(1f, 0.9f, 0.55f, 1f);
        [SerializeField] private Color missionColor = Color.white;
        [SerializeField] private Color completedColor = new(0.72f, 1f, 0.72f, 1f);
        [SerializeField] private float canvasScale = 0.0015f;

        private Canvas _canvas;
        private Image _background;
        private Text _headerLabel;
        private Text _missionLabel;
        private string _lastRenderedText;

        private void Awake()
        {
            EnsureCamera();
            BuildHudIfNeeded();
            RefreshText();
        }

        private void OnValidate()
        {
            backgroundAlpha = Mathf.Clamp01(backgroundAlpha);
            followDistance = Mathf.Max(0.25f, followDistance);
            followSpeed = Mathf.Max(0f, followSpeed);

            if (!Application.isPlaying)
                return;

            ApplyStyle();
            RefreshText();
        }

        private void LateUpdate()
        {
            EnsureCamera();
            FollowCamera();
            RefreshText();
        }

        [ContextMenu("Rebuild HUD Layout")]
        public void RebuildHudLayout()
        {
            EnsureCamera();
            BuildHudIfNeeded();
            ApplyStyle();
            _lastRenderedText = null;
            RefreshText();
            SnapToCamera();
        }

        public void SetMissionCompleted(int index, bool completed)
        {
            if (index < 0 || index >= missions.Count)
                return;

            missions[index].completed = completed;
            RefreshText();
        }

        public void SetMissionVisible(int index, bool visible)
        {
            if (index < 0 || index >= missions.Count)
                return;

            missions[index].visible = visible;
            RefreshText();
        }

        private void EnsureCamera()
        {
            if (vrCamera != null)
                return;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                vrCamera = mainCamera.transform;
        }

        private void FollowCamera()
        {
            if (vrCamera == null)
                return;

            GetCameraPose(out Vector3 targetPosition, out Quaternion targetRotation);

            if (followSpeed <= 0f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        private void SnapToCamera()
        {
            if (vrCamera == null)
                return;

            GetCameraPose(out Vector3 targetPosition, out Quaternion targetRotation);
            transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private void GetCameraPose(out Vector3 targetPosition, out Quaternion targetRotation)
        {
            Vector3 forward = vrCamera.forward;
            Vector3 right = vrCamera.right;
            Vector3 up = vrCamera.up;

            targetPosition = vrCamera.position
                + forward * followDistance
                + right * viewportOffset.x
                + up * viewportOffset.y;

            targetRotation = Quaternion.LookRotation(targetPosition - vrCamera.position, up);
        }

        private void BuildHudIfNeeded()
        {
            if (_canvas != null && _background != null && _headerLabel != null && _missionLabel != null)
                return;

            ClearGeneratedChildren();

            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();

            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = vrCamera != null ? vrCamera.GetComponent<Camera>() : Camera.main;
            _canvas.sortingOrder = 50;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;

            RectTransform canvasRect = GetComponent<RectTransform>();
            canvasRect.sizeDelta = panelSize;

            GameObject panel = new("MissionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _background = panel.GetComponent<Image>();

            GameObject header = new("HeaderText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            header.transform.SetParent(panel.transform, false);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -18f);
            headerRect.sizeDelta = new Vector2(-36f, 44f);
            _headerLabel = header.GetComponent<Text>();

            GameObject mission = new("MissionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            mission.transform.SetParent(panel.transform, false);
            RectTransform missionRect = mission.GetComponent<RectTransform>();
            missionRect.anchorMin = new Vector2(0f, 0f);
            missionRect.anchorMax = new Vector2(1f, 1f);
            missionRect.offsetMin = new Vector2(24f, 18f);
            missionRect.offsetMax = new Vector2(-24f, -66f);
            _missionLabel = mission.GetComponent<Text>();

            ApplyStyle();
            transform.localScale = Vector3.one * canvasScale;
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private void ApplyStyle()
        {
            if (_background != null)
            {
                Color color = backgroundColor;
                color.a = backgroundAlpha;
                _background.color = color;
            }

            if (_headerLabel != null)
            {
                _headerLabel.font = GetDefaultFont();
                _headerLabel.text = headerText;
                _headerLabel.color = headerColor;
                _headerLabel.fontSize = 32;
                _headerLabel.fontStyle = FontStyle.Bold;
                _headerLabel.alignment = TextAnchor.MiddleLeft;
                _headerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                _headerLabel.verticalOverflow = VerticalWrapMode.Overflow;
                _headerLabel.raycastTarget = false;
            }

            if (_missionLabel != null)
            {
                _missionLabel.font = GetDefaultFont();
                _missionLabel.color = missionColor;
                _missionLabel.fontSize = 28;
                _missionLabel.lineSpacing = 1.25f;
                _missionLabel.alignment = TextAnchor.UpperLeft;
                _missionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                _missionLabel.verticalOverflow = VerticalWrapMode.Overflow;
                _missionLabel.supportRichText = true;
                _missionLabel.raycastTarget = false;
            }
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private void RefreshText()
        {
            if (_headerLabel == null || _missionLabel == null)
                return;

            _headerLabel.text = headerText;

            string renderedText = BuildMissionText();
            if (_lastRenderedText == renderedText)
                return;

            _missionLabel.text = renderedText;
            _lastRenderedText = renderedText;
        }

        private string BuildMissionText()
        {
            System.Text.StringBuilder builder = new();

            foreach (MissionLine mission in missions)
            {
                if (mission == null || !mission.visible || string.IsNullOrWhiteSpace(mission.text))
                    continue;

                string color = ColorUtility.ToHtmlStringRGBA(mission.completed ? completedColor : missionColor);
                string marker = mission.completed ? "✓" : "•";
                builder.Append("<color=#");
                builder.Append(color);
                builder.Append(">");
                builder.Append(marker);
                builder.Append(' ');
                builder.Append(mission.text);
                builder.AppendLine("</color>");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
