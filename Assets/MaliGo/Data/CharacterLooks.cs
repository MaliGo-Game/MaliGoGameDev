using System;

namespace MaliGo.Data
{
    /// <summary>
    /// The player's selectable characters (outfits) and skin tones, and how an <see cref="AppearanceData"/> maps to
    /// them. Every outfit is a Kenney CC0 skin on the same model and rig (characterMedium), so the animations work
    /// for all of them; every outfit can be shown in every tone (SkinToneMath recolours its skin texels).
    /// The outfit textures are <c>Resources/MaliGo/Characters/Outfits/{id}</c>, made by
    /// tools/make_character_skins.py. Labels name a style, never a gender; all outfits are shown to everyone.
    ///
    /// Saves: <c>AppearanceData.outfit</c> holds the outfit id. Saves from before it existed have it empty and are
    /// mapped to the look they already had: <c>genderPresentation</c> "masculine" was the male skater ("street"),
    /// anything else the female skater ("skater"). Unknown tones become "medium".
    /// </summary>
    public static class CharacterLooks
    {
        public struct Outfit
        {
            public readonly string id;
            public readonly string label;

            /// <summary>The flat skin colour the outfit's texture was painted with (the recolour's reference).</summary>
            public readonly SkinRgb skinBase;

            public Outfit(string id, string label, SkinRgb skinBase)
            {
                this.id = id;
                this.label = label;
                this.skinBase = skinBase;
            }
        }

        public struct Tone
        {
            public readonly string id;
            public readonly SkinRgb colour;

            public Tone(string id, SkinRgb colour)
            {
                this.id = id;
                this.colour = colour;
            }
        }

        public const string SkaterId = "skater";
        public const string StreetId = "street";
        public const string NightOutId = "nightout";
        public const string SmartId = "smart";
        public const string OutdoorsId = "outdoors";
        public const string WorkwearId = "workwear";

        public const string DefaultOutfitId = SkaterId;
        public const string DefaultToneId = "medium";

        /// <summary>Resources folder of the outfit textures.</summary>
        public const string OutfitResourceFolder = "MaliGo/Characters/Outfits/";

        static readonly SkinRgb KenneySkin = new SkinRgb(245, 146, 113);

        /// <summary>In card order (styles alternate, so no row reads as "the girls" or "the boys").</summary>
        public static readonly Outfit[] Outfits =
        {
            new Outfit(SkaterId, "Skater", KenneySkin),
            new Outfit(StreetId, "Street", KenneySkin),
            new Outfit(NightOutId, "Night out", KenneySkin),
            new Outfit(SmartId, "Smart", KenneySkin),
            new Outfit(OutdoorsId, "Outdoors", new SkinRgb(135, 34, 26)),
            new Outfit(WorkwearId, "Workwear", KenneySkin)
        };

        /// <summary>Six skin tones, lightest to deepest. "light", "medium" and "deep" keep the colours of the
        /// earlier three-tone looks, so existing players look the same.</summary>
        public static readonly Tone[] Tones =
        {
            new Tone("fair", new SkinRgb(234, 186, 148)),
            new Tone("light", new SkinRgb(198, 140, 100)),
            new Tone("medium", new SkinRgb(150, 96, 62)),
            new Tone("brown", new SkinRgb(124, 80, 52)),
            new Tone("deep", new SkinRgb(96, 60, 40)),
            new Tone("deepest", new SkinRgb(78, 48, 32))
        };

        /// <summary>Index of <paramref name="id"/> in <see cref="Outfits"/>, or -1.</summary>
        public static int FindOutfit(string id)
        {
            for (int i = 0; i < Outfits.Length; i++)
            {
                if (string.Equals(Outfits[i].id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Index of <paramref name="id"/> in <see cref="Tones"/>, or -1.</summary>
        public static int FindTone(string id)
        {
            for (int i = 0; i < Tones.Length; i++)
            {
                if (string.Equals(Tones[i].id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The outfit index <paramref name="appearance"/> shows (old saves: see the class summary).</summary>
        public static int OutfitIndex(AppearanceData appearance)
        {
            if (appearance == null)
            {
                return FindOutfit(DefaultOutfitId);
            }

            int index = FindOutfit(appearance.outfit);
            if (index >= 0)
            {
                return index;
            }

            return FindOutfit(appearance.genderPresentation == "masculine" ? StreetId : SkaterId);
        }

        /// <summary>The tone index <paramref name="appearance"/> shows ("medium" when unknown).</summary>
        public static int ToneIndex(AppearanceData appearance)
        {
            int index = appearance != null ? FindTone(appearance.skinTone) : -1;
            return index >= 0 ? index : FindTone(DefaultToneId);
        }

        public static Outfit OutfitOf(AppearanceData appearance) => Outfits[OutfitIndex(appearance)];

        public static Tone ToneOf(AppearanceData appearance) => Tones[ToneIndex(appearance)];

        /// <summary>A new appearance for the picked card and swatch (indices clamped); the other fields keep their
        /// defaults.</summary>
        public static AppearanceData Create(int outfitIndex, int toneIndex)
        {
            int o = Math.Max(0, Math.Min(Outfits.Length - 1, outfitIndex));
            int t = Math.Max(0, Math.Min(Tones.Length - 1, toneIndex));
            return new AppearanceData { outfit = Outfits[o].id, skinTone = Tones[t].id };
        }

        /// <summary>Writes the resolved outfit and tone back into <paramref name="appearance"/> (on load), so a save
        /// from before outfits keeps its look explicitly.</summary>
        public static void Normalize(AppearanceData appearance)
        {
            if (appearance == null)
            {
                return;
            }

            appearance.outfit = OutfitOf(appearance).id;
            appearance.skinTone = ToneOf(appearance).id;
        }

        /// <summary>
        /// Changes whenever anything that changes the player's look changes (the resolved outfit and tone, and the
        /// other appearance fields); PlayerIdentityBridge rebuilds the look only when it does.
        /// </summary>
        public static string Key(AppearanceData a)
        {
            if (a == null)
            {
                return "";
            }

            return string.Join("|", OutfitOf(a).id, ToneOf(a).id, a.hairstyle, a.hairColor, a.clothing, a.accessories,
                a.genderPresentation, a.bodyType);
        }

        /// <summary>Resources path of the outfit texture <paramref name="appearance"/> shows.</summary>
        public static string OutfitResourcePath(AppearanceData appearance) => OutfitResourceFolder + OutfitOf(appearance).id;
    }
}
