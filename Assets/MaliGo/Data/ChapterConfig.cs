namespace MaliGo.Data
{
    /// <summary>The fixed numbers of Chapter 1 (design spec section 0).</summary>
    public static class ChapterConfig
    {
        public const int ChapterLength = 7, PaydayDay = 8;
        public const float StartCash = 600f, StartSavings = 400f, StartStress = 25f;
        public const float DailyEnergy = 100f, ShiftPay = 150f, ShiftEnergyCost = 60f;
        public const float AirtimeAmount = 60f, RentAmount = 500f;
        public const int AirtimeDueDay = 2, RentDueDay = 3, WeeklyIntervalDays = 7;
        public const float StretchedStress = 60f;

        /// <summary>7 on Day 1, 1 on Day 7.</summary>
        public static int DaysToPayday(int day) => PaydayDay - day;
    }
}
