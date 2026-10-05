using System;

namespace MaliGo.Data
{
    public struct GoalPreset
    {
        public string id;
        public string title;
        public string shortName;
        public float target;
    }

    /// <summary>The four savings goals offered in character creation (design spec 2.3).</summary>
    public static class GoalPresets
    {
        public const string BufferId = "buffer";
        public const string DecemberId = "december";
        public const string PhoneId = "phone";
        public const string HustleId = "hustle";

        public static readonly GoalPreset[] All =
        {
            new GoalPreset { id = BufferId, title = "Emergency buffer", shortName = "emergency buffer", target = 2000f },
            new GoalPreset { id = DecemberId, title = "December trip home", shortName = "December trip home", target = 2500f },
            new GoalPreset { id = PhoneId, title = "New phone", shortName = "new phone", target = 3000f },
            new GoalPreset { id = HustleId, title = "Side-hustle stock", shortName = "side-hustle stock", target = 1500f }
        };

        /// <summary>The preset with this id; an unknown id gives "buffer".</summary>
        public static GoalPreset Get(string id)
        {
            foreach (GoalPreset preset in All)
            {
                if (string.Equals(preset.id, id, StringComparison.Ordinal))
                {
                    return preset;
                }
            }

            return All[0];
        }

        /// <summary>goalId = id, goalName = title, target, current 0.</summary>
        public static FinancialGoal CreateGoal(string id)
        {
            GoalPreset preset = Get(id);
            return new FinancialGoal(preset.id, preset.title, preset.target, 0f, 0f, "In Progress", "Savings");
        }

        /// <summary>The preset's short name by goalId, falling back to goalName in lower case.</summary>
        public static string ShortName(FinancialGoal goal)
        {
            if (goal == null)
            {
                return All[0].shortName;
            }

            foreach (GoalPreset preset in All)
            {
                if (string.Equals(preset.id, goal.goalId, StringComparison.Ordinal))
                {
                    return preset.shortName;
                }
            }

            return (goal.goalName ?? "").ToLowerInvariant();
        }
    }
}
