# Asset manifest: CC0 UI, icons, fonts and audio for the beta

Research phase, 3 Oct 2026. Branch `agents/beta-finish`. Nothing under `Assets/`, `ProjectSettings/` or
`Packages/` was changed. Everything below is staged **outside the repo** in `C:\Temp\MaliGoAssetsStaging\`
(46 MB total, 20 folders), and each folder keeps its licence file.

---

## 0. What the project already has (so we don't duplicate it)

`Assets/` has these Kenney folders: `kenney_animated-characters-protagonists`, `kenney_car-kit`,
`kenney_city-kit-commercial_2.1`, `kenney_city-kit-roads`, `kenney_city-kit-suburban_20`, `kenney_food-kit`,
`kenney_furniture-kit`, `kenney_holiday-kit`, `kenney_mini-arcade`, `kenney_nature-kit`, `kenney_train-kit`,
`kenney_ui-pack-adventure`.

- **UI art in use:** five Adventure PNGs copied to `Assets/Resources/MaliGoUI/` (`banner_modern`, `button_brown`,
  `panel_border_brown`, `panel_brown`, `panel_grey_green`). They're loaded by
  `Assets/Scripts/UI/KenneyUiSprites.cs:17-24` with `Resources.Load` and given 9-slice borders in code via
  `Sprite.Create(..., border)`.
- **Fonts:** none. Every UI script uses the built-in `LegacyRuntime.ttf`, for example
  `Scripts/PlayerIdentity/CharacterCreationUI.cs:40`, `Scripts/Characters/MaliDialogueView.cs:65`,
  `Scripts/Scenarios/ScenarioChoiceUI.cs:103` and `Scripts/UI/ActionPanelUI.cs:159`.
- **Audio:** none. A search of `Assets/` found no `.wav`, `.ogg` or `.mp3`.
- **Icons:** none beyond the Adventure pack's minimap glyphs.
- `Assets/MaliGo/UI` and `Assets/MaliGo/Audio` don't exist yet.

**Runtime-loading note for the build phase:** the UI is built in code, so it can only load files that sit
under a `Resources/` folder, or files a baker copies there (the pattern the `KenneyUiSprites` comment
describes). Put the shortlist in `Assets/MaliGo/UI` and `Assets/MaliGo/Audio` as asked, and also make
the files loadable: either nest them under `Assets/MaliGo/Resources/UI|Audio|Fonts`, or extend the existing
bake step. Each imported file needs a `.meta` with a unique GUID (see `docs/MASTER_PROMPT.md` section 7).

## 1. Brand palette used to judge fit

From `src/theme/colors.ts` in the React Native app: `primary #087a18`, `primaryDark #075f15`,
`brown #9b6639`, `gold #dfa464`, `goldBright #f4c84a`, `surfaceWarm #fff4e9`, `warmSurface #fbf6ec`,
`background #f9fff6`, `text #101010`, `brandInk #1b2f3b`. Type in `src/theme/typography.ts` uses the system font
at weights 700-900 (title 28/900, body 14/700, amount 36/900). The RN app sets no `fontFamily` anywhere.

**General finding:** no stock pack matches the palette out of the box. Kenney's colours are saturated mint
(UI Pack `Green` measures `#16BB77`) and bright blue. The fit comes from using the **white or light-grey**
pieces and **tinting them at runtime** with `Image.color`. As a test I multiplied UI Pack
`Grey/Double/button_rectangle_depth_flat.png` (fill `#DADCE7`) by `#087a18` and got a forest green close to
`primaryDark`. Multiplied by `#9b6639` it gave a convincing brand brown. This agrees with
`docs/research/ui-ux-direction.md`, which recommends procedural rounded rects for surfaces and a single-colour
icon set tinted at runtime.

---

## 2. Licence evidence (checked 2026-10-03)

