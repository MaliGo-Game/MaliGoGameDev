using System;
using System.Globalization;

namespace MaliGo.BankFeed
{
    // The slice of the Open Banking Account Information standard the bank feed uses (docs/BANK_FEED.md):
    // consent -> accounts -> transactions. Field names follow the standard (PascalCase) so a provider's JSON maps
    // one to one. These objects live only in memory while a summary is being made; they are never saved, logged
    // or sent anywhere.

    /// <summary>Account Information permissions the bank feed asks for (and nothing more).</summary>
    public static class ObPermission
    {
        public const string ReadAccountsBasic = "ReadAccountsBasic";
        public const string ReadTransactionsBasic = "ReadTransactionsBasic";
        public const string ReadTransactionsDetail = "ReadTransactionsDetail";
        public const string ReadTransactionsCredits = "ReadTransactionsCredits";
        public const string ReadTransactionsDebits = "ReadTransactionsDebits";

        /// <summary>The permissions the consent screen describes, in order.</summary>
        public static readonly string[] Requested =
        {
            ReadAccountsBasic, ReadTransactionsBasic, ReadTransactionsDetail, ReadTransactionsCredits,
            ReadTransactionsDebits
        };
    }

    /// <summary>The endpoints a provider serves (for docs and adapters; there is no network code in the game).</summary>
    public static class ObEndpoint
    {
        public const string Accounts = "GET /accounts";
        public const string AccountTransactions = "GET /accounts/{AccountId}/transactions";
        public const string Transactions = "GET /transactions";
    }

    public static class ObCreditDebit
    {
        public const string Credit = "Credit", Debit = "Debit";
    }

    public static class ObStatus
    {
        public const string Booked = "Booked", Pending = "Pending";
    }

    [Serializable]
    public class ObAccount
    {
        public string AccountId = "";
        public string Currency = "ZAR";
        public string AccountType = "";
        public string AccountSubType = "";
        public string Nickname = "";
    }

    [Serializable]
    public class ObAmount
    {
        /// <summary>A decimal string, as in the standard: "45.00".</summary>
        public string Amount = "0.00";
        public string Currency = "ZAR";

        /// <summary>The amount as a decimal (always positive in the standard; the sign is CreditDebitIndicator); 0 if unreadable.</summary>
        public decimal Value
        {
            get
            {
                return decimal.TryParse(Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v)
                    ? Math.Abs(v)
                    : 0m;
            }
        }
    }

    [Serializable]
    public class ObMerchantDetails
    {
        public string MerchantName = "";

        /// <summary>ISO 18245 merchant category code, four digits ("5814"); "" when the provider has none.</summary>
        public string MerchantCategoryCode = "";
    }

    [Serializable]
    public class ObBankTransactionCode
    {
        public string Code = "";
        public string SubCode = "";
    }

    [Serializable]
    public class ObTransaction
    {
        public string TransactionId = "";
        public string AccountId = "";
        public ObAmount Amount = new ObAmount();

        /// <summary>"Credit" or "Debit".</summary>
        public string CreditDebitIndicator = ObCreditDebit.Debit;

        /// <summary>"Booked" or "Pending".</summary>
        public string Status = ObStatus.Booked;

        /// <summary>ISO 8601, e.g. "2026-09-14T12:31:00+02:00".</summary>
        public string BookingDateTime = "";

        public string TransactionInformation = "";
        public ObMerchantDetails MerchantDetails = new ObMerchantDetails();

        /// <summary>Optional; null when absent.</summary>
        public ObBankTransactionCode BankTransactionCode;

        public bool IsDebit => string.Equals(CreditDebitIndicator, ObCreditDebit.Debit, StringComparison.OrdinalIgnoreCase);

        public bool IsCredit => string.Equals(CreditDebitIndicator, ObCreditDebit.Credit, StringComparison.OrdinalIgnoreCase);

        public bool IsBooked => string.Equals(Status, ObStatus.Booked, StringComparison.OrdinalIgnoreCase);

        /// <summary>The booking time, or false when it cannot be read.</summary>
        public bool TryGetBookingTime(out DateTimeOffset time)
        {
            return DateTimeOffset.TryParse(BookingDateTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out time);
        }
    }

    /// <summary>What a consent request asks for: the permissions and the transaction window.</summary>
    public class ObConsentRequest
    {
        public string[] Permissions = ObPermission.Requested;
        public DateTimeOffset TransactionFromDateTime;
        public DateTimeOffset TransactionToDateTime;
    }
}
