using System;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// One selectable option within a ScenarioDefinition. Consequence fields are
    /// deltas applied to the player's FinancialStats when the choice is made.
    /// </summary>
    [Serializable]
    public class ScenarioChoice
    {
        public string choiceId = "";
        public string label = "";

        /// <summary>Retired (design spec D14): always empty. The choice card builds its columns from the deltas.</summary>
        public string description = "";

        /// <summary>Player-facing ledger text for the money event (at most 28 characters). Empty when no money moves.</summary>
        public string ledgerLabel = "";

        public float cashDelta;
        public float savingsDelta;
        public float financialStressDelta;
        public float energyDelta;
        public float financialXpDelta;

        public ScenarioBehaviourTag behaviourTag = ScenarioBehaviourTag.Neutral;

        /// <summary>
        /// Later payments this choice commits the player to (pay-later, day bundles, the daily fare,
        /// a loan repayment, a payday commitment). Each is added as an Obligation and charged on the
        /// night it falls due. 0 = the choice creates no payments.
        /// </summary>
        public int instalmentCount;
        public float instalmentAmount;
        public int instalmentIntervalDays = 2;
        public string instalmentLabel = "";

        /// <summary>Absolute day of the first payment; 0 = today + instalmentIntervalDays.</summary>
        public int instalmentFirstDueDay;

        /// <summary>HUD pill label for the obligation (at most 10 characters), e.g. "Speaker".</summary>
        public string instalmentShortLabel = "";

        /// <summary>Money category used when a payment is charged (design spec 2.2).</summary>
        public string instalmentCategory = "";

        /// <summary>Obligation kind: "instalment" | "loan" | "commitment" | "repeat".</summary>
        public string instalmentKind = "";

        /// <summary>0 = none; otherwise payments that would fall after this day are dropped.</summary>
        public int instalmentLastDueDay;

        /// <summary>Mali's line after this choice is made. Tokens such as {name} are filled by MaliText.Fill.</summary>
        public string maliReactionLine = "";

        /// <summary>Used instead of maliReactionLine when instalmentLastDueDay leaves no payment.</summary>
        public string maliReactionNoLater = "";

        /// <summary>A follow-up scenario this choice sets off (e.g. "family_callback"); empty = none.</summary>
        public string followUpScenarioId = "";

        /// <summary>Days after the choice when the follow-up becomes active (only if that day is within the chapter).</summary>
        public int followUpAfterDays;

        /// <summary>The Later column's first line for a follow-up, e.g. "Call back".</summary>
        public string followUpLaterText = "";
    }
}