| Source | Licence line (quoted) | Where checked |
|---|---|---|
| Every kenney.nl pack below | Asset page table row: `License` / `Creative Commons CC0`. Page meta: `CC0 licensed!` | Each pack's `https://kenney.nl/assets/<slug>` page (fetched) |
| Kenney packs (in zip) | `License: (Creative Commons Zero, CC0)  http://creativecommons.org/publicdomain/zero/1.0/` (e.g. `kenney_ui-pack/License.txt`, "UI Pack (2.0) ... Creation date: 12-06-2024"). Older packs say `License (CC0)` | `License.txt` / `license.txt` in each staged folder |
| OpenGameArt tracks | Page field `License(s): CC0` | Each OGA page (URLs below). I wrote a `License.txt` into each folder recording this |
| Freesound recordings | Page licence link `creativecommons.org/publicdomain/zero/1.0/` | Each sound page (URLs below). `License.txt` written per folder |
| Aileron font | Official page JSON-LD `"license":"https://creativecommons.org/publicdomain/zero/1.0/"`. The font's own name table (ID 0) reads `No Rights Reserved.` | https://dotcolon.net/font/aileron/ and `Aileron-Bold.otf`. `License.txt` written (the zip ships none) |

---

## 3. Packs: UI and icons

### 3.1 Kenney UI Pack 2.0 (recommended as a fallback and source of controls)
- **URL:** https://kenney.nl/assets/ui-pack (zip `kenney_ui-pack.zip`, 1.23 MB). **Licence:** CC0 (see §2).
- **Staged:** `C:\Temp\MaliGoAssetsStaging\kenney_ui-pack\` (4.4 MB, 1,315 files). Contains `PNG/{Blue,Green,Grey,Red,Yellow,Extra}/{Default,Double}`,
  `Vector/`, `Font/` (2 Kenney Future TTFs) and `Sounds/` (6 OGGs).
- **Style:** a modern, flat rounded-rectangle kit with an optional 16 px "depth" lip (at Double size), a light inner
  border, and gloss, gradient, line and flat variants. It's clean and grown-up, with nothing fantasy about it.
  Out of the box it's toy-bright (blue, mint, yellow, red). The **Grey** and **Extra** sets are neutral and tint well.
  - **Palette match:** good **only when tinted**. Don't ship the `Green` or `Yellow` colourways as-is
    (`#16BB77` mint and `#FFCC00` yellow are off-brand).
- **Measured** (Double = 2x):
  - `Grey/Double/button_rectangle_depth_flat.png`: 384x128, fill `#DADCE7`, 16 px depth strip `#666880`, corner about 12-16 px.
  - `Grey/Double/button_rectangle_flat.png`: 384x128, 8 px bottom edge.
  - `Extra/Double/input_rectangle.png`: 384x128, white fill with a `#989AAF` outline.
- **Recommended files and roles:**

| Role | File | 9-slice border (L,B,R,T) at Double |
|---|---|---|
| Primary button (tint `#087a18`) | `PNG/Grey/Double/button_rectangle_depth_flat.png` | 24, 32, 24, 24 |
| Secondary button (tint `#fff4e9`, brown label) | `PNG/Grey/Double/button_rectangle_flat.png` | 24, 24, 24, 24 |
| Choice card / panel / text field (tint cream) | `PNG/Extra/Double/input_rectangle.png` | 24, 24, 24, 24 |
| Outlined choice (unselected) | `PNG/Extra/Double/button_rectangle_line.png` | 24, 24, 24, 24 |
| Square icon button (pause, settings, close) | `PNG/Grey/Double/button_square_depth_flat.png` | 24, 32, 24, 24 |
| Round icon button | `PNG/Grey/Double/button_round_depth_flat.png` | n/a (simple) |
| Energy / day progress bar track + fill | `PNG/Grey/Double/slide_horizontal_grey.png` + `PNG/Green/Double/slide_horizontal_color.png` (tint fill gold `#dfa464` or green) | 10, 0, 10, 0 |
| Divider in end-of-day ledger | `PNG/Extra/Double/divider.png` (128x8) | 0 |
| Toggle (sound/music on/off) | `PNG/Grey/Double/check_round_grey.png`, `check_round_color.png` | n/a |

The borders are estimates from the measured corners (the top row is opaque from x=6, the left edge from y=8).
Confirm them in the Sprite Editor. **Do not use** `star.png`, `star_outline*.png`: stars read as ratings or
scores, which design rule §3 bans.

