using System;
using System.Collections.Generic;
using System.IO;
using MaliGo.Data;

/// <summary>Selectable characters and skin tones: save mapping, appearance keys, the skin mask and the recolour.</summary>
public static class CharacterLookTests
{
    static readonly SkinRgb KenneySkin = new SkinRgb(245, 146, 113);

    public static void TestOutfitAndToneTables()
    {
        var ids = new HashSet<string>();
        foreach (CharacterLooks.Outfit o in CharacterLooks.Outfits)
        {
            Assert.True(!string.IsNullOrEmpty(o.id) && ids.Add(o.id), "outfit id unique: " + o.id);
            Assert.True(!string.IsNullOrEmpty(o.label), "outfit label: " + o.id);
            string lower = o.label.ToLowerInvariant();
            foreach (string gendered in new[] { "girl", "boy", "man", "woman", "male", "female", "lady", "guy" })
            {
                Assert.True(Array.IndexOf(lower.Split(' '), gendered) < 0, "label names a style, not a gender: " + o.label);
            }
        }

        Assert.True(CharacterLooks.FindOutfit(CharacterLooks.DefaultOutfitId) == 0, "default outfit is the first card");

        var tones = new HashSet<string>();
        float previous = float.MaxValue;
        foreach (CharacterLooks.Tone t in CharacterLooks.Tones)
        {
            Assert.True(tones.Add(t.id), "tone id unique: " + t.id);
            float luminance = 0.2126f * t.colour.r + 0.7152f * t.colour.g + 0.0722f * t.colour.b;
            Assert.True(luminance < previous, "tones run lightest to deepest: " + t.id);
            previous = luminance;
        }

        // The three tones of the earlier looks keep their ids and colours.
        AssertRgb(new SkinRgb(198, 140, 100), CharacterLooks.Tones[CharacterLooks.FindTone("light")].colour, "light kept");
        AssertRgb(new SkinRgb(150, 96, 62), CharacterLooks.Tones[CharacterLooks.FindTone("medium")].colour, "medium kept");
        AssertRgb(new SkinRgb(96, 60, 40), CharacterLooks.Tones[CharacterLooks.FindTone("deep")].colour, "deep kept");
    }

    public static void TestOldSavesKeepTheirLook()
    {
        // Before outfits: genderPresentation picked the skater skin, the outfit field did not exist ("").
        var masculine = new AppearanceData { genderPresentation = "masculine", skinTone = "deep" };
        Assert.Equal(CharacterLooks.StreetId, CharacterLooks.OutfitOf(masculine).id, "masculine -> male skater (street)");
        Assert.Equal("deep", CharacterLooks.ToneOf(masculine).id, "tone kept");

        var feminine = new AppearanceData { genderPresentation = "feminine", skinTone = "light" };
        Assert.Equal(CharacterLooks.SkaterId, CharacterLooks.OutfitOf(feminine).id, "feminine -> female skater");

        var neutral = new AppearanceData();
        Assert.Equal(CharacterLooks.SkaterId, CharacterLooks.OutfitOf(neutral).id, "default appearance -> skater");
        Assert.Equal("medium", CharacterLooks.ToneOf(neutral).id, "default tone medium");
        Assert.Equal("", neutral.outfit, "new field defaults empty, so JSON without it reads as an old save");

        Assert.Equal(CharacterLooks.SkaterId, CharacterLooks.OutfitOf(null).id, "null -> default outfit");
        Assert.Equal("medium", CharacterLooks.ToneOf(new AppearanceData { skinTone = "olive" }).id, "unknown tone -> medium");
        Assert.Equal(CharacterLooks.StreetId,
            CharacterLooks.OutfitOf(new AppearanceData { outfit = "robot", genderPresentation = "masculine" }).id,
            "unknown outfit id (a newer build's) -> the old mapping");

        string keyBefore = CharacterLooks.Key(masculine);
        CharacterLooks.Normalize(masculine);
        Assert.Equal(CharacterLooks.StreetId, masculine.outfit, "normalize writes the outfit");
        Assert.Equal("deep", masculine.skinTone, "normalize keeps a known tone");
        Assert.Equal(keyBefore, CharacterLooks.Key(masculine), "normalizing does not change the look (no rebuild)");

        var odd = new AppearanceData { skinTone = "" };
        CharacterLooks.Normalize(odd);
        Assert.Equal("medium", odd.skinTone, "normalize fixes an empty tone");
        CharacterLooks.Normalize(null);
    }

