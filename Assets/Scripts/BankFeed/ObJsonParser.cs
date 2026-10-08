using System;
using System.Collections.Generic;

namespace MaliGo.BankFeed
{
    /// <summary>
    /// Reads the Open Banking Account Information response envelope: { "Data": { "Transaction": [ ... ] }, "Links",
    /// "Meta" } for GET /accounts/{AccountId}/transactions and GET /transactions, and { "Data": { "Account": [ ... ] } }
    /// for GET /accounts. Unknown fields are ignored; missing ones keep their defaults. Amount.Amount is read as a
    /// decimal string (a bare JSON number is accepted too, as its text). A malformed document gives false and an empty
    /// list; a single malformed entry is skipped. Never logs content (it is the player's banking data).
    /// </summary>
    public static class ObJsonParser
    {
        public static bool TryParseTransactions(string json, out List<ObTransaction> transactions)
        {
            transactions = new List<ObTransaction>();
            if (!TryGetDataArray(json, "Transaction", out List<object> items))
            {
                return false;
            }

            foreach (object item in items)
            {
                if (item is Dictionary<string, object> o)
                {
                    transactions.Add(ReadTransaction(o));
                }
            }

            return true;
        }

        public static bool TryParseAccounts(string json, out List<ObAccount> accounts)
        {
            accounts = new List<ObAccount>();
            if (!TryGetDataArray(json, "Account", out List<object> items))
            {
                return false;
            }

            foreach (object item in items)
            {
                if (item is Dictionary<string, object> o)
                {
                    accounts.Add(new ObAccount
                    {
                        AccountId = Str(o, "AccountId"),
                        Currency = Str(o, "Currency", "ZAR"),
                        AccountType = Str(o, "AccountType"),
                        AccountSubType = Str(o, "AccountSubType"),
                        Nickname = Str(o, "Nickname")
                    });
                }
            }

            return true;
        }

        static bool TryGetDataArray(string json, string arrayName, out List<object> items)
        {
            items = null;
            if (!MiniJson.TryParse(json, out object root) || !(root is Dictionary<string, object> envelope))
            {
                return false;
            }

            if (!envelope.TryGetValue("Data", out object data) || !(data is Dictionary<string, object> dataObject))
            {
                return false;
            }

            if (!dataObject.TryGetValue(arrayName, out object array))
            {
                // A valid page with nothing on it.
                items = new List<object>();
                return true;
            }

            items = array as List<object>;
            return items != null;
        }

        static ObTransaction ReadTransaction(Dictionary<string, object> o)
        {
            var t = new ObTransaction
            {
                TransactionId = Str(o, "TransactionId"),
                AccountId = Str(o, "AccountId"),
                CreditDebitIndicator = Str(o, "CreditDebitIndicator", ObCreditDebit.Debit),
                Status = Str(o, "Status", ObStatus.Booked),
                BookingDateTime = Str(o, "BookingDateTime"),
                TransactionInformation = Str(o, "TransactionInformation")
            };

            if (o.TryGetValue("Amount", out object amount) && amount is Dictionary<string, object> a)
            {
                t.Amount = new ObAmount { Amount = Str(a, "Amount", "0.00"), Currency = Str(a, "Currency", "ZAR") };
            }

            if (o.TryGetValue("MerchantDetails", out object merchant) && merchant is Dictionary<string, object> m)
            {
                t.MerchantDetails = new ObMerchantDetails
                {
                    MerchantName = Str(m, "MerchantName"),
                    MerchantCategoryCode = Str(m, "MerchantCategoryCode")
                };
            }

            if (o.TryGetValue("BankTransactionCode", out object code) && code is Dictionary<string, object> c)
            {
                t.BankTransactionCode = new ObBankTransactionCode { Code = Str(c, "Code"), SubCode = Str(c, "SubCode") };
            }

            return t;
        }

        /// <summary>A string or number field as text; <paramref name="fallback"/> when missing, null or another type.</summary>
        static string Str(Dictionary<string, object> o, string key, string fallback = "")
        {
            return o.TryGetValue(key, out object value) && value is string s ? s : fallback;
        }
    }
}
