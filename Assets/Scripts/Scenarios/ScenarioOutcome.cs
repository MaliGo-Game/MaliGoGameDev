using System;
using System.Collections.Generic;
using System.Globalization;
using MaliGo.Data;
using MaliGo.Economy;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>Whether a choice can be taken with the money the player has now (design spec 7.6).</summary>
    public enum ChoiceAvailability
    {
        Available,
        NotEnoughCash,
        NotEnoughSavings
    }

    /// <summary>
    /// Applies a scenario choice to the player's data (design spec 7.6) and builds the text the choice sheet
    /// and Mali's chip row show for it (4.8). Pure: System + UnityEngine.Mathf/Debug only, so it runs in the
    /// logic tests. Money moves only through MoneyRecorder.
    /// </summary>
    public static class ScenarioOutcome
    {
        /// <summary>XP per scenario choice: the same for every choice, never shown (MASTER 3).</summary>
        public const float ChoiceXp = 5f;

        /// <summary>Shown in a column with nothing in it.</summary>
        public const string EmptyValue = "—";

        const float Tolerance = MoneyRecorder.Tolerance;

        // ------------------------------------------------------------------ apply

        public static ChoiceAvailability Availability(PlayerData d, ScenarioChoice c)
        {
            if (d?.financialStats == null || c == null)
            {
                return ChoiceAvailability.Available;
            }

            if (d.financialStats.cash + c.cashDelta < -Tolerance)
            {
                return ChoiceAvailability.NotEnoughCash;
            }

            if (d.financialStats.savings + c.savingsDelta < -Tolerance)
            {
                return ChoiceAvailability.NotEnoughSavings;
            }

            return ChoiceAvailability.Available;
        }

        /// <summary>True when at least one choice of the scenario can be taken now.</summary>
        public static bool AnyAvailable(PlayerData d, ScenarioDefinition s)
        {
            if (s?.choices == null)
            {
                return false;
            }

            foreach (ScenarioChoice c in s.choices)
            {
                if (c != null && Availability(d, c) == ChoiceAvailability.Available)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// False (no change) if not Available or the scenario is completed. Otherwise: money via
        /// MoneyRecorder (only if a delta is non-zero), energy clamp 0..100, stress clamp 0..100, xp += 5,
        /// behaviour lerp + profile words, completedScenarioIds += id, chapter.choices += record,
        /// instalments -&gt; one Obligation when LaterCount &gt; 0, follow-up -&gt; a followUps key when its day &lt;= 7.
        /// </summary>
        public static bool Apply(PlayerData d, ScenarioDefinition s, ScenarioChoice c)
        {
            if (d?.financialStats == null || s == null || c == null)
            {
                return false;
            }

            if (d.IsScenarioCompleted(s.scenarioId) || Availability(d, c) != ChoiceAvailability.Available)
            {
                return false;
            }

            // Computed before anything changes (they depend only on currentDay and the choice).
            int[] laterDays = LaterDays(d, c);

            // Money first: if the recorder refuses (no open day), nothing else changes either.
            if (Math.Abs(c.cashDelta) >= Tolerance || Math.Abs(c.savingsDelta) >= Tolerance)
            {
                string label = string.IsNullOrEmpty(c.ledgerLabel) ? c.label : c.ledgerLabel;
                MoneyEvent moneyEvent = MoneyRecorder.Apply(d, c.cashDelta, c.savingsDelta, label, s.moneyCategory,
                                                            "scenario:" + s.scenarioId + "/" + c.choiceId);
                if (moneyEvent == null)
                {
                    return false;
                }
            }

            FinancialStats stats = d.financialStats;
            stats.energy = Mathf.Clamp(stats.energy + c.energyDelta, 0f, 100f);
            stats.financialStress = Mathf.Clamp(stats.financialStress + c.financialStressDelta, 0f, 100f);
            stats.financialXP += ChoiceXp;

            ApplyBehaviourSignal(stats, c.behaviourTag);
            d.financialProfile ??= new FinancialProfile();
            d.financialProfile.spendingBehaviour = DescribeSpending(stats.spendingBehaviourScore);
            d.financialProfile.savingBehaviour = DescribeSaving(stats.savingBehaviourScore);

            d.completedScenarioIds = Append(d.completedScenarioIds, s.scenarioId);

            d.chapter ??= new ChapterRecord();
            var choices = new List<ChoiceRecord>(d.chapter.choices ?? Array.Empty<ChoiceRecord>())
            {
                new ChoiceRecord
                {
                    day = d.currentDay,
                    scenarioId = s.scenarioId,
                    choiceId = c.choiceId,
                    tag = c.behaviourTag.ToString()
                }
            };
            d.chapter.choices = choices.ToArray();

            if (laterDays.Length > 0)
            {
                ObligationDefaults.AddObligation(d, new Obligation
                {
                    obligationId = s.scenarioId + "_" + c.choiceId,
                    label = string.IsNullOrEmpty(c.instalmentLabel) ? c.label : c.instalmentLabel,
                    shortLabel = c.instalmentShortLabel ?? "",
                    category = string.IsNullOrEmpty(c.instalmentCategory) ? s.moneyCategory : c.instalmentCategory,
                    kind = string.IsNullOrEmpty(c.instalmentKind) ? Obligation.KindInstalment : c.instalmentKind,
                    amount = c.instalmentAmount,
                    intervalDays = Interval(c),
                    nextDueDay = laterDays[0],
                    paymentsRemaining = laterDays.Length,
                    createdDay = d.currentDay
                });
            }

            int followUpDay = FollowUpDay(d, c);
            if (followUpDay > 0)
            {
                string key = ChapterSchedule.FollowUpKey(c.followUpScenarioId, followUpDay);
                if (d.followUps == null || Array.IndexOf(d.followUps, key) < 0)
                {
                    d.followUps = Append(d.followUps, key);
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ later payments

        /// <summary>
        /// count, or with instalmentLastDueDay &gt; 0: payments due on first, first + interval, ... that are
        /// &lt;= instalmentLastDueDay (at most count). Used by Apply, the Later column and {laterDays}.
        /// </summary>
        public static int LaterCount(PlayerData d, ScenarioChoice c)
        {
            return LaterDays(d, c).Length;
        }

        /// <summary>The due days themselves (first = firstDueDay &gt; 0 ? firstDueDay : currentDay + interval).</summary>
        public static int[] LaterDays(PlayerData d, ScenarioChoice c)
        {
            if (c == null || c.instalmentCount <= 0 || c.instalmentAmount <= Tolerance)
            {
                return Array.Empty<int>();
            }

            int currentDay = d != null ? d.currentDay : 1;
            int interval = Interval(c);
            int first = c.instalmentFirstDueDay > 0 ? c.instalmentFirstDueDay : currentDay + interval;
            var days = new List<int>();
            for (int i = 0; i < c.instalmentCount; i++)
            {
                int day = first + i * interval;
                if (c.instalmentLastDueDay > 0 && day > c.instalmentLastDueDay)
                {
                    break;
                }

                days.Add(day);
            }

            return days.ToArray();
        }

        /// <summary>"Days 4 to 7" (three or more in a row), "Days 3 and 4", "Days 4 and 6", "Day 7"; "" for none.</summary>
        public static string LaterDaysWords(int[] days)
        {
            if (days == null || days.Length == 0)
            {
                return "";
            }

            if (days.Length == 1)
            {
                return "Day " + Num(days[0]);
            }

            if (days.Length == 2)
            {
                return "Days " + Num(days[0]) + " and " + Num(days[1]);
            }

            if (Consecutive(days))
            {
                return "Days " + Num(days[0]) + " to " + Num(days[days.Length - 1]);
            }

            var parts = new List<string>();
            for (int i = 0; i < days.Length - 1; i++)
            {
                parts.Add(Num(days[i]));
            }

            return "Days " + string.Join(", ", parts) + " and " + Num(days[days.Length - 1]);
        }

        /// <summary>
        /// The reaction to show: maliReactionNoLater when instalments exist but LaterCount == 0 and the
        /// field is set, else maliReactionLine. Callers fill it with extra { laterDays }.
        /// </summary>
        public static string ReactionFor(PlayerData d, ScenarioChoice c)
        {
            if (c == null)
            {
                return "";
            }

            if (c.instalmentCount > 0 && LaterCount(d, c) == 0 && !string.IsNullOrEmpty(c.maliReactionNoLater))
            {
                return c.maliReactionNoLater;
            }

            return c.maliReactionLine ?? "";
        }

        /// <summary>The extra tokens for a reaction: { laterDays } computed for today (call before Apply).</summary>
        public static Dictionary<string, string> ReactionExtra(PlayerData d, ScenarioChoice c)
        {
            return new Dictionary<string, string> { { "laterDays", LaterDaysWords(LaterDays(d, c)) } };
        }

        /// <summary>The day a follow-up would become active (currentDay + followUpAfterDays), or 0 when the choice
        /// has none or that day is after Day 7.</summary>
        public static int FollowUpDay(PlayerData d, ScenarioChoice c)
        {
            if (c == null || string.IsNullOrEmpty(c.followUpScenarioId))
            {
                return 0;
            }

            int day = (d != null ? d.currentDay : 1) + Math.Max(0, c.followUpAfterDays);
            return day <= ChapterConfig.ChapterLength ? day : 0;
        }

        // ------------------------------------------------------------------ choice-sheet columns (5.4.5, 4.8)

        /// <summary>True when any choice of the scenario moves savings (the sheet then adds a Savings column).</summary>
        public static bool HasSavingsColumn(ScenarioDefinition s)
        {
            if (s?.choices == null)
            {
                return false;
            }

            foreach (ScenarioChoice c in s.choices)
            {
                if (c != null && Math.Abs(c.savingsDelta) >= Tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>"−R50", "+R400", or "—" when 0.</summary>
        public static string MoneyColumn(float delta)
        {
            return Math.Abs(delta) < Tolerance ? EmptyValue : MoneyFormat.Signed(delta);
        }

        /// <summary>"−20", "+10", or "—" when 0.</summary>
        public static string EnergyColumn(float delta)
        {
            int whole = (int)Math.Round(delta, MidpointRounding.AwayFromZero);
            if (whole == 0)
            {
                return EmptyValue;
            }

            return (whole < 0 ? MoneyFormat.Minus : "+") + Num(Math.Abs(whole));
        }

        /// <summary>
        /// The Later column for today (4.8): 1 payment "R{amt}" + "on Day {due}" / "on payday"; n payments
        /// "R{amt} × {n}" + "Days 4, 6" / "Days 4–7" / "from Day {first}"; a follow-up (no instalments)
        /// followUpLaterText + "on Day {d}"; otherwise "—" and an empty small line.
        /// </summary>
        public static string LaterColumn(PlayerData d, ScenarioChoice c, out string smallLine)
        {
            smallLine = "";
            if (c == null)
            {
                return EmptyValue;
            }

            if (c.instalmentCount > 0)
            {
                int[] days = LaterDays(d, c);
                if (days.Length == 0)
                {
                    return EmptyValue;
                }

                string amount = MoneyFormat.Rand(c.instalmentAmount);
                if (days.Length == 1)
                {
                    smallLine = days[0] >= ChapterConfig.PaydayDay ? "on payday" : "on Day " + Num(days[0]);
                    return amount;
                }

                bool anyAfterChapter = days[days.Length - 1] > ChapterConfig.ChapterLength;
                if (anyAfterChapter)
                {
                    smallLine = "from Day " + Num(days[0]);
                }
                else if (days.Length >= 3 && Consecutive(days))
                {
                    smallLine = "Days " + Num(days[0]) + "–" + Num(days[days.Length - 1]);
                }
                else
                {
                    var parts = new string[days.Length];
                    for (int i = 0; i < days.Length; i++)
                    {
                        parts[i] = Num(days[i]);
                    }

                    smallLine = "Days " + string.Join(", ", parts);
                }

                return amount + " × " + Num(days.Length);
            }

            int followUpDay = FollowUpDay(d, c);
            if (followUpDay > 0 && !string.IsNullOrEmpty(c.followUpLaterText))
            {
                smallLine = "on Day " + Num(followUpDay);
                return c.followUpLaterText;
            }

            return EmptyValue;
        }

        /// <summary>
        /// Mali's chip row after a choice (4.8), computed for today (call before Apply): "Cash −R20",
        /// "Savings +R120", "Energy −45", "Later R34 × 4" / "Later R350", "Call back Day 6".
        /// </summary>
        public static string[] Chips(PlayerData d, ScenarioChoice c)
        {
            var chips = new List<string>();
            if (c == null)
            {
                return chips.ToArray();
            }

            if (Math.Abs(c.cashDelta) >= Tolerance)
            {
                chips.Add("Cash " + MoneyFormat.Signed(c.cashDelta));
            }

            if (Math.Abs(c.savingsDelta) >= Tolerance)
            {
                chips.Add("Savings " + MoneyFormat.Signed(c.savingsDelta));
            }

            if ((int)Math.Round(c.energyDelta, MidpointRounding.AwayFromZero) != 0)
            {
                chips.Add("Energy " + EnergyColumn(c.energyDelta));
            }

            int[] days = LaterDays(d, c);
            if (days.Length == 1)
            {
                chips.Add("Later " + MoneyFormat.Rand(c.instalmentAmount));
            }
            else if (days.Length > 1)
            {
                chips.Add("Later " + MoneyFormat.Rand(c.instalmentAmount) + " × " + Num(days.Length));
            }

            int followUpDay = FollowUpDay(d, c);
            if (c.instalmentCount <= 0 && followUpDay > 0 && !string.IsNullOrEmpty(c.followUpLaterText))
            {
                chips.Add(c.followUpLaterText + " Day " + Num(followUpDay));
            }

            return chips.ToArray();
        }

        /// <summary>A transfer choice: cash and savings both move and cancel out (drawn in the neutral colour).</summary>
        public static bool IsTransfer(ScenarioChoice c)
        {
            return c != null && Math.Abs(c.cashDelta) >= Tolerance && Math.Abs(c.savingsDelta) >= Tolerance
                   && Math.Abs(c.cashDelta + c.savingsDelta) < Tolerance;
        }

        // ------------------------------------------------------------------ behaviour signal (3.5, hidden)

        /// <summary>
        /// Nudges the rolling behaviour scores toward this choice's signal (alpha 0.25 / 0.20) rather than
        /// overwriting them, so the hidden profile reflects a pattern of decisions.
        /// </summary>
        static void ApplyBehaviourSignal(FinancialStats stats, ScenarioBehaviourTag tag)
        {
            float spendSignal;
            float saveSignal;

            switch (tag)
            {
                case ScenarioBehaviourTag.Discretionary:
                    spendSignal = 0.85f;
                    saveSignal = 0.15f;
                    break;
                case ScenarioBehaviourTag.Frugal:
                    spendSignal = 0.15f;
                    saveSignal = 0.8f;
                    break;
                case ScenarioBehaviourTag.Deferred:
                    spendSignal = 0.05f;
                    saveSignal = 0.6f;
                    break;
                default:
                    spendSignal = 0.5f;
                    saveSignal = 0.5f;
                    break;
            }

            const float spendAlpha = 0.25f;
            const float saveAlpha = 0.2f;
            stats.spendingBehaviourScore = Mathf.Lerp(stats.spendingBehaviourScore, spendSignal, spendAlpha);
            stats.savingBehaviourScore = Mathf.Lerp(stats.savingBehaviourScore, saveSignal, saveAlpha);
        }

        static string DescribeSpending(float score)
        {
            if (score > 0.6f) return "impulsive";
            if (score < 0.35f) return "careful";
            return "balanced";
        }

        static string DescribeSaving(float score)
        {
            if (score > 0.6f) return "consistent";
            if (score < 0.35f) return "rarely";
            return "sometimes";
        }

        // ------------------------------------------------------------------ helpers

        static int Interval(ScenarioChoice c) => Math.Max(1, c.instalmentIntervalDays);

        static bool Consecutive(int[] days)
        {
            for (int i = 1; i < days.Length; i++)
            {
                if (days[i] != days[i - 1] + 1)
                {
                    return false;
                }
            }

            return true;
        }

        static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

        static string[] Append(string[] array, string value)
        {
            if (array != null && Array.IndexOf(array, value) >= 0)
            {
                return array;
            }

            var list = new List<string>(array ?? Array.Empty<string>()) { value };
            return list.ToArray();
        }
    }
}