    public static void TestCreateAndKeys()
    {
        AppearanceData a = CharacterLooks.Create(2, 5);
        Assert.Equal(CharacterLooks.Outfits[2].id, a.outfit, "create outfit");
        Assert.Equal(CharacterLooks.Tones[5].id, a.skinTone, "create tone");
        Assert.Equal(CharacterLooks.Outfits[CharacterLooks.Outfits.Length - 1].id, CharacterLooks.Create(99, -3).outfit, "outfit clamped");
        Assert.Equal(CharacterLooks.Tones[0].id, CharacterLooks.Create(99, -3).skinTone, "tone clamped");
        Assert.Equal(CharacterLooks.OutfitResourceFolder + CharacterLooks.Outfits[2].id, CharacterLooks.OutfitResourcePath(a), "resource path");

        Assert.True(CharacterLooks.Key(CharacterLooks.Create(0, 2)) != CharacterLooks.Key(CharacterLooks.Create(1, 2)), "key changes with the outfit");
        Assert.True(CharacterLooks.Key(CharacterLooks.Create(0, 2)) != CharacterLooks.Key(CharacterLooks.Create(0, 3)), "key changes with the tone");
        Assert.Equal(CharacterLooks.Key(CharacterLooks.Create(0, 2)), CharacterLooks.Key(CharacterLooks.Create(0, 2)), "key stable");
        Assert.Equal("", CharacterLooks.Key(null), "null key");

        AppearanceData copy = a.Clone();
        Assert.Equal(a.outfit, copy.outfit, "clone copies the outfit");
    }

    public static void TestSkinColourClassification()
    {
        // The Kenney skin and its shades / accents.
        foreach (SkinRgb skin in new[] { new SkinRgb(245, 140, 106), new SkinRgb(245, 146, 113), new SkinRgb(245, 151, 119),
                     new SkinRgb(246, 152, 120), new SkinRgb(244, 130, 94) })
        {
            Assert.True(SkinToneMath.SkinWeightFromColour(skin, KenneySkin) == 1f, "skin: " + Describe(skin));
        }

        // Shirts, hair and the rest in nearby hues: channels scale unevenly or too dark.
        foreach (SkinRgb other in new[] { new SkinRgb(242, 101, 76), new SkinRgb(228, 120, 62), new SkinRgb(234, 48, 49),
                     new SkinRgb(163, 104, 65), new SkinRgb(77, 22, 14), new SkinRgb(255, 255, 255), new SkinRgb(37, 37, 37),
                     new SkinRgb(204, 221, 231) })
        {
            Assert.True(SkinToneMath.SkinWeightFromColour(other, KenneySkin) == 0f, "not skin: " + Describe(other));
        }

        // The survivors' darker base.
        var survivorBase = new SkinRgb(135, 34, 26);
        Assert.True(SkinToneMath.SkinWeightFromColour(new SkinRgb(127, 32, 24), survivorBase) == 1f, "survivor shade is skin");
        Assert.True(SkinToneMath.SkinWeightFromColour(new SkinRgb(228, 120, 62), survivorBase) == 0f, "survivor shorts are not skin");
        Assert.True(SkinToneMath.SkinWeightFromColour(KenneySkin, new SkinRgb(0, 10, 10)) == 0f, "degenerate base");
    }

    public static void TestMaskAlphaEncoding()
    {
        Assert.Equal(0f, SkinToneMath.WeightFromAlpha(255), "255 = not skin");
        Assert.Equal(1f, SkinToneMath.WeightFromAlpha(128), "128 = all skin");
        Assert.Equal(1f, SkinToneMath.WeightFromAlpha(0), "below 128 = all skin");
        Assert.Equal(0.5f, SkinToneMath.WeightFromAlpha(SkinToneMath.AlphaFromWeight(0.5f)), "round trip 0.5", 0.01f);
        Assert.True(SkinToneMath.AlphaFromWeight(0f) == 255 && SkinToneMath.AlphaFromWeight(1f) == 128, "encode ends");
        Assert.True(SkinToneMath.AlphaFromWeight(2f) == 128, "encode clamps");
    }

