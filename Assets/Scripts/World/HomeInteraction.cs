using System.Text;
using MaliGo.Characters;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.UI;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Home: the player's base. Shows current finances, the goal and upcoming bills, and is
    /// where the day ends: sleeping moves to the next day, charges whatever bills fall due
    /// and restores energy. Kept deliberately minimal - the fuller day cycle (an end-of-day
    /// event and summary, new scenarios each day) builds on SleepAndEndDay().
    /// </summary>
    public class HomeInteraction : ProximityInteraction
    {
        ActionPanelUI panel;

        protected override void OnAwake()
        {
            SetPrompt("Press E to go inside");
            panel = gameObject.AddComponent<ActionPanelUI>();
        }

        protected override bool CanInteract()
        {
            return panel == null || !panel.IsOpen;
        }

        protected override void OnInteract()
        {
            panel.Show("Home", BuildStatusText, new[]
            {
                new ActionPanelUI.ActionButton("Sleep - end the day", SleepAndEndDay)
            });
        }

        string BuildStatusText()
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data?.financialStats == null)
            {
                return "No data yet.";
            }

            FinancialStats stats = data.financialStats;
            FinancialGoal goal = data.GetPrimaryGoal();

            var text = new StringBuilder();
            text.Append($"Day {data.currentDay}\n");
            text.Append($"Cash: R{stats.cash:0}\n");
            text.Append($"Savings: R{stats.savings:0}\n");
            text.Append($"Financial Stress: {stats.financialStress:0}%\n");
            text.Append($"Energy: {stats.energy:0}%\n");
            text.Append($"Goal - {goal.goalName}: R{goal.currentAmount:0} / R{goal.targetAmount:0}\n");
            AppendBills(text, data);
            return text.ToString();
        }

        static void AppendBills(StringBuilder text, PlayerData data)
        {
            if (data.obligations == null || data.obligations.Length == 0)
            {
                return;
            }

            text.Append("\nBills:\n");
            foreach (Obligation bill in data.obligations)
            {
                if (bill == null)
                {
                    continue;
                }

                text.Append($"{bill.label}: R{bill.amount:0}");
                if (bill.paymentsRemaining != 0)
                {
                    // Bills are charged when the player sleeps, so count in nights: a bill due
                    // on tomorrow's day number comes off tonight.
                    int nights = bill.nextDueDay - data.currentDay;
                    text.Append(nights <= 1 ? " due tonight" : $" due in {nights} nights");
                }

                if (bill.arrears > 0f)
                {
                    text.Append($" (R{bill.arrears:0} still owed)");
                }

                text.Append('\n');
            }
        }

        void SleepAndEndDay()
        {
            if (PlayerDataManager.Instance == null)
            {
                return;
            }

            ObligationSettlement settlement = null;

            PlayerDataManager.Instance.UpdatePlayerData(data =>
            {
                data.currentDay = Mathf.Max(1, data.currentDay) + 1;
                settlement = ObligationLedger.SettleDue(data, data.currentDay);
                data.financialStats.energy = 100f;
            }, saveImmediately: true);

            if (settlement != null)
            {
                ShowMaliLine(DescribeNight(settlement));
            }
        }

        /// <summary>What happened overnight, stated plainly: what was paid and what's still owed. No verdict.</summary>
        static string DescribeNight(ObligationSettlement settlement)
        {
            var line = new StringBuilder($"Morning, {{0}}. Day {settlement.day}.");

            if (settlement.payments.Count == 0)
            {
                line.Append(" No bills came off overnight.");
                return line.ToString();
            }

            foreach (ObligationPayment payment in settlement.payments)
            {
                line.Append(payment.stillOwed > 0f
                    ? $" {payment.label}: R{payment.paid:0} paid, R{payment.stillOwed:0} still owed."
                    : $" {payment.label}: R{payment.paid:0} paid.");
            }

            return line.ToString();
        }

        static void ShowMaliLine(string template)
        {
            GameObject maliObject = GameObject.Find("Mali");
            MaliDialogueController dialogue = maliObject != null ? maliObject.GetComponent<MaliDialogueController>() : null;
            dialogue?.ShowFormatted(template, PlayerDataAccess.GetCharacterName());
        }
    }
}
