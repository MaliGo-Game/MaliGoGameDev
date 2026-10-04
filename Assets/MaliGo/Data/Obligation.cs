using System;

namespace MaliGo.Data
{
    /// <summary>
    /// Money that leaves on its own schedule: rent, airtime, a buy-now-pay-later
    /// instalment. Settled at the end of each in-game day by ObligationLedger. What the
    /// player could not cover stays in arrears and is paid first next time, so a missed
    /// bill keeps following the player instead of disappearing.
    /// </summary>
    [Serializable]
    public class Obligation
    {
        // Values of `kind`.
        public const string KindBill = "bill";
        public const string KindInstalment = "instalment";
        public const string KindLoan = "loan";
        public const string KindCommitment = "commitment";

        /// <summary>A cost that comes back each night (day bundles, the daily fare); shown and charged like an instalment.</summary>
        public const string KindRepeat = "repeat";

        public string obligationId = "";
        public string label = "";

        /// <summary>At most 10 characters, for the HUD pill: "Rent", "Airtime", "Speaker", "Bra K", ...</summary>
        public string shortLabel = "";

        /// <summary>The MoneyCategory used when it is charged.</summary>
        public string category = "Bills";

        /// <summary>"bill" | "instalment" | "loan" | "commitment" | "repeat".</summary>
        public string kind = KindBill;

        /// <summary>Day the scenario choice created it (0 for the base bills); feeds "New promise".</summary>
        public int createdDay;

        /// <summary>Rand per payment.</summary>
        public float amount;

        public int intervalDays = 7;

        /// <summary>In-game day the next payment falls due (PlayerData.currentDay); charged on that day's night.</summary>
        public int nextDueDay = 1;

        /// <summary>Payments left; -1 means it never ends (rent).</summary>
        public int paymentsRemaining = -1;

        /// <summary>Rand that fell due but could not be paid.</summary>
        public float arrears;

        public bool IsFinished => paymentsRemaining == 0 && arrears <= 0.005f;
    }
}
