# UI current state: every player-facing surface, read on a landscape phone

Phase 1 research report (ui-current-state). Branch `agents/beta-finish`, read 3 Oct 2026.
Read-only: nothing under `Assets/`, `ProjectSettings/` or `Packages/` was changed.

## 0. Summary

- All game UI is legacy uGUI (`UnityEngine.UI.Text`), using Unity's built-in `LegacyRuntime.ttf`
  (Arial). There is no TextMeshPro, no custom font, and no safe-area code anywhere
  (`grep safeArea|TMPro` over `Assets/Scripts` and `Assets/Editor` finds nothing).
- Every canvas is Screen Space Overlay, `ScaleWithScreenSize`, reference 1920x1080. All except
  the mobile controls match **width** (`matchWidthOrHeight = 0`, the default). The mobile controls
  match **height** (`MobileControlsUI.cs:302`).
- Converted to real phone sizes (section 1), **every text size in the game is under 12 sp and
  every tappable control except the joystick and the ACT button is under 48 dp.** The biggest
  body text (Mali's line, 22 units) is about 10.5 sp on a 2400x1080 phone and 9.2 sp on a
  1600x720 phone.
- **The core moment has no UI.** Nothing records the start-of-day balance (no
  `startCash`/`dayStart`-style field in `Assets/Scripts` or `Assets/MaliGo`). Sleeping
  (`HomeInteraction.cs:320-361`) only posts a Mali line, "Morning, {name}. Day N. Rent: R… paid.",
  in the small dialogue box.
- Mali has a name label but **no portrait.** Mali artwork is in the project
  (`Assets/Mali Dumbfound.png`, 520x657) but nothing references it.
- Prompts say **"Press E …"** on a phone that has no E key. The on-screen button says **ACT**.
  The prompt itself can't be tapped.
- Computed layout defects, not yet seen on a device: two-line scenario descriptions are hidden
  behind the first choice button; long HUD goal text is cut off before the Rand amount; the Home
  panel's bill list runs into its button; prompts stack on top of each other when the player is
  near Mali (who follows the player) and a location at the same time.
- Kenney UI Pack Adventure (CC0, 128 PNGs at 1x, 128 at 2x, 128 SVGs, 2 spritesheets) is in the
  project. **Three sprites are actually rendered:** `panel_brown`, `button_brown` and
  `panel_grey_green`. Two more are copied into Resources but never used. The look reads as a
  fantasy-RPG asset pack (cream parchment with brown frames, blue-grey HUD), not the MaliGo
  forest-green and cream brand.

## 1. How the scaling works, converted to real phones

Reference 1920x1080 at `ScaleWithScreenSize`:

- **Width-matched canvases** (everything except the mobile controls): 1 reference unit =
  (screen width in dp) / 1920 dp.
- **Height-matched canvas** (`MobileControls_Canvas`): 1 unit = (screen height in dp) / 1080 dp.

Two reference phones in landscape (dp figures are standard Android density buckets; I have not
measured them on hardware):

| Device | px | density | dp (landscape) | dp per unit (width-match) | dp per unit (height-match) |
|---|---|---|---|---|---|
| A: typical 20:9 mid/flagship (Pixel 7 class) | 2400x1080 | 2.625 | 914x411 | **0.476** | 0.381 |
| B: typical budget HD+ 20:9 (Galaxy A0x class) | 1600x720 | 2.0 | 800x360 | **0.417** | 0.333 |

Reference sizes converted. Targets: Android/Material body text 14-16 sp (12 sp is the floor),
and touch targets of at least 48 dp, which the brief requires.

| Ref font size | Used for | Phone A (sp) | Phone B (sp) |
|---|---|---|---|
| 15 | Choice consequence line (`ScenarioChoiceUI.cs:161`) | 7.1 | 6.3 |
| 16 | Character-creation labels, dropdowns, step indicator | 7.6 | 6.7 |
| 18 | Prompts, HUD stats, action buttons, Mali speaker name | 8.6 | 7.5 |
| 19 | Home/Bank body | 9.0 | 7.9 |
| 20 | Choice label, HUD player line, CC summary | 9.5 | 8.3 |
| 22 | Mali body line, HUD goal value | 10.5 | 9.2 |
| 22 (height-matched) | ACT label | 8.4 | 7.3 |
| 26 | Scenario/panel title | 12.4 | 10.8 |
| 30 | CC step title | 14.3 | 12.5 |

**Rule of thumb for the design spec:** with this scaler, body text needs about **34 units** to
reach 16 sp on phone A and **38 units** on phone B. A 48 dp target needs about **101 units**
(A) and **115 units** (B) in its smaller dimension.

Project settings that matter (`ProjectSettings/ProjectSettings.asset`):

- `defaultScreenOrientation: 3`, which is LandscapeLeft (line 11).
- `androidStartInFullscreen: 1` (line 70).
- **`androidRenderOutsideSafeArea: 1`** (line 71). The game draws under camera cutouts and no UI
  reads `Screen.safeArea`, so anything near the left or right edge can sit under a punch-hole or
  notch, or inside the system back-gesture zone.

Build scenes (`ProjectSettings/EditorBuildSettings.asset`): `CharacterCreation.unity` and
`MaliGoWorld.unity` (enabled); `SampleScene` and `MaliGoIsometricWorld` (disabled).

## 2. Canvas inventory

The game shows up to about 17 overlay canvases at once. `ScenarioWorldWiring.cs:33-63` creates
8 scenario triggers and `WorldLocationWiring.cs` creates 3 locations; each builds its own prompt
canvas.

| Canvas | Built at | Sort order | Scaler | Raycaster |
|---|---|---|---|---|
| `CharacterCreation_Canvas` | `CharacterCreationUI.cs:48-54` | 0 | 1920x1080, width | yes |
| `MaliGo_Canvas` (HUD; in scene) | `MaliGoWorld.unity:11510-11570`; generated by `Editor/MaliGoWorldGenerator.cs:497-585` / `MaliGoSceneSetup.cs:139-242` | 0 | 1920x1080, match 0 (scene YAML line ~11560) | yes |
| `<obj>_PromptCanvas` (Home/Bank/Work) | `ProximityInteraction.cs:113-122` | 15 | 1920x1080, width | no |
| `ScenarioPrompt_Canvas` (x8) | `ScenarioTrigger.cs:304-313` | 15 | 1920x1080, width | no |
| `MaliPrompt_Canvas` | `MaliCompanionInteraction.cs:148-157` | 15 | 1920x1080, width | no |
| `MaliDialogue_Canvas` | `MaliDialogueView.cs:32-41` | 20 | 1920x1080, width | yes |
| `MobileControls_Canvas` | `MobileControlsUI.cs:292-303` | 25 | 1920x1080, **height** | yes |
| `ScenarioChoice_Canvas` | `ScenarioChoiceUI.cs:36-45` | 30 | 1920x1080, width | yes |
| `ActionPanel_Canvas` (Home, and a separate one for Bank) | `ActionPanelUI.cs:236-245` | 30 | 1920x1080, width | yes |
| Dropdown list + blocker (Unity creates these) | when a CC dropdown opens | 30000 | inherits | yes |

`EventSystemUtility.cs:245-265` makes sure an EventSystem with `InputSystemUIInputModule` exists.
That is correct for this Input-System-only project.

All prompts share sort order 15, and the choice and action panels share 30, so when two of them
are visible at once their draw order is undefined.

---

## 3. Surface by surface

### 3.1 CharacterCreationUI (`Assets/Scripts/PlayerIdentity/CharacterCreationUI.cs`)

- **Built:** `Awake` (36-44) → `BuildCanvas` (46-77). Added at runtime by
  `MaliGoIdentityRuntimeBootstrap.cs:129-133`. The CharacterCreation scene has no canvas of its
  own.
- **Layout:**
  - Full-screen `MossGreen` #087A18 background (56).
  - Centred `MainPanel` 840x560 in `DeepPanel` #0F5E2E at 95% (58). On phone A that is
    400x267 dp.
  - Step indicator, title and body sit at fixed top-left offsets (60-63). Step content goes in
    `StepRoot`: panel inset 24/96 at the bottom, -170 at the top, so it is 294 units tall (65-71).
  - Back and Next buttons are 140x48, bottom-left and bottom-right (73-74).
- **Font and sizes:** LegacyRuntime/Arial (40-41).
  - Step indicator 16 Sage; title 30 bold Cream; body 18 Cream; error 16 italic GoldenAmber.
  - Field labels 16 bold Sage (145, 185, 208, 215).
  - Dropdown caption and items 16 (588, 663); dropdown arrow "▼" 14 (601-603).
  - Input text 20 (542); button labels 18 bold Cream (517); completion summary 20 (180).
- **Colours and contrast** (WCAG ratios, computed):

  | Pair | Ratio |
  |---|---|
  | Cream on DeepPanel | 7.7:1 |
  | Sage on DeepPanel | 6.1:1 |
  | GoldenAmber error on DeepPanel | **3.6:1** (fails 4.5:1 for small text) |
  | **Cream "Next" label on GoldenAmber #DFA464** | **2.2:1, fails** |
  | **Cream "Back" label on Sage #C9EFB4** | **1.25:1, effectively invisible** |

- **Sprites:** none. Flat colour `Image`s only.
- **Text display:** instant. **Dismissal:** Next moves forward, Back moves back; the final step
  reads "Begin Journey" (107) and loads the world.
- **Tap targets** (phone A / B):

  | Control | Ref size | Phone A | Phone B |
  |---|---|---|---|
  | Back / Next | 140x48 | 67x23 dp | 58x20 dp |
  | Dropdown | 500x32 (146, 209) | 15 dp tall | 13 dp tall |
  | Dropdown item | 28 tall (642) | 13 dp | 12 dp |
  | Confidence slider track | 24 tall (224) | 11 dp | 10 dp |
  | Confidence slider handle | 18x18 (257) | 8.6 dp | 7.5 dp |
  | Name input | 44 tall after insets (117, 529-530) | 21 dp | 18 dp |

- **Defects:**
  1. Every control is far below 48 dp and all text is 7-10 sp, except the title (about 14 sp on
     A).
  2. The Back label can't be read (1.25:1) and the Next label barely can (2.2:1).
  3. **Raw tokens appear in dropdowns.** Financial-mirror options are passed straight through
     (158-163), so players see `part_time`, `self_employed`, `emergency_fund`. `FormatToken` (460)
     is applied only on the summary step.
  4. **Content overflows in the financial step.** It stacks 8 rows at 44-unit pitch (157-164,
     183-273): 8×44 = 352 units in a 294-unit `StepRoot`.
     - The last row ("Financial confidence" label, slider, "3/5" value) lands 54-82 units above
       the panel bottom, while the Back/Next buttons occupy 24-72.
     - Computed overlaps: the label overlaps Back (x 24-164, y 54-72) and the "3/5" value overlaps
       Next (x 656-796, y 54-72).
  5. **Placeholder copy is shown to players:** "Visual assets will expand in a future update."
     (124).
  6. "Begin Journey" ends on a lecturing line: "build healthier money habits" (178).
  7. Mali doesn't appear anywhere in first-run.
  8. There is no first-run guidance on the controls.
  9. The 7 appearance dropdowns don't preview anything. The appearance step has no character
     preview.
  10. Risk tolerance, investing and similar fields are generic finance-app questions; nothing is
      SA-specific.

### 3.2 HUD: `MaliGo_Canvas` and HUDController

- **Built:** baked into `Assets/Scenes/MaliGoWorld.unity`. The canvas GameObject is at line 11523
  and the scaler at 11546-11563 (`m_ReferenceResolution 1920x1080`, `m_MatchWidthOrHeight: 0`).
  It was generated by `Editor/MaliGoWorldGenerator.cs:497-585`.
- **Runtime binding:** `HUDController` is added by `MaliGoIdentityRuntimeBootstrap.cs:79-86`
  (`Assets/Scripts/PlayerIdentity/HUDController.cs`). It finds its labels **by matching their
  current text** (51-98), e.g. `Contains("Cash:")`, so it breaks silently if anyone edits a label
  in the scene.
- **Layout (from scene YAML):**

  | Element | Scene line | Anchor / position | Size | Sprite / colour | Text |
  |---|---|---|---|---|---|
  | `HUD_StatusPanel` | 7666 | top-left, (24, -24) | 300x170 (A: 143x81 dp) | embedded sprite on `panel_grey_green` (fill #94AFC6), 9-slice 12 | — |
  | Player line | 11242 | (16, -14) | 268x28 | — | 20 bold, "Player: {name} \| Level {n}" (`HUDController.cs:132`) |
  | XP bar | 3277 / 11167 | — | 268x12 (A: 5.7 dp tall) | gold #DFA464 fill on black 40% | — |
  | Cash / Savings / Financial Stress | 3011, 9149, 4043 | — | — | — | 18 regular; strings in `HUDController.cs:139-149` |
  | `Goal_Panel` | 1255 | top-right, (-24, -24) | 280x110 | `panel_grey_green` | title 18 bold "Today's Financial Goal:"; value 22 bold #9E610F |

  The Cash, Savings and Financial Stress strings start with emoji: "💰 Cash: R…",
  "🏦 Savings: R…", "🌱 Financial Stress: n%". The goal value reads "{goal}: Save R{n} today"
  (161).
- **Text colour:** #0F331F on #94AFC6 = 6.1:1. **Goal value #9E610F on #94AFC6 = 2.2:1, fails.**
  This is the most important number on the panel and the hardest to read.
- **Font:** built-in Arial (scene `m_Font: {fileID: 10102}`). Overflow is Wrap horizontally and
  **Truncate vertically** (`m_VerticalOverflow: 0`, around line 1153).
- **Text display:** instant, refreshed on `OnPlayerDataChanged` (30, 120-169). **Dismissal:**
  always visible; it is not interactive.
- **Defects:**
  1. **The goal value is truncated.** "Emergency Fund: Save R200 today" at 22 bold measures about
     361 units against a 248-unit box (measured with Arial Bold 22 via PIL). It wraps to two lines,
     and the 28-unit-tall box truncates the second, so the player sees roughly
     "Emergency Fund: Save" **without the Rand amount.** "Home Deposit: Save R300 today" (331)
     does the same.
  2. **Emoji are unlikely to render** in legacy `Text` with Arial on Android. Expect blanks or
     tofu boxes. Not verified on a device.
  3. **The HUD lacks** energy, the day number, bills and a start-of-day balance, all of which the
     brief requires. Energy is shown only inside the Home panel (`HomeInteraction.cs:281`).
  4. "Level n" and an XP bar are shown. That conflicts with the rule against showing scores and
     progress meters. "Financial Stress: n%" is effectively a visible score too. Both need a
     design decision.
  5. Text is 8.6-10.5 sp on phone A.
  6. `HUD_StatusPanel` sits 24 units (about 11 dp) from the left and top edges, with
     `androidRenderOutsideSafeArea: 1`, so on phones with a corner punch-hole it can sit under the
     cutout.
  7. Sort order 0 means every other canvas draws over it, which is fine.
  8. `FormatCurrency` uses `R{amount:0,0}` (173). That gives "R00" for zero cash: the format
     forces two digits, so R0 displays as "R00".

### 3.3 Mali dialogue: MaliDialogueView and MaliDialogueController

- **Built:** `Assets/Scripts/Characters/MaliDialogueView.cs:25-72`. Added to the Mali GameObject
  by `PlayerCharacterSpawner.cs:294`; the controller is in `MaliDialogueController.cs`.
- **Layout:** panel anchored bottom-centre, pivot bottom, y = 48, size **760x140** (47-51).
  - Phone A: 362x67 dp, about 40% of screen width.
  - Phone B: 317x58 dp.
- **Sprite:** `KenneyUiSprites.PanelWarm` = `panel_brown`, 9-slice 14 (54-59): cream #FFF1D2
  parchment with a brown frame. Falls back to DeepForest #0F5E2E.
- **Text:**
  - Speaker "Mali": 18 bold DeepForest, 704x28 at (28, -18) (68-69). 7.1:1 on cream.
  - Body: 22 regular DarkBrown #54381F, 704x68 at (28, -50) (71). 9.6:1.
  - Wrap on; **vertical overflow on** (92-93), so a 4th line spills out below the panel.
- **Display:** **instant**. `Show()` sets `text` and activates the panel (97-102). No typewriter,
  no fade, no sound cue.
- **Dismissal:**
  - **Only** pressing ACT or E again near Mali, via `MaliCompanionInteraction.cs:104-114`.
  - The panel has no tap-to-dismiss, no close button, no auto-hide and no hint text.
  - A line triggered at Home, Bank or Work (`HomeInteraction.cs:363-368`, `BankInteraction.cs`,
    `WorkInteraction.cs`) stays on screen until the player presses ACT.
  - The ACT press goes to whichever listener's `Update` consumes `MobileInputBridge` first (see
    section 4). Near Home, one ACT tap may reopen the Home panel instead of closing Mali's line.
- **Defects:**
  1. **No portrait.** The name is 8.6 sp on phone A.
  2. Body text is 10.5 sp on A and 9.2 sp on B. That is below the "large text" bar the brief
     sets.
  3. Long lines overflow. The stokvel intro with a name is about 1,280 units at 22 regular in a
     704-unit column (two lines, fits). The morning bill summary grows one sentence per bill, so
     three or more bills pass the 68-unit box and spill below the panel frame.
  4. No reveal pacing, so the "here's what moved it" moment is never staged.
  5. Some Mali lines are judging, against rule 3:
     - `ScenarioLibrary.cs:193` "That's the move, {0}."
     - `MaliDialogueLibrary.cs:81` "Nice, {0}. You actually chose your future over the impulse."
     - `ScenarioLibrary.cs:266` "Nice."

     Not a layout issue, but it shows in this panel.

### 3.4 ScenarioChoiceUI (`Assets/Scripts/Scenarios/ScenarioChoiceUI.cs`)

- **Built:** 29-88. Created by `ScenarioManager.cs:39-44`.
- **Layout:** centred panel **760x420** (50-54); `panel_brown` 9-slice (56-66).
  - Title 26 bold DeepForest, 712x40 at (24, -20) (68).
  - Description 20 regular DarkBrown, 712x60 at (24, -66), vertical overflow on (69).
  - Choice list: bottom-anchored, 300 tall at y = 24 (71-78), so it spans panel-top -96 to -396.
    `VerticalLayoutGroup` with spacing 12 and default `UpperLeft` alignment (80-85).
- **Choice buttons** (134-163):
  - `LayoutElement.preferredHeight` 76 (140), 712 wide. Phone A: 339x36 dp; phone B: 297x32 dp.
  - Sprite `button_brown`, whose fill is **cream #FFF1D2, the same colour as the panel**, so a
    choice is distinguished only by its thin brown outline.
  - Label: 20 bold DeepForest, 680x28 (158).
  - Consequence: **15 regular** DarkBrown, 680x28 (161). This is 7.1 sp on A and 6.3 sp on B,
    and it is the line that carries "Cash -R50, Stress -5".
- **Display:** instant. Mali's intro line appears in the dialogue box at the same moment
  (`ScenarioManager.cs:80-85`).
- **Dismissal:** only by choosing. There is no close or "not now" option. Picking a choice hides
  the panel (165-169), and Mali's reaction replaces the intro line.
- **Defects:**
  1. **The second line of the description is hidden.** The description starts at -66, and at 20
     pt each line is about 23 units, so a second line ends at about -112. The first choice button
     starts at -96 and draws after (on top of) the description. Measured with Arial 20, these
     descriptions run to two lines in the 712-unit box:
     - Stokvel (`ScenarioLibrary.cs:346`, 1,201 units)
     - Family (300, 906 units)
     - Emergency (205), transport (110), pay-later (391)
     - Food (61) is exactly at the limit, 712/712.

     Most scenarios therefore lose half their setup text behind the first button. Computed, not
     seen on a device.
  2. Consequences are tiny (15 units), written as raw stat-deltas, and set in the same brown as
     the label. They don't line up into columns, so choices are hard to compare. Cash, Savings,
     Stress and Energy come in a different order in each line.
  3. No Mali portrait on the choice screen. The panel doesn't show the player's current cash, so
     "Cash -R120" can't be read against what they have.
  4. **The consequence text is wrong in one case.** Impulse "Skip it, put R120 into savings
     instead" shows "Savings +R120" and applies `savingsDelta = 120` with **no cash debit**
     (`ScenarioLibrary.cs:185-194`), so R120 appears from nothing. The UI hides that this choice
     prints money. The windfall "Bank all of it" (258-266) is fine, since it is new money.
  5. Choice buttons are 36 dp tall on A, under 48.
  6. Movement isn't locked while the panel is open: `MaliGoPlayerController` has no gate, and the
     joystick at order 25 sits outside the panel and stays live. The player can walk away with
     the panel still open.

### 3.5 ActionPanelUI: Home and Bank (`Assets/Scripts/UI/ActionPanelUI.cs`)

- **Built:** 229-289. One instance per location (`HomeInteraction.cs:249`,
  `BankInteraction.cs:391`).
- **Layout:** centred **720x460** (250-254); `panel_brown` (256-266).
  - Title 26 bold DeepForest (268).
  - Body 19 regular DarkBrown, 672x160, vertical overflow on (269).
  - Actions: `GridLayoutGroup` cells **330x60**, spacing 12x10, area 210 tall, bottom 20
    (271-283). Grid top is at panel-top -230.
  - Close "X": **44x36** at the top-right, label 18 bold DeepForest on `button_brown` (291-328).
- **Content:**
  - Home body (`HomeInteraction.cs:265-318`): Day, Cash, Savings, Financial Stress, Energy, Goal,
    then a blank line, "Bills:" and one line per bill. Single button "Sleep - end the day" (261).
  - Bank body (`BankInteraction.cs:410-425`): Cash, Savings, Goal, plus an explainer line.
    Buttons: Deposit R50, Deposit R100, Withdraw R50, Contribute R50 to goal (401-407).
- **Display:** instant. The body refreshes after each button press (374-380, 401-405).
  **Dismissal:** the X button only. The panel doesn't close when the player walks away.
- **Tap targets:**

  | Control | Phone A | Phone B |
  |---|---|---|
  | Action buttons | 157x29 dp | 138x25 dp |
  | **Close X** | **21x17 dp** | 18x15 dp |

- **Defects:**
  1. **The Home body runs into the Sleep button.** The body has 6 lines, a blank line, "Bills:"
     and N bills. At 19 pt (about 22 units per line), 10 lines take about 220 units from -64, so
     they end around -284. That crosses the grid top at -230, where the Sleep button sits
     (`UpperCenter` alignment). Text renders under the button.
  2. A tiny close target.
  3. Sleep, the most consequential button in the game, ends the day with **no confirmation and no
     end-of-day screen.** The result is a single Mali line.
  4. Bank amounts are fixed (R50/R100). That is acceptable for the beta, but each button gives no
     visible feedback except a Mali line, which the panel's 30 sort order mostly covers (no
     overlap at 20:9; see section 4).
  5. The Bank explainer "Deposits and withdrawals move exactly what you have available." reads as
     developer text.

