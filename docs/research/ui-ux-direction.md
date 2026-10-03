# MaliGo UI kit: visual and interaction direction

Research report for the Phase 2 design spec (`docs/DESIGN_SPEC.md`). Written 3 Oct 2026, read-only on code.
Scope: how the new runtime-built uGUI kit should look, read and move on a landscape phone, with exact tokens and per-component specs.

**Direction in one line:** *A calm, paper-and-forest "money journal" with Mali beside you.* Cream paper surfaces, deep-forest chrome, gold used sparingly as Mali's colour, big honest numbers, and no fantasy-RPG frames.

---

## 0. Summary of what matters most

1. **Text and buttons are far too small today.** I measured the canvas setup in code. Under the current scaler, the Mali dialogue body (22 units) shows at about **9-11 sp** on a phone, and the choice consequence lines (15 units) at about **6-7 sp**. The Home/Bank close button (44x36 units) is about **17-22 dp**, and the minimum is 48 dp. See §2.
2. **Use one scale rule: 1 canvas unit is about 0.4 dp on a landscape phone (with `matchWidthOrHeight = 1`).** Multiply any dp or sp value by **2.5** to get Unity units. Body text is then 40 u, dialogue 46 u, and the smallest touch target 120 u.
3. **Safe area is not handled at all.** `ProjectSettings.asset:71` sets `androidRenderOutsideSafeArea: 1`, and no script reads `Screen.safeArea`. On punch-hole and notch phones, HUD chips and the joystick can sit under the cutout.
4. **The Kenney "adventure" panels must go.** `panel_brown` and `button_brown` are 64x64 and 48x24 fantasy-RPG frames. They break the brief's rule that MaliGo must not look like a stock asset pack. Every surface in the kit can be drawn procedurally: rounded rects, pills, soft shadows and scrims. Only Mali, the icons and one font need real asset files.
5. **The money format has bugs.** `HUDController.cs:173` uses `R{amount:0,0}`. I ran it: it gives `R05` for 5, `R00` for 0 and `R-45` for −45, and it uses the device culture's group separator (a comma on en-US, a space on en-ZA). The South African convention is `R1 250` with a non-breaking space. Use `−R45` with U+2212.
6. **The end-of-day reveal should show total money (cash + savings) as the headline.** Moving money into savings should then appear as a neutral transfer row, not as a loss. Otherwise the core moment ends up penalising saving.
7. **Choices should show their consequences in aligned columns** (Cash | Energy | Later), built from the `ScenarioChoice` deltas rather than from the free-text `description`. This keeps choices comparable without labelling any of them as "correct".

---

## 1. Inputs: the identity we must stay inside

### 1.1 React Native brand theme (read-only reference)

Source: `…/MaliGo/src/theme/colors.ts`, `typography.ts`, `radius.ts`, `shadows.ts`, `spacing.ts`.

| Group | Tokens (hex) |
|---|---|
| Backgrounds | `background #F9FFF6`, `surface #FFFFFF`, `surfaceSoft #EEF8E8`, `surfaceWarm #FFF4E9`, `warmSurface #FBF6EC`, `brownSoft #F2EDDC` |
| Greens | `primary #087A18`, `primaryDark #075F15`, `primarySoft #C9EFB4`, `primaryTint #DFF4D6` |
| Browns / golds | `brown #9B6639`, `brownDark #8A5633`, `warm #D88B5B`, `gold #DFA464`, `goldBright #F4C84A`, `goldRing #FFC52F` |
| Text | `text #101010`, `textSoft #344330`, `textMuted #52604E`, `muted #667460`, `brandInk #1B2F3B` |
| Borders | `border #E5EEE1`, `borderStrong #B8D6AE`, `borderWarm #EFD0BB`, `dividerWarm #EAD7CB` |
| Signals | `success #5EA869`, `danger #E63B3B`, `coral #E85F48`, `warning #F28B2E`, `info #4F7D8A` |
| Shadow | `shadow #102217`. Card: opacity 0.07, radius 14, offset y 6. Soft: 0.05 / 10 / 4 |
| Radius | sm 12, md 18, lg 28, xl 34, pill 999 |
| Spacing | 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40. Screen padding 20 |
| Type | System font (no `fontFamily` set anywhere; `expo-font` is installed but unused), heavy weights (700-900). Title 28/900, section 19/900, body 14/700 with line height 21, button 15/900, amount 36/900 with tracking −1 |

### 1.2 Colours sampled from brand artwork (measured pixel modes)

| Where | Colour |
|---|---|
| Logo wordmark "Mali" (`mali.png`) | `#203139` (≈ `brandInk #1B2F3B`) |
| Logo wordmark "Go" | `#559024` |
| Mali's scarf | `#5E9224` |
| Mali's Rand coin | `#F8BD1E` |
| Mali's outline | `#3C240C` |
| Logo paper background | `#F9F1E6` |
| RN mock-up `Assets/MaliGo UI.png`: primary button / "Log Saving" tile | `#2E6C00` |
| Mock-up Mali speech bubble / quick tiles | `#EBF0E1` |
| Mock-up "+10 XP" pill | `#BF8C66` |
| Mock-up headline ink / body ink | `#1D2319` / `#565E4E` |

Mali artwork available. All three files are the founder's own brand art, not CC0. They need copying into the repo, so the founder should confirm that.

- `…/assets/images/mali2.png`: 534x615 with **real alpha**. This is the portrait to use.
- `mali.png` (727x618) and `mali1.png` (471x595) have **no alpha** (opaque cream background), so they are unusable as portraits without cutting out.
- `Assets/Mali Dumbfound.png`: 520x657 with alpha, a "concerned" pose. **Art bug: the coin's R is mirrored ("Я").** Fix before use.
- `Assets/MaliGo Pitch Deck.png`: 779x779 with alpha, a full-body wave. Good for the chapter-end screen.

The RN mock-up (`Assets/MaliGo UI.png`) shows "5 Days Active Streak" and "+10 XP" badges. **Don't port these.** Streaks are banned by the brief (§3).

### 1.3 Unity colours already in code

| Name in code | Value | Where |
|---|---|---|
| DeepForest | `#0F5E2E` (0.059, 0.369, 0.180) | `MaliDialogueView.cs:11`, `ScenarioChoiceUI.cs:13`, `ActionPanelUI.cs:18`, prompts at 0.88 alpha (`ProximityInteraction.cs:135`) |
| DarkBrown | `#54381F` (0.33, 0.22, 0.12) | same three files, line +1 |
| MossGreen | `#087A18` | `CharacterCreationUI.cs:14` (= RN `primary`) |
| Sage | `#C9EFB4` | `CharacterCreationUI.cs:15` (= RN `primarySoft`) |
| Cream | `#F9FFF6` | `CharacterCreationUI.cs:16` (= RN `background`) |
| GoldenAmber | `#DFA464` | `CharacterCreationUI.cs:17`, ACT button at 0.85 (`MobileControlsUI.cs:23`) (= RN `gold`) |
| WarmSand | `#FFF4E9` | `Assets/Editor/MaliGoWorldGenerator.cs:17` (= RN `surfaceWarm`) |
| EmberCoral | `#E85F48` | `MaliGoSceneSetup.cs:28` (= RN `coral`) |
| Kenney panel fill / edge | `#FFF1D2` / `#B47B41`, `#6D4B27` | measured from `Assets/Resources/MaliGoUI/panel_brown.png` |

