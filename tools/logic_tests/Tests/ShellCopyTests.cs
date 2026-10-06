// WP8 shell copy checks (DESIGN_SPEC §8 WP8 "Tests", §4.6, §5.4.13): the character-creation strings fit their
// boxes in Aileron, pass the glyph whitelist and name no brand.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MaliGo.Data;
using MaliGo.Scenarios;

public static class ShellCopyTests
{
    const float SheetTextWidth = 1380f;
    const float CardLabelWidth = 250f;

    static readonly string[] Foci = { "food", "transport", "data_social", "home_family" };
    static readonly string[] Travels = { "taxi", "ehailing", "walk", "car" };

    // Same list as ContentTests (WP3).
    static readonly string[] Brands =
    {
        "Uber", "Bolt", "Shoprite", "Checkers", "Pick n Pay", "Spar", "Woolworths", "KFC", "Nando's", "Steers",
        "Vodacom", "MTN", "Telkom", "Cell C", "Absa", "Capitec", "FNB", "Nedbank", "Standard Bank", "Takealot", "PEP",
        "Mr Price", "Engen", "Shell", "Sasol", "Caltex", "Virgin Active", "Planet Fitness",
    };

    static float W(string text, string weight, float size) => AileronMetrics.Width(text, weight, size);

    /// <summary>Greedy word wrap (as UiTextLayout) in Aileron; returns the lines.</summary>
    static List<string> Wrap(string text, string weight, float size, float width)
    {
        var lines = new List<string>();
        float space = W(" ", weight, size);
        string line = null;
        float lineWidth = 0f;
        foreach (string word in text.Split(' ').Where(x => x.Length > 0))
        {
            float ww = W(word, weight, size);
            if (line == null)
            {
                line = word;
                lineWidth = ww;
            }
            else if (lineWidth + space + ww <= width + 0.01f)
            {
                line += " " + word;
                lineWidth += space + ww;
            }
            else
            {
                lines.Add(line);
                line = word;
                lineWidth = ww;
            }
        }

        if (line != null)
        {
            lines.Add(line);
        }

        foreach (string l in lines)
        {
            Assert.True(W(l, weight, size) <= width + 0.01f, "single word wider than " + width + ": \"" + l + "\"");
        }

        return lines;
    }

    static void FitsOneLine(string text, string weight, float size, float width, string what)
    {
        float w = W(text, weight, size);
        Assert.True(w <= width, what + " is " + w.ToString("0") + " u, over " + width + " u: \"" + text + "\"");
    }

    static IEnumerable<string> JoinedPlaces()
    {
        foreach (string f in Foci)
        {
            foreach (string t in Travels)
            {
                string[] places = ChapterSchedule.WeekPlaces(f, t);
                Assert.True(places != null && places.Length == 3, "WeekPlaces(" + f + ", " + t + ") has 3 places");
                yield return string.Join(OnboardingCopy.WeekPlacesSeparator, places);
            }
        }
    }

    static IEnumerable<string> AllShellStrings()
    {
        yield return OnboardingCopy.PromiseLine1;
        yield return OnboardingCopy.PromiseLine2;
        yield return OnboardingCopy.NameQuestion;
        yield return OnboardingCopy.NamePlaceholder;
        yield return OnboardingCopy.LookTitle;
        for (int i = 1; i <= 6; i++)
        {
            yield return OnboardingCopy.StyleLabel(i);
        }

        yield return OnboardingCopy.SpendQuestion;
        yield return OnboardingCopy.TravelQuestion;
        yield return OnboardingCopy.WeekBuiltLine;
        yield return OnboardingCopy.GoalTitle;
        yield return OnboardingCopy.GoalSavedCaption;
        yield return OnboardingCopy.NextButton;
        yield return OnboardingCopy.BackButton;
        yield return OnboardingCopy.LetsGoButton;
        yield return OnboardingCopy.UpdatedNotice;
        foreach (ProfileOption o in SpendingFocus.All.Concat(TravelMode.All))
        {
            yield return o.cardLabel;
        }

        foreach (GoalPreset g in GoalPresets.All)
        {
            yield return g.title;
        }

        foreach (string places in JoinedPlaces())
        {
            yield return places;
        }
    }

