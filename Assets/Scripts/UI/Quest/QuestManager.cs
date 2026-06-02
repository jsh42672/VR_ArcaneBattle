using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneVR.UI.Quest
{
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public event Action<QuestData> OnQuestAdded;
        public event Action<QuestData> OnQuestUpdated;
        public event Action<QuestData> OnQuestCompleted;

        private readonly Dictionary<string, QuestData> _activeQuests = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void AddQuest(QuestData quest)
        {
            if (_activeQuests.ContainsKey(quest.id)) return;
            _activeQuests[quest.id] = quest;
            OnQuestAdded?.Invoke(quest);
        }

        public void UpdateObjective(string questId, int objectiveIndex, int amount)
        {
            if (!_activeQuests.TryGetValue(questId, out var quest)) return;
            quest.UpdateObjective(objectiveIndex, amount);
            OnQuestUpdated?.Invoke(quest);

            if (quest.isCompleted)
                CompleteQuest(questId);
        }

        public void CompleteQuest(string questId)
        {
            if (!_activeQuests.TryGetValue(questId, out var quest)) return;
            _activeQuests.Remove(questId);
            OnQuestCompleted?.Invoke(quest);
        }

        public IEnumerable<QuestData> GetActiveQuests() => _activeQuests.Values;

        // 테스트용 샘플 퀘스트 추가
        [ContextMenu("Add Sample Quest")]
        public void AddSampleQuest()
        {
            var quest = new QuestData(
                "quest_001",
                "마법사의 시련",
                new System.Collections.Generic.List<QuestObjective>
                {
                    new QuestObjective("슬라임 처치", 5),
                    new QuestObjective("마법 크리스탈 수집", 3)
                }
            );
            AddQuest(quest);
        }
    }
}
