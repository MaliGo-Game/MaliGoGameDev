using System;
using System.Collections.Generic;
using System.Globalization;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Scenarios;

namespace MaliGo.Copy
{
    /// <summary>One row of the reveal's "What moved it" ledger (DESIGN_SPEC §5.4.8).</summary>
    public struct RevealRow
    {
        /// <summary>Player-facing label (loans carry " · R{repay} back Day {due}").</summary>
        public string label;

        /// <summary>The row's total delta (cash + savings); for a transfer, the amount moved into savings
        /// (positive) or out of savings (negative).</summary>
        public float amount;

        public MoneyEventKind kind;

        /// <summary>A loan received: arrow up, amount in the transfer colour, never the "in" colour.</summary>
        public bool isLoan;

        /// <summary>"+R150", "−R20", "R120 to savings", "R199 from savings".</summary>
        public string amountText;

        /// <summary>True for the "Other (n)" row.</summary>
        public bool isOther;
    }

    /// <summary>
    /// The end-of-day reveal's words (DESIGN_SPEC §4.3, §5.4.8, §4.8): Mali's line, Coming up, the ledger rows and
    /// the left column's lines. Pure, and built only from persisted data (<c>chapter.days</c> incl.
    /// <c>owedAtClose</c>, <c>today</c>, obligations, stats); <c>night.settlement</c> is never read, so a reveal
    /// rebuilt after the app was killed (<see cref="DayCycle.Rebuild"/>) reads exactly like a fresh one.
    /// </summary>
    public static class RevealLineBuilder
    {
        public const string StretchedPrefix = "You seem stretched, {name}. ";
        public const int MaxLedgerRows = 7;
        public const string EmptyLedger = "Nothing moved today.";
        public const string OnToPayday = "On to payday";

        /// <summary>B2's second sentence: arrears are charged at night, from cash only (ObligationLedger.SettleDue).</summary>
        public const string ArrearsNext = "It comes off tomorrow night from your cash, and the Bank can move savings into cash before then.";

        const float Tol = 0.005f;

        // ================================================================ Mali's line (§4.3)

        /// <summary>Mali's reveal line: [prefix when hidden stress ≥ 60] + A + " " + B, tokens filled.</summary>
        public static string Build(PlayerData data, NightResult night)
        {
            return Build(data, night, true);
        }

        /// <summary>The line with or without the stretched prefix (the view drops it first on overflow).</summary>
        public static string Build(PlayerData data, NightResult night, bool includePrefix)
        {
            if (data == null || night == null || night.record == null)
            {
                return "";
            }

            string a = PartA(data, night.record);
            string b = PartB(data, night.record);
            string line = a + " " + b;
            if (includePrefix && IsStretched(data))
            {
                line = MaliText.Fill(StretchedPrefix, data) + line;
            }

            return line;
        }

        /// <summary>True when hidden stress is at or above the stretched threshold.</summary>
        public static bool IsStretched(PlayerData data)
        {
            return data?.financialStats != null && data.financialStats.financialStress >= ChapterConfig.StretchedStress;
        }

        /// <summary>The non-transfer event with the largest |TotalDelta|; a tie goes to the later event. Null if none.</summary>
        public static MoneyEvent BiggestMover(DayRecord record)
        {
            MoneyEvent best = null;
            if (record?.events == null)
            {
                return null;
            }

            foreach (MoneyEvent e in record.events)
            {
                if (e == null || e.kind == MoneyEventKind.Transfer)
                {
                    continue;
                }

                if (best == null || Math.Abs(e.TotalDelta) >= Math.Abs(best.TotalDelta) - Tol)
                {
                    best = e;
                }
            }

            return best;
        }

        static string PartA(PlayerData data, DayRecord record)
        {
            MoneyEvent[] events = record.events ?? Array.Empty<MoneyEvent>();
            int count = 0;
            foreach (MoneyEvent e in events)
            {
                if (e != null)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return "Nothing moved your money today. You ended where you started: R" + MoneyFormat.Digits(record.EndTotal) + ".";
            }

            MoneyEvent mover = BiggestMover(record);
            if (mover == null)
            {
                return "Money moved between cash and savings today, and your total stayed at R"
                       + MoneyFormat.Digits(record.EndTotal) + ".";
            }

            string amt = MoneyFormat.Digits(mover.TotalDelta);
            string label = CleanLabel(mover.label);
            if (mover.kind == MoneyEventKind.In)
            {
                if (mover.category == MoneyCategory.Work)
                {
                    return "The shift brought in the most today: R" + amt + ".";
                }

                if (mover.category == MoneyCategory.Loan)
                {
                    LoanTerms(data, mover, record.day, out float repay, out int due);
                    return "The R" + amt + " from Bra K was the biggest change today. R" + MoneyFormat.Digits(repay)
                           + " goes back on Day " + due.ToString(CultureInfo.InvariantCulture) + ".";
                }

                return label + ": R" + amt + " in. That was the biggest change today.";
            }

            return label + " took the most today: R" + amt + ".";
        }