### 3.2 Kenney Game Icons (recommended: system and HUD icons)
- **URL:** https://kenney.nl/assets/game-icons (1.05 MB zip). **Licence:** CC0.
- **Staged:** `...\kenney_game-icons\` (4.8 MB, 434 files): `PNG/{White,Black}/{1x (50 px),2x (100 px)}`, `Vector/`, `Spritesheet/`.
- **Style:** bold, filled, single-colour glyphs with rounded joins, Material-like. Neutral and grown-up. Tinted
  to any brand colour it matches well, and it reads at 48 dp.
- **Note:** 1x and 2x names differ in places. 2x uses `shoppingBasket.png`, `shoppingCart.png`, `exit.png`,
  `movie.png` and the typo `siganl1.png`. 1x uses `basket.png`, `cart.png`, `door.png`, `film.png`, `signal1.png`.
- **Recommended (use `PNG/White/2x/`):**
  - home → `home.png`
  - settings → `gear.png`
  - close → `cross.png`
  - sound → `audioOn.png` / `audioOff.png`
  - music → `musicOn.png` / `musicOff.png`
  - pause → `pause.png`
  - info / first-run tip → `information.png`
  - confirm → `checkmark.png`
  - back → `return.png`
  - airtime / phone scenario → `phone.png`
  - shop / food → `shoppingBasket.png`
  - leave / exit building → `exit.png`
  - save → `save.png`
  - work (stand-in) → `wrench.png`
- **Do not use:** `trophy.png`, `medal1.png`, `medal2.png`, `leaderboardsSimple.png`, `leaderboardsComplex.png`,
  `star.png` (all break the no-scores/leaderboards rule).

### 3.3 Kenney Game Icons Expansion (recommended: 3 icons)
- **URL:** https://kenney.nl/assets/game-icons-expansion (2.0 MB zip). **Licence:** CC0.
- **Staged:** `...\kenney_game-icons-expansion\` (8.7 MB, 810 files). It includes a duplicate `Game icons (base)` folder, which you can ignore.
- **Style:** identical to 3.2, plus a `Colored` set (ignore it and tint the white set instead).
- **Recommended (`PNG/White/2x/`):**
  - money / cash (no currency symbol, so it works for Rand) → `coin.png`
  - taxi / transport → `car.png`
  - personal goal → `flag.png`

### 3.4 Kenney Board Game Icons (recommended: money, bill and time icons)
- **URL:** https://kenney.nl/assets/board-game-icons (1.04 MB zip). **Licence:** CC0.
- **Staged:** `...\kenney_board-game-icons\` (3.2 MB, 774 files): `PNG/Default (64px)`, `PNG/Double (128px)`, `Vector/`, `Tilesheet/`.
- **Style:** filled white glyphs, a little chunkier and rounder than Game Icons but in the same family. They sit
  together well at icon size.
- **Recommended (`PNG/Double (128px)/`):**
  - savings → `pouch.png` (money bag)
  - move money into savings → `pouch_add.png`
  - take money out → `pouch_remove.png`
  - savings growth / stokvel pot → `tokens_stack.png`
  - lending / giving to family → `token_give.png` or `hand_token.png`
  - bill / receipt → `notepad.png`
  - bill being written up (end-of-day ledger) → `notepad_write.png`
  - home (alternative) → `structure_house.png`
  - time left today → `hourglass.png`
  - day progress (pie) → `timer_0.png`, `timer_CW_25.png`, `timer_CW_50.png`, `timer_CW_75.png`, `timer_100.png`
- **Do not use:** `dollar.png` (US$, not Rand), `award.png`, crowns, dice, skulls and swords (off-theme or gambling/combat cues).

### 3.5 Kenney Emotes Pack (optional: world-space speech bubbles)
- **URL:** https://kenney.nl/assets/emotes-pack (380 KB zip). **Licence:** CC0.
- **Staged:** `...\kenney_emotes-pack\` (1.1 MB, 534 files): `PNG/Vector/Style 1-8` and `PNG/Pixel/Style 1-8` (each 32x38 px), plus `Vector/emotes_vector.svg`.
- **Style:** white speech balloons over heads with small coloured glyphs. Friendly, slightly playful.
  - **The PNGs are only 32x38 px**, so scaling them up blurs. Re-export larger from `Vector/emotes_vector.svg` if you use them.
- **Possible uses (`PNG/Vector/Style 1/`):**
  - Mali has something to say → `emote_dots3.png`
  - an NPC with a pending scenario → `emote_exclamation.png`
  - sleep prompt at home → `emote_sleeps.png`
  - idea / tip → `emote_idea.png`
- **Avoid:** `emote_cash.png` (orange `$`), `emote_faceSad.png`, `emote_faceAngry.png`, `emote_anger.png` (they judge).

### 3.6 Kenney UI Pack: RPG Expansion (staged, NOT recommended)
- **URL:** https://kenney.nl/assets/ui-pack-rpg-expansion (214 KB zip). **Licence:** CC0.
- **Staged:** `...\kenney_ui-pack-rpg-expansion\` (457 KB, 94 files).
- **Style:** riveted parchment and wood panels, swords, gauntlet cursors (preview checked). Panels are 100x100 and buttons 190x49, so they're low-res for phones.
- **Verdict:** this is the fantasy-RPG look the brief and `ui-ux-direction.md` say to retire.
  The one usable piece is `PNG/panel_beigeLight.png` (`#ECE3CE`, a cream fill), and a procedural rounded rect does the same job better.

