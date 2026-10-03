using System;
using System.Collections.Generic;

namespace MaliGo.Data
{
    /// <summary>
    /// The bills every player starts with, and the numbers to tune them. A first pass,
    /// sized against R150 a shift at Work: a week of shifts (R1050) covers rent and
    /// airtime (R560) with room left for the day-to-day choices, but not for every treat.
    /// </summary>
    public static class ObligationDefaults
    {
        public const string RentId = "rent";
        public const string AirtimeId = "airtime_data";

        public const float RentAmount = 500f;
        public const float AirtimeAmount = 60f;
        public const int WeeklyIntervalDays = 7;

        /// <summary>
        /// Days after the current day that each bill first falls due. Staggered and early
        /// so a short playtest meets a bill instead of finishing before the first one.
        /// </summary>
        public const int AirtimeFirstDueAfterDays = 1;
        public const int RentFirstDueAfterDays = 2;

        /// <summary>Stress added on a night when anything is left unpaid.</summary>
        public const float MissedPaymentStress = 10f;

        public static void AddBaseObligations(PlayerData data)
        {
            if (data == null || data.baseObligationsAdded)
            {
                return;
            }

            int today = Math.Max(1, data.currentDay);

            AddObligation(data, new Obligation
            {
                obligationId = AirtimeId,
                label = "Airtime & data",
                amount = AirtimeAmount,
                intervalDays = WeeklyIntervalDays,
                nextDueDay = today + AirtimeFirstDueAfterDays,
                paymentsRemaining = -1
            });

            AddObligation(data, new Obligation
            {
                obligationId = RentId,
                label = "Rent",
                amount = RentAmount,
                intervalDays = WeeklyIntervalDays,
                nextDueDay = today + RentFirstDueAfterDays,
                paymentsRemaining = -1
            });

            data.baseObligationsAdded = true;
        }

        public static void AddObligation(PlayerData data, Obligation obligation)
        {
            if (data == null || obligation == null)
            {
                return;
            }

            var list = new List<Obligation>(data.obligations ?? Array.Empty<Obligation>()) { obligation };
            data.obligations = list.ToArray();
        }
    }
}
