using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ArcaneVR.UI.Quest
{
    /// <summary>
    /// VR용 퀘스트 HUD — World Space Canvas + OVRCanvas 기반.
    /// 카메라를 부드럽게 따라다니며 왼쪽 상단에 퀘스트를 표시합니다.
    ///
    /// 씬 설정:
    ///  - Canvas (World Space, OVRRaycaster 추가)
    ///    └─ QuestHUDRoot (이 컴포넌트 부착)
    ///       ├─ HeaderText  (TMP)
    ///       └─ QuestListContainer (Vertical Layout Group)
    ///          └─ [QuestEntryPrefab 인스턴스들]
    ///
    /// Canvas의 Event Camera를 OVRCameraRig의 CenterEyeAnchor 카메라로 지정하세요.
    /// </summary>
    public class QuestHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject questEntryPrefab;
        [SerializeField] private Transform questListContainer;

        [Header("VR Follow Settings")]
        [SerializeField] private Transform vrCamera;          // CenterEyeAnchor
        [SerializeField] private float followDistance = 2.0f; // 카메라로부터 거리 (m)
        [SerializeField] private Vector3 offset = new Vector3(-0.5f, 0.3f, 0f); // 왼쪽 상단 오프셋
        [SerializeField] private float followSpeed = 3f;      // 부드럽게 따라가는 속도

        [Header("Appearance")]
        [SerializeField] private int maxDisplayedQuests = 3;
        [SerializeField] private float fadeInDuration = 0.3f;

        private readonly Dictionary<string, QuestEntryUI> _entryMap = new();
        private CanvasGroup _canvasGroup;

        // ─────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // vrCamera 미지정 시 MainCamera로 폴백
            if (vrCamera == null && Camera.main != null)
                vrCamera = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (vrCamera == null) return;

            // 카메라 정면 방향 기준으로 목표 위치 계산 (Y축 회전만 반영)
            Vector3 forward = vrCamera.forward;
            forward.y = 0f;
            if (forward == Vector3.zero) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 targetPos = vrCamera.position
                + forward * followDistance
                + right * offset.x
                + Vector3.up * offset.y;

            // 부드럽게 이동
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

            // 항상 카메라를 바라보게
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(transform.position - vrCamera.position),
                Time.deltaTime * followSpeed
            );
        }

        private void OnEnable()
        {
            if (QuestManager.Instance == null) return;
            QuestManager.Instance.OnQuestAdded     += HandleQuestAdded;
            QuestManager.Instance.OnQuestUpdated   += HandleQuestUpdated;
            QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;

            // 이미 진행 중인 퀘스트 초기 표시
            foreach (var quest in QuestManager.Instance.GetActiveQuests())
                HandleQuestAdded(quest);
        }

        private void OnDisable()
        {
            if (QuestManager.Instance == null) return;
            QuestManager.Instance.OnQuestAdded     -= HandleQuestAdded;
            QuestManager.Instance.OnQuestUpdated   -= HandleQuestUpdated;
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }

        // ─────────────────────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────────────────────

        private void HandleQuestAdded(QuestData quest)
        {
            if (_entryMap.ContainsKey(quest.id)) return;
            if (_entryMap.Count >= maxDisplayedQuests) return;

            var go = Instantiate(questEntryPrefab, questListContainer);
            var entry = go.GetComponent<QuestEntryUI>();
            if (entry == null) entry = go.AddComponent<QuestEntryUI>();

            entry.Initialize(quest);
            _entryMap[quest.id] = entry;

            StartCoroutine(FadeIn(go.GetComponent<CanvasGroup>()));
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (_entryMap.TryGetValue(quest.id, out var entry))
                entry.Refresh(quest);
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (!_entryMap.TryGetValue(quest.id, out var entry)) return;
            _entryMap.Remove(quest.id);
            StartCoroutine(FadeOutAndDestroy(entry.gameObject));
        }

        // ─────────────────────────────────────────────────────────────
        // Animations
        // ─────────────────────────────────────────────────────────────

        private IEnumerator FadeIn(CanvasGroup cg)
        {
            if (cg == null) yield break;
            cg.alpha = 0f;
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
        }

        private IEnumerator FadeOutAndDestroy(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                float t = fadeInDuration;
                while (t > 0f)
                {
                    t -= Time.deltaTime;
                    cg.alpha = Mathf.Clamp01(t / fadeInDuration);
                    yield return null;
                }
            }
            Destroy(go);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // QuestEntryUI — 개별 퀘스트 항목 표시
    // ─────────────────────────────────────────────────────────────────

    public class QuestEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI objectivesText;

        public void Initialize(QuestData quest)
        {
            AutoBindTexts();
            Refresh(quest);
        }

        public void Refresh(QuestData quest)
        {
            if (titleText != null)
                titleText.text = quest.title;

            if (objectivesText != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var obj in quest.objectives)
                {
                    string check = obj.isCompleted ? "✓ " : "• ";
                    sb.AppendLine(check + obj.GetProgressText());
                }
                objectivesText.text = sb.ToString().TrimEnd();
            }
        }

        // 프리팹에 이미 TMP가 있으면 자동 바인딩
        private void AutoBindTexts()
        {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length >= 1 && titleText == null)     titleText = texts[0];
            if (texts.Length >= 2 && objectivesText == null) objectivesText = texts[1];
        }
    }
}
