using System;
using System.Collections.Generic;

namespace ArcaneVR.UI.Quest
{
    [Serializable]
    public class QuestObjective
    {
        public string description;
        public int currentAmount;
        public int requiredAmount;
        public bool isCompleted => currentAmount >= requiredAmount;

        public QuestObjective(string description, int required)
        {
            this.description = description;
            this.requiredAmount = required;
            this.currentAmount = 0;
        }

        public string GetProgressText()
        {
            if (requiredAmount <= 1)
                return description;
            return $"{description} ({currentAmount}/{requiredAmount})";
        }
    }

    [Serializable]
    public class QuestData
    {
        public string id;
        public string title;
        public List<QuestObjective> objectives;
        public bool isCompleted;

        public QuestData(string id, string title, List<QuestObjective> objectives)
        {
            this.id = id;
            this.title = title;
            this.objectives = objectives ?? new List<QuestObjective>();
            this.isCompleted = false;
        }

        public void UpdateObjective(int index, int amount)
        {
            if (index < 0 || index >= objectives.Count) return;
            objectives[index].currentAmount = Math.Min(amount, objectives[index].requiredAmount);
            CheckCompletion();
        }

        private void CheckCompletion()
        {
            isCompleted = objectives.TrueForAll(o => o.isCompleted);
        }
    }
}
