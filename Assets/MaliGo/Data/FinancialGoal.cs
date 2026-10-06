using System;
using UnityEngine;

namespace MaliGo.Data
{
    /// <summary>
    /// The player's savings goal: a target for savings (progress = savings / targetAmount). goalId is a
    /// GoalPresets id. The separate goal pot (currentAmount) and dailyTarget are retired: always 0.
    /// </summary>
    [Serializable]
    public class FinancialGoal
    {
        public string goalId;
        public string goalName;
        public float targetAmount;
        public float currentAmount;
        public float dailyTarget;
        public string status;
        public string category;

        /// <summary>The "buffer" preset (Emergency buffer, R2 000), nothing in the retired pot.</summary>
        public FinancialGoal()
        {
            goalId = GoalPresets.BufferId;
            goalName = "Emergency buffer";
            targetAmount = 2000f;
            currentAmount = 0f;
            dailyTarget = 0f;
            status = "In Progress";
            category = "Savings";
        }

        public FinancialGoal(string id, string name, float target, float current, float daily, string status = "In Progress", string category = "Savings")
        {
            goalId = id;
            goalName = name;
            targetAmount = target;
            currentAmount = current;
            dailyTarget = daily;
            this.status = status;
            this.category = category;
        }

        /// <summary>Savings as a share of the target, 0-1.</summary>
        public float Progress(float savings) => targetAmount > 0f ? Mathf.Clamp01(savings / targetAmount) : 0f;

        /// <summary>What savings still lacks to reach the target (never negative).</summary>
        public float Remaining(float savings) => Mathf.Max(0f, targetAmount - savings);
    }
}
