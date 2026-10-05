using UnityEngine;

namespace MaliGo.UI.Kit
{
    /// <summary>Aileron weights the kit ships (DESIGN_SPEC §5.1, §6.1).</summary>
    public enum UiFontWeight
    {
        SemiBold,
        Bold,
        Black
    }

    /// <summary>
    /// Loads the three Aileron weights from <c>Resources/MaliGo/Fonts</c> (TrueType conversions made by
    /// <c>tools/convert_fonts.py</c>, DESIGN_SPEC §6.1) as dynamic fonts.
    ///
    /// Self-test (§5.2 rule 6): on first use of a weight, <c>RequestCharactersInTexture("R0123456789 −·", 44)</c>
    /// then <c>GetCharacterInfo</c> for each of those characters. If the font is missing or any lookup fails,
    /// a warning is logged once for that weight and Unity's built-in <c>LegacyRuntime.ttf</c> is used instead.
    /// </summary>
    public static class UiFonts
    {
        public const string ResourceFolder = "MaliGo/Fonts/";
        /// <summary>Characters every weight must render: digits, the Rand sign, the group space, U+2212 and the middle dot.</summary>
        public const string SelfTestCharacters = "R0123456789 −·";
        public const int SelfTestSize = 44;
        const string BuiltinFallback = "LegacyRuntime.ttf";

        static readonly Font[] cache = new Font[3];
        static readonly bool[] fallback = new bool[3];
        static readonly bool[] warned = new bool[3];
        static Font builtin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < cache.Length; i++)
            {
                cache[i] = null;
                fallback[i] = false;
                warned[i] = false;
            }
            builtin = null;
        }

        /// <summary>The Aileron font of that weight, or the built-in fallback if it failed its self-test.</summary>
        public static Font Get(UiFontWeight weight)
        {
            int index = Index(weight);
            Font font = cache[index];
            if (font != null)
            {
                return font;
            }

            font = Resources.Load<Font>(ResourceFolder + FileName(weight));
            if (font != null && SelfTest(font))
            {
                fallback[index] = false;
            }
            else
            {
                if (!warned[index])
                {
                    warned[index] = true;
                    Debug.LogWarning(font == null
                        ? "[UiFonts] " + FileName(weight) + " not found in Resources/" + ResourceFolder + "; using " + BuiltinFallback + "."
                        : "[UiFonts] " + FileName(weight) + " failed its glyph self-test; using " + BuiltinFallback + ".");
                }
                fallback[index] = true;
                font = Builtin();
            }

            cache[index] = font;
            return font;
        }

        /// <summary>True when that weight is being drawn with the built-in fallback (after its first <see cref="Get"/>).</summary>
        public static bool IsFallback(UiFontWeight weight)
        {
            Get(weight);
            return fallback[Index(weight)];
        }

        /// <summary>Unity's built-in runtime font (never null in a player).</summary>
        public static Font Builtin()
        {
            if (builtin == null)
            {
                builtin = Resources.GetBuiltinResource<Font>(BuiltinFallback);
            }
            return builtin;
        }

        public static string FileName(UiFontWeight weight)
        {
            switch (weight)
            {
                case UiFontWeight.SemiBold: return "Aileron-SemiBold";
                case UiFontWeight.Black: return "Aileron-Black";
                default: return "Aileron-Bold";
            }
        }

        static bool SelfTest(Font font)
        {
            try
            {
                font.RequestCharactersInTexture(SelfTestCharacters, SelfTestSize, FontStyle.Normal);
                foreach (char c in SelfTestCharacters)
                {
                    if (!font.GetCharacterInfo(c, out CharacterInfo _, SelfTestSize, FontStyle.Normal))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[UiFonts] self-test threw: " + exception.Message);
                return false;
            }
        }

        static int Index(UiFontWeight weight)
        {
            int index = (int)weight;
            return index >= 0 && index < cache.Length ? index : (int)UiFontWeight.Bold;
        }
    }
}
