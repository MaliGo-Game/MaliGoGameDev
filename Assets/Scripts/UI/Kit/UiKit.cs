using System;
using System.Collections.Generic;
using MaliGo.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Procedural surfaces, icons, Mali art and the basic widgets of the MaliGo UI kit (DESIGN_SPEC §5.3, §5.4).
    ///
    /// Every sprite made here (rounded rect, soft shadow, circle, white, energy bolt, each icon, Mali art) is
    /// created once and cached by name; a cached entry that has become Unity-null is recreated. No code here
    /// uses the retired Kenney adventure panels.
    ///
    /// Rounded rects: one white 132 x 132 texture with a 64 px corner radius and a 1.5 px anti-aliased edge,
    /// sliced with a 64 px border and tinted by <c>Image.color</c>; a radius of r u is set with
    /// <c>image.pixelsPerUnitMultiplier = 64 / r</c> (<see cref="SetRadius"/>). The soft shadow (192 x 192,
    /// radius 48, 40 px falloff, border 88) shares that texture, so a panel's shadow is drawn in the panel's own
    /// mesh, behind it, and follows its size, alpha, layout and masking.
    /// </summary>
    public static class UiKit
    {
        public const string IconFolder = "MaliGo/Icons/";
        public const string MaliFolder = "MaliGo/Mali/";

        const int RectSize = 132;
        const int RectRadius = 64;
        const float RectEdge = 1.5f;
        const int ShadowSize = 192;
        const int ShadowRadius = 48;
        const int ShadowFalloff = 40;
        const int ShadowBorder = 88;
        const int AtlasGap = 4;
        const int CircleSize = 128;
        const int BoltSize = 128;
        const float SpritePixelsPerUnit = 100f;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly HashSet<string> missingWarned = new HashSet<string>();

        /// <summary>Raised by every kit button just before its own action (the UI click sound listens to it).</summary>
        public static event Action ButtonClicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ButtonClicked = null;
            missingWarned.Clear();
        }

        /// <summary>Raises <see cref="ButtonClicked"/>. For tappable views that are not kit buttons (choice cards,
        /// chips) so they sound the same.</summary>
        public static void NotifyButtonClicked()
        {
            ButtonClicked?.Invoke();
        }

        // ================================================================ sprites

        /// <summary>White rounded rect (radius 64 px, border 64), sliced; tint with <c>Image.color</c>.</summary>
        public static Sprite RoundedRect => Cached("roundedRect", () => MakeAtlasSprite(false));

        /// <summary>Soft shadow (rounded rect radius 48 with a 40 px falloff, border 88), same texture as
        /// <see cref="RoundedRect"/>.</summary>
        public static Sprite SoftShadow => Cached("softShadow", () => MakeAtlasSprite(true));

        /// <summary>128 px anti-aliased white disc.</summary>
        public static Sprite Circle => Cached("circle", MakeCircle);

        /// <summary>Plain white sprite for scrims, dividers and bars (use <c>Image.Type.Filled</c> for progress).</summary>
        public static Sprite White => Cached("white", MakeWhite);

        /// <summary>The procedural energy bolt (128 px, white; tint <see cref="UiTheme.Coin"/>).</summary>
        public static Sprite EnergyBolt => Cached("energyBolt", MakeBolt);

        /// <summary>Mali's portrait (Resources/MaliGo/Mali/MaliPortrait, 512 x 512). Until that file is prepared
        /// (tools/prepare_mali_art.py) a bust cut from <see cref="MaliWave"/> is used; null if neither exists.</summary>
        public static Sprite MaliPortrait => Cached("mali:MaliPortrait", MakeMaliPortrait);

        /// <summary>Mali waving, full body (Resources/MaliGo/Mali/MaliWave, 512 x 640); null if missing.</summary>
        public static Sprite MaliWave => Cached("mali:MaliWave", () => LoadSprite(MaliFolder + "MaliWave"));

        /// <summary>An icon from Resources/MaliGo/Icons (white; tint at runtime), or null if it is not shipped.</summary>
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return Cached("icon:" + name, () => LoadSprite(IconFolder + name));
        }

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (sprites.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            sprite = make();
            if (sprite != null)
            {
                sprite.name = key;
                sprites[key] = sprite;
            }
            else
            {
                sprites.Remove(key);
            }
            return sprite;
        }

        static Sprite LoadSprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                if (missingWarned.Add(resourcePath))
                {
                    Debug.LogWarning("[UiKit] Resources/" + resourcePath + " is missing.");
                }
                return null;
            }
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeMaliPortrait()
        {
            var texture = Resources.Load<Texture2D>(MaliFolder + "MaliPortrait");
            if (texture != null)
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                    SpritePixelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            // Fallback: head, scarf and coin from the 512 x 640 wave art (top 400 px, centred).
            var wave = Resources.Load<Texture2D>(MaliFolder + "MaliWave");
            if (wave == null)
            {
                if (missingWarned.Add("MaliPortrait"))
                {
                    Debug.LogWarning("[UiKit] Neither MaliPortrait nor MaliWave is in Resources/" + MaliFolder + ".");
                }
                return null;
            }
            float side = Mathf.Min(400f, wave.width, wave.height);
            var rect = new Rect((wave.width - side) * 0.5f, wave.height - side - Mathf.Min(10f, wave.height - side), side, side);
            return Sprite.Create(wave, rect, new Vector2(0.5f, 0.5f), SpritePixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        // ---------------------------------------------------------------- procedural textures

        static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        static Texture2D atlas;

        // Rounded rect at (0, 0) and soft shadow at (RectSize + AtlasGap, 0) in one texture. The gap is filled by
        // extending each region's edge pixels so bilinear sampling never bleeds transparent texels into an edge.
        static Texture2D Atlas()
        {
            if (atlas != null)
            {
                return atlas;
            }

            int width = RectSize + AtlasGap + ShadowSize;
            int height = ShadowSize;
            int shadowX = RectSize + AtlasGap;
            atlas = NewTexture(width, height, "UiKitAtlas");
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha;
                    if (x < RectSize + AtlasGap / 2)
                    {
                        int rx = Mathf.Min(x, RectSize - 1);
                        int ry = Mathf.Min(y, RectSize - 1);
                        alpha = RoundedRectAlpha(rx + 0.5f, ry + 0.5f);
                    }
                    else
                    {
                        int sx = Mathf.Max(x - shadowX, 0);
                        alpha = ShadowAlpha(sx + 0.5f, y + 0.5f);
                    }
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            return atlas;
        }

        static float RoundedRectSignedDistance(float px, float py, float size, float radius, float inset)
        {
            float half = size * 0.5f - inset;
            float qx = Mathf.Abs(px - size * 0.5f) - (half - radius);
            float qy = Mathf.Abs(py - size * 0.5f) - (half - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        static float RoundedRectAlpha(float px, float py)
        {
            float d = RoundedRectSignedDistance(px, py, RectSize, RectRadius, 0f);
            return Mathf.Clamp01(0.5f - d / RectEdge);
        }

        static float ShadowAlpha(float px, float py)
        {
            // Solid rounded rect inset by the falloff; a logistic (Gaussian-like) ramp across its edge reaching
            // about 0 at the texture edge and about 1 a falloff inside it.
            float d = RoundedRectSignedDistance(px, py, ShadowSize, ShadowRadius, ShadowFalloff);
            const float softness = ShadowFalloff / 5.3f;
            return 1f / (1f + Mathf.Exp(d / softness));
        }

        static Sprite MakeAtlasSprite(bool shadow)
        {
            Texture2D texture = Atlas();
            if (shadow)
            {
                return Sprite.Create(texture, new Rect(RectSize + AtlasGap, 0, ShadowSize, ShadowSize), new Vector2(0.5f, 0.5f),
                    SpritePixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(ShadowBorder, ShadowBorder, ShadowBorder, ShadowBorder));
            }
            return Sprite.Create(texture, new Rect(0, 0, RectSize, RectSize), new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(RectRadius, RectRadius, RectRadius, RectRadius));
        }

        static Sprite MakeCircle()
        {
            Texture2D texture = NewTexture(CircleSize, CircleSize, "UiKitCircle");
            var pixels = new Color32[CircleSize * CircleSize];
            float centre = CircleSize * 0.5f;
            float radius = CircleSize * 0.5f;
            for (int y = 0; y < CircleSize; y++)
            {
                for (int x = 0; x < CircleSize; x++)
                {
                    float dx = x + 0.5f - centre;
                    float dy = y + 0.5f - centre;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * CircleSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, CircleSize, CircleSize), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit,
                0, SpriteMeshType.FullRect);
        }

        static Sprite MakeWhite()
        {
            const int size = 4;
            Texture2D texture = NewTexture(size, size, "UiKitWhite");
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit, 0,
                SpriteMeshType.FullRect);
        }

        // The bolt polygon in unit coordinates (y up), DESIGN_SPEC §5.3.
        static readonly Vector2[] BoltPolygon =
        {
            new Vector2(0.58f, 1.00f), new Vector2(0.18f, 0.45f), new Vector2(0.46f, 0.45f),
            new Vector2(0.36f, 0.00f), new Vector2(0.84f, 0.58f), new Vector2(0.54f, 0.58f)
        };

        static Sprite MakeBolt()
        {
            Texture2D texture = NewTexture(BoltSize, BoltSize, "UiKitBolt");
            var pixels = new Color32[BoltSize * BoltSize];
            const int samples = 4; // 4 x 4 supersampling for the anti-aliased edge
            for (int y = 0; y < BoltSize; y++)
            {
                for (int x = 0; x < BoltSize; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / BoltSize;
                            float v = (y + (sy + 0.5f) / samples) / BoltSize;
                            if (InsidePolygon(BoltPolygon, u, v))
                            {
                                inside++;
                            }
                        }
                    }
                    byte alpha = (byte)Mathf.RoundToInt(255f * inside / (samples * samples));
                    pixels[y * BoltSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, BoltSize, BoltSize), new Vector2(0.5f, 0.5f), SpritePixelsPerUnit, 0,
                SpriteMeshType.FullRect);
        }

        static bool InsidePolygon(Vector2[] polygon, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // ================================================================ building blocks

        /// <summary>An empty child RectTransform stretched over <paramref name="parent"/>.</summary>
        public static RectTransform Rect(RectTransform parent, string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Rect" : name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                rect.SetParent(parent, false);
            }
            Stretch(rect);
            return rect;
        }

        /// <summary>Anchors <paramref name="rect"/> to fill its parent with no offsets.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A rounded panel stretched over <paramref name="parent"/> (resize it as needed), coloured
        /// <paramref name="c"/> with corner radius <paramref name="radius"/> u. With <paramref name="shadow"/>, a sheet
        /// shadow is drawn when the radius is at least <see cref="UiTheme.RadiusSheet"/>, else a card shadow.</summary>
        public static Image Panel(RectTransform parent, string name, Color c, float radius, bool shadow)
        {
            Image image = Panel(parent, name, c, radius);
            if (shadow)
            {
                AddShadow(image, radius >= UiTheme.RadiusSheet ? UiTheme.ShadowSheet : UiTheme.ShadowCard);
            }
            return image;
        }

        /// <summary>A rounded panel with an explicit shadow style.</summary>
        public static Image Panel(RectTransform parent, string name, Color c, float radius, UiTheme.ShadowStyle shadow)
        {
            Image image = Panel(parent, name, c, radius);
            AddShadow(image, shadow);
            return image;
        }

        static Image Panel(RectTransform parent, string name, Color c, float radius)
        {
            RectTransform rect = Rect(parent, string.IsNullOrEmpty(name) ? "Panel" : name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = c;
            SetRadius(image, radius);
            return image;
        }

        /// <summary>Gives <paramref name="image"/> the rounded-rect sprite (sliced) with corner radius
        /// <paramref name="radius"/> u (<c>pixelsPerUnitMultiplier = 64 / r</c>). A pill uses height / 2.</summary>
        public static void SetRadius(Image image, float radius)
        {
            if (image == null)
            {
                return;
            }
            image.sprite = RoundedRect;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = RectRadius / Mathf.Max(radius, 0.5f);
            image.SetVerticesDirty();
        }

        /// <summary>Adds (or updates) a procedural soft shadow drawn behind <paramref name="image"/>, inside its own
        /// mesh. The image must use the <see cref="RoundedRect"/> sprite (as every kit panel and button does).</summary>
        public static void AddShadow(Image image, UiTheme.ShadowStyle style)
        {
            if (image == null)
            {
                return;
            }
            var effect = image.GetComponent<SoftShadowEffect>();
            if (effect == null)
            {
                effect = image.gameObject.AddComponent<SoftShadowEffect>();
            }
            effect.Style = style;
        }

        /// <summary>Removes a shadow added by <see cref="AddShadow"/>.</summary>
        public static void RemoveShadow(Image image)
        {
            var effect = image != null ? image.GetComponent<SoftShadowEffect>() : null;
            if (effect != null)
            {
                UnityEngine.Object.Destroy(effect);
                image.SetVerticesDirty();
            }
        }

        /// <summary>A text label stretched over <paramref name="parent"/> in the given role (size, Aileron weight and
        /// line spacing) and colour. Wraps horizontally, never shrinks (no best fit), rich text on, not a raycast
        /// target. Text that may contain money should be set through <see cref="UiTextLayout.WrapKeepingAmounts"/>.</summary>
        public static Text Label(RectTransform parent, string name, string text, UiTheme.TextRole role, Color color,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            RectTransform rect = Rect(parent, string.IsNullOrEmpty(name) ? "Label" : name);
            var label = rect.gameObject.AddComponent<Text>();
            ApplyRole(label, role);
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.resizeTextForBestFit = false;
            label.alignByGeometry = false;
            label.raycastTarget = false;
            label.text = text ?? string.Empty;
            return label;
        }

        /// <summary>Applies a type role (font weight, size, line spacing) to an existing Text.</summary>
        public static void ApplyRole(Text label, UiTheme.TextRole role)
        {
            if (label == null)
            {
                return;
            }
            label.font = UiFonts.Get(role.Weight);
            label.fontStyle = FontStyle.Normal;
            label.fontSize = Mathf.Max(role.Size, UiTheme.TextFloor);
            label.lineSpacing = role.LineSpacing / UiTheme.FontLineHeightEm;
        }

        /// <summary>An icon image (<paramref name="size"/> u square, centred in <paramref name="parent"/>, aspect kept,
        /// not a raycast target) tinted <paramref name="color"/>. A missing icon leaves the image empty and clear.</summary>
        public static Image IconImage(RectTransform parent, string name, string iconName, float size, Color color)
        {
            return SpriteImage(parent, string.IsNullOrEmpty(name) ? "Icon " + iconName : name, Icon(iconName), size, color);
        }

        /// <summary>An image of any kit sprite (<see cref="EnergyBolt"/>, <see cref="Circle"/>, Mali art...) sized
        /// <paramref name="size"/> u square and centred, aspect kept, not a raycast target.</summary>
        public static Image SpriteImage(RectTransform parent, string name, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "Image" : name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                rect.SetParent(parent, false);
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = sprite != null ? color : Color.clear;
            return image;
        }

        // ================================================================ buttons

        /// <summary>Filled forest-green button (radius 28, Button 44 Bold on <see cref="UiTheme.TextOnInverse"/>).
        /// <paramref name="w"/> x <paramref name="h"/> u, centred on its anchor; the tap area is at least 144 x 144.</summary>
        public static Button PrimaryButton(RectTransform parent, string label, Action onClick, float w, float h = 144f)
        {
            Button button = ButtonBase(parent, label, onClick, w, h, out Image background);
            background.color = UiTheme.AccentPrimary;
            SetRadius(background, UiTheme.RadiusButton);
            button.targetGraphic = background;
            button.colors = Tint(PressedTint(UiTheme.AccentPrimary, UiTheme.AccentPressed));
            AddButtonLabel(button, label, UiTheme.TextOnInverse);
            return button;
        }

        /// <summary>Outlined button: card fill inside a 4 u <see cref="UiTheme.BorderControl"/> outline, radius 28,
        /// <see cref="UiTheme.TextPrimary"/> label.</summary>
        public static Button SecondaryButton(RectTransform parent, string label, Action onClick, float w, float h = 144f)
        {
            Button button = ButtonBase(parent, label, onClick, w, h, out Image background);
            background.color = UiTheme.BorderControl;
            SetRadius(background, UiTheme.RadiusButton);
            Image fill = Panel((RectTransform)background.transform, "Fill", UiTheme.Card, UiTheme.RadiusButton - UiTheme.Stroke);
            fill.rectTransform.offsetMin = new Vector2(UiTheme.Stroke, UiTheme.Stroke);
            fill.rectTransform.offsetMax = new Vector2(-UiTheme.Stroke, -UiTheme.Stroke);
            fill.raycastTarget = false;
            button.targetGraphic = fill;
            button.colors = Tint(0.92f);
            AddButtonLabel(button, label, UiTheme.TextPrimary);
            return button;
        }

        /// <summary>A text-only button (no fill) with an <see cref="UiTheme.AccentPrimary"/> label.</summary>
        public static Button TextButton(RectTransform parent, string label, Action onClick, float w, float h = 144f)
        {
            Button button = ButtonBase(parent, label, onClick, w, h, out Image background);
            background.sprite = White;
            background.color = Color.clear;
            Text text = AddButtonLabel(button, label, UiTheme.AccentPrimary);
            button.targetGraphic = text;
            button.colors = Tint(0.75f);
            return button;
        }

        static Button ButtonBase(RectTransform parent, string label, Action onClick, float w, float h, out Image background)
        {
            var go = new GameObject("Button " + (label ?? string.Empty), typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
                rect.SetParent(parent, false);
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = Vector2.zero;

            background = go.AddComponent<Image>();
            background.raycastTarget = true;

            // Invisible padding so the tap target is never under 144 x 144 u.
            if (w < UiTheme.TargetMin || h < UiTheme.TargetMin)
            {
                var hit = new GameObject("HitArea", typeof(RectTransform));
                hit.layer = go.layer;
                var hitRect = (RectTransform)hit.transform;
                hitRect.SetParent(rect, false);
                hitRect.SetAsFirstSibling();
                hitRect.anchorMin = hitRect.anchorMax = hitRect.pivot = new Vector2(0.5f, 0.5f);
                hitRect.sizeDelta = new Vector2(Mathf.Max(w, UiTheme.TargetMin), Mathf.Max(h, UiTheme.TargetMin));
                var hitImage = hit.AddComponent<Image>();
                hitImage.sprite = White;
                hitImage.color = Color.clear;
                hitImage.raycastTarget = true;
            }

            var button = go.AddComponent<Button>();
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() =>
            {
                NotifyButtonClicked();
                onClick?.Invoke();
            });
            go.AddComponent<PressFeedback>();
            return button;
        }

        static Text AddButtonLabel(Button button, string label, Color color)
        {
            var rect = (RectTransform)button.transform;
            Text text = Label(rect, "Label", label, UiTheme.Button, color, TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.rectTransform.offsetMin = new Vector2(24f, 0f);
            text.rectTransform.offsetMax = new Vector2(-24f, 0f);
            return text;
        }

        static float PressedTint(Color normal, Color pressed)
        {
            float a = Mathf.Max(normal.r, normal.g, normal.b);
            float b = Mathf.Max(pressed.r, pressed.g, pressed.b);
            return a > 0f ? Mathf.Clamp01(b / a) : 0.8f;
        }

        static ColorBlock Tint(float pressed)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(pressed, pressed, pressed, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = UiTheme.Motion.Press;
            return colors;
        }

        // ================================================================ components

        /// <summary>Press feedback: scales the button to 0.97 over 100 ms (ease-out, unscaled time) while held.
        /// The darken comes from the button's colour tint. Off when reduce motion is on.</summary>
        sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            Selectable selectable;
            float target = 1f;
            float current = 1f;

            void Awake()
            {
                selectable = GetComponent<Selectable>();
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (selectable != null && !selectable.IsInteractable())
                {
                    return;
                }
                target = GameSettings.ReduceMotion ? 1f : UiTheme.Motion.PressScale;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                target = 1f;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                target = 1f;
            }

            void OnDisable()
            {
                target = current = 1f;
                transform.localScale = Vector3.one;
            }

            void Update()
            {
                if (Mathf.Approximately(current, target))
                {
                    return;
                }
                float step = (1f - UiTheme.Motion.PressScale) * Time.unscaledDeltaTime / UiTheme.Motion.Press;
                current = Mathf.MoveTowards(current, target, step);
                transform.localScale = new Vector3(current, current, 1f);
            }
        }

        /// <summary>Draws the soft-shadow sprite as a 9-sliced quad behind the graphic, in the same mesh (the
        /// graphic's texture must be the kit atlas). Offset and blur in u from <see cref="UiTheme.ShadowStyle"/>.</summary>
        sealed class SoftShadowEffect : BaseMeshEffect
        {
            UiTheme.ShadowStyle style = UiTheme.ShadowCard;
            static readonly List<UIVertex> Stream = new List<UIVertex>();
            static readonly List<UIVertex> Combined = new List<UIVertex>();

            public UiTheme.ShadowStyle Style
            {
                get => style;
                set
                {
                    style = value;
                    if (graphic != null)
                    {
                        graphic.SetVerticesDirty();
                    }
                }
            }

            public override void ModifyMesh(VertexHelper vh)
            {
                if (!IsActive() || vh.currentVertCount == 0)
                {
                    return;
                }
                Sprite shadow = SoftShadow;
                if (shadow == null || graphic == null || graphic.mainTexture != shadow.texture)
                {
                    return;
                }

                Stream.Clear();
                Combined.Clear();
                vh.GetUIVertexStream(Stream);

                Rect r = graphic.rectTransform.rect;
                float blur = Mathf.Max(style.Blur, 1f);
                float xMin = r.xMin - blur + style.Offset.x;
                float xMax = r.xMax + blur + style.Offset.x;
                float yMin = r.yMin - blur + style.Offset.y;
                float yMax = r.yMax + blur + style.Offset.y;

                // The texture's 88 px border holds the 40 px falloff; scale it so the falloff spans the blur.
                float border = ShadowBorder * blur / ShadowFalloff;
                float bx = Mathf.Min(border, (xMax - xMin) * 0.5f);
                float by = Mathf.Min(border, (yMax - yMin) * 0.5f);

                Vector4 outer = UnityEngine.Sprites.DataUtility.GetOuterUV(shadow);
                Vector4 inner = UnityEngine.Sprites.DataUtility.GetInnerUV(shadow);
                float[] xs = { xMin, xMin + bx, xMax - bx, xMax };
                float[] ys = { yMin, yMin + by, yMax - by, yMax };
                float[] us = { outer.x, inner.x, inner.z, outer.z };
                float[] vs = { outer.y, inner.y, inner.w, outer.w };

                Color color = style.Color;
                color.a *= graphic.color.a;
                Color32 color32 = color;

                for (int ix = 0; ix < 3; ix++)
                {
                    for (int iy = 0; iy < 3; iy++)
                    {
                        UIVertex a = Vertex(xs[ix], ys[iy], us[ix], vs[iy], color32);
                        UIVertex b = Vertex(xs[ix], ys[iy + 1], us[ix], vs[iy + 1], color32);
                        UIVertex c = Vertex(xs[ix + 1], ys[iy + 1], us[ix + 1], vs[iy + 1], color32);
                        UIVertex d = Vertex(xs[ix + 1], ys[iy], us[ix + 1], vs[iy], color32);
                        Combined.Add(a);
                        Combined.Add(b);
                        Combined.Add(c);
                        Combined.Add(c);
                        Combined.Add(d);
                        Combined.Add(a);
                    }
                }

                Combined.AddRange(Stream);
                vh.Clear();
                vh.AddUIVertexTriangleStream(Combined);
                Stream.Clear();
                Combined.Clear();
            }

            static UIVertex Vertex(float x, float y, float u, float v, Color32 color)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(x, y, 0f);
                vertex.uv0 = new Vector4(u, v, 0f, 0f);
                vertex.color = color;
                return vertex;
            }
        }
    }
}