        static string PartB(PlayerData data, DayRecord record)
        {
            int endedDay = record.day;
            if (endedDay >= ChapterConfig.ChapterLength)
            {
                return "Tomorrow is payday.";
            }

            Obligation owed = ObligationLedger.LargestArrears(data);
            if (owed != null)
            {
                return "R" + MoneyFormat.Digits(owed.arrears) + " is still owed for " + Label(owed.label, owed.shortLabel)
                       + ". " + ArrearsNext;
            }

            List<DueItem> tomorrow = ObligationLedger.DueOnNight(data, endedDay + 1);
            if (tomorrow.Count > 0)
            {
                DueItem item = tomorrow[0];
                float cash = data.financialStats != null ? data.financialStats.cash : record.endCash;
                return "Tomorrow night, " + Label(item.shortLabel, item.label) + " takes R" + MoneyFormat.Digits(item.Total)
                       + ". You've got R" + MoneyFormat.Digits(cash) + " in cash.";
            }

            if (endedDay + 1 == ChapterConfig.ChapterLength)
            {
                return "Tomorrow is the last day before payday.";
            }

            bool savingsMoved = false;
            foreach (MoneyEvent e in record.events ?? Array.Empty<MoneyEvent>())
            {
                if (e != null && Math.Abs(e.savingsDelta) > Tol)
                {
                    savingsMoved = true;
                    break;
                }
            }

            if (savingsMoved)
            {
                return MaliText.Fill("Savings is at R{savings} of your R{goalTarget} {goalName}.", data);
            }

            return "Payday is " + MaliText.PaydayWhen(endedDay + 1) + ".";
        }

        /// <summary>The plain default (reveal flag off): "Day {d} is done. You started with R{start} and ended with R{end}."</summary>
        public static string PlainLine(DayRecord record)
        {
            if (record == null)
            {
                return "";
            }

            return "Day " + record.day.ToString(CultureInfo.InvariantCulture) + " is done. You started with R"
                   + MoneyFormat.Digits(record.StartTotal) + " and ended with R" + MoneyFormat.Digits(record.EndTotal) + ".";
        }

        // ================================================================ Coming up (§5.4.8)

        /// <summary>
        /// Up to two lines, the first two that exist of: tomorrow night's first bill ("Day {n} night: {shortLabel}
        /// R{amt}"), "Tomorrow: your aunt calls back." for a follow-up due tomorrow, and the teaser. After Day 7
        /// only "Tomorrow is payday." (payday commitments are never charged in the beta).
        /// </summary>
        public static string[] ComingUp(PlayerData data, int endedDay)
        {
            var lines = new List<string>(2);
            if (endedDay >= ChapterConfig.ChapterLength)
            {
                lines.Add(ChapterSchedule.TeaserForNight(endedDay, Focus(data)) ?? ChapterSchedule.PaydayTeaserText);
                return lines.ToArray();
            }

            int next = endedDay + 1;
            List<DueItem> due = ObligationLedger.DueOnNight(data, next);
            if (due.Count > 0)
            {
                DueItem item = due[0];
                lines.Add("Day " + next.ToString(CultureInfo.InvariantCulture) + " night: " + Label(item.shortLabel, item.label)
                          + " " + MoneyFormat.Rand(item.Total));
            }

            string followUp = FollowUpDue(data, next);
            if (followUp != null)
            {
                string teaser = ChapterSchedule.FollowUpTeaser(followUp);
                if (!string.IsNullOrEmpty(teaser))
                {
                    lines.Add(teaser);
                }
            }

            if (lines.Count < 2)
            {
                string teaser = ChapterSchedule.TeaserForNight(endedDay, Focus(data));
                if (!string.IsNullOrEmpty(teaser))
                {
                    lines.Add(teaser);
                }
            }

            if (lines.Count > 2)
            {
                lines.RemoveRange(2, lines.Count - 2);
            }

            return lines.ToArray();
        }

