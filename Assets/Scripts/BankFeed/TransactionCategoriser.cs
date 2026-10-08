using System;
using System.Collections.Generic;

namespace MaliGo.BankFeed
{
    /// <summary>Spending categories the bank feed sorts debits into (ids stored in BankHabitSummary).</summary>
    public static class SpendCategory
    {
        public const string EatingOut = "eating_out";
        public const string Groceries = "groceries";
        public const string Transport = "transport";
        public const string EHailing = "ehailing";
        public const string Fuel = "fuel";
        public const string DataAirtime = "data_airtime";
        public const string Clothing = "clothing";
        public const string Entertainment = "entertainment";
        public const string Housing = "housing";
        public const string Cash = "cash";
        public const string LoanRepayment = "loan_repayment";
        public const string Savings = "savings";
        public const string Family = "family";
        public const string Fees = "fees";
        public const string Other = "other";

        public static readonly string[] All =
        {
            EatingOut, Groceries, Transport, EHailing, Fuel, DataAirtime, Clothing, Entertainment, Housing, Cash,
            LoanRepayment, Savings, Family, Fees, Other
        };
    }

    /// <summary>Kinds of income credit.</summary>
    public static class IncomeKind
    {
        public const string None = "", Salary = "salary", Allowance = "allowance", Bursary = "bursary", Grant = "grant",
            Irregular = "irregular";
    }

    /// <summary>The categoriser's verdict on one transaction.</summary>
    public struct Categorised
    {
        /// <summary>True for a debit that counts as spending.</summary>
        public bool IsSpend;

        /// <summary>True for a credit that counts as income (refunds and reversals do not).</summary>
        public bool IsIncome;

        /// <summary>A SpendCategory id for spending; "" otherwise.</summary>
        public string Category;

        /// <summary>An IncomeKind id for income; "" otherwise.</summary>
        public string IncomeKind;

        /// <summary>"mcc", "keyword" or "fallback": which rule decided (for tests).</summary>
        public string Rule;
    }

    /// <summary>
    /// Sorts transactions into spending categories (docs/BANK_FEED.md): the ISO 18245 merchant category code first,
    /// then keyword rules on MerchantName and TransactionInformation, then "other". Debits count as spending; credits
    /// are income unless they read as a refund or reversal. Pure; runs on the phone.
    /// </summary>
    public static class TransactionCategoriser
    {
        // MCC -> category. A null value means "the code alone is not enough; use the keywords" (transfers, e.g. 6012).
        static readonly Dictionary<string, string> MccMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Eating places and fast food.
            { "5812", SpendCategory.EatingOut }, { "5814", SpendCategory.EatingOut },
            // Local commuter transport, buses, rail; 4121 (taxicabs) is refined by keywords below.
            { "4111", SpendCategory.Transport }, { "4112", SpendCategory.Transport },
            { "4131", SpendCategory.Transport }, { "4121", SpendCategory.Transport },
            // Telecom: airtime, data, phones.
            { "4812", SpendCategory.DataAirtime }, { "4814", SpendCategory.DataAirtime },
            { "4816", SpendCategory.DataAirtime },
            // Grocery and food stores.
            { "5411", SpendCategory.Groceries }, { "5422", SpendCategory.Groceries }, { "5441", SpendCategory.Groceries },
            { "5451", SpendCategory.Groceries }, { "5462", SpendCategory.Groceries }, { "5499", SpendCategory.Groceries },
            // Fuel.
            { "5541", SpendCategory.Fuel }, { "5542", SpendCategory.Fuel }, { "5983", SpendCategory.Fuel },
            // Clothing and shoes.
            { "5311", SpendCategory.Clothing }, { "5611", SpendCategory.Clothing }, { "5621", SpendCategory.Clothing },
            { "5651", SpendCategory.Clothing }, { "5661", SpendCategory.Clothing }, { "5691", SpendCategory.Clothing },
            { "5699", SpendCategory.Clothing },
            // Entertainment: cinema, video streaming/rental, betting, bars, gyms, events, cable.
            { "7832", SpendCategory.Entertainment }, { "7841", SpendCategory.Entertainment },
            { "7994", SpendCategory.Entertainment }, { "7995", SpendCategory.Entertainment },
            { "7997", SpendCategory.Entertainment }, { "7922", SpendCategory.Entertainment },
            { "7999", SpendCategory.Entertainment }, { "5813", SpendCategory.Entertainment },
            { "4899", SpendCategory.Entertainment },
            // Rent and utilities.
            { "6513", SpendCategory.Housing }, { "4900", SpendCategory.Housing },
            // Cash withdrawals.
            { "6010", SpendCategory.Cash }, { "6011", SpendCategory.Cash },
            // Financial institutions and money transfers: keywords decide (loan, stokvel, money home...).
            { "6012", null }, { "6051", null }, { "4829", null },
        };