### 3.6 Proximity prompts: ProximityInteraction, ScenarioTrigger, MaliCompanionInteraction

The three classes are near-identical copies.

| | ProximityInteraction (Home/Bank/Work) | ScenarioTrigger (x8) | MaliCompanionInteraction |
|---|---|---|---|
| Built | `World/ProximityInteraction.cs:111-156` | `Scenarios/ScenarioTrigger.cs:302-347` | `Characters/MaliCompanionInteraction.cs:146-191` |
| Position | bottom-centre, y = 210, 420x36 | same | same |
| Look | DeepForest at 88%, cream 18 regular centred, text inset 12/4 | same | same |
| Radius | 0.7 | 0.61 | 0.74 |
| Shown when | in range && `CanInteract()` | in range && no scenario in progress && not completed | in range && Mali not talking |

- **Prompt strings:**
  - Locations: "Press E to go inside" (`HomeInteraction.cs:248`), "Press E to enter the bank"
    (`BankInteraction.cs:390`), "Press E to work" / "Done for today - sleep at home"
    (`WorkInteraction.cs:530-532`).
  - Scenarios: `ScenarioWorldWiring.cs:27-63`.
  - Mali: "Press E to talk to Mali" (`MaliCompanionInteraction.cs:188`).
- **Size:** 36 units tall (17 dp on A). Contrast is 5.8-7.2:1 depending on the world behind.
- **Display:** appears and disappears instantly with range. **The prompt isn't a button:** the
  player has to find ACT in the bottom-right corner.
