using MaliGo.Data;

namespace MaliGo.Economy
{
    /// <summary>Moving money between cash and savings at the Bank. Whole amounts only, never partial.</summary>
    public static class BankRules
    {
        public static readonly float[] Amounts = { 50f, 100f, 200f };

        /// <summary>Cash -&gt; savings. Null (and no change) if the amount is not positive or cash &lt; amount.</summary>
        public static MoneyEvent MoveToSavings(PlayerData d, float amount)
        {
            if (d?.financialStats == null || amount <= MoneyRecorder.Tolerance
                || d.financialStats.cash < amount - MoneyRecorder.Tolerance)
            {
                return null;
            }

            return MoneyRecorder.Apply(d, -amount, amount, "Moved to savings", MoneyCategory.SavingsMove, "bank:to_savings");
        }

        /// <summary>Savings -&gt; cash. Null (and no change) if the amount is not positive or savings &lt; amount.</summary>
        public static MoneyEvent TakeOut(PlayerData d, float amount)
        {
            if (d?.financialStats == null || amount <= MoneyRecorder.Tolerance
                || d.financialStats.savings < amount - MoneyRecorder.Tolerance)
            {
                return null;
            }

            return MoneyRecorder.Apply(d, amount, -amount, "Taken out of savings", MoneyCategory.SavingsMove, "bank:from_savings");
        }
    }
}