        // Keyword rules, first match wins, so the specific ones come first. Matched against lower-case
        // "merchant name + transaction information".
        static readonly KeyValuePair<string, string[]>[] KeywordRules =
        {
            Rule(SpendCategory.Savings, "stokvel", "savings", "save pocket"),
            Rule(SpendCategory.LoanRepayment, "loan", "repayment", "instalment", "installment", "credit agreement",
                "account payment"),
            Rule(SpendCategory.Family, "money home", "send money", "family", "gogo", "to mom", "to mama"),
            Rule(SpendCategory.Housing, " rent", "res fee", "residence", "landlord", "electricity", "prepaid elec",
                "municipal", "water"),
            Rule(SpendCategory.EHailing, "ride app", "e-hail", "ehail", "ride share"),
            Rule(SpendCategory.EatingOut, "kota", "takeaway", "take-away", "chips", "bunny chow", "grill", "chicken",
                "pizza", "burger", "fast food", "lunch", "canteen", "vetkoek"),
            Rule(SpendCategory.Groceries, "spaza", "grocer", "supermarket", "butcher", "bakery", "fruit", "veg",
                "hyper"),
            Rule(SpendCategory.Transport, "taxi", "rank", "bus ", "train", "fare", "commuter"),
            Rule(SpendCategory.Fuel, "fuel", "petrol", "diesel", "garage", "filling station"),
            Rule(SpendCategory.DataAirtime, "data", "airtime", "bundle", "recharge"),
            Rule(SpendCategory.Clothing, "cloth", "fashion", "shoe", "sneaker", "apparel", "boutique"),
            Rule(SpendCategory.Entertainment, "cinema", "movie", "stream", "tavern", "club", " bet", "lotto", "gym",
                "concert", "game"),
            Rule(SpendCategory.Cash, "atm", "cash withdrawal"),
            Rule(SpendCategory.Fees, " fee", "service charge"),
        };

        static readonly KeyValuePair<string, string[]>[] IncomeRules =
        {
            Rule(IncomeKind.Bursary, "bursary", "stipend", "scholarship"),
            Rule(IncomeKind.Allowance, "allowance", "pocket money"),
            Rule(IncomeKind.Grant, "grant"),
            Rule(IncomeKind.Salary, "salary", "wage", "payroll", "pay run"),
        };

        static readonly string[] NotIncome = { "refund", "reversal", "reversed", "chargeback" };

        static KeyValuePair<string, string[]> Rule(string result, params string[] words)
        {
            return new KeyValuePair<string, string[]>(result, words);
        }

        public static Categorised Categorise(ObTransaction t)
        {
            var result = new Categorised { Category = "", IncomeKind = "", Rule = "fallback" };
            if (t == null)
            {
                return result;
            }

            // A leading space lets " rent" / " fee" match only at a word start ("rent", not "current" or "coffee").
            string text = (" " + (t.MerchantDetails?.MerchantName ?? "") + " " + (t.TransactionInformation ?? ""))
                .ToLowerInvariant();

            if (t.IsCredit)
            {
                if (ContainsAny(text, NotIncome))
                {
                    return result;
                }

                result.IsIncome = true;
                result.IncomeKind = Match(IncomeRules, text) ?? IncomeKind.Irregular;
                result.Rule = result.IncomeKind == IncomeKind.Irregular ? "fallback" : "keyword";
                return result;
            }

            result.IsSpend = true;
            string mcc = (t.MerchantDetails?.MerchantCategoryCode ?? "").Trim();
            if (mcc.Length > 0 && MccMap.TryGetValue(mcc, out string byCode) && byCode != null)
            {
                result.Category = byCode;
                result.Rule = "mcc";
                // A taxicab code on a ride app is e-hailing.
                if (byCode == SpendCategory.Transport && mcc == "4121" &&
                    Match(KeywordRules, text) == SpendCategory.EHailing)
                {
                    result.Category = SpendCategory.EHailing;
                }

                return result;
            }

            string byWord = Match(KeywordRules, text);
            if (byWord != null)
            {
                result.Category = byWord;
                result.Rule = "keyword";
                return result;
            }

            result.Category = SpendCategory.Other;
            return result;
        }

        /// <summary>The category an MCC alone gives, or null (unknown or a transfer code).</summary>
        public static string CategoryForMcc(string mcc)
        {
            return mcc != null && MccMap.TryGetValue(mcc, out string category) ? category : null;
        }

        static string Match(KeyValuePair<string, string[]>[] rules, string text)
        {
            foreach (KeyValuePair<string, string[]> rule in rules)
            {
                if (ContainsAny(text, rule.Value))
                {
                    return rule.Key;
                }
            }

            return null;
        }

        static bool ContainsAny(string text, string[] words)
        {
            foreach (string word in words)
            {
                if (text.IndexOf(word, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