- **Defects:**
  1. **"Press E" on touch.** The mobile button is labelled "ACT" (`MobileControlsUI.cs:353`).
     Every prompt is wrong for the target device.
  2. **Text is clipped.** Default `Text` vertical overflow is Truncate and the label box is 28
     units tall, so a second line is dropped. "Press E - something in the window caught your eye"
     measures 409 units against 396 available (Arial 18), so it wraps and loses its ending.
     "…the easy-payment offer" (354) and "…needs attention at home" (360) fit, but only just.
  3. **Prompts stack on top of each other.** Every prompt is drawn at the same rectangle, at the
     same sort order.
     - Mali follows the player (`PlayerCharacterSpawner.cs:287`) at an offset of about 0.39
       (`MaliNpcController.cs:20`), inside her 0.74 talk radius, so "Press E to talk to Mali" is
       up nearly whenever the player stands still.
     - Standing at any location or scenario trigger therefore draws two prompts on top of each
       other.
     - Triggers also overlap each other: Emergency, Windfall and Family sit within 0.6 of each
       other around the house, Home is 0.4 from Family, and Stokvel, BNPL and Impulse sit within
       0.6 of each other at the hub. All have radius 0.61-0.7, so three prompts can stack.
  4. One ACT tap goes to whichever component polls `MobileInputBridge.ConsumeInteractRequest()`
     first (`MobileInputBridge.cs:20-30`). With stacked prompts the player can't tell which thing
     they will activate.

