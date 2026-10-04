using System;

namespace MaliGo.Data
{
    /// <summary>Derived by MoneyRecorder from the deltas, never passed in.</summary>
    public enum MoneyEventKind
    {
        In = 0,
        Out = 1,
        Transfer = 2
    }

    /// <summary>
    /// One change to the player's money (cash and/or savings), as recorded by
    /// MaliGo.Economy.MoneyRecorder.Apply - the only writer of cash and savings.
    /// </summary>
    [Serializable]
    public class MoneyEvent
    {
        /// <summary>Player-facing ledger text, at most 28 characters.</summary>
        public string label = "";

        /// <summary>A MaliGo.Economy.MoneyCategory constant.</summary>
        public string category = "";

        /// <summary>The delta actually applied; never clamped afterwards.</summary>
        public float cashDelta;

        public float savingsDelta;

        /// <summary>Set by MoneyRecorder.</summary>
        public MoneyEventKind kind;

        /// <summary>
        /// "scenario:&lt;scenarioId&gt;/&lt;choiceId&gt;", "work:&lt;day&gt;", "bank:to_savings",
        /// "bank:from_savings" or "bill:&lt;obligationId&gt;".
        /// </summary>
        public string sourceId = "";

        public float TotalDelta => cashDelta + savingsDelta;

        public MoneyEvent Clone()
        {
            return new MoneyEvent
            {
                label = label,
                category = category,
                cashDelta = cashDelta,
                savingsDelta = savingsDelta,
                kind = kind,
                sourceId = sourceId
            };
        }
    }
}
