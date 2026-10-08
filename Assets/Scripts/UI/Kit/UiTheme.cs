using UnityEngine;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Design tokens of the MaliGo UI kit (DESIGN_SPEC §5.1). Every size is in canvas units ("u") on the
    /// 1920 x 1080 reference canvas with matchWidthOrHeight = 1, so the canvas is always 1080 u tall.
    /// Colours are flat members (<c>UiTheme.Paper</c>), type roles are <see cref="TextRole"/> members
    /// (<c>UiTheme.Body</c>), motion durations live in <see cref="Motion"/> and canvas sort orders in
    /// <see cref="Sort"/>.
    /// </summary>
    public static class UiTheme
    {
        // ---------------------------------------------------------------- colour

        /// <summary>#FBF6EC: sheets, dialogue box, character-creation sheet.</summary>
        public static readonly Color Paper = Hex(0xFBF6EC);
        /// <summary>#FFFFFF: choice cards, ledger card.</summary>
        public static readonly Color Card = Hex(0xFFFFFF);
        /// <summary>#FFF4E9: Mali's box, coach bubble.</summary>
        public static readonly Color Warm = Hex(0xFFF4E9);
        /// <summary>#F2EDDC: status cards, chips, delta pill, toggles off.</summary>
        public static readonly Color Sunken = Hex(0xF2EDDC);
        /// <summary>#EEF8E8: selected card fill.</summary>
        public static readonly Color Tint = Hex(0xEEF8E8);
        /// <summary>#0F5E2E: reveal backdrop, character-creation backdrop (opaque).</summary>
        public static readonly Color Inverse = Hex(0x0F5E2E);
        /// <summary>#0F5E2E at 94%: HUD pills and the pause button.</summary>
        public static readonly Color InverseHud = Hex(0x0F5E2E, 0.94f);
        /// <summary>#102217 at 55%: behind sheets.</summary>
        public static readonly Color Scrim = Hex(0x102217, 0.55f);
        /// <summary>#102217 at 25%: behind Mali's blocking box.</summary>
        public static readonly Color ScrimLight = Hex(0x102217, 0.25f);

        /// <summary>#101010: text on paper and card.</summary>
        public static readonly Color TextPrimary = Hex(0x101010);
        /// <summary>#344330: supporting text.</summary>
        public static readonly Color TextSecondary = Hex(0x344330);
        /// <summary>#52604E: labels and captions (32 u or larger).</summary>
        public static readonly Color TextMuted = Hex(0x52604E);
        /// <summary>#F9FFF6: text on forest green.</summary>
        public static readonly Color TextOnInverse = Hex(0xF9FFF6);
        /// <summary>#C9EFB4: labels on forest green.</summary>
        public static readonly Color TextOnInverseMuted = Hex(0xC9EFB4);

        /// <summary>#087A18: primary buttons, progress, toggles on.</summary>
        public static readonly Color AccentPrimary = Hex(0x087A18);
        /// <summary>#075F15: primary button pressed.</summary>
        public static readonly Color AccentPressed = Hex(0x075F15);
        /// <summary>#DFA464: Mali's name tag, focus ring alternative, gold circle. Never text on paper.</summary>
        public static readonly Color Gold = Hex(0xDFA464);
        /// <summary>#F4C84A: energy bar, coin icon tint, focus ring.</summary>
        public static readonly Color Coin = Hex(0xF4C84A);

        /// <summary>#087A18: "+" amounts and in-arrows on paper.</summary>
        public static readonly Color MoneyIn = Hex(0x087A18);
        /// <summary>#8A5633: "−" amounts and out-arrows on paper (deliberately not red).</summary>
        public static readonly Color MoneyOut = Hex(0x8A5633);
        /// <summary>#344330: transfers.</summary>
        public static readonly Color MoneyTransfer = Hex(0x344330);
        /// <summary>#C9EFB4: HUD "+" delta tags.</summary>
        public static readonly Color MoneyInOnInverse = Hex(0xC9EFB4);
        /// <summary>#F8E4D6: HUD "−" delta tags.</summary>
        public static readonly Color MoneyOutOnInverse = Hex(0xF8E4D6);

        /// <summary>#A8432F: only arrears ("still owed") and the reset confirm.</summary>
        public static readonly Color Attention = Hex(0xA8432F);
        /// <summary>#E85F48: non-text dot on the HUD bill pill when something is owed.</summary>
        public static readonly Color AttentionDot = Hex(0xE85F48);

        /// <summary>#EAD7CB: dividers.</summary>
        public static readonly Color BorderSubtle = Hex(0xEAD7CB);
        /// <summary>#B47B41: outlines of secondary buttons and toggles (<see cref="Stroke"/> wide).</summary>
        public static readonly Color BorderControl = Hex(0xB47B41);
        /// <summary>#102217: every shadow (alpha from the shadow token).</summary>
        public static readonly Color Shadow = Hex(0x102217);

        // ---------------------------------------------------------------- type

        /// <summary>A type role: size in u, Aileron weight and line spacing (line height / font size).</summary>
        public readonly struct TextRole
        {
            public readonly int Size;
            public readonly UiFontWeight Weight;
            public readonly float LineSpacing;

            public TextRole(int size, UiFontWeight weight, float lineSpacing)
            {
                Size = size;
                Weight = weight;
                LineSpacing = lineSpacing;
            }

            /// <summary>The same role in another weight (e.g. the HUD's Bold 40 day line).</summary>
            public TextRole WithWeight(UiFontWeight weight) => new TextRole(Size, weight, LineSpacing);

            /// <summary>The same weight and spacing at another size (never below <see cref="TextFloor"/>).</summary>
            public TextRole WithSize(int size) => new TextRole(Mathf.Max(size, TextFloor), Weight, LineSpacing);
        }

        /// <summary>104 Black 1.0: reveal and chapter-end amounts.</summary>
        public static readonly TextRole DisplayAmount = new TextRole(104, UiFontWeight.Black, 1.0f);
        /// <summary>72 Black 1.1: "Seven days to payday".</summary>
        public static readonly TextRole DisplayTitle = new TextRole(72, UiFontWeight.Black, 1.1f);
        /// <summary>56 Black 1.15: sheet and scenario titles.</summary>
        public static readonly TextRole Title = new TextRole(56, UiFontWeight.Black, 1.15f);
        /// <summary>46 SemiBold 1.3: Mali, blocking box.</summary>
        public static readonly TextRole Dialogue = new TextRole(46, UiFontWeight.SemiBold, 1.3f);
        /// <summary>44 Bold 1.15: choice labels.</summary>
        public static readonly TextRole ChoiceLabel = new TextRole(44, UiFontWeight.Bold, 1.15f);
        /// <summary>44 Bold 1.0: buttons.</summary>
        public static readonly TextRole Button = new TextRole(44, UiFontWeight.Bold, 1.0f);
        /// <summary>44 Black 1.0: HUD numbers.</summary>
        public static readonly TextRole HudValue = new TextRole(44, UiFontWeight.Black, 1.0f);
        /// <summary>44 Bold 1.0: ledger numbers.</summary>
        public static readonly TextRole LedgerValue = new TextRole(44, UiFontWeight.Bold, 1.0f);
        /// <summary>40 SemiBold 1.3: situations, sheet copy, compact Mali box.</summary>
        public static readonly TextRole Body = new TextRole(40, UiFontWeight.SemiBold, 1.3f);
        /// <summary>36 Bold 1.2: ledger header, name tag, short labels.</summary>
        public static readonly TextRole Label = new TextRole(36, UiFontWeight.Bold, 1.2f);
        /// <summary>32 SemiBold 1.25: the floor (HUD labels, column headers, hints).</summary>
        public static readonly TextRole Caption = new TextRole(32, UiFontWeight.SemiBold, 1.25f);

        /// <summary>No text is ever smaller than this (u). Copy is shortened instead of shrunk.</summary>
        public const int TextFloor = 32;

        /// <summary>Aileron's natural line height in em (hhea ascent 970 + descent 230, line gap 0, UPM 1000).
        /// Unity's <c>Text.lineSpacing</c> is a multiple of it, so a role's spacing is divided by this.</summary>
        public const float FontLineHeightEm = 1.2f;

        // ---------------------------------------------------------------- shape and spacing

        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public const float RadiusButton = 28f;
        public const float RadiusCard = 36f;
        public const float RadiusSheet = 48f;

        /// <summary>Radius of a pill: half its height.</summary>
        public static float RadiusPill(float height) => height * 0.5f;

        public const float Space10 = 10f;
        public const float Space20 = 20f;
        public const float Space30 = 30f;
        public const float Space40 = 40f;
        public const float Space60 = 60f;
        public const float Space80 = 80f;

        public const float SheetPadding = 48f;
        /// <summary>Minimum gap between two tappable elements.</summary>
        public const float TappableGap = 20f;
        /// <summary>HUD margin inside the safe area.</summary>
        public const float HudMargin = 24f;
        /// <summary>Margin of every other screen inside the safe area.</summary>
        public const float ScreenMargin = 40f;
        /// <summary>Minimum touch target, 144 u (about 48 dp on a 1600 x 720, 2.0-density phone).</summary>
        public const float TargetMin = 144f;
        /// <summary>Outline width of secondary buttons and toggles.</summary>
        public const float Stroke = 4f;

        /// <summary>A procedural shadow: offset (u), blur (u) and opacity of <see cref="UiTheme.Shadow"/>.</summary>
        public readonly struct ShadowStyle
        {
            public readonly Vector2 Offset;
            public readonly float Blur;
            public readonly float Opacity;

            public ShadowStyle(Vector2 offset, float blur, float opacity)
            {
                Offset = offset;
                Blur = blur;
                Opacity = opacity;
            }

            public Color Color => new Color(Shadow.r, Shadow.g, Shadow.b, Opacity);
        }

        /// <summary>Cards: (0, −12), blur 32, 10%.</summary>
        public static readonly ShadowStyle ShadowCard = new ShadowStyle(new Vector2(0f, -12f), 32f, 0.10f);
        /// <summary>Sheets: (0, −20), blur 56, 18%.</summary>
        public static readonly ShadowStyle ShadowSheet = new ShadowStyle(new Vector2(0f, -20f), 56f, 0.18f);

        // ---------------------------------------------------------------- motion

        /// <summary>Motion tokens in seconds, all on unscaled time. With reduce motion every transition is a
        /// <see cref="ReducedCrossfade"/> crossfade and numbers jump (<see cref="UiTween"/> applies this).</summary>
        public static class Motion
        {
            /// <summary>100 ms ease-out: scale 0.97 and darken.</summary>
            public const float Press = 0.10f;
            public const float PressScale = 0.97f;
            /// <summary>200 ms standard: prompts, tags.</summary>
            public const float Fade = 0.20f;
            /// <summary>300 ms decelerate: sheets rise <see cref="SheetRise"/> u.</summary>
            public const float SheetIn = 0.30f;
            /// <summary>200 ms accelerate.</summary>
            public const float SheetOut = 0.20f;
            public const float SheetRise = 40f;
            /// <summary>700 ms decelerate number count-up (at most <see cref="CountMax"/>).</summary>
            public const float Count = 0.70f;
            public const float CountMax = 1.20f;
            /// <summary>120 ms per ledger row.</summary>
            public const float Stagger = 0.12f;
            /// <summary>400 ms standard: reveal backdrop.</summary>
            public const float Backdrop = 0.40f;
            /// <summary>1 200 ms sine loop of 6 u: dialogue continue cue.</summary>
            public const float CueBob = 1.20f;
            public const float CueBobDistance = 6f;
            /// <summary>HUD delta tags: hold 1 600 ms, then fade 200 ms.</summary>
            public const float DeltaTagHold = 1.60f;
            public const float DeltaTagFade = 0.20f;
            /// <summary>Every transition when reduce motion is on.</summary>
            public const float ReducedCrossfade = 0.15f;
        }

        // ---------------------------------------------------------------- sort orders

        /// <summary>One Screen Space Overlay canvas per layer (DESIGN_SPEC §5.1).</summary>
        public static class Sort
        {
            public const int CharacterCreation = 0;
            public const int WorldPrompt = 10;
            public const int Hud = 20;
            public const int MobileControls = 25;
            public const int MaliDialogue = 40;
            public const int Sheets = 50;
            public const int SleepConfirm = 55;
            public const int Reveal = 60;
            public const int ChapterEnd = 60;
            public const int Pause = 70;
            public const int ResetConfirm = 75;
            public const int CoachMarks = 80;
            /// <summary>The quick fade when going through a door (walk-in rooms).</summary>
            public const int DoorFade = 85;
            public const int Notices = 90;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The colour with another alpha.</summary>
        public static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }
    }
}
