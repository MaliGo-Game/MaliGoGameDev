using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.UI;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Home (DESIGN_SPEC §5.4.6, §4.8): the Home sheet shows the day, money, energy, tonight's bills, what is still
    /// owed and the next bill, all from the shared bill helpers (§2.4), and holds "Sleep: end Day {day}". Sleep
    /// opens the confirm; on Sleep the confirm and the sheet both close (and pop) before
    /// <see cref="GameEvents.RaiseSleepRequested"/> is raised. The night itself belongs to <c>DayFlowController</c>.
    /// </summary>
    public class HomeInteraction : ProximityInteraction
    {
        const int MaxTonightRows = 3;

        ActionPanelUI panel;
        SleepConfirmView sleepConfirm;

        protected override void OnAwake()
        {
            SetPrompt("Home");
            SetVerbAndIcon("Open", "home");
            panel = gameObject.AddComponent<ActionPanelUI>();
            sleepConfirm = gameObject.AddComponent<SleepConfirmView>();
        }

        protected override bool CanInteract()
        {
            return (panel == null || !panel.IsOpen) && (sleepConfirm == null || !sleepConfirm.IsOpen);
        }

        public override bool IsBuildingEntrance => true;

        protected override void OnInteract()
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            int day = data != null ? data.currentDay : 0;
            panel.ShowRows("Home", BuildRows, new[]
            {
                new ActionPanelUI.ActionButton("Sleep: end Day " + day, OpenSleepConfirm, "hourglass")
            });
        }

        void OpenSleepConfirm()
        {
            sleepConfirm.Open(PlayerDataAccess.GetCurrentPlayer(), ConfirmSleep);
        }

        void ConfirmSleep()
        {
            // The confirm has already closed and popped itself; close the Home sheet too, then ask for the night.
            if (sleepConfirm.IsOpen)
            {
                sleepConfirm.Close();
            }

            panel.HideImmediate();
            GameEvents.RaiseSleepRequested();
        }

        // ================================================================ rows (§4.8 Home sheet)

        static IReadOnlyList<(string label, string value, bool attention)> BuildRows()
        {
            var rows = new List<(string label, string value, bool attention)>();
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data?.financialStats == null)
            {
                return rows;
            }

            FinancialStats stats = data.financialStats;
            rows.Add(("Day", data.currentDay.ToString(System.Globalization.CultureInfo.InvariantCulture), false));
            rows.Add(("Cash", MoneyFormat.Rand(stats.cash), false));
            rows.Add(("Savings", MoneyFormat.Rand(stats.savings), false));
            rows.Add(("Energy", Mathf.RoundToInt(Mathf.Clamp(stats.energy, 0f, 100f))
                .ToString(System.Globalization.CultureInfo.InvariantCulture), false));
            rows.Add((null, null, false));

            List<DueItem> tonight = ObligationLedger.DueOnNight(data, data.currentDay);
            if (tonight.Count == 0)
            {
                rows.Add(("Tonight", "Nothing due", false));
            }
            else
            {
                float rest = 0f;
                for (int i = 0; i < tonight.Count; i++)
                {
                    DueItem item = tonight[i];
                    if (i < MaxTonightRows - 1 || tonight.Count == MaxTonightRows)
                    {
                        rows.Add((i == 0 ? "Tonight" : "", Short(item) + " " + MoneyFormat.Rand(item.Total), false));
                    }
                    else
                    {
                        rest += item.Total;
                    }
                }

                if (rest > 0.005f)
                {
                    rows.Add(("", (tonight.Count - (MaxTonightRows - 1)) + " more " + MoneyFormat.Rand(rest), false));
                }
            }

            Obligation owed = ObligationLedger.LargestArrears(data);
            if (owed != null)
            {
                string label = string.IsNullOrEmpty(owed.shortLabel) ? owed.label : owed.shortLabel;
                rows.Add(("Still owed", label + " " + MoneyFormat.Rand(owed.arrears), true));
                rows.Add(("The Bank is up the road.", "", false));
            }

            DueItem? next = NextAfterTonight(data);
            rows.Add(("Next", next.HasValue ? NextText(next.Value) : "No bills till payday", false));
            return rows;
        }

        static string Short(DueItem item)
        {
            return string.IsNullOrEmpty(item.shortLabel) ? item.label : item.shortLabel;
        }

        static string NextText(DueItem item)
        {
            string when = item.day >= ChapterConfig.PaydayDay
                ? "payday"
                : "Day " + item.day.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Short(item) + " " + MoneyFormat.Rand(item.amount) + " · " + when;
        }

        /// <summary>
        /// "Next" = <see cref="ObligationLedger.NextDue"/> after tonight (§5.4.6): asked of a copy of the state in
        /// which tonight's payments have already been counted off (nothing in the real save changes).
        /// </summary>
        static DueItem? NextAfterTonight(PlayerData data)
        {
            int today = data.currentDay;
            var probe = new PlayerData
            {
                currentDay = today + 1,
                obligations = new Obligation[data.obligations?.Length ?? 0]
            };

            for (int i = 0; i < probe.obligations.Length; i++)
            {
                Obligation source = data.obligations[i];
                if (source == null)
                {
                    continue;
                }

                Obligation copy = JsonUtility.FromJson<Obligation>(JsonUtility.ToJson(source));
                int interval = Mathf.Max(1, copy.intervalDays);
                while (copy.paymentsRemaining != 0 && copy.nextDueDay <= today)
                {
                    copy.nextDueDay += interval;
                    if (copy.paymentsRemaining > 0)
                    {
                        copy.paymentsRemaining--;
                    }
                }

                probe.obligations[i] = copy;
            }

            return ObligationLedger.NextDue(probe);
        }
    }
}