### 3.7 MobileControlsUI (`Assets/Scripts/UI/MobileControlsUI.cs`)

- **Built:** 290-355. Added by `MaliGoIdentityRuntimeBootstrap.cs:63-69`.
- **Scaler:** **height-matched** (302), unlike everything else.
- **Joystick:** base circle 170 at bottom-left (140, 130), handle 78 (217-218, 307-320).
  - Phone A: base 65 dp, handle 30 dp, centre 53 dp from the left edge.
  - Phone B: base 57 dp.
  - Colours: base DeepForest at 45%, handle cream at 75% (220-221).
  - Fixed-position joystick: touches outside the base do nothing.
- **ACT button:** circle 130 at bottom-right (-140, 130), gold #DFA464 at 85%, label "ACT" 22
  bold #0F331F (5.9:1) (325-355). 50 dp on A, 43 dp on B.
- **Sprite:** a procedural 128px circle (377-403). No Kenney art.
- **Defects:**
  1. Always visible, including on desktop. Not hidden during dialogue or panels.
  2. The joystick stays live while the choice or action panel is open.
  3. "ACT" is a generic label that doesn't change with context ("Talk", "Enter", "Work").
  4. ACT is under 48 dp on budget phones.
  5. With `androidRenderOutsideSafeArea: 1` the joystick base's left edge sits about 21 dp from
     the screen edge. That is inside a typical cutout inset and the Android back-gesture zone.
     The joystick may conflict with back-swipe. Not verified on a device.
  6. **Overlap check** (phone A px):

     | Element | x (px) | y (px) |
     |---|---|---|
     | Joystick | 55-225 | 45-215 |
     | ACT | 2195-2325 | 65-195 |
     | Dialogue | 725-1675 | 60-235 |
     | Prompts | 937-1462 | 262-307 |

     Nothing overlaps at 20:9 or 16:9. The UI does not cover the joystick today. Once dialogue and
     choice panels are enlarged to readable sizes, they will collide with the joystick and ACT, so
     the new kit has to plan for it.

