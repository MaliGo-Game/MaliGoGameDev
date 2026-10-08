using System.Collections.Generic;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    /// <summary>
    /// Runtime skin textures: an outfit (<see cref="CharacterLooks.Outfits"/>) recoloured into a skin tone, made on
    /// first use and shared by reference count, so the world player and the look preview showing the same look use
    /// one texture, and a texture nobody uses any more is destroyed straight away.
    ///
    /// Making one: the outfit's Read/Write source (Resources/MaliGo/Characters/Outfits/{id}, 512 x 512 with the skin
    /// mask in alpha) is read once, recoloured by <see cref="SkinToneMath"/> (optionally halved, for the small card
    /// thumbnails), copied into a new mipmapped RGB24 texture whose CPU copy is dropped after upload, and the source
    /// is unloaded again. About 1 MB of GPU memory per full-size look, 0.25 MB per thumbnail.
    /// </summary>
    public static class CharacterSkins
    {
        sealed class Entry
        {
            public string key;
            public Texture2D texture;
            public int references;
        }

        static readonly Dictionary<string, Entry> ByKey = new Dictionary<string, Entry>();
        static readonly Dictionary<Texture2D, Entry> ByTexture = new Dictionary<Texture2D, Entry>();
        static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>Number of runtime skin textures alive (for diagnostics).</summary>
        public static int LiveCount => ByKey.Count;

        /// <summary>Forgets the cache when play starts without a domain reload (Editor fast enter-play-mode).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ByKey.Clear();
            ByTexture.Clear();
            Warned.Clear();
        }

        /// <summary>
        /// The texture for <paramref name="appearance"/>'s outfit in its tone, adding a reference (pair every call
        /// with <see cref="Release"/>). <paramref name="half"/> makes a half-size copy for thumbnails. Null when the
        /// outfit texture is missing or not readable (the caller falls back to the stock skins).
        /// </summary>
        public static Texture2D Acquire(AppearanceData appearance, bool half = false)
        {
            CharacterLooks.Outfit outfit = CharacterLooks.OutfitOf(appearance);
            CharacterLooks.Tone tone = CharacterLooks.ToneOf(appearance);
            string key = outfit.id + "|" + tone.id + (half ? "|half" : "");
            if (ByKey.TryGetValue(key, out Entry existing) && existing.texture != null)
            {
                existing.references++;
                return existing.texture;
            }

            Texture2D texture = Build(outfit, tone, half, key);
            if (texture == null)
            {
                return null;
            }

            var entry = new Entry { key = key, texture = texture, references = 1 };
            ByKey[key] = entry;
            ByTexture[texture] = entry;
            return texture;
        }

        /// <summary>Drops one reference to <paramref name="texture"/>; destroys it at zero. Textures this class did
        /// not make (the stock skins) are ignored.</summary>
        public static void Release(Texture texture)
        {
            if (!(texture is Texture2D texture2D) || !ByTexture.TryGetValue(texture2D, out Entry entry))
            {
                return;
            }

            entry.references--;
            if (entry.references > 0)
            {
                return;
            }

            ByTexture.Remove(texture2D);
            ByKey.Remove(entry.key);
            Object.Destroy(texture2D);
        }

        static Texture2D Build(CharacterLooks.Outfit outfit, CharacterLooks.Tone tone, bool half, string key)
        {
            string path = CharacterLooks.OutfitResourceFolder + outfit.id;
            Texture2D source = Resources.Load<Texture2D>(path);
            if (source == null)
            {
                WarnOnce(path, "[CharacterSkins] No outfit texture at Resources/" + path + "; using the stock skin.");
                return null;
            }

            if (!source.isReadable)
            {
                WarnOnce(path, "[CharacterSkins] Resources/" + path + " is not Read/Write enabled; using the stock skin.");
                Resources.UnloadAsset(source);
                return null;
            }

            int width = source.width;
            int height = source.height;
            Color32[] pixels = source.GetPixels32();
            Resources.UnloadAsset(source);

            if (half && width >= 2 && height >= 2)
            {
                pixels = Halve(pixels, width, height);
                width /= 2;
                height /= 2;
            }

            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                int j = i * 4;
                bytes[j] = c.r;
                bytes[j + 1] = c.g;
                bytes[j + 2] = c.b;
                bytes[j + 3] = c.a;
            }

            SkinToneMath.RecolourTexels(bytes, outfit.skinBase, tone.colour);

            for (int i = 0; i < pixels.Length; i++)
            {
                int j = i * 4;
                pixels[i] = new Color32(bytes[j], bytes[j + 1], bytes[j + 2], 255);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, true)
            {
                name = "Skin_" + key.Replace('|', '_'),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>2 x 2 box downsample. The mask alpha is averaged too, which keeps it a valid skin weight.</summary>
        static Color32[] Halve(Color32[] pixels, int width, int height)
        {
            int w = width / 2;
            int h = height / 2;
            var result = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int row0 = 2 * y * width;
                int row1 = row0 + width;
                for (int x = 0; x < w; x++)
                {
                    Color32 a = pixels[row0 + 2 * x];
                    Color32 b = pixels[row0 + 2 * x + 1];
                    Color32 c = pixels[row1 + 2 * x];
                    Color32 d = pixels[row1 + 2 * x + 1];
                    result[y * w + x] = new Color32(
                        (byte)((a.r + b.r + c.r + d.r + 2) / 4),
                        (byte)((a.g + b.g + c.g + d.g + 2) / 4),
                        (byte)((a.b + b.b + c.b + d.b + 2) / 4),
                        (byte)((a.a + b.a + c.a + d.a + 2) / 4));
                }
            }

            return result;
        }

        static void WarnOnce(string path, string message)
        {
            if (Warned.Add(path))
            {
                Debug.LogWarning(message);
            }
        }
    }
}
