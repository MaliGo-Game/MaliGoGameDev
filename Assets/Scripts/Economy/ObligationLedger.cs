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
    /// Pays whatever has fallen due by a given day, from cash only. Savings are never
    /// touched automatically: moving money out of savings is the player's decision, made
    /// at the Bank. Cash never goes negative - what can't be covered is carried as
    /// arrears, paid first on the next night, and adds stress for every night it remains.
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
                    stats.cash -= paid;
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
    }
}