### 3.8 KenneyUiSprites (`Assets/Scripts/UI/KenneyUiSprites.cs`)

- Five getters (20-24): `PanelWarm` = `panel_brown` (14px 9-slice), `PanelStatus` =
  `panel_grey_green` (12), `ButtonWarm` = `button_brown` (8,6,8,6), `BannerModern` =
  `banner_modern`, `ProgressBarFrame` = `panel_border_brown`.
- Loads from `Resources/MaliGoUI/` and falls back to AssetDatabase in the Editor (26-51). The
  Resources copies are produced by `Editor/MaliGoResourceBaker.cs:23-31`.
- **Each property access calls `Sprite.Create`** (43-50), so every button builds a new Sprite.
  It's a small leak; cache the sprites when this is rebuilt.
- Uses the 1x PNGs (64x64 panel). At about 1.25 px per unit the 14px frame is upscaled. The
  `PNG/Double` versions (128 px) would be crisper on phones.

### 3.9 Anything else player-facing

- **Dropdown popups** (CharacterCreation): Unity spawns a "Dropdown List" canvas and a full-screen
  blocker. Items are 28 tall with no scrollbar, and the template is 150 tall (615). That fits 5
  options, the largest list.
- **No other canvases:** no pause menu, no end-of-day screen, no chapter-end screen, no settings,
  no loading screen.
