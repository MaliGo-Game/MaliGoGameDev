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
        public string obligationId = "";
        public string label = "";

        /// <summary>Rand per payment.</summary>
        public float amount;

        public int intervalDays = 7;

        /// <summary>In-game day the next payment falls due (PlayerData.currentDay).</summary>
        public int nextDueDay = 1;

        /// <summary>Payments left; -1 means it never ends (rent).</summary>
        public int paymentsRemaining = -1;

        /// <summary>Rand that fell due but could not be paid.</summary>
        public float arrears;

        public bool IsFinished => paymentsRemaining == 0 && arrears <= 0.005f;
    }
}
