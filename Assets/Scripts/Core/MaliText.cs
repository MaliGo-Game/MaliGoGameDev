using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MaliGo.Data;
using MaliGo.Economy;
using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// Fills {tokens} in Mali's lines and other copy (design spec 4.1). Amount tokens give digits with
    /// the group space and no sign; templates write the R ("R{cash}"). Unknown tokens are left visible
    /// and logged as a warning.
    /// </summary>
    public static class MaliText
    {
        public const string FallbackName = "friend";

        /// <summary>
        /// Replaces every {key} in the template. Built-in keys: name (and legacy 0), cash, savings, total,
        /// goalName, goalTarget, goalLeft, day, paydayWhen, energy, plan, friend. Any key in extra is used
        /// as given (and wins over a built-in key of the same name).
        /// </summary>
        public static string Fill(string template, PlayerData data, IDictionary<string, string> extra = null)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template ?? "";
            }

            var result = new StringBuilder(template.Length + 16);
            int i = 0;
            while (i < template.Length)
            {
                char c = template[i];
                if (c == '{')
                {
                    int close = template.IndexOf('}', i + 1);
                    if (close > i + 1)
                    {
                        string key = template.Substring(i + 1, close - i - 1);
                        if (IsTokenKey(key))
                        {
                            if (TryResolve(key, data, extra, out string value))
                            {
                                result.Append(value);
                            }
                            else
                            {
                                Debug.LogWarning($"[MaliText] Unknown token {{{key}}} in \"{template}\".");
                                result.Append('{').Append(key).Append('}');
                            }

                            i = close + 1;
                            continue;
                        }
                    }
                }

                result.Append(c);
                i++;
            }

            return result.ToString();
        }

        /// <summary>The player's name, or "friend" when it is empty.</summary>
        public static string Name(PlayerData data)
        {
            string name = data?.characterName;
            return string.IsNullOrWhiteSpace(name) ? FallbackName : name.Trim();
        }

        /// <summary>"Thandi", or "Lerato" when the player's own name starts with "Thandi" (ignoring case).</summary>
        public static string Friend(PlayerData data)
        {
            string name = data?.characterName ?? "";
            return name.TrimStart().StartsWith("Thandi", StringComparison.OrdinalIgnoreCase) ? "Lerato" : "Thandi";
        }

        /// <summary>"in N days" (N = days to payday), or "tomorrow" when N is 1.</summary>
        public static string PaydayWhen(int currentDay)
        {
            int days = ChapterConfig.DaysToPayday(currentDay);
            return days == 1 ? "tomorrow" : "in " + days.ToString(CultureInfo.InvariantCulture) + " days";
        }

        static bool IsTokenKey(string key)
        {
            foreach (char k in key)
            {
                if (!(char.IsLetterOrDigit(k) || k == '_'))
                {
                    return false;
                }
            }

            return key.Length > 0;
        }

        static bool TryResolve(string key, PlayerData data, IDictionary<string, string> extra, out string value)
        {
            if (extra != null && extra.TryGetValue(key, out value))
            {
                value ??= "";
                return true;
            }

            switch (key)
            {
                case "name":
                case "0":
                    value = Name(data);
                    return true;
                case "friend":
                    value = Friend(data);
                    return true;
            }

            if (data != null)
            {
                FinancialStats stats = data.financialStats ?? new FinancialStats();
                switch (key)
                {
                    case "cash":
                        value = MoneyFormat.Digits(stats.cash);
                        return true;
                    case "savings":
                        value = MoneyFormat.Digits(stats.savings);
                        return true;
                    case "total":
                        value = MoneyFormat.Digits(stats.cash + stats.savings);
                        return true;
                    case "goalName":
                        value = GoalPresets.ShortName(data.GetPrimaryGoal());
                        return true;
                    case "goalTarget":
                        value = MoneyFormat.Digits(data.GetPrimaryGoal().targetAmount);
                        return true;
                    case "goalLeft":
                        value = MoneyFormat.Digits(data.GetPrimaryGoal().Remaining(stats.savings));
                        return true;
                    case "day":
                        value = data.currentDay.ToString(CultureInfo.InvariantCulture);
                        return true;
                    case "paydayWhen":
                        value = PaydayWhen(data.currentDay);
                        return true;
                    case "energy":
                        value = ((int)Math.Round(stats.energy, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                        return true;
                    case "plan":
                        value = data.paydayPlanText ?? "";
                        return true;
                }
            }

            value = null;
            return false;
        }
    }
}