- `BuildingInteriorController.cs` and `PlayerCharacterSpawner.cs` create no UI.
- `UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` exists but no UIDocument uses it.

---

## 4. Cross-surface overlaps and conflicts

1. **Shared ACT/E input.** All 12 interactables plus Mali poll one flag
   (`MobileInputBridge.cs:20-30`); the first `Update` to run wins.
   - On keyboard, `eKey.wasPressedThisFrame` is true for every listener at once. One E press near
     Home while Mali is talking both hides Mali's line and opens the Home panel. Editor only.
   - On touch, which listener gets the press is unpredictable.
2. **Prompts stack** (see 3.6, point 3): same rectangle, same sort order 15.
3. **Choice panel vs action panel:** both sort order 30. The Home prompt stays active while a
   scenario panel is open (Home doesn't check `IsScenarioInProgress`), so a player can open Home
   on top of a choice. The draw order between them is undefined.
4. **Dialogue vs panels:** Mali's dialogue (20) is under the choice and action panels (30) and the
   mobile controls (25).
   - At 20:9 and 16:9 the rectangles don't overlap: on phone A the choice panel covers y 277-802
     px and the dialogue y 60-235 px.
   - The Mali intro and the choice panel are on screen at the same moment, which splits the
     player's attention.
5. **Prompts vs dialogue:** a location prompt (y 210-246) sits 22 units above the dialogue panel's
   top (188). When both show, they read as one cluttered block at the bottom centre.
6. **Safe area:** none handled. The cutout can sit over the HUD status panel (top-left),
   `Goal_Panel` (top-right) and the joystick and ACT (bottom corners), depending on the device.

## 5. Kenney and other UI assets in the project

**Pack:** `Assets/kenney_ui-pack-adventure/`, UI Pack - Adventure 1.1, **CC0**
(`License.txt`).

- `PNG/Default/`: 128 PNGs at 1x.
- `PNG/Double/`: the same 128 at 2x.
- `Vector/`: 128 SVGs.
- `Spritesheet/`: `spritesheet-default.png/.xml`, `spritesheet-double.png/.xml`.
- `Preview.png`, `Sample.png`.
- No font is included.

**Used at runtime:**

| Asset | Size | Fill | Where it shows |
|---|---|---|---|
| `panel_brown` | 64x64 | #FFF1D2 | Mali dialogue, scenario choice and Home/Bank panels |
| `button_brown` | 48x24 | #FFF1D2, same as the panel | choice buttons, action buttons, close X |
| `panel_grey_green` | 64x64 | #94AFC6 | HUD status and goal panels. Embedded as Sprite sub-objects in `MaliGoWorld.unity` (lines 348, 2653, texture guid `6da594b6e3ad853428d4cdfd067b9059`) and also baked to Resources |

**Baked but unused:** `banner_modern`, `panel_border_brown` (copied to
`Assets/Resources/MaliGoUI/`; getters exist but are never called).

**Unused,** by family (Default and Double each):

- banner (`banner_classic_curtain`, `banner_hanging`)
- buttons (`button_brown_close`, `button_grey`, `button_grey_close`, `button_red`,
  `button_red_close`)
- checkboxes (9: beige, brown and grey × checked/cross/empty)
- hexagons (9)
- minimap arrows, compasses, icons and rings (27)
- panels: `panel_border_brown_detail`, `panel_border_grey(_detail)`, `panel_brown_arrows*` (4),
  `panel_brown_corners_a/b`, `panel_brown_damaged(_dark)`, `panel_brown_dark*` (3), `panel_grey`,
  `panel_grey_blue`, `panel_grey_bolts*` (7), `panel_grey_dark`, `panel_grey_red`,
  `panel_grid_blueprint`, `panel_grid_paper`
- patterns (8)
- progress bars (20: blue, green, red, white and transparent, each in normal, small and bordered
  variants)
- round frames (8)
- scrollbars (12)

**Candidates for the new kit:**

- `progress_green*` for an energy bar.
- `round_brown` / `round_grey` as a portrait frame.
- `button_brown_close` or `button_grey_close` as a proper close icon.
- `checkbox_*` for settings.
- The `Double/` versions throughout, for sharpness.

All of these would need recolouring or tinting to the brand palette. The pack's parchment and
grey-metal look is the stock-asset feel the brief warns against.

**Other images:**

| File | Size | Type | Used? |
|---|---|---|---|
| `Assets/Mali Dumbfound.png` | 520x657 | Sprite, multiple | **Unused.** Mali art; could be a portrait source |
| `Assets/MaliGo UI.png` | 347x892 | Sprite, multiple | Unused. A screenshot of the React Native app's home screen: brand reference only |
| `Assets/MaliGo Pitch Deck.png` | 779x779 | — | Unused |
| `Assets/Sprites/Sam_Idle.png` | 390x512 | — | Referenced by both world scenes as a 2D character visual, not UI |

**Brand palette for comparison** (React Native `src/theme/colors.ts`):

- primary #087A18, primaryDark #075F15, primarySoft #C9EFB4
- brown #9B6639, gold #DFA464
- background #F9FFF6, surfaceWarm #FFF4E9
- text #101010, textSoft #344330

The Unity code already reuses several of these: `MossGreen` = primary, `Sage` = primarySoft,
`Cream` ≈ background, `GoldenAmber` = gold. The Kenney blue-grey (#94AFC6) and the parchment
frames are not part of the brand. The React Native app has `assets/images/mali.png`,
`mali1.png` and `mali2.png` (not opened here).

## 6. Prioritised defect list (for DESIGN_SPEC)

1. **No end-of-day screen.** "Started with R__ / ended with R__ / what moved it" has no UI and no
   data.
2. **All text 6-12 sp and nearly all targets 8-36 dp on real phones.** Raise sizes: about 34-38
   units for 16 sp and about 100-115 units for 48 dp with the current scaler. Or change the
   scaler to match height (0.5-1) and size in dp terms.
3. **No Mali portrait.** The art exists (`Assets/Mali Dumbfound.png`, plus the React Native
   `mali*.png`).
4. **"Press E" prompts on touch.** Prompts aren't tappable, and they stack on each other because
   Mali's prompt is always on. Use one context prompt that is itself the button, or a
   context-labelled ACT.
5. **Scenario description hidden behind the first choice.** Consequences are tiny and can't be
   compared. One consequence is wrong (impulse "into savings" creates R120).
6. **Low-contrast text:** HUD goal value (2.2:1), CC Next (2.2:1), CC Back (1.25:1), CC error
   (3.6:1).
7. **HUD truncates the goal amount.** It lacks energy, day and bills. Emoji are unlikely to render.
   Level and XP and the stress % conflict with the no-scores rule.
8. **Dialogue dismissal is hidden** (ACT only, racing with other listeners). No typewriter or
   pacing. Long lines overflow.
9. **Home body overflows into Sleep.** Sleep has no confirmation. The close X is 21x17 dp.
10. **No safe-area handling** while `androidRenderOutsideSafeArea: 1`.
11. **Character creation:** raw `snake_case` options, placeholder copy, row overflow onto
    Back/Next, no Mali, no preview.
12. **Movement and joystick stay live under modal panels.** Panels don't close when the player
    walks away.

## 7. What I could not verify

- **Nothing was run in the Editor or on a phone.** Every position, overlap, truncation and
  wrapping finding is computed from code and scene YAML. Text widths were measured with Windows
  `arial.ttf`/`arialbd.ttf` through PIL, on the assumption that Unity's `LegacyRuntime.ttf`
  matches Arial metrics. Unity line height was assumed to be about 1.15× the font size.
- dp conversions use standard density buckets for two representative devices, not measurements
  from real hardware.
- Emoji rendering in legacy `Text` on Android, cutout and back-gesture conflicts, and the ACT
  input race all need a device test.
- I did not open the `.unity` scenes in Unity. The HUD hierarchy comes from parsing
  `MaliGoWorld.unity` YAML, and `MaliGoIsometricWorld.unity` (disabled in build) was not
  inspected for canvases.
- The React Native `mali*.png` files were listed but not opened.
