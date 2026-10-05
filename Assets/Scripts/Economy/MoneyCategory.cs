namespace MaliGo.Economy
{
    /// <summary>The money categories of every MoneyEvent and Obligation (design spec 2.2).</summary>
    public static class MoneyCategory
    {
        public const string Work = "Work";
        public const string Food = "Food";
        public const string Transport = "Transport";
        public const string PhoneData = "Phone & data";
        public const string Shopping = "Shopping";
        public const string Family = "Family";
        public const string Friends = "Friends";
        public const string Home = "Home";
        public const string Bills = "Bills";
        public const string PayLater = "Pay-later";
        public const string Loan = "Loan";
        public const string ExtraMoney = "Extra money";
        public const string SavingsMove = "Savings move";

        public static readonly string[] All =
        {
            Work, Food, Transport, PhoneData, Shopping, Family, Friends, Home, Bills, PayLater, Loan, ExtraMoney, SavingsMove
        };

        public static bool IsKnown(string category)
        {
            return category != null && System.Array.IndexOf(All, category) >= 0;
        }
    }
}