The Unity palette is already the RN palette plus DeepForest and DarkBrown. The kit below only reuses these values, plus one derived border and one derived "money out" colour, both checked for contrast.

---

## 2. Audit of the current UI (measured)

All canvases are Screen Space Overlay with `ScaleWithScreenSize` at a 1920x1080 reference. **They are inconsistent:**

- The dialogue, choice, action-panel and prompt canvases leave `matchWidthOrHeight` at its default of 0 (width).
- `MobileControlsUI.cs:103` uses 1 (height).
- The scene HUD canvas uses 0 (`MaliGoWorld.unity:11563`).

On a 20:9 phone the two groups therefore render at **different scales (1.25x apart)**.

Physical size per canvas unit (computed: ppi from resolution and diagonal, dp = px / (ppi/160)):

| Phone (landscape) | match = 0 (width, current) | match = 1 (height, proposed) |
|---|---|---|
| 6.5" 2400x1080 (405 ppi) | canvas 1920x**864**, 1 u = 0.49 dp | canvas 2400x1080, 1 u = **0.40 dp** |
| 6.5" 1600x720 (270 ppi), typical budget HD+ | 1920x864, 0.49 dp | 2400x1080, 0.40 dp |
| 6.4" 2340x1080 | 1920x886, 0.48 dp | 2340x1080, 0.40 dp |
| 5.5" 1920x1080 (16:9) | 1920x1080, 0.40 dp | 1920x1080, 0.40 dp |

What today's numbers mean on a 6.5" 20:9 phone:

| Element (file:line) | Units | Physical size | Target |
|---|---|---|---|
| Dialogue body (`MaliDialogueView.cs:71`) | 22 | 8.7-10.9 sp | ≥ 18 sp (46 px @1080, see §3) |
| Dialogue speaker (`:68`) | 18 | 7-9 sp | ≥ 14 sp |
| Choice label (`ScenarioChoiceUI.cs:158`) | 20 | 8-10 sp | ≥ 16 sp |
| Choice consequence (`:161`) | 15 | **5.9-7.4 sp** | ≥ 13 sp |
| Choice button height (`:140`) | 76 | 30-38 dp | ≥ 48 dp |
| World prompt (`ProximityInteraction.cs:150`) | 18 on a 36-tall pill | 7-9 sp | ≥ 14 sp |
| Action panel close (`ActionPanelUI.cs:117`) | 44x36 | **17-22 dp** | ≥ 48 dp |
| Action button cell (`ActionPanelUI.cs:97`) | 330x60 | 24-30 dp tall | ≥ 48 dp |
| Joystick / ACT (`MobileControlsUI.cs` 130 u) | 130 | 51 dp | OK |
| Scene HUD texts (`MaliGoWorld.unity`) | 18 (x4), 20, 22 | 7-11 sp | ≥ 14 sp |

Other problems found:

- **Contrast fail:** dark-green labels on the brown part of `button_brown` measure **2.20:1** (DeepForest on `#B47B41`). The cream centre is fine at 7.05:1, but text near the edge sits on the brown.
- **Emoji in the HUD** (`HUDController.cs:139,144,149`: 💰 🏦 🌱) will likely not render in `LegacyRuntime.ttf` on Android. They show as tofu or nothing. I did not verify this on a device.
- **The HUD shows "Financial Stress: N%" and "Level N" plus an XP bar** (`HUDController.cs:132,149,164`). A percentage stress meter reads as a grade. The level/XP bar is harmless in theory, since every choice gives equal XP, but it is a progress score in the player's face. Recommendation: drop both from the HUD. Show Day, Cash, Savings, Energy and Bills only.
- **Sorting order:** the Mali dialogue canvas is 20 and the mobile controls are 25 (`MobileControlsUI.cs:97`), so the joystick and ACT draw over Mali's box. Choices and panels are 30, prompts 15.
- **Sliced-sprite fallback** is a flat DeepForest rectangle with dark text on it. If the bake hasn't run, dark-green text sits on a dark-green panel, which is unreadable.
- **No typewriter, no advance affordance, no portrait.** `MaliDialogueView.Show` just sets text. Lines overflow (`VerticalWrapMode.Overflow`) out of a 68-unit text box.
- Text uses only the built-in `LegacyRuntime.ttf`. **There is no brand font in the project** (no `.ttf/.otf` under the UI pack or Resources). TextMesh Pro ships inside `com.unity.ugui 2.0.0` (in `Packages/manifest.json`), but its Essential Resources have not been imported (no `Assets/TextMesh Pro`).

---

## 3. Research findings (best practice, with what I took from each)