        /// <summary>The id of a follow-up scenario due on <paramref name="day"/> and not yet played, or null.</summary>
        static string FollowUpDue(PlayerData data, int day)
        {
            if (data?.followUps == null)
            {
                return null;
            }

            foreach (string key in data.followUps)
            {
                if (ChapterSchedule.TryParseFollowUp(key, out string id, out int when) && when == day && !data.IsScenarioCompleted(id))
                {
                    return id;
                }
            }

            return null;
        }

        // ================================================================ ledger (§5.4.8)

        /// <summary>
        /// The "What moved it" rows, chronological. All <c>bank:to_savings</c> events form one "Moved to savings"
        /// row at the first one's place (likewise <c>bank:from_savings</c>, "Taken out of savings"); loans get
        /// " · R{repay} back Day {due}". More than <paramref name="maxRows"/> rows: the first maxRows − 1, then
        /// "Other ({n})" with the rest summed. Empty when nothing moved.
        /// </summary>
        public static List<RevealRow> LedgerRows(PlayerData data, DayRecord record, int maxRows = MaxLedgerRows)
        {
            var rows = new List<RevealRow>();
            if (record?.events == null)
            {
                return rows;
            }

            int toSavingsRow = -1;
            int fromSavingsRow = -1;
            foreach (MoneyEvent e in record.events)
            {
                if (e == null)
                {
                    continue;
                }

                if (e.sourceId == "bank:to_savings" || e.sourceId == "bank:from_savings")
                {
                    bool into = e.sourceId == "bank:to_savings";
                    int index = into ? toSavingsRow : fromSavingsRow;
                    if (index < 0)
                    {
                        rows.Add(new RevealRow
                        {
                            label = into ? "Moved to savings" : "Taken out of savings",
                            kind = MoneyEventKind.Transfer
                        });
                        index = rows.Count - 1;
                        if (into)
                        {
                            toSavingsRow = index;
                        }
                        else
                        {
                            fromSavingsRow = index;
                        }
                    }

                    RevealRow grouped = rows[index];
                    grouped.amount += e.savingsDelta;
                    rows[index] = grouped;
                    continue;
                }

                var row = new RevealRow { label = e.label ?? "", kind = e.kind, amount = e.TotalDelta };
                if (e.kind == MoneyEventKind.Transfer)
                {
                    row.amount = e.savingsDelta;
                }
                else if (e.kind == MoneyEventKind.In && e.category == MoneyCategory.Loan)
                {
                    row.isLoan = true;
                    LoanTerms(data, e, record.day, out float repay, out int due);
                    row.label = row.label + " · R" + MoneyFormat.Digits(repay) + " back Day " + due.ToString(CultureInfo.InvariantCulture);
                }

                rows.Add(row);
            }

            if (maxRows > 0 && rows.Count > maxRows)
            {
                int keep = maxRows - 1;
                float rest = 0f;
                int restCount = rows.Count - keep;
                for (int i = keep; i < rows.Count; i++)
                {
                    rest += rows[i].kind == MoneyEventKind.Transfer ? 0f : rows[i].amount;
                }

                rows.RemoveRange(keep, rows.Count - keep);
                rows.Add(new RevealRow
                {
                    label = "Other (" + restCount.ToString(CultureInfo.InvariantCulture) + ")",
                    amount = rest,
                    kind = rest > Tol ? MoneyEventKind.In : rest < -Tol ? MoneyEventKind.Out : MoneyEventKind.Transfer,
                    isOther = true
                });
            }

            for (int i = 0; i < rows.Count; i++)
            {
                RevealRow r = rows[i];
                r.amountText = AmountText(r);
                rows[i] = r;
            }

            return rows;
        }

        static string AmountText(RevealRow row)
        {
            if (row.kind == MoneyEventKind.Transfer && !row.isOther)
            {
                return row.amount >= 0f
                    ? MoneyFormat.Rand(row.amount) + " to savings"
                    : MoneyFormat.Rand(-row.amount) + " from savings";
            }

            return MoneyFormat.Signed(row.amount);
        }

        // ================================================================ left column (§5.4.8, §4.8)

        public static string StartedLine(DayRecord record)
        {
            return "You started Day " + (record != null ? record.day : 0).ToString(CultureInfo.InvariantCulture) + " with";
        }

        public const string EndedLine = "You ended it with";
        public const string TotalCaption = "cash + savings";
        public const string LedgerTitle = "What moved it";
        public const string ComingUpTitle = "Coming up";

