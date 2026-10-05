using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Line breaking that never splits a Rand amount (DESIGN_SPEC §5.2 rule 3) and pagination for Mali's boxes
    /// (§5.4.3: at most 3 lines a page, sentence boundaries first, word boundaries otherwise; never shrunk).
    ///
    /// Money is written with plain spaces between groups of three ("R1 250", "−R12 450", "+R1 000 000"), and a plain
    /// space is a break opportunity for Unity's Text, so text that may hold money is pre-broken here: each amount is
    /// kept as one unbreakable unit and lines are filled greedily by measured width. Widths come from the Text's own
    /// font and size (glyph advances, as Unity lays them out; rich-text tags measure zero).
    /// </summary>
    public static class UiTextLayout
    {
        // Measuring at a large size keeps per-glyph rounding of the advances small.
        const int MeasureSize = 100;

        /// <summary>Pre-breaks <paramref name="value"/> with '\n' so every line fits <paramref name="width"/> u in
        /// <paramref name="text"/>'s font and size, keeping amounts whole; assigns it to <paramref name="text"/>
        /// (switching its horizontal overflow to Overflow, since the lines are already broken) and returns it.
        /// A single word wider than the box keeps its own line.</summary>
        public static string WrapKeepingAmounts(Text text, string value, float width)
        {
            string wrapped = Wrap(text, value, width);
            if (text != null)
            {
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.text = wrapped;
            }
            return wrapped;
        }

        /// <summary>Splits <paramref name="value"/> into pages of at most <paramref name="maxLines"/> lines of
        /// <paramref name="width"/> u, each already wrapped (as <see cref="WrapKeepingAmounts"/>). Pages break at
        /// sentence ends; a sentence too long for one page breaks between words. Does not assign the text.</summary>
        public static string[] Paginate(Text text, string value, float width, int maxLines)
        {
            var pages = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                pages.Add(string.Empty);
                return pages.ToArray();
            }
            maxLines = Mathf.Max(1, maxLines);
            Measurer measure = MeasurerFor(text);

            string current = string.Empty;
            foreach (string sentence in Sentences(value))
            {
                string candidate = current.Length == 0 ? sentence : current + " " + sentence;
                if (WrapLines(candidate, width, measure).Count <= maxLines)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    pages.Add(string.Join("\n", WrapLines(current, width, measure)));
                    current = string.Empty;
                }

                List<string> lines = WrapLines(sentence, width, measure);
                if (lines.Count <= maxLines)
                {
                    current = sentence;
                    continue;
                }

                // One sentence longer than a page: break it between words.
                int index = 0;
                while (lines.Count - index > maxLines)
                {
                    pages.Add(string.Join("\n", lines.GetRange(index, maxLines)));
                    index += maxLines;
                }
                current = string.Join(" ", lines.GetRange(index, lines.Count - index));
            }

            if (current.Length > 0)
            {
                pages.Add(string.Join("\n", WrapLines(current, width, measure)));
            }
            return pages.ToArray();
        }

        /// <summary><paramref name="value"/> pre-broken to <paramref name="width"/> u, without assigning it.</summary>
        public static string Wrap(Text text, string value, float width)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }
            return string.Join("\n", WrapLines(value, width, MeasurerFor(text)));
        }

        /// <summary>The number of lines <paramref name="value"/> takes at <paramref name="width"/> u.</summary>
        public static int CountLines(Text text, string value, float width)
        {
            return string.IsNullOrEmpty(value) ? 0 : WrapLines(value, width, MeasurerFor(text)).Count;
        }

        /// <summary>Width in u of one line of <paramref name="value"/> in <paramref name="text"/>'s font and size
        /// (rich-text tags excluded).</summary>
        public static float MeasureWidth(Text text, string value)
        {
            return MeasurerFor(text).Width(value);
        }

        // ================================================================ implementation

        sealed class Measurer
        {
            readonly Font font;
            readonly FontStyle style;
            readonly float scale;
            readonly int fallbackSize;
            readonly bool richText;
            readonly Dictionary<char, float> advances = new Dictionary<char, float>();

            public Measurer(Text text)
            {
                font = text != null ? text.font : null;
                style = text != null ? text.fontStyle : FontStyle.Normal;
                fallbackSize = text != null ? Mathf.Max(1, text.fontSize) : UiTheme.Body.Size;
                scale = fallbackSize / (float)MeasureSize;
                richText = text == null || text.supportRichText;
            }

            public float Width(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0f;
                }
                if (font != null)
                {
                    font.RequestCharactersInTexture(StripTags(value), MeasureSize, style);
                }
                float total = 0f;
                bool inTag = false;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (richText)
                    {
                        if (c == '<' && LooksLikeTag(value, i))
                        {
                            inTag = true;
                        }
                        if (inTag)
                        {
                            if (c == '>')
                            {
                                inTag = false;
                            }
                            continue;
                        }
                    }
                    total += Advance(c);
                }
                return total;
            }

            float Advance(char c)
            {
                if (advances.TryGetValue(c, out float cached))
                {
                    return cached;
                }
                float advance;
                if (font != null && font.GetCharacterInfo(c, out CharacterInfo info, MeasureSize, style))
                {
                    advance = info.advance * scale;
                }
                else
                {
                    advance = fallbackSize * 0.55f; // unknown glyph: a typical average width
                }
                advances[c] = advance;
                return advance;
            }

            string StripTags(string value)
            {
                if (!richText || value.IndexOf('<') < 0)
                {
                    return value;
                }
                var builder = new StringBuilder(value.Length);
                bool inTag = false;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == '<' && LooksLikeTag(value, i))
                    {
                        inTag = true;
                    }
                    if (!inTag)
                    {
                        builder.Append(c);
                    }
                    else if (c == '>')
                    {
                        inTag = false;
                    }
                }
                return builder.ToString();
            }
        }

        static Measurer MeasurerFor(Text text) => new Measurer(text);

        static bool LooksLikeTag(string value, int index)
        {
            int close = value.IndexOf('>', index + 1);
            if (close < 0)
            {
                return false;
            }
            string inner = value.Substring(index + 1, close - index - 1).TrimStart('/');
            int equals = inner.IndexOf('=');
            string tag = (equals >= 0 ? inner.Substring(0, equals) : inner).Trim();
            switch (tag)
            {
                case "b":
                case "i":
                case "color":
                case "size":
                case "material":
                case "quad":
                    return true;
                default:
                    return false;
            }
        }

        static List<string> WrapLines(string value, float width, Measurer measure)
        {
            var lines = new List<string>();
            float spaceWidth = measure.Width(" ");
            foreach (string paragraph in value.Replace("\r\n", "\n").Split('\n'))
            {
                List<string> units = Units(paragraph);
                if (units.Count == 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }
                var line = new StringBuilder(units[0]);
                float lineWidth = measure.Width(units[0]);
                for (int i = 1; i < units.Count; i++)
                {
                    float unitWidth = measure.Width(units[i]);
                    if (lineWidth + spaceWidth + unitWidth <= width + 0.01f)
                    {
                        line.Append(' ').Append(units[i]);
                        lineWidth += spaceWidth + unitWidth;
                    }
                    else
                    {
                        lines.Add(line.ToString());
                        line.Length = 0;
                        line.Append(units[i]);
                        lineWidth = unitWidth;
                    }
                }
                lines.Add(line.ToString());
            }
            return lines;
        }

        /// <summary>Words of one paragraph, with each Rand amount ("R1 250", "−R12 450.") glued into one unit.</summary>
        static List<string> Units(string paragraph)
        {
            var units = new List<string>();
            string[] words = paragraph.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length == 0)
                {
                    continue;
                }
                if (EndsWithRandHead(word))
                {
                    while (i + 1 < words.Length && IsDigitGroup(words[i + 1]))
                    {
                        word += " " + words[i + 1];
                        i++;
                        if (!IsBareDigitGroup(words[i]))
                        {
                            break; // the group carried punctuation, so the amount ended there
                        }
                    }
                }
                units.Add(word);
            }
            return units;
        }

        // "R1", "−R12", "+R150", "(R1", "“R999": a Rand sign then 1–3 digits ending the word.
        static bool EndsWithRandHead(string word)
        {
            int end = word.Length;
            int digits = 0;
            while (digits < end && digits < 4 && char.IsDigit(word[end - 1 - digits]))
            {
                digits++;
            }
            if (digits < 1 || digits > 3)
            {
                return false;
            }
            int r = end - 1 - digits;
            if (r < 0 || word[r] != 'R')
            {
                return false;
            }
            // Before the R: start of word, a sign, or opening punctuation (not a letter: "BR1" is not money).
            return r == 0 || !char.IsLetterOrDigit(word[r - 1]);
        }

        // "250", "250.", "250,", "250)" — exactly three digits, optionally followed by non-digit punctuation.
        static bool IsDigitGroup(string word)
        {
            if (word.Length < 3 || !char.IsDigit(word[0]) || !char.IsDigit(word[1]) || !char.IsDigit(word[2]))
            {
                return false;
            }
            for (int i = 3; i < word.Length; i++)
            {
                if (char.IsLetterOrDigit(word[i]))
                {
                    return false;
                }
            }
            return true;
        }

        static bool IsBareDigitGroup(string word)
        {
            return word.Length == 3 && IsDigitGroup(word);
        }

        /// <summary>Sentences of <paramref name="value"/>: split after . ! ? or … followed by a space (closing quotes
        /// stay with their sentence). Line breaks inside a sentence are kept.</summary>
        static List<string> Sentences(string value)
        {
            var sentences = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                current.Append(c);
                bool end = c == '.' || c == '!' || c == '?' || c == '…';
                if (!end)
                {
                    continue;
                }
                // Keep closing quotes/brackets with the sentence.
                while (i + 1 < value.Length && (value[i + 1] == '”' || value[i + 1] == '’' || value[i + 1] == '"' ||
                                                value[i + 1] == ')'))
                {
                    current.Append(value[++i]);
                }
                if (i + 1 < value.Length && value[i + 1] == ' ')
                {
                    sentences.Add(current.ToString().Trim());
                    current.Length = 0;
                    while (i + 1 < value.Length && value[i + 1] == ' ')
                    {
                        i++;
                    }
                }
            }
            string rest = current.ToString().Trim();
            if (rest.Length > 0)
            {
                sentences.Add(rest);
            }
            return sentences;
        }
    }
}