    static bool GlyphAllowed(char c)
    {
        if (c >= 0x20 && c <= 0x7E)
        {
            return true;
        }

        if ("−—–‘’“”…·×•".IndexOf(c) >= 0)
        {
            return true;
        }

        return c >= 0x00C0 && c <= 0x017F && c != 0x00D7 && c != 0x00F7 && char.IsLetter(c);
    }

    public static void TestPromiseLinesFitOneLine()
    {
        FitsOneLine(OnboardingCopy.PromiseLine1, "Black", 72f, SheetTextWidth, "PromiseLine1");
        FitsOneLine(OnboardingCopy.PromiseLine2, "Black", 72f, SheetTextWidth, "PromiseLine2");
        Assert.Equal("Live the week before payday.", OnboardingCopy.PromiseLine1, "promise line 1");
        Assert.Equal("See where your money goes.", OnboardingCopy.PromiseLine2, "promise line 2");
    }

    public static void TestProfileQuestionsFitOneLine()
    {
        FitsOneLine(OnboardingCopy.SpendQuestion, "Bold", 40f, SheetTextWidth, "SpendQuestion");
        FitsOneLine(OnboardingCopy.TravelQuestion, "Bold", 40f, SheetTextWidth, "TravelQuestion");
        FitsOneLine(OnboardingCopy.NameQuestion, "Bold", 40f, SheetTextWidth, "NameQuestion");
    }

    public static void TestEveryCardLabelFitsTwoLines()
    {
        var labels = SpendingFocus.All.Concat(TravelMode.All).Select(o => o.cardLabel).ToArray();
        Assert.True(labels.Length == 8, "8 card labels, got " + labels.Length);
        foreach (string label in labels)
        {
            Assert.True(!string.IsNullOrEmpty(label), "empty card label");
            int lines = Wrap(label, "Bold", 36f, CardLabelWidth).Count;
            Assert.True(lines <= 2, "\"" + label + "\" takes " + lines + " lines of Label 36 Bold in 250 u");
        }
    }

    public static void TestWeekBuiltLineFitsOneLine()
    {
        Assert.Equal("We've built your week around where your money goes.", OnboardingCopy.WeekBuiltLine, "WeekBuiltLine");
        FitsOneLine(OnboardingCopy.WeekBuiltLine, "SemiBold", 40f, SheetTextWidth, "WeekBuiltLine");
    }

    public static void TestWeekPlacesFitOneLineForAllProfiles()
    {
        int n = 0;
        foreach (string joined in JoinedPlaces())
        {
            FitsOneLine(joined, "Bold", 36f, SheetTextWidth, "places");
            n++;
        }

        Assert.True(n == 16, "16 profiles, got " + n);
        Assert.Equal("Kota shop · Taxi rank · Shops by the bank",
            string.Join(OnboardingCopy.WeekPlacesSeparator, ChapterSchedule.WeekPlaces("food", "taxi")), "default places");
    }

    public static void TestShellStringsGlyphsAndBrands()
    {
        var patterns = Brands
            .Select(b => new Regex(@"(?<![A-Za-z])" + Regex.Escape(b).Replace(@"\ ", @"\s+") + @"(?![A-Za-z])",
                RegexOptions.IgnoreCase))
            .ToArray();
        foreach (string text in AllShellStrings())
        {
            Assert.True(!string.IsNullOrEmpty(text), "empty shell string");
            foreach (char c in text)
            {
                Assert.True(GlyphAllowed(c), "U+" + ((int)c).ToString("X4") + " not allowed in \"" + text + "\"");
                Assert.True(AileronMetrics.Has(c), "Aileron has no glyph U+" + ((int)c).ToString("X4") + " in \"" + text + "\"");
            }

            foreach (Regex p in patterns)
            {
                Assert.True(!p.IsMatch(text), "\"" + text + "\" names a brand (" + p + ")");
            }
        }
    }

    public static void TestNameRulesAndSixLooks()
    {
        Assert.True(OnboardingCopy.NameMaxLength == 16, "name max 16");
        for (int i = 1; i <= 6; i++)
        {
            Assert.Equal("Style " + i, OnboardingCopy.StyleLabel(i), "style label " + i);
        }

        Assert.True(GoalPresets.All.Length == 4, "four goals");
    }
}