### 3.7 Icon gaps (no CC0 Kenney match)

| Role | Status | Recommendation |
|---|---|---|
| **Energy** | No bolt or battery in any Kenney pack | **Procedural** lightning-bolt polygon (6 points) in the `UiKit`, tinted gold `#dfa464`. Not CC0 (needs approval): System UIcons `lightning.svg` / `battery_*.svg` (see §6) |
| **Calendar / day** | No calendar glyph | Show "Day 3" as text in the brand font inside a rounded chip, or use `hourglass.png` / `timer_*` (§3.4). Not CC0 (needs approval): System UIcons `calendar_day.svg` |
| **Bank** | No bank building glyph | **Procedural** (triangle roof + 3 pillars + base, all rects), or reuse the in-world bank building's look as a thumbnail. Not CC0 (needs approval): none found with a bank glyph either |
| **Work** | Only `wrench.png` (§3.2) | `wrench.png` as a stand-in. Not CC0 (needs approval): System UIcons `briefcase.svg` |

I looked for a CC0 set that covers all of these in one style. OpenGameArt "New Icons Pack" (CC0) turned out to be
generic media glyphs, so I deleted it from staging. "CC0 Currency Icons" is pixel fantasy coins. SVG Repo has
per-icon CC0 items, but they come from mixed sets with no consistent style, and each licence would need checking one by one.

---

## 4. Fonts

### 4.1 Aileron (CC0): **recommended brand font, no approval needed**
- **URL:** https://dotcolon.net/font/aileron/ by Sora Sagano (dotcolon). Zip: https://dotcolon.net/files/fonts/aileron_0102.zip (351 KB, v1.102).
- **Licence:** CC0 1.0. Evidence: the page JSON-LD `"license":"https://creativecommons.org/publicdomain/zero/1.0/"`, and the font name table reads `No Rights Reserved.`
- **Staged:** `C:\Temp\MaliGoAssetsStaging\dotcolon_aileron\` (501 KB, 16 OTFs + my `License.txt`).
  Weights: UltraLight, Thin, Light, Regular, SemiBold, Bold, Heavy, Black, each with an italic.
- **Style:** a neo-grotesque in the Helvetica family, neutral and serious, like a banking app. It's close to the RN
  app's system-font look, and Heavy and Black cover the RN 800/900 weights. I rendered "You started the day with
  R350. You ended it with R212." in Black, Bold, SemiBold and Regular on `#fff4e9`: crisp and grown-up.
  It's less warm than Nunito, so Mali's warmth has to come from her portrait and copy.
- **Glyph check** (fontTools, `Aileron-Regular.otf`, 337 glyphs):
  - present: U+2212 minus, U+2013, U+2019, ê, ë, é, š, Ŋ, …, €
  - missing: **U+00A0 no-break space** (also U+2009, U+202F), ▼ ✓ ✕
  - So format Rand with a plain space or no separator ("R1250", "R 1 250"), and draw arrows, ticks and crosses as sprites.
- **Use:**
  - `Aileron-Black.otf`: the end-of-day amounts (36-44 u)
  - `Aileron-Heavy.otf`: titles
  - `Aileron-Bold.otf`: buttons and choice text
  - `Aileron-SemiBold.otf`: body and dialogue
  - `Aileron-Regular.otf`: captions
  - Unity's legacy `Text` accepts OTF as a dynamic Font. Import each weight as its own Font asset, because legacy `Text` fakes bold.