    public static void TestRecolour()
    {
        var tone = new SkinRgb(96, 60, 40);
        AssertRgb(tone, SkinToneMath.Recolour(KenneySkin, 1f, KenneySkin, tone), "base skin becomes the tone");

        // A lighter shade stays lighter than the tone, by the same ratio.
        SkinRgb shade = SkinToneMath.Recolour(new SkinRgb(245, 151, 119), 1f, KenneySkin, tone);
        Assert.True(shade.r == 96 && shade.g > 60 && shade.b > 40, "shade keeps its shading: " + Describe(shade));

        var shirt = new SkinRgb(242, 101, 76);
        AssertRgb(shirt, SkinToneMath.Recolour(shirt, 0f, KenneySkin, tone), "weight 0 untouched");

        // An edge texel half skin, half black hair: half the tone and half the hair.
        var hair = new SkinRgb(0, 0, 0);
        var edge = new SkinRgb((byte)((KenneySkin.r + hair.r) / 2), (byte)((KenneySkin.g + hair.g) / 2), (byte)((KenneySkin.b + hair.b) / 2));
        SkinRgb mixed = SkinToneMath.Recolour(edge, 0.5f, KenneySkin, tone);
        Assert.True(Math.Abs(mixed.r - 48) <= 1 && Math.Abs(mixed.g - 30) <= 1 && Math.Abs(mixed.b - 20) <= 1, "edge mixes: " + Describe(mixed));

        // Lighter tone than the base clamps instead of wrapping.
        SkinRgb bright = SkinToneMath.Recolour(new SkinRgb(255, 255, 255), 1f, new SkinRgb(200, 200, 200), new SkinRgb(250, 250, 250));
        AssertRgb(new SkinRgb(255, 255, 255), bright, "clamped");
    }

    public static void TestRecolourTexels()
    {
        var tone = new SkinRgb(150, 96, 62);
        // Masked: texel 0 all skin, texel 1 not skin (alpha 255), texel 2 a skin-coloured texel the mask excludes.
        byte[] masked =
        {
            245, 146, 113, 128,
            242, 101, 76, 255,
            245, 146, 113, 255
        };
        int count = SkinToneMath.RecolourTexels(masked, KenneySkin, tone);
        Assert.True(count == 1, "the mask decides when present");
        Assert.True(masked[0] == 150 && masked[1] == 96 && masked[2] == 62, "masked skin recoloured");
        Assert.True(masked[4] == 242 && masked[8] == 245, "unmasked texels untouched");
        Assert.True(masked[3] == 255 && masked[7] == 255 && masked[11] == 255, "alpha reset to opaque");

        // No mask anywhere: classified by colour.
        byte[] plain =
        {
            245, 146, 113, 255,
            242, 101, 76, 255
        };
        Assert.True(SkinToneMath.RecolourTexels(plain, KenneySkin, tone) == 1, "colour fallback finds the skin");
        Assert.True(plain[0] == 150 && plain[4] == 242, "fallback recolours only the skin");
        Assert.True(SkinToneMath.RecolourTexels(null, KenneySkin, tone) == 0, "null is safe");
    }

    /// <summary>Every outfit ships its texture where the runtime loads it, readable (CharacterSkins reads it).</summary>
    public static void TestOutfitTexturesShip()
    {
        string assets = FindAssets();
        Assert.True(assets != null, "Assets folder found");
        foreach (CharacterLooks.Outfit o in CharacterLooks.Outfits)
        {
            string png = Path.Combine(assets, "Resources", CharacterLooks.OutfitResourceFolder.Replace('/', Path.DirectorySeparatorChar), o.id + ".png");
            Assert.True(File.Exists(png), "outfit texture: " + png);
            Assert.True(File.Exists(png + ".meta"), "outfit meta: " + o.id);
            string meta = File.ReadAllText(png + ".meta");
            Assert.True(meta.Contains("isReadable: 1"), "outfit texture is Read/Write: " + o.id);
            Assert.True(meta.Contains("alphaIsTransparency: 0"), "outfit alpha (the skin mask) is not treated as transparency: " + o.id);
            Assert.True(!meta.Contains("textureCompression: 1"), "outfit texture is uncompressed (exact mask): " + o.id);
        }
    }

    static string FindAssets()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "Assets");
            if (Directory.Exists(Path.Combine(candidate, "Resources")))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    static void AssertRgb(SkinRgb expected, SkinRgb actual, string message)
    {
        Assert.True(expected.r == actual.r && expected.g == actual.g && expected.b == actual.b,
            message + ": expected " + Describe(expected) + ", got " + Describe(actual));
    }

    static string Describe(SkinRgb c) => "(" + c.r + ", " + c.g + ", " + c.b + ")";
}
