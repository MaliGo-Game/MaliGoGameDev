using System;
using System.Text;

namespace MaliGo.Economy
{
    /// <summary>
    /// Rand amounts as the player sees them (design spec 5.2): whole Rand, rounded half away from
    /// zero, groups of three separated by a plain space from 1 000, U+2212 for minus. Never culture
    /// dependent.
    /// </summary>
    public static class MoneyFormat
    {
        public const string Minus = "−";

        /// <summary>"R1 250", "R0", "−R45".</summary>
        public static string Rand(float amount)
        {
            long whole = RoundWhole(amount);
            if (whole < 0)
            {
                return Minus + "R" + Group(-whole);
            }

            return "R" + Group(whole);
        }

        /// <summary>"+R150", "−R45", "R0".</summary>
        public static string Signed(float amount)
        {
            long whole = RoundWhole(amount);
            if (whole > 0)
            {
                return "+R" + Group(whole);
            }

            if (whole < 0)
            {
                return Minus + "R" + Group(-whole);
            }

            return "R0";
        }

        /// <summary>Digits only, grouped, no sign and no R ("1 250"; −45 gives "45"). Templates write the R.</summary>
        public static string Digits(float amount)
        {
            long whole = RoundWhole(amount);
            return Group(whole < 0 ? -whole : whole);
        }

        static long RoundWhole(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return 0;
            }

            return (long)Math.Round((double)amount, MidpointRounding.AwayFromZero);
        }

        static string Group(long value)
        {
            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (digits.Length <= 3)
            {
                return digits;
            }

            // Built by hand into a char array. StringBuilder.Append(string, int, int) hung the IL2CPP Android build
            // inside the library (main thread stuck, managed heap growing ~270 MB/s until Android killed the game),
            // which froze every count-up of an amount of R1 000 or more: after a choice, at the Bank, at Sleep.
            int lead = digits.Length % 3;
            if (lead == 0)
            {
                lead = 3;
            }

            var chars = new char[digits.Length + (digits.Length - 1) / 3];
            int write = 0;
            for (int read = 0; read < digits.Length; read++)
            {
                if (read > 0 && (read - lead) % 3 == 0)
                {
                    chars[write++] = ' ';
                }

                chars[write++] = digits[read];
            }

            return new string(chars, 0, write);
        }
    }
}
