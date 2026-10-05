using System;
using System.Collections.Generic;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.Economy
{
    /// <summary>One bill's outcome on one night.</summary>
    public struct ObligationPayment
    {
        public string label;
        public float paid;
        public float stillOwed;
    }

    /// <summary>What settling the bills did on a given night.</summary>
    public class ObligationSettlement
    {
        public int day;
        public readonly List<ObligationPayment> payments = new List<ObligationPayment>();
        public float totalPaid;
        public float totalStillOwed;
        public float stressAdded;

        public bool AnyStillOwed => totalStillOwed > 0.005f;
    }

    /// <summary>
    /// One obligation's payment(s) as shown to the player: due tonight, next, promised or new.
    /// amount is the amount of ONE payment; count is how many payments the item stands for
    /// (Total = amount x count); day is the (first) due day.
    /// </summary>
    public struct DueItem
    {
        public string obligationId, label, shortLabel, kind;
        public float amount;
        public int day;
        public int count;

        public float Total => amount * count;
    }

    /// <summary>
    /// Pays whatever has fallen due by a given day, from cash only. Savings are never
    /// touched automatically: moving money out of savings is the player's decision, made
    /// at the Bank. Cash never goes negative - what can't be covered is carried as
    /// arrears, paid first on the next night, and adds stress for every night it remains.
    /// Every payment goes through MoneyRecorder.Apply. The read-only helpers below are the only
    /// source of every "due tonight", "next bill", "still owed" and "promised" text.
    /// </summary>
    public static class ObligationLedger
    {
        public static ObligationSettlement SettleDue(PlayerData data, int day)
        {
            var result = new ObligationSettlement { day = day };
            if (data?.obligations == null || data.financialStats == null)
            {
                return result;
            }

            FinancialStats stats = data.financialStats;
            var stillActive = new List<Obligation>();

            foreach (Obligation obligation in data.obligations)
            {
                if (obligation == null)
                {
                    continue;
                }

                // Count every payment that fell due up to today, so skipping straight past
                // a due day (or several) still charges each one.
                float newlyDue = 0f;
                while (obligation.paymentsRemaining != 0 && obligation.nextDueDay <= day)
                {
                    newlyDue += obligation.amount;
                    obligation.nextDueDay += Mathf.Max(1, obligation.intervalDays);
                    if (obligation.paymentsRemaining > 0)
                    {
                        obligation.paymentsRemaining--;
                    }
                }

                float owed = obligation.arrears + newlyDue;
                if (owed > 0.005f)
                {
                    float paid = Mathf.Min(Mathf.Max(0f, stats.cash), owed);
                    if (paid > 0.005f)
                    {
                        bool part = owed - paid > 0.005f;
                        string label = part ? obligation.label + " (part)" : obligation.label;
                        string category = string.IsNullOrEmpty(obligation.category) ? MoneyCategory.Bills : obligation.category;
                        MoneyEvent charged = MoneyRecorder.Apply(data, -paid, 0f, label, category, "bill:" + obligation.obligationId);
                        if (charged == null)
                        {
                            // The recorder refused (no open day): nothing was paid, all of it is carried.
                            paid = 0f;
                        }
                    }
                    else
                    {
                        paid = 0f;
                    }

                    obligation.arrears = owed - paid > 0.005f ? owed - paid : 0f;

                    result.payments.Add(new ObligationPayment
                    {
                        label = obligation.label,
                        paid = paid,
                        stillOwed = obligation.arrears
                    });
                    result.totalPaid += paid;
                    result.totalStillOwed += obligation.arrears;
                }

                if (!obligation.IsFinished)
                {
                    stillActive.Add(obligation);
                }
            }

            if (result.AnyStillOwed)
            {
                result.stressAdded = ObligationDefaults.MissedPaymentStress;
                stats.financialStress = Mathf.Clamp(stats.financialStress + result.stressAdded, 0f, 100f);
            }

            data.obligations = stillActive.ToArray();
            return result;
        }

        /// <summary>Total currently in arrears across all bills.</summary>
        public static float TotalArrears(PlayerData data)
        {
            float total = 0f;
            if (data?.obligations == null)
            {
                return total;
            }

            foreach (Obligation obligation in data.obligations)
            {
                if (obligation != null)
                {
                    total += obligation.arrears;
                }
            }

            return total;
        }

        /// <summary>The obligation with the most in arrears (the first on a tie); null if nothing is owed.</summary>
        public static Obligation LargestArrears(PlayerData d)
        {
            Obligation largest = null;
            if (d?.obligations == null)
            {
                return null;
            }

            foreach (Obligation obligation in d.obligations)
            {
                if (obligation == null || obligation.arrears <= 0.005f)
                {
                    continue;
                }

                if (largest == null || obligation.arrears > largest.arrears + 0.005f)
                {
                    largest = obligation;
                }
            }

            return largest;
        }

        /// <summary>
        /// What SettleDue(d, day) would newly charge on that night (arrears excluded), one item per
        /// obligation (count = payments falling due by that day, normally 1; day = the night), largest
        /// first (by Total; ties keep creation order). Changes nothing.
        /// </summary>
        public static List<DueItem> DueOnNight(PlayerData d, int day)
        {
            var items = new List<DueItem>();
            if (d?.obligations == null)
            {
                return items;
            }

            foreach (Obligation obligation in d.obligations)
            {
                if (obligation == null)
                {
                    continue;
                }

                int count = 0;
                int next = obligation.nextDueDay;
                int remaining = obligation.paymentsRemaining;
                while (remaining != 0 && next <= day)
                {
                    count++;
                    next += Math.Max(1, obligation.intervalDays);
                    if (remaining > 0)
                    {
                        remaining--;
                    }
                }

                if (count > 0 && obligation.amount > 0.005f)
                {
                    items.Add(Item(obligation, obligation.amount, day, count));
                }
            }

            StableSort(items, (a, b) => b.Total.CompareTo(a.Total));
            return items;
        }

        /// <summary>
        /// The soonest payment: nextDueDay &gt;= currentDay, payments left and nextDueDay &lt;= 8 (payday);
        /// ties go to the larger amount. count = 1. Null if there is none.
        /// </summary>
        public static DueItem? NextDue(PlayerData d)
        {
            if (d?.obligations == null)
            {
                return null;
            }

            Obligation best = null;
            foreach (Obligation obligation in d.obligations)
            {
                if (obligation == null || obligation.paymentsRemaining == 0 || obligation.amount <= 0.005f)
                {
                    continue;
                }

                if (obligation.nextDueDay < d.currentDay || obligation.nextDueDay > ChapterConfig.PaydayDay)
                {
                    continue;
                }

                if (best == null
                    || obligation.nextDueDay < best.nextDueDay
                    || (obligation.nextDueDay == best.nextDueDay && obligation.amount > best.amount + 0.005f))
                {
                    best = obligation;
                }
            }

            if (best == null)
            {
                return null;
            }

            return Item(best, best.amount, best.nextDueDay, 1);
        }

        /// <summary>
        /// Payments of non-recurring obligations (paymentsRemaining &gt; 0) that fall on or after Day 8
        /// (payday), one item per obligation (amount per payment x count), in creation order; day = the
        /// first such due day.
        /// </summary>
        public static List<DueItem> Promised(PlayerData d)
        {
            var items = new List<DueItem>();
            if (d?.obligations == null)
            {
                return items;
            }

            foreach (Obligation obligation in d.obligations)
            {
                if (obligation == null || obligation.paymentsRemaining <= 0 || obligation.amount <= 0.005f)
                {
                    continue;
                }

                int count = 0;
                int first = 0;
                int interval = Math.Max(1, obligation.intervalDays);
                for (int k = 0; k < obligation.paymentsRemaining; k++)
                {
                    int due = obligation.nextDueDay + k * interval;
                    if (due >= ChapterConfig.PaydayDay)
                    {
                        if (count == 0)
                        {
                            first = due;
                        }

                        count++;
                    }
                }

                if (count > 0)
                {
                    items.Add(Item(obligation, obligation.amount, first, count));
                }
            }

            return items;
        }

        /// <summary>
        /// Obligations created on this day (createdDay == day), in creation order: amount per payment,
        /// count = payments still to come (-1 for one that never ends), day = the first due day still
        /// to come.
        /// </summary>
        public static List<DueItem> NewPromises(PlayerData d, int day)
        {
            var items = new List<DueItem>();
            if (d?.obligations == null || day <= 0)
            {
                return items;
            }

            foreach (Obligation obligation in d.obligations)
            {
                if (obligation == null || obligation.createdDay != day || obligation.paymentsRemaining == 0)
                {
                    continue;
                }

                items.Add(Item(obligation, obligation.amount, obligation.nextDueDay, obligation.paymentsRemaining));
            }

            return items;
        }

        static DueItem Item(Obligation obligation, float amount, int day, int count)
        {
            return new DueItem
            {
                obligationId = obligation.obligationId ?? "",
                label = obligation.label ?? "",
                shortLabel = string.IsNullOrEmpty(obligation.shortLabel) ? (obligation.label ?? "") : obligation.shortLabel,
                kind = string.IsNullOrEmpty(obligation.kind) ? Obligation.KindBill : obligation.kind,
                amount = amount,
                day = day,
                count = count
            };
        }

        /// <summary>Insertion sort: stable, and the lists are tiny.</summary>
        static void StableSort(List<DueItem> items, Comparison<DueItem> comparison)
        {
            for (int i = 1; i < items.Count; i++)
            {
                DueItem current = items[i];
                int j = i - 1;
                while (j >= 0 && comparison(items[j], current) > 0)
                {
                    items[j + 1] = items[j];
                    j--;
                }

                items[j + 1] = current;
            }
        }
    }
}