### 4.2 Kenney Fonts (CC0): staged, NOT recommended for text
- **URL:** https://kenney.nl/assets/kenney-fonts (58 KB zip). **Licence:** CC0.
- **Staged:** `...\kenney_kenney-fonts\` (357 KB, 12 TTFs: Future, Future Narrow, High, Mini, Pixel, Rocket, Blocks plus Square variants).
- **Style** (rendered sample): Future, Future Narrow and Rocket are all-caps, wide and sci-fi; Mini, Pixel, High and Blocks are pixel fonts.
  - Kenney Future has 214 glyphs and lacks U+2212 and U+2013.
  - **Verdict:** wrong tone for a calm money game. Don't use it for dialogue or amounts.
  - The UI Pack's `Font/` folder holds the same Kenney Future files.

### 4.3 OFL alternatives: **NEEDS FOUNDER APPROVAL** (not downloaded)
All are SIL OFL 1.1. Each `OFL.txt` is confirmed to exist (HTTP 200) at
`https://raw.githubusercontent.com/google/fonts/main/ofl/<name>/OFL.txt`. Nunito's file reads "This Font Software is
licensed under the SIL Open Font License, Version 1.1."

| Font | Why | Source |
|---|---|---|
| **Nunito** | Rounded and warm, weights to 900. This is `ui-ux-direction.md`'s pick | https://github.com/googlefonts/nunito |
| Plus Jakarta Sans | Modern fintech feel, weights to 800 | google/fonts `ofl/plusjakartasans` |
| Inter | Most legible UI sans, huge glyph coverage | google/fonts `ofl/inter` |
| DM Sans / Figtree / Manrope | Friendly geometric alternatives | google/fonts `ofl/dmsans`, `ofl/figtree`, `ofl/manrope` |
| Atkinson Hyperlegible | Accessibility-first fallback | google/fonts `ofl/atkinsonhyperlegible` |
| Roboto | Literally what the RN app shows on Android | google/fonts `ofl/roboto` |

---

## 5. Audio

I **could not listen to any audio** (no player or decoder here). Durations were read from the OGG headers or
the source pages, and the role picks are based on file names, pack descriptions and lengths. **Someone must
audition every pick** before it's wired in.

Design rule for sound: a money change plays **the same neutral sound whether money goes up or down**. Never
use `error_*` or "fail" jingles on a choice, because that would judge the player.

