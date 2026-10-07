using System.Collections.Generic;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.UI;

namespace MaliGo.World
{
    /// <summary>
    /// Bank (DESIGN_SPEC §5.4.6, §4.8): moves whole amounts between cash and savings through
    /// <see cref="BankRules"/> (and so the money recorder). Chips R50 / R100 / R200 each way; a chip is disabled
    /// when the pool cannot cover it (no partial moves). The sheet stays open and its values refresh in place.
    /// No Mali line.
    /// </summary>
    public class BankInteraction : ProximityInteraction
    {
        const string MoveGroup = "Move to savings";
        const string TakeOutGroup = "Take out of savings";

        ActionPanelUI panel;

        protected override void OnAwake()
        {
            SetPrompt("Bank");
            SetVerbAndIcon("Open", "pouch");
            panel = gameObject.AddComponent<ActionPanelUI>();
        }

        protected override bool CanInteract()
        {
            return panel == null || !panel.IsOpen;
        }

        public override bool IsBuildingEntrance => true;

        protected override void OnInteract()
        {
            var actions = new List<ActionPanelUI.ActionButton>();
            foreach (float amount in BankRules.Amounts)
            {
                float a = amount;
                actions.Add(ActionPanelUI.ActionButton.Chip(MoveGroup, MoneyFormat.Rand(a), "pouch_add",
                    () => MoveToSavings(a), () => CanAfford(a, true), "Not enough cash"));
            }

            foreach (float amount in BankRules.Amounts)
            {
                float a = amount;
                actions.Add(ActionPanelUI.ActionButton.Chip(TakeOutGroup, MoneyFormat.Rand(a), "pouch_remove",
                    () => TakeOut(a), () => CanAfford(a, false), "Not enough savings"));
            }

            panel.ShowRows("Bank", BuildRows, actions, GoalProgress);
        }

        static IReadOnlyList<(string label, string value, bool attention)> BuildRows()
        {
            var rows = new List<(string label, string value, bool attention)>();
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data?.financialStats == null)
            {
                return rows;
            }

            rows.Add(("Cash", MoneyFormat.Rand(data.financialStats.cash), false));
            rows.Add(("Savings", MoneyFormat.Rand(data.financialStats.savings), false));
            FinancialGoal goal = data.GetPrimaryGoal();
            if (goal != null)
            {
                rows.Add(("Goal", goal.goalName + " " + MoneyFormat.Rand(goal.targetAmount), false));
            }

            return rows;
        }

        static float GoalProgress()
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            FinancialGoal goal = data?.GetPrimaryGoal();
            return goal != null && data.financialStats != null ? goal.Progress(data.financialStats.savings) : 0f;
        }

        static bool CanAfford(float amount, bool fromCash)
        {
            FinancialStats stats = PlayerDataAccess.GetFinancialStats();
            if (stats == null)
            {
                return false;
            }

            float pool = fromCash ? stats.cash : stats.savings;
            return pool >= amount - MoneyRecorder.Tolerance;
        }

        static void MoveToSavings(float amount)
        {
            if (PlayerDataManager.Instance == null || !CanAfford(amount, true))
            {
                return;
            }

            PlayerDataManager.Instance.UpdatePlayerData(data => BankRules.MoveToSavings(data, amount), saveImmediately: true);
        }

        static void TakeOut(float amount)
        {
            if (PlayerDataManager.Instance == null || !CanAfford(amount, false))
            {
                return;
            }

            PlayerDataManager.Instance.UpdatePlayerData(data => BankRules.TakeOut(data, amount), saveImmediately: true);
        }
    }
}