        /// <summary>"+R210 today" / "−R85 today" / "R0 today". A neutral figure. On a day with a loan the pill says so
        /// ("+R166 today · R400 borrowed"), so borrowed money never reads as a good day (Revision 4).</summary>
        public static string DeltaPill(DayRecord record)
        {
            string pill = MoneyFormat.Signed(record != null ? record.EndTotal - record.StartTotal : 0f) + " today";
            float borrowed = Borrowed(record);
            return borrowed > Tol ? pill + " · " + MoneyFormat.Rand(borrowed) + " borrowed" : pill;
        }

        /// <summary>Money received from loans on the day (In events in the Loan category).</summary>
        public static float Borrowed(DayRecord record)
        {
            float total = 0f;
            foreach (MoneyEvent e in record?.events ?? Array.Empty<MoneyEvent>())
            {
                if (e != null && e.kind == MoneyEventKind.In && e.category == MoneyCategory.Loan)
                {
                    total += e.TotalDelta;
                }
            }

            return total;
        }

        /// <summary>"Cash R{cash} · Savings R{savings}" at the day's close.</summary>
        public static string SplitLine(DayRecord record)
        {
            return record == null
                ? ""
                : "Cash " + MoneyFormat.Rand(record.endCash) + " · Savings " + MoneyFormat.Rand(record.endSavings);
        }

        /// <summary>
        /// "Still owed: R{owed}" plus " (R{owedBefore} this morning)" when the morning figure differs; null when
        /// nothing is owed. owed = the day's <c>owedAtClose</c>; owedBefore = the previous closed day's (0 on Day 1).
        /// </summary>
        public static string StillOwedLine(PlayerData data, DayRecord record)
        {
            if (record == null || record.owedAtClose <= Tol)
            {
                return null;
            }

            float before = OwedBefore(data, record.day);
            string line = "Still owed: " + MoneyFormat.Rand(record.owedAtClose);
            if (MoneyFormat.Digits(before) != MoneyFormat.Digits(record.owedAtClose))
            {
                line += " (" + MoneyFormat.Rand(before) + " this morning)";
            }

            return line;
        }

        static float OwedBefore(PlayerData data, int day)
        {
            DayRecord[] days = data?.chapter?.days;
            if (days == null)
            {
                return 0f;
            }

            for (int i = days.Length - 1; i >= 0; i--)
            {
                if (days[i] != null && days[i].day == day - 1)
                {
                    return days[i].owedAtClose;
                }
            }

            return 0f;
        }

        /// <summary>
        /// The day's new promises, at most two lines: "New promise: {shortLabel} R{amt} on Day {due}" / "… on payday" /
        /// "New promise: {shortLabel} R{amt} × {n}, Days {d1}, {d2}" ("Days {d1}–{dn}" for three or more in a row;
        /// "from Day {first}" when any falls after Day 7). With three or more, the second line reads
        /// "+{n} more new promises".
        /// </summary>
        public static string[] NewPromiseLines(PlayerData data, int day)
        {
            List<DueItem> items = ObligationLedger.NewPromises(data, day);
            var lines = new List<string>();
            if (items.Count == 0)
            {
                return lines.ToArray();
            }

            int show = items.Count <= 2 ? items.Count : 1;
            for (int i = 0; i < show; i++)
            {
                lines.Add(NewPromiseLine(data, items[i]));
            }

            if (items.Count > 2)
            {
                int more = items.Count - 1;
                lines.Add("+" + more.ToString(CultureInfo.InvariantCulture) + (more == 1 ? " more new promise" : " more new promises"));
            }

            return lines.ToArray();
        }

        /// <summary>One "New promise: …" line.</summary>
        public static string NewPromiseLine(PlayerData data, DueItem item)
        {
            string head = "New promise: " + Label(item.shortLabel, item.label) + " " + MoneyFormat.Rand(item.amount);
            if (item.count == 1)
            {
                return head + (item.day >= ChapterConfig.PaydayDay
                    ? " on payday"
                    : " on Day " + item.day.ToString(CultureInfo.InvariantCulture));
            }

            int interval = IntervalOf(data, item.obligationId);
            if (item.count < 0)
            {
                return head + " from Day " + item.day.ToString(CultureInfo.InvariantCulture);
            }

            var days = new int[item.count];
            for (int k = 0; k < item.count; k++)
            {
                days[k] = item.day + k * interval;
            }

            head += " × " + item.count.ToString(CultureInfo.InvariantCulture) + ", ";
            return head + DaysWords(days);
        }