### 5.1 Kenney Interface Sounds (recommended: core UI)
- **URL:** https://kenney.nl/assets/interface-sounds (835 KB zip). **Licence:** CC0.
- **Staged:** `...\kenney_interface-sounds\Audio\` (1.2 MB, 100 OGGs). Soft modern UI blips: clicks, glass, pluck, open/close.
- **Picks:**

| Role | File | Length |
|---|---|---|
| Tap / button | `click_001.ogg` | 0.10 s |
| Confirm a choice | `confirmation_001.ogg` | 0.29 s |
| Cancel / back | `back_001.ogg` | 0.06 s |
| Panel open / close | `open_001.ogg` / `close_001.ogg` | 0.15 s each |
| **Mali notification** (soft) | `glass_001.ogg` (alts: `pluck_002.ogg` 0.16 s, `question_002.ogg` 0.33 s) | 0.28 s |
| Bill due / reminder (neutral) | `bong_001.ogg` | 0.12 s |
| Toggle | `toggle_001.ogg` | 0.14 s |

- **Avoid** `error_001..008`, `glitch_*` and `scratch_*`.

### 5.2 Kenney UI Audio (alternates)
- **URL:** https://kenney.nl/assets/ui-audio (412 KB zip). **Licence:** CC0.
- **Staged:** `...\kenney_ui-audio\Audio\` (599 KB, 50 OGGs + preview): `click1-5`, `rollover1-6`, `switch1-38`.
- **Picks:** `click3.ogg` (0.09 s) as an alternative tap, and `switch2.ogg` (0.30 s) as an alternative toggle.
- The UI Pack's own `kenney_ui-pack\Sounds\tap-a.ogg` (0.09 s) and `switch-a.ogg` (0.23 s) are also good, softer taps.

### 5.3 Kenney Music Jingles (end-of-day chime)
- **URL:** https://kenney.nl/assets/music-jingles (1.24 MB zip). **Licence:** CC0.
- **Staged:** `...\kenney_music-jingles\Audio\` (1.6 MB, 85 OGGs in 8-Bit, Hit, Pizzicato, Sax and Steel sets).
- **Picks** (audition required):
  - **End-of-day reveal chime:** `Steel jingles/jingles_STEEL01.ogg` (1.38 s), a warm steel-pan feel. Alternative: `Pizzicato jingles/jingles_PIZZI07.ogg` (1.32 s).
  - **Chapter end:** `Sax jingles/jingles_SAX07.ogg` (1.74 s).
  - **New day / wake:** `Pizzicato jingles/jingles_PIZZI01.ogg` (1.00 s).
- Use **one** chime regardless of how the day went.
- **Avoid** `8-Bit jingles/*` (retro-arcade) and `Hit jingles/*` (stingers that can read as win or fail).

### 5.4 Kenney RPG Audio (money and world foley)
- **URL:** https://kenney.nl/assets/rpg-audio (965 KB zip). **Licence:** CC0.
- **Staged:** `...\kenney_rpg-audio\Audio\` (1.2 MB, 51 OGGs).
- **Picks:**

| Role | File | Length |
|---|---|---|
| **Money moved** (any direction) | `handleCoins2.ogg` | 0.34 s |
| Bigger amount / payday | `handleCoins.ogg` | 0.85 s |
| End-of-day ledger lines appearing (paper) | `bookFlip3.ogg` | 0.23 s |
| Ledger shown | `bookPlace1.ogg` | 0.28 s |
| Enter / leave home or shop | `doorOpen_1.ogg`, `doorClose_1.ogg` | 0.92 s / 0.68 s |
| Footsteps | `footstep00.ogg`-`footstep09.ogg` | 0.22-0.32 s |

- **Avoid** `drawKnife*`, `knifeSlice*` and `chop.ogg`.

### 5.5 South African ambience: Freesound, CC0 (recommended: this is the moat)
Freesound only lets logged-in users download originals, so I staged the public **HQ previews** (128 kbps
MP3). That quality is fine for a background bed on a phone. A team member with a free account can fetch the
originals for better quality.

| Folder | Source | Duration | Use |
|---|---|---|---|
| `freesound_soweto-township-day_jackaTTackeditor\soweto-township-day-ambience.mp3` (2.3 MB) | https://freesound.org/people/jackaTTackeditor/sounds/417744/ "Soweto - Busy Township Day Ambience". Licence link `creativecommons.org/publicdomain/zero/1.0/` | 2:12 (original AIFF, 48 kHz/24-bit, 36.3 MB) | **Main world ambience bed**, low volume |
| `freesound_knysna-birds_hedgehog1\knysna-morning-birds.mp3` (1.7 MB) | https://freesound.org/people/hedgehog1/sounds/328915/ "Bird Sounds of Knysna". CC0 | 1:12 | Morning / home / Mali scenes. Turtle dove, hadeda, sunbird |
| `freesound_johannesburg-cbd_noisymichael\johannesburg-cbd-walk.mp3` (7.9 MB) | https://freesound.org/people/noisymichael/sounds/725158/ "Johannesburg_CBD", binaural. CC0 | 5:55 | **Taxi rank / town zone**: hawkers, taxis hooting. Trim to a 60-90 s loop to save size |

None of these is guaranteed to loop seamlessly. Cut each one and crossfade the loop point (about 2 s) in
Audacity before import.

### 5.6 Music: OpenGameArt, CC0

| Folder | Source | Duration | Fit |
|---|---|---|---|
| `oga_feel-good-slow-ambient_annandistance\ambienttrack.ogg` (1.8 MB) | https://opengameart.org/content/feel-good-slow-ambient-track-with-a-beat by annandistance. `License(s): CC0` | 1:54.7 | **Recommended calm music loop.** "A simple ambient track with a slow beat in A Major ... cozy" |
| `oga_ambient-relaxing-loop_isaiah658\Ambient-Loop-isaiah658.ogg` (1.3 MB) | https://opengameart.org/content/ambient-relaxing-loop by isaiah658. CC0 | 0:24.5 | Seamless pad loop. Pause menu / character creation |
| `oga_contemplation_joth\Contemplation.mp3` (2.3 MB) | https://opengameart.org/content/contemplation-0 by Joth. CC0 | about 2:00 | "No real melody ... just ambience". **Mali's chapter-end reflection** |
| `oga_calm-loop_wipics\Relaxing.mp3` (309 KB) | https://opengameart.org/content/calm-loop by wipics. CC0 | about 0:19 | Short chill synth loop, a backup |
| `oga_ambient-bird-sounds_isaiah658\birds-isaiah658.ogg` (533 KB) | https://opengameart.org/content/ambient-bird-sounds by isaiah658. CC0 | 0:30.7 (48 kHz) | A generic birds bed. Fallback if the Knysna one doesn't loop well |

Rejected:
- "Feel Good Island Loop" (page lists `OGA-BY 3.0` alongside CC0, so it's mixed).
- "Hadedas flying in the distance" (Freesound 407657, CC BY-NC 4.0, not usable).
- "hadedas and thunder" (CC0, but recorded in Zambia with thunder).
- I downloaded **Kenney Casino Audio** and then **deleted it**, because chip and dice sounds signal gambling in a money game.

---

## 6. NEEDS FOUNDER APPROVAL (not CC0; nothing downloaded)

| Asset | Licence | Why it's worth it |
|---|---|---|
| Nunito (and the other fonts in §4.3) | SIL OFL 1.1 | Warmer than Aileron. Nunito is `ui-ux-direction.md`'s pick |
| System UIcons: https://github.com/CoreyGinnivan/system-uicons (`lightning.svg`, `battery_*.svg`, `calendar_day.svg`, `briefcase.svg`, `wallet.svg`, `receipt.svg`, `coins.svg`, `bell.svg`) | The Unlicense (GitHub API `spdx_id: Unlicense`, "released into the public domain"). Public-domain-equivalent but not CC0 | Fills the energy, calendar and work gaps. Note: it's a **thin line** style (21 px stroke icons) that clashes with Kenney's filled glyphs, and it's SVG only, so it needs rasterising |
| "Park Ambience" by mars_98: https://freesound.org/people/mars_98/sounds/444904/ | CC BY 4.0 (attribution required) | Glenhazel, Johannesburg suburb with hadedas and pigeons, 2:08. The most "SA neighbourhood" bed found |
| Tabler / Phosphor / Lucide / Health Icons | MIT / ISC | Complete, consistent icon sets with bank, calendar and energy glyphs |

---

## 7. Size summary (staging)

| Folder | Size | Files |
|---|---|---|
| kenney_ui-pack | 4.4 MB | 1,315 |
| kenney_game-icons | 4.8 MB | 434 |
| kenney_game-icons-expansion | 8.7 MB | 810 |
| kenney_board-game-icons | 3.2 MB | 774 |
| kenney_emotes-pack | 1.1 MB | 534 |
| kenney_ui-pack-rpg-expansion | 457 KB | 94 |
| kenney_kenney-fonts | 357 KB | 13 |
| dotcolon_aileron | 501 KB | 17 |
| kenney_interface-sounds | 1.2 MB | 103 |
| kenney_ui-audio | 599 KB | 55 |
| kenney_music-jingles | 1.6 MB | 89 |
| kenney_rpg-audio | 1.2 MB | 55 |
| freesound_* (3) | 11.9 MB | 6 |
| oga_* (5) | 6.2 MB | 10 |
| **Total** | **46 MB** | |

The shortlist below adds about **5 MB** to the project if the Johannesburg CBD clip is trimmed to about 90 s
(about 1.4 MB at 128 kbps).

---

## 8. Shortlist: exact files to import

Paths are relative to `C:\Temp\MaliGoAssetsStaging\`. Copy each pack's licence file alongside as
`Assets/MaliGo/Licenses/<pack>-License.txt`. See the runtime-loading note in §0 about `Resources/`.

**→ `Assets/MaliGo/UI/Panels/`** (tinted at runtime; or skip if the procedural `UiKit` lands first)
- `kenney_ui-pack/PNG/Grey/Double/button_rectangle_depth_flat.png`
- `kenney_ui-pack/PNG/Grey/Double/button_rectangle_flat.png`
- `kenney_ui-pack/PNG/Grey/Double/button_square_depth_flat.png`
- `kenney_ui-pack/PNG/Extra/Double/input_rectangle.png`
- `kenney_ui-pack/PNG/Extra/Double/button_rectangle_line.png`
- `kenney_ui-pack/PNG/Extra/Double/divider.png`
- `kenney_ui-pack/PNG/Grey/Double/slide_horizontal_grey.png`
- `kenney_ui-pack/PNG/Green/Double/slide_horizontal_color.png`

**→ `Assets/MaliGo/UI/Icons/`** (all white, tinted at runtime)
- From `kenney_game-icons/PNG/White/2x/`: `home.png`, `gear.png`, `cross.png`, `audioOn.png`, `audioOff.png`,
  `musicOn.png`, `musicOff.png`, `pause.png`, `information.png`, `checkmark.png`, `return.png`, `phone.png`,
  `shoppingBasket.png`, `exit.png`, `wrench.png`
- From `kenney_game-icons-expansion/PNG/White/2x/`: `coin.png`, `car.png`, `flag.png`
- From `kenney_board-game-icons/PNG/Double (128px)/`: `pouch.png`, `pouch_add.png`, `pouch_remove.png`,
  `tokens_stack.png`, `token_give.png`, `notepad.png`, `notepad_write.png`, `hourglass.png`, `timer_0.png`,
  `timer_CW_25.png`, `timer_CW_50.png`, `timer_CW_75.png`, `timer_100.png`
- Draw these procedurally: **energy bolt**, **bank**, **calendar/day chip**.
- Optional world bubbles from `kenney_emotes-pack/PNG/Vector/Style 1/`: `emote_dots3.png`, `emote_exclamation.png`, `emote_sleeps.png`

**→ `Assets/MaliGo/UI/Fonts/`**
- `dotcolon_aileron/Aileron-Black.otf`, `Aileron-Heavy.otf`, `Aileron-Bold.otf`, `Aileron-SemiBold.otf`,
  `Aileron-Regular.otf` + `License.txt`
- (Swap to Nunito only if the founder approves OFL.)

**→ `Assets/MaliGo/Audio/UI/`**
- `kenney_interface-sounds/Audio/click_001.ogg`, `confirmation_001.ogg`, `back_001.ogg`, `open_001.ogg`,
  `close_001.ogg`, `glass_001.ogg` (Mali cue), `bong_001.ogg`, `toggle_001.ogg`
- `kenney_ui-pack/Sounds/tap-a.ogg`
- `kenney_rpg-audio/Audio/handleCoins2.ogg` (money moved), `handleCoins.ogg`, `bookFlip3.ogg`, `bookPlace1.ogg`,
  `doorOpen_1.ogg`, `doorClose_1.ogg`

**→ `Assets/MaliGo/Audio/Jingles/`**
- `kenney_music-jingles/Audio/Steel jingles/jingles_STEEL01.ogg` (end-of-day chime)
- `kenney_music-jingles/Audio/Pizzicato jingles/jingles_PIZZI01.ogg` (new day)
- `kenney_music-jingles/Audio/Sax jingles/jingles_SAX07.ogg` (chapter end)

**→ `Assets/MaliGo/Audio/Ambience/`** (trim and crossfade-loop first; import as Streaming / Vorbis, Load In Background)
- `freesound_soweto-township-day_jackaTTackeditor/soweto-township-day-ambience.mp3`
- `freesound_knysna-birds_hedgehog1/knysna-morning-birds.mp3`
- `freesound_johannesburg-cbd_noisymichael/johannesburg-cbd-walk.mp3` (trimmed to about 90 s)

**→ `Assets/MaliGo/Audio/Music/`**
- `oga_feel-good-slow-ambient_annandistance/ambienttrack.ogg` (world music, low volume, switchable)
- `oga_contemplation_joth/Contemplation.mp3` (Mali's chapter reflection)
- `oga_ambient-relaxing-loop_isaiah658/Ambient-Loop-isaiah658.ogg` (menus)

## 9. What I could not verify
- **How any sound actually sounds:** I listened to nothing. Loudness, tone, whether the loops are seamless and
  whether the jingles feel neutral are all unconfirmed.
- **9-slice borders:** estimated from pixel sampling, not set in Unity's Sprite Editor.
- **Rendering on a phone:** no Editor or phone was run. Icon legibility at 48 dp and Aileron at small sizes are unconfirmed.
- **Freesound originals:** only the previews are staged. I didn't compare their quality against the originals.
- **MP3 durations for the OGA files:** estimated from bitrate. The Freesound durations come from the source pages.
