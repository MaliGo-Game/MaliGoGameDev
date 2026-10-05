using System;

namespace MaliGo.Data
{
    /// <summary>
    /// One in-game day's money: where it started, every event, and where it ended.
    /// A record with day == 0 is "no record" (JsonUtility never leaves class fields null).
    /// </summary>
    [Serializable]
    public class DayRecord
    {
        /// <summary>0 = no record.</summary>
        public int day;

        public float startCash;
        public float startSavings;

        /// <summary>Valid when closed.</summary>
        public float endCash;

        public float endSavings;
        public MoneyEvent[] events = Array.Empty<MoneyEvent>();
        public bool closed;

        /// <summary>
        /// ObligationLedger.TotalArrears after the night's bills (set by DayCycle.EndDay), so the
        /// "still owed" history survives a restart.
        /// </summary>
        public float owedAtClose;

        public float StartTotal => startCash + startSavings;
        public float EndTotal => endCash + endSavings;

        /// <summary>A deep copy (the events are copied too).</summary>
        public DayRecord Clone()
        {
            MoneyEvent[] source = events ?? Array.Empty<MoneyEvent>();
            var copied = new MoneyEvent[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                copied[i] = source[i]?.Clone();
            }

            return new DayRecord
            {
                day = day,
                startCash = startCash,
                startSavings = startSavings,
                endCash = endCash,
                endSavings = endSavings,
                events = copied,
                closed = closed,
                owedAtClose = owedAtClose
            };
        }
    }
}