**Text size**
- Game Accessibility Guidelines: **28 px minimum at 1080p**, "a minimum rather than a target" ([GAG](https://gameaccessibilityguidelines.com/use-an-easily-readable-default-font-size/)).
- For subtitle and dialogue text, GameAnalytics cites the BBC: **≥ 46 px at 1080p**, at most **2 lines and ≤ 38 characters per line on phones**, sentence case (never all caps), a background box, a speaker name or portrait (not colour alone), bottom-centre placement that stays clear of thumbs, and adjustable size ([GameAnalytics](https://www.gameanalytics.com/blog/adding-subtitles-to-your-mobile-game-dos-and-donts)).
- **Taken:** dialogue at 46 u, absolute floor of 30 u, sentence case everywhere, a name plus portrait for Mali.
- Mali's lines are read at the player's pace, not timed like subtitles, so I allow **3 lines of about 50 characters** inside a fixed box and paginate anything longer.

**Contrast**
- WCAG 1.4.3: **4.5:1** for normal text, **3:1** for large text (≥ 18 pt, or 14 pt bold) ([W3C](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html)).
- WCAG 1.4.11: **3:1** for UI component boundaries and state indicators ([W3C](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html)).
- Android uses the same thresholds in sp ([Android](https://developer.android.com/guide/topics/ui/accessibility/apps)).
- **Taken:** every text token below is ≥ 4.5:1 on its intended surface, most are ≥ 6:1, and control borders are ≥ 3:1.

**Touch targets**
- Android recommends **48x48 dp minimum**, "larger is even better" ([Android](https://developer.android.com/guide/topics/ui/accessibility/apps)).
- WCAG 2.5.5 (AAA) asks for 44x44 CSS px ([W3C](https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html)).
- The brief says ≥ 48 dp.
- **Taken:** a 120 u minimum (48 dp) and 140 u for primary actions.

**Safe areas**
- `Screen.safeArea` returns the safe rect in pixels with a bottom-left origin ([Unity](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html)).
- With render-outside-safe-area on, the app draws under the cutout and must inset its own UI ([Unity](https://docs.unity3d.com/ScriptReference/PlayerSettings.Android-renderOutsideSafeArea.html)).
- Android 15 with target SDK 35+ enforces edge-to-edge, and in landscape the punch-hole sits on a side edge ([Android cutouts](https://developer.android.com/develop/ui/views/layout/display-cutout), [edge-to-edge codelab](https://developer.android.com/codelabs/edge-to-edge)).
- MaliGo's target SDK is "Auto" (`MaliGoAndroidSetup.cs:46`), so it will be the highest installed, very likely ≥ 35.
- **Taken:** a SafeArea fitter on every canvas.

**Canvas scaling**
- Match Width or Height blends the reference axis ([Unity uGUI 2.0](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html)).
- For landscape phones, matching **height** makes the vertical budget a constant 1080 u and gives the same physical size per unit on every phone (about 0.40 dp, see §2). Extra width on 19.5:9 to 2.4:1 phones becomes side room for edge-anchored HUD and controls.
- **Taken:** `matchWidthOrHeight = 1` everywhere. Layouts must anchor to the left and right edges, not assume x = 1920.

**Dialogue pattern**
- Standard practice is that the **first tap fast-forwards the typewriter and the second tap advances** (Pixel Crushers' "Continue Button Fast Forward" ([docs](https://www.pixelcrushers.com/dialogue_system/manual2x/html/dialogue_u_is.html))).
- Show one line at a time, put a continue cue (▼) bottom-right once complete, reserve padding so the cue never overlaps text, and use punctuation pauses ([Pixel Crushers](https://www.pixelcrushers.com/dialogue_system/manual2x/html/dialogue_u_is.html), [example spec](https://github.com/Palmkonde/rpg-webbase/issues/21)).

**Choices whose consequences can be compared**
- Reigns previews **which meters a choice touches and by how much** (dot size) before you commit, without saying whether that is good ([Gamezebo](https://www.gamezebo.com/walkthroughs/reigns-tips-cheats-and-strategies/), [Emily Short](https://emshort.blog/2016/08/20/reigns/)).
- **Taken:** show what moves and by how much, in the same column position on every choice, and never label direction as good or bad.
- MaliGo is about *living* the consequence. So we show the immediate, known amounts (cash, energy, repayment schedule). The reveal and Mali make the meaning.

**End-of-day ledgers in games**
- Papers, Please's end-of-day screen is a line-by-line ledger of income against rent, food and heat, with a running total, a summary of the day's events, and the family's state. The budget items are checkboxes, so the cost of living is concrete and personal ([Papers Please wiki](https://papersplease.fandom.com/wiki/End_of_day_screen), [Wikipedia](https://en.wikipedia.org/wiki/Papers,_Please)).
- Stardew Valley shows earnings by category with a total after sleeping, as a ritual between days. Since 1.6 the category pages no longer take the full screen ([Stardew wiki](https://stardewvalleywiki.com/Shipping)).
- **Taken:** a fixed ritual after Sleep, line items grouped by cause, a big total, then Mali.

**Banking apps**
- Transaction lists should be chronological, scannable, grouped in chunks of about 5-9, with readable labels. **Colour alone must never carry meaning** ([Appricotsoft](https://appricotsoft.com/blog/mobile-banking-app-development-how-to-design-transaction-history-that-users-actually-trust/), [Tapix](https://www.tapix.io/resources/post/11-essential-ux-laws-every-digital-banking-app-should-follow)).
- **Taken:** a sign plus a glyph on every amount, at most 7 rows before grouping into "Other", and right-aligned amounts.

**Cozy and calm**
- Cozy UIs use generous padding, rounded forms, soft shadows, warm palettes and relaxed timing ([Design Lab](https://thedesignlab.blog/2025/06/02/the-rise-of-cozy-games-designing-for-calm-comfort-connection/)). We take the padding, radii and warmth but not the pastel-childish look: the brief says grown-up.
- Material 3 motion tokens: short 50-200 ms, medium 250-400 ms. Standard easing is `cubic-bezier(0.2, 0, 0, 1)`, decelerate `(0, 0, 0, 1)`, accelerate `(0.3, 0, 1, 1)` ([M3 tokens page](https://m3.material.io/styles/motion/easing-and-duration/tokens-specs); the page did not render for me, so these values came via a search summary).

**Number format**
- South Africa groups thousands with a space, and CLDR en_ZA uses a non-breaking space ([summary](https://medium.com/@fletch.jeff/south-african-number-formats-60927bbf7382), [Siyavula](https://www.siyavula.com/read/za/mathematical-literacy/grade-10/numbers-and-calculations-with-numbers/01-numbers-and-calculations-with-numbers-02)).
- **Taken:** `R1 250` and `−R45`, formatted with an explicit invariant routine, never `CurrentCulture`.

---

## 4. Tokens

All sizes are in **canvas units (u)** with reference 1920x1080 and `matchWidthOrHeight = 1`. Rule of thumb: **u ≈ dp x 2.5**. Contrast figures are computed WCAG ratios.

### 4.1 Colour

| Token | Hex | Use | Contrast check |
|---|---|---|---|
| `surface.paper` | `#FBF6EC` (RN warmSurface) | Panel, sheet and dialogue background | base |
| `surface.card` | `#FFFFFF` | Choice cards, ledger card, raised items on paper | 1.08 vs paper, so separate with shadow or border |
| `surface.warm` | `#FFF4E9` (WarmSand) | Mali bubble, Mali-voiced cards | |
| `surface.sunken` | `#F2EDDC` (brownSoft) | Chips, alternate ledger rows, inputs | ink 13.7 |
| `surface.tint` | `#EEF8E8` (surfaceSoft) | Selected / "in" rows, goal card | ink 17.4 |
| `surface.inverse` | `#0F5E2E` (DeepForest) at 94% | HUD chips, toast, end-of-day backdrop | cream 7.77 (6.37 at 92% over white sky) |
| `scrim` | `#102217` at 55% | Behind modal sheets | |
| `text.primary` | `#101010` (RN text) | Headlines, body, amounts on paper | 17.7 on paper |
| `text.secondary` | `#344330` (textSoft) | Supporting copy | 9.8 |
| `text.muted` | `#52604E` (textMuted) | Labels, captions (≥ 30 u only) | 6.2 paper, 5.7 sunken |
| `text.onInverse` | `#F9FFF6` (Cream) | Text on forest | 7.8 |
| `text.onInverseMuted` | `#C9EFB4` (Sage) | Labels on forest | 6.2 |
| `accent.primary` | `#087A18` (MossGreen / RN primary) | Primary button fill, focus, progress | cream label 5.43; 5.12 vs paper as a component |
| `accent.primaryPressed` | `#075F15` | Pressed state | |
| `accent.gold` | `#DFA464` (GoldenAmber) | Mali's colour: name tag, highlight ring, chapter accents. **Never text on paper** (2.02) | ink on gold 8.7 |
| `accent.coin` | `#F4C84A` (goldBright) | Coin icon, amount highlight on forest | 4.96 on forest |
| `money.in` | `#087A18` + "+" and ▲ | Income, refunds | 5.12 paper, 5.52 card |
| `money.out` | `#8A5633` (brownDark) + "−" and ▼ | Spending, bills. **Deliberately not red**, because red reads as a verdict | 5.64 paper, 6.07 card |
| `money.transfer` | `#344330` + "↔" | Cash ↔ savings moves | 9.8 |
| `money.in.onInverse` | `#C9EFB4` | Income on forest | 6.2 |
| `money.out.onInverse` | `#F8E4D6` (warmSoft) | Spending on forest | 6.41 |
| `attention` | `#A8432F` (derived from coral) | Only for overdue/arrears and destructive actions (Reset save) | 5.56 paper, 5.98 card |
| `attention.dot` | `#E85F48` (EmberCoral) | Non-text dot or badge only (3.16) | |
| `border.subtle` | `#EAD7CB` (dividerWarm) | Decorative dividers (no contrast requirement) | |
| `border.control` | `#B47B41` (Kenney panel edge) | Outlines of controls that need a boundary (secondary buttons, toggles) | 3.33 on paper, meets 1.4.11 |
| `focus.ring` | `#F4C84A`, 6 u | Selected choice / keyboard focus | |
| `shadow` | `#102217` | All shadows | |

Rules:
- Colour never carries meaning alone. Every amount has a sign and a glyph.
- Green is for income and the primary action, never a reward "for being good".
- Gold belongs to Mali. Use it where she speaks or reflects.

### 4.2 Type

**Font.** The recommendation is **Nunito**: rounded, warm and very legible at small sizes. It fits Mali's soft line art without looking childish, and it covers Latin Extended so it can carry Afrikaans, isiZulu and isiXhosa names. Licence: **SIL OFL 1.1** ([repo](https://github.com/googlefonts/nunito)). The brief says CC0 for *art*, so **list the font for founder approval**. The fallback is the current `LegacyRuntime.ttf`.

Ship Regular (400), Bold (700) and ExtraBold (800) as **separate Font assets**. Legacy `Text` fakes bold on a single-weight file, and the RN brand look depends on 800-900 weights.

Engine choice (decide in the spec):
- **Option A (recommended for the beta):** keep legacy `UnityEngine.UI.Text` with dynamic Nunito TTFs. This needs no extra resources and is consistent with the existing code.
- **Option B:** TextMesh Pro, which is already in `com.unity.ugui 2.0.0`. It gives sharper text and tracking, but someone has to run *Window > TextMeshPro > Import TMP Essential Resources* ([docs](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html)), which writes into `Assets/`. A person must do this in the Editor.

| Role | Size (u) | ≈ sp | Weight | Line height | Use |
|---|---|---|---|---|---|
| `display.amount` | 112 | 45 | ExtraBold | 1.0 | End-of-day start/end amounts |
| `display.title` | 72 | 29 | ExtraBold | 1.1 | Chapter title, "Day 3 is done" |
| `title.panel` | 56 | 22 | ExtraBold | 1.15 | Panel and scenario titles |
| `body.dialogue` | 46 | 18 | Bold (RN body is 700) | 1.3 | Mali's lines |
| `body` | 40 | 16 | Regular/Bold | 1.35 | Scenario situation, panel copy |
| `label.choice` | 44 | 17.5 | ExtraBold | 1.15 | Choice label |
| `label.button` | 42 | 17 | ExtraBold | 1.0 | Buttons |
| `value.hud` | 44 | 17.5 | ExtraBold | 1.0 | HUD numbers |
| `value.ledger` | 44 | 17.5 | Bold, right-aligned | 1.0 | Ledger amounts |
| `label.meta` | 34 | 13.5 | Bold | 1.2 | Chips, HUD labels, consequence chips |
| `caption` | 30 | 12 | Bold | 1.25 | **Absolute floor.** Hints, timestamps |

Further type rules:
- Sentence case everywhere. No all-caps labels (the current "ACT" becomes the verb in title case).
- Amounts are right-aligned. Legacy Text cannot turn on tabular figures, so alignment does the work.
- Format money with a helper: `R1 250` (U+00A0 group separator), `−R45` (U+2212), no decimals unless cents matter.
- Check that Nunito and LegacyRuntime both contain U+2212 and U+00A0. I could not check this here.

### 4.3 Shape, spacing, elevation

| Token | Value (u) | ≈ dp | Notes |
|---|---|---|---|
| `radius.chip` | height / 2 | | Pills: HUD chips, toasts, name tag |
| `radius.button` | 28 | 11 | |
| `radius.card` | 36 | 14 | Choice cards, ledger card (RN md 18 scaled down; on a 1080-u-tall screen RN's lg 28 dp looks bubbly) |
| `radius.sheet` | 48 | 19 | Panels, dialogue box |
| `space.xs / sm / md / lg / xl / xxl` | 10 / 20 / 30 / 40 / 60 / 80 | 4 / 8 / 12 / 16 / 24 / 32 | RN spacing x 2.5 |
| `pad.sheet` | 48 | 19 | Inner padding of sheets and dialogue |
| `gap.targets` | ≥ 20 | 8 | Between adjacent tappables |
| `margin.safe` | 40 inside safe area, +24 at bottom | | Clears the gesture bar |
| `target.min / primary` | 120 / 140 | 48 / 56 | |
| `stroke.control` | 4 | 1.6 | `border.control` |
| `shadow.card` | offset (0, −12), blur 32, `#102217` at 10% | | RN card (0.07, 14, y 6), slightly stronger because it sits over the 3D world |
| `shadow.sheet` | offset (0, −20), blur 56, at 18% | | Sheets over the world |

uGUI's `Shadow` component cannot blur, so shadows are a **procedurally generated soft rounded-rect sprite** placed behind the element (see §6).

### 4.4 Motion (calm)

| Token | Duration | Easing | Use |
|---|---|---|---|
| `motion.press` | 100 ms | out | Button scale to 0.97 and darken |
| `motion.fade` | 200 ms | standard | Prompts and toasts in/out |
| `motion.sheet.in` | 300 ms | decelerate | Panels rise 40 u and fade in |
| `motion.sheet.out` | 200 ms | accelerate | |
| `motion.count` | 700 ms (max 1 200) | decelerate | Number count-ups (HUD delta, reveal) |
| `motion.stagger` | 120 ms per row | | Ledger rows |
| `motion.idle.cue` | 1 200 ms loop, 6 u bob | sine | Dialogue continue chevron |

Motion rules:
- No bounce or overshoot, no shake, no confetti, no flashing (respect WCAG 2.3.x).
- A **"Reduce motion"** setting in Pause turns all of the above into 150 ms crossfades and shows numbers instantly.
- All UI timing uses `Time.unscaledDeltaTime` so it keeps running while paused.
- There is no tween library in the project, so write one small coroutine tween helper.

---

## 5. Components

Sketches use a **2400x1080 u canvas** (20:9 at match = 1). On 16:9 the canvas is 1920 wide and the centred content is unchanged. `▒` marks the safe-area inset (cutout side). Canvas sort orders are proposed per component.

### 5.0 Screen map in play

```
┌──────────────────────────────────────────────────────────────────────────────┐
│▒ [Day 3]  [Cash R1 250] [Savings R300]          [Energy ███░ 60] [Rent R1 200 ·│
│▒                                                  due Day 5]          [ II ] │  HUD (sort 20)
│▒                                                                             │
│▒                         (3D world)                                          │
│▒                                                                             │
│▒                    ╭───────────────────────╮                                │
│▒                    │ ⌂  Sleep at home      │   ← world prompt (sort 10)     │
│▒   ( ◯ )            ╰───────────────────────╯                  (  Talk  )    │  controls (sort 25)
│▒  joystick                                                       action      │
└──────────────────────────────────────────────────────────────────────────────┘
```

Proposed sort orders: prompts 10, HUD 20, controls 25, **dialogue 40** (above controls; the controls also dim while dialogue is open), choice/action sheets 50, full-screen reveals and pause 60, toast 70, first-run coach 80. Every canvas root gets a `SafeAreaFitter` child that holds the content; full-bleed backdrops sit outside it.

### 5.1 HUD (cash, savings, energy, day, bills due)

- **Layout:** a top strip inside the safe area, 40 u from the top. A left group and a right group of **pills 96 u tall** in `surface.inverse` at 94%.
  - Label 34 u in `text.onInverseMuted`, value 44 u ExtraBold in `text.onInverse`, with a 48 u icon at the left.
  - 20 u gaps between pills. No outer panel: free-floating pills keep the world visible (HUD minimalism).
- **Contents, in order:**
  - Left: **Day** ("Day 3"), **Cash** ("R1 250"), **Savings** ("R300").
  - Right: **Energy** (a 160x16 u bar in `accent.coin` on a 25% cream track, plus the number), **Next bill** ("Rent R1 200 · Day 5"), **Pause** (a 120x120 u circle; the only tappable item).
  - When nothing is due within 7 days, the bill pill reads "No bills this week".
  - When a bill is due today it gains an `attention.dot` (non-text). The dot is the only alarm, and it is not red text.
- **Removed:** player name and level, the XP bar, and Financial Stress % (see §2).
- **On change:** the value counts to its new figure over 700 ms. A delta tag ("−R45" or "+R350") slides out under the pill in `money.*.onInverse`, holds for 1.6 s, then fades.
  - **This is the immediate causal feedback the brief asks for.**
  - The HUD never flashes or turns red.
- **Optional:** tapping the Cash pill opens a mini ledger of today so far (the same rows as the reveal). This is a cheap preview of the day's story, and can be left for after the beta.

```
╭───────────────╮ ╭────────────────────╮ ╭──────────────────╮        ╭───────────────────────╮ ╭──────────────────────╮ ╭────╮
│ ☀ Day 3       │ │ R  Cash   R1 250   │ │ ▣ Savings  R300  │  ...   │ ϟ Energy ████░░  60   │ │ ▤ Rent R1 200 · Day 5│ │ II │
╰───────────────╯ ╰────────────────────╯ ╰──────────────────╯        ╰───────────────────────╯ ╰──────────────────────╯ ╰────╯
                    −R45  (fades)
```

### 5.2 World interaction prompt

- **Layout:** a pill 112 u tall, auto-width (min 480, max 900), bottom-centre, 260 u above the safe bottom so it sits above the controls row and never under the dialogue.
- **Style:** `surface.paper` with `shadow.card`, a 56 u icon (home, bank, work, Mali, scenario), and a 40 u Bold verb phrase ("Sleep at home", "Talk to Mali", "Taxi rank: get across town").
- **Behaviour:**
  - The **pill itself is tappable** (≥ 120 u hit area via a transparent padding rect) and does the same as the action button.
  - The action button's label changes to the verb ("Talk", "Open", "Sleep", "Work"). It is not "ACT".
  - The pill fades in and out over 200 ms. When the action is unavailable, the pill stays visible with a muted reason ("Done for today: sleep at home") and no tap action, which matches `WorkInteraction.cs:20`.
- Today four scripts (`ProximityInteraction`, `ScenarioTrigger`, `MaliCompanionInteraction` and its kin) each build their own copy. Build one shared `WorldPromptView` instead.

### 5.3 Mali dialogue box

```
            ┌──────┐
            │ (••) │  ← Mali portrait 280 u, overlaps box top by 72 u
       ╭────│  ‿   │───────────────────────────────────────────────────────────────╮
       │    │ /🟢\ │ ╭─────╮                                                        │
       │    └──────┘ │Mali │  (gold name tag, 34 u ExtraBold ink on #DFA464)        │
       │             ╰─────╯                                                        │
       │             R45 for the taxi, and you'll be there before the rain. Your    │
       │             fridge goal is still R180 away.                              ▼  │
       ╰────────────────────────────────────────────────────────────────────────────╯
        bottom: 40 u above safe bottom · width min(1640, safeWidth − 96) · height 300
```

- **Size and position:**
  - Bottom-centre; width `min(1640, safeWidth − 96)`, height **300 u** (fixed, so it does not jump between lines).
  - `surface.warm`, `radius.sheet`, `shadow.sheet`, and a 6 u top edge in `accent.gold`.
  - Text area left inset 340 u (after the portrait), right inset 120 u (reserved for the cue).
- **Portrait:** Mali bust cut from `mali2.png` (alpha) at 280x280 u, bottom-left, rising 72 u above the box. Expressions:
  - neutral/happy: `mali2`
  - thinking/concerned: `Mali Dumbfound` (fix the mirrored coin first)
  - Swapping by line type is optional for the beta.
- **Speaker:** a "Mali" name tag pill (`accent.gold`, ink text, 34 u) above the first line. The name is always shown, not just a colour.
- **Text:** `body.dialogue` 46 u Bold in `text.primary`. **Maximum 3 lines (about 50 characters per line, about 150 characters per page).** Longer lines paginate at sentence boundaries. Never shrink to fit.
- **Typewriter:**
  - About 45 characters per second by default (my judgement; tune in play-test). Pause 180 ms after `. ! ?` and 80 ms after `,`.
  - Pause-menu "Text speed" options: Slow 25 / Normal 45 / Instant.
  - Reduce motion forces Instant.
  - Lay out the full string first and reveal characters, so words never jump lines mid-type. With legacy Text, reveal by wrapping the hidden tail in `<color=#00000000>`, which keeps the layout stable.
- **Advance:**
  - **Tap anywhere on the box (or on the screen behind the scrim)**: the first tap completes the line, the second advances.
  - The **▼ chevron** (48 u, `accent.primary`) appears bottom-right only once the line is complete, and bobs 6 u every 1.2 s. On the last page it becomes a ✓, or closes.
  - A 150 ms input lockout after each page prevents accidental double-advance.
- **While open:** a 25% scrim over the world. Joystick and action button go to 30% alpha and are non-interactive. The HUD stays visible.
- **After a choice:** Mali's reaction line plus a **consequence chip row** under the text, drawn from the actual deltas ("Cash −R45 · Energy −2 · Then R150 × 3, weekly"). This is the immediate *this choice → this number* link.

### 5.4 Scenario choice panel

```
╭──────────────────────────────── sheet: safeWidth − 160, height 880, surface.paper ────────────────────────────────╮
│ ┌───────────────── left 34% ─────────────────┐ ┌─────────────────────── right 66% ─────────────────────────────┐ │
│ │ TAXI RANK  (caption, muted)                 │ │                            Cash      Energy     Later        │ │
│ │ Getting across town          (56 u)         │ │ ╭───────────────────────────────────────────────────────────╮│ │
│ │                                             │ │ │ Walk                      —        ▼ 15       —          ││ │
│ │ You need to get across town today. Cost,    │ │ ╰───────────────────────────────────────────────────────────╯│ │
│ │ time and energy all trade off differently.  │ │ ╭───────────────────────────────────────────────────────────╮│ │
│ │ (40 u, max 5 lines)                         │ │ │ Minibus taxi              −R15     ▼ 5        —          ││ │
│ │                                             │ │ ╰───────────────────────────────────────────────────────────╯│ │
│ │  ┌────┐  "Your call. It's the same trip    │ │ ╭───────────────────────────────────────────────────────────╮│ │
│ │  │Mali│   either way."  (optional, 34 u)   │ │ │ E-hailing ride            −R45     ▼ 2        —          ││ │
│ │  └────┘                                     │ │ ╰───────────────────────────────────────────────────────────╯│ │
│ │ You have R1 250 cash · Energy 60            │ │                                                              │ │
│ └─────────────────────────────────────────────┘ └──────────────────────────────────────────────────────────────┘ │
╰──────────────────────────────────────────────────────────────────────────────────────────────────────────────────╯
```

- **Layout:**
  - A centred sheet (`radius.sheet`, `shadow.sheet`, 55% scrim) of width `safeWidth − 160` (max 2000) and height 880.
  - Left column (34%): the situation (eyebrow caption with the location, title 56 u, body 40 u, at most 5 lines), an optional small Mali, and a **"You have …" line** so the player sees their current numbers next to the costs.
  - Right column (66%): **2-4 choice cards**, each **150 u tall** (min 140), 20 u apart, in `surface.card` with `radius.card` and `shadow.card`.
- **Comparable consequences:**
  - A fixed **column header row** (Cash | Energy | Later) in `caption` / `text.muted`, with every card's values right-aligned under those headers.
  - Generate the values from `ScenarioChoice.cashDelta`, `savingsDelta`, `energyDelta` and `instalmentCount` × `instalmentAmount` / `instalmentIntervalDays`. Use "—" for no change so the columns always line up. A Savings column appears only if any choice touches savings.
  - "Later" shows the commitment ("R150 × 3, weekly"). This is how pay-later becomes visible without moralising.
  - Stop showing the free-text `description` strings ("Cash -R50, Stress -5"). They are hand-typed, use hyphen-minus, and can drift from the real deltas.
- **Never shown:** XP (always 5), the behaviour tag, any "recommended" badge or ordering. Keep the authored choice order. Do not sort by cost.
- **Stress:** leave numeric stress off the chips; the stress figure is not something to manage on screen. If the spec wants it visible, use words ("calmer" / "more pressure") in `text.secondary`, never a number.
- **Interaction:**
  - Press state: 100 ms scale to 0.98 with a `focus.ring` outline.
  - On release, the sheet closes over 200 ms and Mali's reaction box (§5.3) opens with the chip row.
  - 300 ms of input lockout on open so a tap from walking up cannot land on a choice.
  - No confirm step.
- With more than 3 choices on a 1920-wide screen, the right column keeps 150 u cards and the sheet grows to 960 tall at most. Never shrink text.

### 5.5 Action panel (Home / Bank)

```
╭────────────────────────────── 1560 x 860, surface.paper ─────────────────────────────╮
│  Home                                                                       ╭────╮    │
│  (56 u)                                                                     │ ✕  │ 120│
│ ┌──────────── left: status (surface.sunken card) ───────┐ ┌───── right: actions ─────┐ │
│ │ Day          3                                         │ │ ╭──────────────────────╮ │ │
│ │ Cash         R1 250                                    │ │ │ ☾ Sleep: end the day │ │ │ ← primary 140 u, accent.primary
│ │ Savings      R300                                      │ │ ╰──────────────────────╯ │ │
│ │ Energy       60                                        │ │ ╭──────────────────────╮ │ │
│ │ ───────────────────────────────                        │ │ │ Eat at home   −R20   │ │ │ ← secondary 120 u, border.control
│ │ Due tonight  Rent R1 200                               │ │ ╰──────────────────────╯ │ │
│ │ Next         Phone R99 · Day 6                         │ │                          │ │
│ └───────────────────────────────────────────────────────┘ └──────────────────────────┘ │
╰───────────────────────────────────────────────────────────────────────────────────────╯
```

- **Layout:**
  - A centred sheet of 1560x860 u (`radius.sheet`, `shadow.sheet`, 55% scrim).
  - Title 56 u, and a close button at **120x120 u** in the top-right corner (✕ glyph 48 u). Close also fires on Android Back.
  - Left: a status card of label-value rows (row height 72 u, labels `text.muted` 34 u, values 44 u right-aligned). Right: a vertical action stack with 20 u gaps.
- **Bank:**
  - The actions are amount chips ("Move to savings: R50 · R100 · R200 · All"), each ≥ 120 u tall, plus "Take out" chips.
  - Every action shows its effect in the label, and the status values count to the new figure in place. Keep `ActionPanelUI`'s "stays open across actions" behaviour; it is right.
- **Sleep** always shows what will be charged tonight before the tap ("Due tonight: Rent R1 200").

### 5.6 End-of-day reveal (the unmissable moment)

```
full-bleed surface.inverse (#0F5E2E) backdrop, paper card 1900 x 900 centred
╭──────────────────────────────────────────────────────────────────────────────────────────────╮
│  Day 3 is done.  (72 u ExtraBold)                                     Chapter 1 · First pay   │
│ ┌───────────── left 42% ───────────────┐  ┌──────────── right 58%: what moved it ──────────┐  │
│ │ You started the day with             │  │ ▲ Shift at the shop             +R350          │  │
│ │ R1 500        (112 u)                │  │ ▼ Lunch: kota from the stall     −R20          │  │
│ │                                      │  │ ▼ Taxi to town                   −R15          │  │
│ │ You ended it with                    │  │ ▼ Rent (due today)            −R1 200          │  │
│ │ R615          (112 u, counts up)     │  │ ▼ Pay-later: speaker, 1 of 3    −R150          │  │
│ │                                      │  │ ↔ Moved to savings              R100  (neutral)│  │
│ │ ╭────────────────╮                   │  │ ───────────────────────────────────────────    │  │
│ │ │  −R885 today   │ (pill)            │  │   Cash R515 · Savings R100                     │  │
│ │ ╰────────────────╯                   │  └────────────────────────────────────────────────┘  │
│ └──────────────────────────────────────┘                                                    │
│  ┌────┐ "Rent took most of today. Payday's in 4 days. The speaker repayments                  │
│  │Mali│  keep coming every Friday until they're done."                                        │
│  └────┘                                                         ╭─────────────────────────╮ │
│                                                                 │  On to Day 4   →        │ │ 140 u primary
│  Coming up: Phone R99 on Day 6                                  ╰─────────────────────────╯ │
╰──────────────────────────────────────────────────────────────────────────────────────────────╯
```

- **What the headline number is:** **total money = cash + savings**, labelled exactly *"You started the day with R___ / You ended it with R___"*.
  - Savings moves appear as `money.transfer` rows (↔, neutral colour) and do not count as "moved", so saving is never shown as a loss.
  - A sub-line shows the split.
- **Sequence** (about 4 s total; any tap jumps to the final state):
  1. The world fades to the forest backdrop (400 ms) after Sleep.
  2. The "Day N is done." title.
  3. "You started the day with R1 500".
  4. Ledger rows stagger in (120 ms each), each with a glyph, a plain-language cause and a signed right-aligned amount.
  5. "You ended it with" counts from the start value to the end value (700-1 200 ms, decelerate).
  6. The delta pill appears ("−R885 today" / "+R120 today") in `text.primary` on `surface.sunken`, **not coloured green or red**, because the number speaks for itself.
  7. Mali's line (fades in 300 ms). It names the **biggest mover** and links it to the player's own goal or the next bill (reflect, don't judge).
  8. The "On to Day N+1" button plus "Coming up" (the next bill), which works as the open-question hook ("what happens tomorrow?") instead of a streak.
- **Ledger rules** (banking-app practice): chronological; at most **7 rows**, then group the rest as "Other (3) −R37". The cause label is the scenario choice label or the bill name. Bills are marked "(due today)", and arrears use `attention` text: "Rent: R200 still owed".
- **Never on this screen:** XP, level, stress %, grades, "Great day!" / "Bad day" wording, confetti, red flashes, sad sounds.
- Pure data: build this from a per-day transaction log (date, cause, cash delta, savings delta). That log does not exist yet. It is a prerequisite for the build phase.

### 5.7 Chapter-end screen

- **Layout:** full-screen `surface.paper`.
  - Left 36%: Mali full body (`MaliGo Pitch Deck.png`, waving) at about 700 u tall on a soft gold circle (`accent.gold` at 25%).
  - Right: the chapter title (72 u), then "Over N days you started with R___ and finished with R___" (112 u amounts).
  - Then **three "moments that moved it most"** cards (the biggest absolute deltas across the chapter, each with day number, cause and amount), your own goal's progress bar (`accent.primary` on a `surface.sunken` track, with "R___ of R___ towards a fridge"), and Mali's longer reflection (3-5 lines; paginate if longer).
- **Buttons:** "Keep playing" (primary 140 u) and "Start the chapter again" (secondary, `border.control`).
- No grade, no star rating, no "You saved more than 80% of players".

### 5.8 First-run guide overlay (coach marks)

```
 scrim 55% everywhere except a rounded cut-out around the target (+24 u padding)
 ╭─────────────────────────╮
 │   ( ◯ ) joystick        │ ← cut-out with a 6 u focus.ring
 ╰─────────────────────────╯
       ╭───────────────────────────────────────────╮
       │ [Mali 120] Drag here to walk. Find me by    │  surface.warm bubble, 40 u text
       │            the spaza shop when you're ready. │
       │                          [ Skip tips ] [Got it] │ 120 u buttons
       ╰───────────────────────────────────────────╯
```

- **Just-in-time, not front-loaded:** steps fire at first need, at most 4.
  1. Move (on load).
  2. Interact (first time a prompt appears).
  3. Money in the HUD (after the first cash change; points at the Cash pill and the delta tag).
  4. Sleep ends the day (first time Home is near after the shift).
- Each step is **one sentence in Mali's voice**, with "Got it" (primary) and "Skip tips" (text button, still 120 u tall).
- The cut-out is drawn as **4 scrim rects around the target**, which is trivial in code. No mask shader is needed.
- Store `firstRunStepsSeen` as flags in the save, and offer "Show tips again" in Pause. The overlay pauses input to everything except the highlighted control, so the player can try it.

### 5.9 Pause menu

- **Trigger:** the HUD ‖ button (120 u) or Android Back. `Time.timeScale = 0`, and UI animation uses unscaled time.
- **Sheet:** 1100x900 u, centred, 55% scrim, on `surface.paper`.
  1. "Paused" (56 u).
  2. **Resume** (primary, 140 u).
  3. Settings rows (120 u each): Sound on/off, Music volume, **Text speed** (Slow / Normal / Instant segmented control), **Reduce motion** toggle, **Show tips again**.
  4. Divider.
  5. **Start over…** (text in `attention`, with `border.control` outline).
- **Reset confirm:** a second small sheet reading "This deletes your progress and starts a new life from Day 1. You can't undo it." The buttons are **"Keep playing"** (primary, on the right where the thumb rests) and **"Delete and start over"** (secondary, `attention` text). Never make the destructive option the primary.
- Toggles: 120x72 u pill track. On is `accent.primary`, off is `surface.sunken` with a `border.control` outline, and the state is also given in text ("On" / "Off").

### 5.10 Toast

- **Position:** top-centre, 24 u under the HUD row. A pill 96 u tall, auto-width (max 1200), in `surface.inverse` with a 48 u icon and 40 u text in `text.onInverse`. Amounts use `money.*.onInverse`.
- **Timing:** fade and slide 20 u down over 200 ms. Hold 2.5 s plus 50 ms per character (max 6 s), then fade out. One visible at a time, FIFO queue, tap to dismiss.
- **Use for system facts only:** "Rent R1 200 paid", "Saved: progress kept", "Shift done +R350". Mali's interpretation never goes in a toast; it goes in her box.

---

## 6. Procedural vs sprite

| Piece | How | Notes |
|---|---|---|
| Rounded rects (sheets, cards, buttons, chips, pills) | **Procedural.** Generate one white 128x128 rounded-rect texture per radius at runtime, `Sprite.Create(..., border = radius)`, `Image.type = Sliced`, tinted by `Image.color` | Same technique already used in `MobileControlsUI.CreateCircleSprite` (`MobileControlsUI.cs:186-200`). Cache in a static `UiKit` class. Set `pixelsPerUnit` so the 9-slice border equals the radius in canvas units |
| Soft shadows | **Procedural.** Rounded rect with a gaussian alpha falloff (blur baked into the texture), 9-sliced, placed behind | uGUI `Shadow` can't blur |
| Scrim, dividers, progress bars, energy bar | **Procedural.** 1x1 white sprite, `Image.Type.Filled` for bars | |
| Coach-mark cut-out | **Procedural.** 4 scrim rects + a rounded-rect ring | No mask shader |
| Chevron ▼ / ✓ / ✕ / ‖ | Procedural triangles and lines from small generated textures, or font glyphs **if** confirmed in the chosen font | LegacyRuntime glyph coverage not verified |
| Name tag, delta tags, consequence chips | Procedural pills + text | |
| **Mali portrait and poses** | **Sprites (founder's own art)** | `mali2.png` (bust, alpha), `Mali Dumbfound.png` (fix the mirrored coin), `MaliGo Pitch Deck.png` (full body). Import with mip maps off, max size 512 (portrait) or 1024 (full body) |
| **Icons** (cash, savings, energy, day, bill, home, bank, work, taxi, food, pause, sound) | **Sprites, CC0** | Kenney icon packs are CC0 (per [kenney.nl](https://kenney.nl/support)); the asset-sourcing agent should confirm the exact pack. Use one single-colour line set, tinted at runtime. A 64-96 px source is enough at 48 u |
| **Font** | **File (OFL), founder approval** | Nunito Regular/Bold/ExtraBold |
| Kenney adventure panels (`panel_brown`, `button_brown`, `banner_modern`, ...) | **Retire** | Fantasy-RPG look; 64 px sources upscaled. `KenneyUiSprites` stays only as a last-resort fallback, or is removed |

---

## 7. Checks the build phase should run against this spec

- [ ] Every canvas: `ScaleWithScreenSize`, 1920x1080, **match = 1**, content under a SafeArea fitter. Tested at 1920x1080, 2340x1080, 2400x1080 and 1600x720 in the Game view, with a simulated cutout on the left and on the right (Device Simulator).
- [ ] No text under 30 u. Dialogue 46 u, body 40 u. Nothing auto-shrinks below the role size.
- [ ] Every tappable is ≥ 120x120 u with ≥ 20 u gaps.
- [ ] Every text/background pair is one of the checked pairs in §4.1.
- [ ] Money formats as `R1 250` / `−R45` regardless of device culture. `R5` stays `R5`, never `R05`.
- [ ] No XP, level, stress %, streak, grade or "good/bad" wording on any player-facing screen.
- [ ] All UI animation runs on unscaled time. Reduce motion and Instant text work.
- [ ] Player-visible text never mentions anything from the brief's internal-only section.

---

## 8. What I could not verify

- **On-device rendering:** no phone or Editor was run. Emoji tofu in the HUD, glyph coverage of U+2212 / U+00A0 / ▼ in LegacyRuntime and Nunito, and the actual feel of 45 cps typing are unverified.
- **The physical-size maths** assumes typical 6.4-6.7" phones and Android's 160 dpi baseline. Real devices report bucketed densities, so expect ±10%.
- **Material 3 motion values:** the M3 page would not render for me. The values come from a search summary that matched my prior knowledge.
- **Papers, Please wiki:** the fandom page returned HTTP 402. Its description comes from a search summary plus Wikipedia.
- **Stardew's shipping-summary layout:** the per-category layout is from memory and the patch note on the wiki. The wiki does not describe the screen in detail.
- **Kenney icon pack:** I did not confirm which pack or which icons exist for cash, savings, energy, bill and taxi. That is for the asset-sourcing agent.
- **Target SDK:** "Auto" resolves to whatever SDK is installed on the founder's laptop. Edge-to-edge enforcement depends on that.
- **Mali art:** whether `mali2.png` and the two Assets PNGs are cleared for the game build is for the founder to confirm (they are brand art, not CC0).

---

## Sources

- Game Accessibility Guidelines, readable default font size: https://gameaccessibilityguidelines.com/use-an-easily-readable-default-font-size/
- GameAnalytics, subtitles in mobile games: https://www.gameanalytics.com/blog/adding-subtitles-to-your-mobile-game-dos-and-donts
- W3C WCAG 2.2, Contrast (Minimum) 1.4.3: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
- W3C WCAG 2.2, Non-text Contrast 1.4.11: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- W3C WCAG 2.2, Target Size (Enhanced) 2.5.5: https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html
- Android, accessibility (48 dp targets, contrast): https://developer.android.com/guide/topics/ui/accessibility/apps
- Android, display cutouts: https://developer.android.com/develop/ui/views/layout/display-cutout
- Android, edge-to-edge in Android 15: https://developer.android.com/codelabs/edge-to-edge
- Unity, Screen.safeArea: https://docs.unity3d.com/ScriptReference/Screen-safeArea.html
- Unity, PlayerSettings.Android.renderOutsideSafeArea: https://docs.unity3d.com/ScriptReference/PlayerSettings.Android-renderOutsideSafeArea.html
- Unity uGUI 2.0, Canvas Scaler: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html
- Unity uGUI 2.0, TextMesh Pro: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html
- Pixel Crushers Dialogue System, dialogue UIs (typewriter, fast-forward continue): https://www.pixelcrushers.com/dialogue_system/manual2x/html/dialogue_u_is.html
- Dialogue advance cue example spec: https://github.com/Palmkonde/rpg-webbase/issues/21
- Reigns consequence preview: https://www.gamezebo.com/walkthroughs/reigns-tips-cheats-and-strategies/ and https://emshort.blog/2016/08/20/reigns/
- Papers, Please end-of-day screen: https://papersplease.fandom.com/wiki/End_of_day_screen and https://en.wikipedia.org/wiki/Papers,_Please
- Stardew Valley shipping: https://stardewvalleywiki.com/Shipping
- Banking transaction history UX: https://appricotsoft.com/blog/mobile-banking-app-development-how-to-design-transaction-history-that-users-actually-trust/ and https://www.tapix.io/resources/post/11-essential-ux-laws-every-digital-banking-app-should-follow
- Cozy game design: https://thedesignlab.blog/2025/06/02/the-rise-of-cozy-games-designing-for-calm-comfort-connection/
- Material 3 easing and duration tokens: https://m3.material.io/styles/motion/easing-and-duration/tokens-specs
- Nunito (OFL 1.1): https://github.com/googlefonts/nunito
- Kenney licence (CC0): https://kenney.nl/support
- South African number format: https://medium.com/@fletch.jeff/south-african-number-formats-60927bbf7382 and https://www.siyavula.com/read/za/mathematical-literacy/grade-10/numbers-and-calculations-with-numbers/01-numbers-and-calculations-with-numbers-02
