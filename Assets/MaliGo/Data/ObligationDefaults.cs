using System;
using System.Collections.Generic;

namespace MaliGo.Data
{
    /// <summary>
    /// The bills every player starts with: airtime R60 due Day 2 and rent R500 due Day 3, then every
    /// 7 days (outside the chapter). Due days are absolute, not "today + n" (design spec 2.3).
    /// </summary>
    public static class ObligationDefaults
    {
        public const string RentId = "rent";
        public const string AirtimeId = "airtime_data";

        public const float RentAmount = ChapterConfig.RentAmount;
        public const float AirtimeAmount = ChapterConfig.AirtimeAmount;
        public const int WeeklyIntervalDays = ChapterConfig.WeeklyIntervalDays;

        /// <summary>Retired: due days are absolute now (ChapterConfig.AirtimeDueDay / RentDueDay).</summary>
        public const int AirtimeFirstDueAfterDays = 1;

        /// <summary>Retired: due days are absolute now (ChapterConfig.AirtimeDueDay / RentDueDay).</summary>
        public const int RentFirstDueAfterDays = 2;

        /// <summary>Stress added on a night when anything is left unpaid.</summary>
        public const float MissedPaymentStress = 10f;

        public static void AddBaseObligations(PlayerData data)
        {
            if (data == null || data.baseObligationsAdded)
            {
                return;
            }

            AddObligation(data, new Obligation
            {
                obligationId = AirtimeId,
                label = "Airtime",
                shortLabel = "Airtime",
                amount = AirtimeAmount,
                intervalDays = WeeklyIntervalDays,
                nextDueDay = ChapterConfig.AirtimeDueDay,
                paymentsRemaining = -1,
                category = "Phone & data",
                kind = Obligation.KindBill,
                createdDay = 0
            });

            AddObligation(data, new Obligation
            {
                obligationId = RentId,
                label = "Rent",
                shortLabel = "Rent",
                amount = RentAmount,
                intervalDays = WeeklyIntervalDays,
                nextDueDay = ChapterConfig.RentDueDay,
                paymentsRemaining = -1,
                category = "Bills",
                kind = Obligation.KindBill,
                createdDay = 0
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
