using System;

namespace MaliGo.Data
{
    /// <summary>An 8-bit sRGB colour (kept free of UnityEngine so the recolour can be tested outside Unity).</summary>
    [Serializable]
    public struct SkinRgb
    {
        public byte r;
        public byte g;
        public byte b;

        public SkinRgb(byte r, byte g, byte b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }

    /// <summary>
    /// Shows an outfit texture in a chosen skin tone, texel by texel (once per selection, never per frame).
    ///
    /// Skin mask. The outfit textures (tools/make_character_skins.py) carry a skin mask in their alpha: 255 = not
    /// skin; 255 - round(127 w) for skin weight w, so <see cref="FullSkinAlpha"/> (128) = all skin and alpha never
    /// drops below it. The weight is partial only on anti-aliased edges (skin next to hair, eyes, a collar).
    /// A texture without a mask (every alpha 255) is classified by colour with <see cref="SkinWeightFromColour"/>,
    /// the same rule the tool uses: a texel is skin when its per-channel ratio to the outfit's flat skin colour is
    /// nearly uniform (mean ratio k in [<see cref="RatioMin"/>, <see cref="RatioMax"/>], every channel within
    /// <see cref="RatioTolerance"/> of k). That keeps the lighter/darker shades and the ear, nose and lip accents,
    /// and rejects shirts and hair in nearby hues, whose channels scale unevenly.
    ///
    /// Recolour. All-skin texels: tone x texel / base per channel (keeps the shading and accents). Edge texels:
    /// texel + w (tone - base), the exact answer for a texel that mixes the base skin with another colour.
    /// </summary>
    public static class SkinToneMath
    {
        public const byte NotSkinAlpha = 255;
        public const byte FullSkinAlpha = 128;
        public const float RatioMin = 0.8f;
        public const float RatioMax = 1.2f;
        public const float RatioTolerance = 0.12f;

        /// <summary>Skin weight in [0, 1] stored in a mask alpha.</summary>
        public static float WeightFromAlpha(byte alpha)
        {
            if (alpha >= NotSkinAlpha)
            {
                return 0f;
            }

            if (alpha <= FullSkinAlpha)
            {
                return 1f;
            }

            return (NotSkinAlpha - alpha) / 127f;
        }

        /// <summary>The mask alpha for a skin weight (the inverse of <see cref="WeightFromAlpha"/>).</summary>
        public static byte AlphaFromWeight(float weight)
        {
            if (weight <= 0f)
            {
                return NotSkinAlpha;
            }

            return (byte)(NotSkinAlpha - (int)Math.Round(Math.Min(1f, weight) * 127f));
        }

        /// <summary>1 when <paramref name="c"/> is a shade of <paramref name="skinBase"/> (see the class summary), else 0.</summary>
        public static float SkinWeightFromColour(SkinRgb c, SkinRgb skinBase)
        {
            if (skinBase.r == 0 || skinBase.g == 0 || skinBase.b == 0)
            {
                return 0f;
            }

            float rr = c.r / (float)skinBase.r;
            float rg = c.g / (float)skinBase.g;
            float rb = c.b / (float)skinBase.b;
            float k = (rr + rg + rb) / 3f;
            if (k < RatioMin || k > RatioMax)
            {
                return 0f;
            }

            float deviation = Math.Max(Math.Abs(rr - k), Math.Max(Math.Abs(rg - k), Math.Abs(rb - k)));
            return deviation <= RatioTolerance ? 1f : 0f;
        }

        /// <summary>The colour of one texel with skin weight <paramref name="weight"/> in <paramref name="tone"/>.</summary>
        public static SkinRgb Recolour(SkinRgb texel, float weight, SkinRgb skinBase, SkinRgb tone)
        {
            if (weight <= 0f)
            {
                return texel;
            }

            if (weight >= 1f)
            {
                return new SkinRgb(Scale(texel.r, tone.r, skinBase.r), Scale(texel.g, tone.g, skinBase.g),
                    Scale(texel.b, tone.b, skinBase.b));
            }

            return new SkinRgb(Shift(texel.r, tone.r, skinBase.r, weight), Shift(texel.g, tone.g, skinBase.g, weight),
                Shift(texel.b, tone.b, skinBase.b, weight));
        }

        /// <summary>
        /// Recolours a whole texture in place: <paramref name="rgba"/> holds 4 bytes per texel (r, g, b, mask alpha),
        /// and every alpha is set to 255 afterwards. When no texel carries a mask (all alpha 255) the skin is found by
        /// colour instead. Returns the number of texels treated as skin (any weight).
        /// </summary>
        public static int RecolourTexels(byte[] rgba, SkinRgb skinBase, SkinRgb tone)
        {
            if (rgba == null)
            {
                return 0;
            }

            bool masked = false;
            for (int i = 3; i < rgba.Length; i += 4)
            {
                if (rgba[i] < NotSkinAlpha)
                {
                    masked = true;
                    break;
                }
            }

            int skin = 0;
            for (int i = 0; i + 3 < rgba.Length; i += 4)
            {
                var texel = new SkinRgb(rgba[i], rgba[i + 1], rgba[i + 2]);
                float weight = masked ? WeightFromAlpha(rgba[i + 3]) : SkinWeightFromColour(texel, skinBase);
                if (weight > 0f)
                {
                    SkinRgb result = Recolour(texel, weight, skinBase, tone);
                    rgba[i] = result.r;
                    rgba[i + 1] = result.g;
                    rgba[i + 2] = result.b;
                    skin++;
                }

                rgba[i + 3] = NotSkinAlpha;
            }

            return skin;
        }

        static byte Scale(byte value, byte tone, byte skinBase)
        {
            if (skinBase == 0)
            {
                return tone;
            }

            return Clamp((int)Math.Round(tone * value / (double)skinBase));
        }

        static byte Shift(byte value, byte tone, byte skinBase, float weight)
        {
            return Clamp((int)Math.Round(value + weight * (tone - skinBase)));
        }

        static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
    }
}