        /// <summary>"Days 4, 6" / "Days 4–7" (three or more in a row) / "from Day {first}" (any after Day 7) / "Day 4".</summary>
        public static string DaysWords(int[] days)
        {
            if (days == null || days.Length == 0)
            {
                return "";
            }

            if (days[days.Length - 1] >= ChapterConfig.PaydayDay)
            {
                return "from Day " + days[0].ToString(CultureInfo.InvariantCulture);
            }

            if (days.Length == 1)
            {
                return "Day " + days[0].ToString(CultureInfo.InvariantCulture);
            }

            bool consecutive = true;
            for (int i = 1; i < days.Length; i++)
            {
                if (days[i] != days[i - 1] + 1)
                {
                    consecutive = false;
                    break;
                }
            }

            if (consecutive && days.Length >= 3)
            {
                return "Days " + days[0].ToString(CultureInfo.InvariantCulture) + "–" + days[days.Length - 1].ToString(CultureInfo.InvariantCulture);
            }

            var parts = new string[days.Length];
            for (int i = 0; i < days.Length; i++)
            {
                parts[i] = days[i].ToString(CultureInfo.InvariantCulture);
            }

            return "Days " + string.Join(", ", parts);
        }

        /// <summary>"On to Day {n}" / "On to payday" (Day 7).</summary>
        public static string ButtonLabel(DayRecord record)
        {
            int day = record != null ? record.day : 0;
            return day >= ChapterConfig.ChapterLength
                ? OnToPayday
                : "On to Day " + (day + 1).ToString(CultureInfo.InvariantCulture);
        }

        // ================================================================ helpers

        /// <summary>The event label without " (part)".</summary>
        public static string CleanLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return "";
            }

            const string part = " (part)";
            return label.EndsWith(part, StringComparison.Ordinal) ? label.Substring(0, label.Length - part.Length) : label;
        }

        /// <summary>What a loan costs back: from the matching obligation, or the choice's instalment fields if it is gone.</summary>
        public static void LoanTerms(PlayerData data, MoneyEvent loan, int day, out float repay, out int due)
        {
            repay = 0f;
            due = day + 2;
            if (loan == null)
            {
                return;
            }

            string scenarioId = null;
            string choiceId = null;
            string source = loan.sourceId ?? "";
            const string prefix = "scenario:";
            if (source.StartsWith(prefix, StringComparison.Ordinal))
            {
                string rest = source.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                if (slash > 0)
                {
                    scenarioId = rest.Substring(0, slash);
                    choiceId = rest.Substring(slash + 1);
                }
            }

            if (scenarioId == null)
            {
                return;
            }

            string obligationId = scenarioId + "_" + choiceId;
            if (data?.obligations != null)
            {
                foreach (Obligation o in data.obligations)
                {
                    if (o != null && o.obligationId == obligationId)
                    {
                        int count = o.paymentsRemaining > 0 ? o.paymentsRemaining : 1;
                        repay = o.amount * count + o.arrears;
                        due = o.paymentsRemaining > 0 ? o.nextDueDay : due;
                        return;
                    }
                }
            }

            ScenarioDefinition s = ScenarioLibrary.Get(scenarioId, Focus(data), data?.spendingProfile?.travel);
            if (s?.choices == null)
            {
                return;
            }

            foreach (ScenarioChoice c in s.choices)
            {
                if (c != null && c.choiceId == choiceId)
                {
                    repay = c.instalmentAmount * Math.Max(1, c.instalmentCount);
                    due = c.instalmentFirstDueDay > 0 ? c.instalmentFirstDueDay : day + Math.Max(1, c.instalmentIntervalDays);
                    return;
                }
            }
        }

        static int IntervalOf(PlayerData data, string obligationId)
        {
            if (data?.obligations != null)
            {
                foreach (Obligation o in data.obligations)
                {
                    if (o != null && o.obligationId == obligationId)
                    {
                        return Math.Max(1, o.intervalDays);
                    }
                }
            }

            return 1;
        }

        static string Label(string preferred, string fallback)
        {
            return string.IsNullOrEmpty(preferred) ? (fallback ?? "") : preferred;
        }

        static string Focus(PlayerData data)
        {
            return data?.spendingProfile != null ? data.spendingProfile.focus : null;
        }
    }
}
