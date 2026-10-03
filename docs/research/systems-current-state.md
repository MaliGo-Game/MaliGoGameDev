# MaliGo systems: current state, data flow and beta gaps

Research report, phase 1 (read-only). Written 3 Oct 2026 against branch `agents/beta-finish` @ `184fe73f`.
All line numbers are from that commit. Nothing under `Assets/`, `ProjectSettings/` or `Packages/` was changed.

Scope: 54 runtime `.cs` files (about 6,700 lines) under `Assets/Scripts` and `Assets/MaliGo`, plus the
scenes and project settings they depend on. Editor scripts (`Assets/Editor/*`) were only skimmed.

> Note: the brief mentions `TEAM_TASKS.md`. It does not exist in the repo at this commit (`git ls-files | grep -i team_tasks` returns nothing).

---

## 1. Summary

- **The core loop runs end to end**: character creation, then the world, one-time scenarios, Work (one shift a day),
  Bank, Sleep at Home (moves to the next day and pays bills), and the next day. Every money change saves immediately.
- **The end-of-day reveal does not exist, and nothing records the data it needs.** The game keeps no
  start-of-day snapshot and no log of what happened during the day. The only per-day record is the bill-settlement
  object `ObligationSettlement`. It lives only for the duration of the Sleep tap and is never saved. Section 5 gives the exact hook points.
- **There is no "chapter" of days.** The 8 scenarios can all be played on day 1, there is no daily pacing, and
  there is no chapter end or closing reflection from Mali. After the 8 scenarios the world has no decisions left.
- **The biggest runtime risk on a phone is input.** One shared ACT flag is used by Mali (who follows
  the player and so is almost always within reach), the 8 scenario triggers and the 3 locations. Which of them
  responds depends on Unity's Update order. Mali's lines also never close on their own, and any ACT press closes them first.
- Other gaps against the definition of done: no safe-area handling (and `androidRenderOutsideSafeArea: 1`), no pause
  menu, no reset-save UI, no Android back handling, no audio at all, HUD emoji that the built-in font can't render,
  tap targets below 48 dp, and two content bugs that break "this choice moved this number"
  (scenario costs are cut off at R0, and a choice that adds R120 to savings without taking it from cash).

---

## 2. System map and data flow

### 2.1 Boot and scene flow

```
App start (first enabled scene in EditorBuildSettings: CharacterCreation.unity)
 └─ [RuntimeInitializeOnLoad AfterSceneLoad] MaliGoIdentityRuntimeBootstrap.BootstrapAfterSceneLoad  (Bootstrap.cs:15-28)
     ├─ subscribes SceneManager.sceneLoaded (−= then +=, so it is subscribed once)            (:24-25)
     └─ WireScene(active scene)                                                               (:35-48)
         ├─ GameFlowController.EnsurePlayerDataManager()  → new "PlayerDataManager" GO        (GameFlowController.cs:35-50)
         │     └─ PlayerDataManager.Awake: singleton, DontDestroyOnLoad, TryLoad()           (PlayerDataManager.cs:29-40)
         ├─ EventSystemUtility.EnsureEventSystem() → InputSystemUIInputModule, DontDestroyOnLoad (EventSystemUtility.cs:16-36)
         ├─ "CharacterCreation": if save says isCharacterCreated → GameFlowController.LoadWorldScene()  (Bootstrap.cs:119-127)
         │                        else add CharacterCreationUI                                 (:129-133)
         │     CharacterCreationUI.CompleteCharacterCreation → SetPlayerData(save) → LoadWorldScene  (CharacterCreationUI.cs:392-401)
         └─ "MaliGoWorld": WireWorldScene(), with each part wrapped in a try/catch Step()       (Bootstrap.cs:50-95)
               1 PlayerCharacterSpawner on "MaliGo_Systems"  (found by name, or created)
               2 MobileControls GO + MobileControlsUI
               3 GameFlowController (its Start sends the player back to CharacterCreation if no character exists)
               4 HUDController added to scene object "MaliGo_Canvas"
               5 ScenarioManager on MaliGo_Systems + 8 ScenarioTriggers (ScenarioWorldWiring.cs:33-64)
               6 Location_Home / Location_Bank / Location_Work (WorldLocationWiring.cs:16-75)
```

Build scenes (`ProjectSettings/EditorBuildSettings.asset`): CharacterCreation (enabled, index 0), SampleScene
(disabled), MaliGoWorld (enabled), MaliGoIsometricWorld (disabled).

Scene objects found by name, checked against `MaliGoWorld.unity`: `Mali` (line 10616, untagged, already has
MaliCompanionInteraction, MaliDialogueController and MaliNpcController), `MaliGo_Canvas`, `PlayerSpawnPoint`,
`Player_House`, `Local_Commercial_Hub`, `Local_Bank_Building`, `Road_Main_4` and `Road_Player_Driveway` are all
present. `MaliGo_Systems` and `EventSystem` are not in the scene; they are created at runtime as intended. No
scene object is tagged `Player`. The player is built at runtime by `PlayerCharacterSpawner.BuildPlayerFromCatalog`
(PlayerCharacterSpawner.cs:177-234) from `Assets/Resources/PlayerCharacterCatalog.asset`, which has its model,
animator and material references filled in.

### 2.2 Player data and save

- `PlayerData` (Data/PlayerData.cs) holds the name, appearance, financialProfile, `currentLifeChapter`,
  `financialStats`, `goals[]`, `progression`, `isCharacterCreated`, `completedScenarioIds[]`, `hasMetMali`,
  `currentDay` (from 1), `lastWorkedDay`, `obligations[]` and `baseObligationsAdded`.
- `FinancialStats` (Data/FinancialStats.cs) holds cash, savings, emergencyFund, financialStress, income, expenses,
  savingsRate, financialXP, energy=100, and the hidden `spendingBehaviourScore` and `savingBehaviourScore`.
  The defaults depend on life chapter (`CreateDefaults`, :26-80). Young professional starts with R5,000 cash and R1,200 savings.
- `Obligation` (Data/Obligation.cs) holds a label, amount, intervalDays, nextDueDay, paymentsRemaining (−1 means it never ends) and arrears.
  `ObligationDefaults` (:30-60) adds Airtime & data (R60 a week, first due on day+1) and Rent (R500 a week, first due on day+2).
- Save: `PlayerDataManager` writes JSON with `JsonUtility` to `Application.persistentDataPath/player_data.json`
  (:24-27, :123-139). `TryLoad` fills in null sub-objects and back-fills bills for older saves (:77-121).
- **When saves happen**: every mutation passes `saveImmediately: true`. The list is CharacterCreationUI.cs:400,
  MaliDialogueController.cs:84, ScenarioManager.cs:111-127, BankInteraction.cs:65/91/117, HomeInteraction.cs:104-109
  and WorkInteraction.cs:63-70. There is no `OnApplicationPause` or `OnApplicationQuit` handler anywhere (grep finds none).
  Player position and any open panel are not saved.
- `OnPlayerDataChanged` has two subscribers: HUDController (HUDController.cs:26-41) and PlayerIdentityBridge
  (PlayerIdentityBridge.cs:34-49). Both unsubscribe in OnDisable.

### 2.3 Scenarios

- `ScenarioLibrary` (MaliGo/Scenarios/ScenarioLibrary.cs) builds 8 `ScenarioDefinition` ScriptableObjects in code:
  food, transport, impulse, emergency, windfall, family, stokvel and credit/pay-later. Each has 3 choices, and every
  choice has `financialXpDelta = 5f`, which is compliant with the equal-XP rule.
- `ScenarioTrigger` (Scripts/Scenarios/ScenarioTrigger.cs) is a proximity prompt. On ACT it calls
  `ScenarioManager.TryBeginScenario` (:99-115). A trigger goes quiet for good once its id is in `completedScenarioIds` (:46-53).
- `ScenarioManager.TryBeginScenario` (:56-87) checks life chapter and whether the scenario is already done, shows
  Mali's intro and opens `ScenarioChoiceUI`. `ResolveChoice` → `ApplyConsequences` (:104-128) applies cash, savings,
  stress, energy and XP deltas, nudges the hidden behaviour scores, writes `financialProfile.spendingBehaviour`
  and `savingBehaviour`, marks the scenario complete, and adds pay-later instalments as an `Obligation` (:184-201). Then Mali's reaction line is shown.
- `ScenarioChoiceUI` is a centred 760×420 panel with a button per choice (label plus consequence line). It has no cancel or close button.
- **Pacing**: none. All 8 triggers exist from day 1. Nothing is tied to `currentDay`.

### 2.4 Bills and the day boundary

- `HomeInteraction.SleepAndEndDay` (HomeInteraction.cs:95-115) does `currentDay += 1`, then
  `ObligationLedger.SettleDue(data, currentDay)`, then `energy = 100`, saves, and has Mali say `DescribeNight(settlement)`.
- `ObligationLedger.SettleDue` (Economy/ObligationLedger.cs:35-97) charges every payment that has fallen due, pays
  from cash only (never from savings), carries any shortfall as arrears, adds `MissedPaymentStress` = 10 stress on any
  night something is still owed, and drops finished obligations. It returns an `ObligationSettlement`
  (payments[], totalPaid, totalStillOwed, stressAdded). **That object is never saved.**
- `WorkInteraction` (World/WorkInteraction.cs:43-73) pays +R150 cash, costs 20 energy and gives +5 XP, once per
  `currentDay` (`lastWorkedDay`). It needs at least 20 energy.
- Day boundary: the day changes only by sleeping. Sleep is allowed at any time, with no condition.

### 2.5 Bank

`BankInteraction` (World/BankInteraction.cs) has Deposit R50 and R100 (cash→savings), Withdraw R50
(savings→cash) and Contribute R50 to goal (cash→`goals[0].currentAmount`). Each moves only what is available
(`Mathf.Min`). Note that the goal pot is a third money pool, separate from `savings` (see risk R24).

### 2.6 Mali dialogue

- `MaliDialogueController` (Characters/MaliDialogueController.cs) has `ShowLine` and `ShowFormatted`
  (`string.Format(template, name)`) and `ShowGreeting`, which uses `MaliContextualDialogueSelector.SelectLine`.
- Selector priority (Dialogue/MaliContextualDialogueSelector.cs:12-55): first meeting → stress ≥ 60 → cash below
  15% of `income` (the monthly R12,000 for young professionals, so below R1,800) → spending score > 0.6 → savings ≥ 50% of the goal target → saving score > 0.6 → a default line for the life chapter.
- Line library: 8 entries in code (MaliGo/Dialogue/MaliDialogueLibrary.cs:45-96).
- `MaliDialogueView` is a bottom-centre 760×140 panel. It has **no auto-hide and no tap-to-close**. It closes only
  through `MaliCompanionInteraction` when ACT is pressed (MaliCompanionInteraction.cs:104-113).
- Everything else reaches Mali through `GameObject.Find("Mali")`: ScenarioManager.cs:221, BankInteraction.cs:137,
  HomeInteraction.cs:140 and WorkInteraction.cs:82.

### 2.7 HUD

`HUDController` (PlayerIdentity/HUDController.cs) attaches to the scene's `MaliGo_Canvas` and finds its Text
fields by matching their current text ("Cash:", "Savings:", ...) (:51-98). It shows name and Level, cash, savings,
stress, a fixed "Save R{dailyTarget} today" goal line and an XP bar. It does **not** show day, energy, bills,
arrears or goal progress. Energy and bills are visible only inside the Home panel text (HomeInteraction.cs:52-58).

### 2.8 Player controller, camera and mobile input

- `MaliGoPlayerController` (Scripts/MaliGoPlayerController.cs) uses a CharacterController with movement relative to
  the camera. Input comes from the Input System keyboard (WASD/arrows) plus `virtualJoystickInput`.
- `MaliGoCameraController` uses a fixed isometric rotation and SmoothDamp follow. Zoom is mouse-wheel only, with no pinch.
  The spawner points the camera at the player (PlayerCharacterSpawner.cs:265-277).
- `MobileControlsUI` draws a joystick (bottom-left, 170 ref px) and an "ACT" button (bottom-right, 130 ref px). The
  button calls the static `MobileInputBridge.RequestInteract`. The controls are always shown, on every platform.
- `ProjectSettings.asset:879 activeInputHandler: 1` means Input System only. The `#if ENABLE_LEGACY_INPUT_MANAGER`
  blocks that call `UnityEngine.Input` are therefore compiled out and cannot throw (MaliCompanionInteraction.cs:135,
  ScenarioTrigger.cs:132, ProximityInteraction.cs:105, MaliGoPlayerController.cs:138-139, MaliGoCameraController.cs:80).
  They are dead code and should be removed, so that a later switch to the "Both" setting can't bring them back to life.

---

## 3. Runtime risks a phone build would hit

Severity: **High** blocks or derails play. **Med** is visible breakage or data loss in normal play. **Low** is
polish, performance or hygiene. "Plausible" means it follows from the code but needs an Editor or phone run to confirm.

| # | Sev | Where | Risk |
|---|---|---|---|
| R1 | **High** (plausible) | MaliCompanionInteraction.cs:104-116; ScenarioTrigger.cs:64; ProximityInteraction.cs:61; MobileInputBridge.cs:18-27 | **ACT contention.** Mali follows the player at offset (0.32, 0, −0.22), about 0.39 units away (MaliNpcController.cs:17-20, 150-196), and her interaction radius is 0.74. So she is almost always in range. On a phone, one ACT tap sets a single static flag, and whichever `Update` runs first takes it. Unity doesn't guarantee that order, so a tap at a scenario, Home, Bank or Work can open Mali's greeting instead. With a keyboard, `eKey.wasPressedThisFrame` isn't consumed, so Mali's greeting and the location both fire in the same frame. Needs one interaction arbiter with an explicit priority: nearest location or scenario first, Mali last. |
| R2 | **Med** | MaliCompanionInteraction.cs:104-113 + MaliDialogueView (no auto-hide) | Every action ends with a Mali line, and the line stays up until ACT. While it is showing, `canAct` is true anywhere, so **the next ACT closes the line instead of using the location**. Players need two taps after every action, which reads as "the button didn't work". |
| R3 | **Med** | MobileInputBridge.cs:10-27; consumers only check it when eligible (ScenarioTrigger.cs:64, ProximityInteraction.cs:61, MaliCompanionInteraction.cs:104-105) | A tap made when nothing is eligible leaves the static flag set. It fires later, when the player walks into range, and it survives scene loads. The flag should be cleared each frame (`LateUpdate`) or carry a timestamp. |
| R4 | **Med** | HomeInteraction.cs:36, 95-109; ActionPanelUI.cs:217-221 | The Home panel stays open after "Sleep - end the day". Each extra tap skips another day, charges bills and adds stress. There is no confirmation and no limit, so a double-tap loses a day. |
| R5 | **Med** | PlayerDataManager.cs:132-133 (`File.WriteAllText`), 115-120 (`catch` → `CreateNew`) | The save write isn't atomic. If Android kills the app mid-write, the JSON is truncated. The next launch quietly falls back to a new player and sends them to character creation, and the next save overwrites the broken file. Write to a temp file, then replace, and keep a `.bak`. |
| R6 | **Low** | PlayerDataManager.cs (no `OnApplicationPause`/`OnApplicationQuit`) | No save on pause or quit. Exposure is low today because every mutation saves immediately (§2.2). Add it anyway as a safety net, and it will be needed once the day log (§5) exists. |
| R7 | **Med** | ProjectSettings.asset:71 `androidRenderOutsideSafeArea: 1`; no `Screen.safeArea` anywhere in code | In landscape, a camera cutout or rounded corners can cover the HUD corners (scene `MaliGo_Canvas`), the joystick (centre 140 ref px from the left edge, MobileControlsUI.cs:108-109) and ACT (MobileControlsUI.cs:130-131). Needs a safe-area root RectTransform on every runtime canvas. |
| R8 | **Med** | none: no reads of `Keyboard.current.escapeKey` | Android back does nothing. It doesn't close panels, doesn't pause and doesn't quit. |
| R9 | **Med** | PlayerDataManager.cs:141-151 (`DeleteSave` has no caller) | There is no pause menu, no `Time.timeScale` use and no reset-save path. All three are definition-of-done items. Reset also has to reload to CharacterCreation, because the world scene has no way to rebuild itself after the data is wiped. |
| R10 | **Med** (plausible) | MobileControlsUI.cs:45-76 | If the app is paused or the touch is cancelled mid-drag, `OnPointerUp` may never arrive. `virtualJoystickInput` then stays set and the player keeps walking after resume. Reset on `OnApplicationPause(true)`/`OnApplicationFocus(false)` and on `OnDisable`. |
| R11 | **Med** | HUDController.cs:139, 144, 149; scene texts at MaliGoWorld.unity:3081, 4113, 9219 | The HUD strings start with emoji (💰 🏦 🌱). `LegacyRuntime.ttf` has no emoji glyphs, so on Android these probably show as boxes or blank space. Not seen on a device. |
| R12 | **Med** | ScenarioManager.cs:114-115 | `cash = Max(0, cash + delta)`. A choice that costs more than the player has charges only part of the cost, and the rest disappears (for example "Pay R600 from cash" with R200 left costs R200). Unaffordable choices aren't disabled. This breaks "this choice moved this number" and would make the reveal's numbers not add up. |
| R13 | **Med** | ScenarioLibrary.cs:187-190 (`redirect_to_savings`) | "Skip it, put R120 into savings instead" adds savings +R120 and takes nothing from cash, so money comes from nowhere. It should be `cashDelta = -120`. |
| R14 | **Med** | ScenarioChoiceUI.cs:140, 158-161; ActionPanelUI.cs:97, 117; MaliDialogueView.cs:68-71; ProximityInteraction.cs:150 | Tap targets and text are below the brief's minimum. On a typical 2400×1080 landscape phone at about 400 dpi (CanvasScaler 1920×1080, match width, so a scale of 1.25): choice button 76 → about 38 dp, Bank/Home action buttons 60 → about 30 dp, panel close "X" 44×36 → about 18 dp. Text: consequence line 15 → about 7.5 sp, prompt 18 → about 9 sp, Mali body 22 → about 11 sp. These are estimates and the device dpi isn't known. Character creation dropdowns are 32 ref px tall with a 16 px font (CharacterCreationUI.cs:579, 588). |
| R15 | **Low** (Med on wide phones) | every runtime canvas uses `matchWidthOrHeight = 0` except MobileControlsUI.cs:103 | With width matching on a 20:9 screen, only about 864 of the 1080 reference units of height are visible. Anything laid out near the top or bottom of a 1080-tall design is clipped. Character creation is the most exposed. |
| R16 | **Low** | ProximityInteraction.cs:131, ScenarioTrigger.cs:158, MaliCompanionInteraction.cs:166; WorldLocationWiring.cs:32 vs ScenarioWorldWiring.cs:45-55 | Every prompt is drawn at the same screen spot (bottom centre, y=210). Around the house, Home (anchor+0,0,1.0), Family (+0,0,0.6), Emergency (+0.5,0,0.3) and Windfall (−0.5,0,0.3) are 0.4-0.7 units apart, and the radii are 0.61-0.74, so several prompts stack on top of each other. With a keyboard, E fires all of them. |
| R17 | **Low** | ScenarioChoiceUI (no cancel); MobileControlsUI keeps working under panels | When a scenario panel is open, the player can't back out, and they can still walk. `ScenarioManager.scenarioInProgress` stays true until a choice is made, which is acceptable, but Home and Bank can still open on top of it (both canvases use sortingOrder 30). |
| R18 | **Low** | PlayerIdentityBridge.cs:68-80 → KenneyAppearanceVisualProvider.cs:26 → PlayerCharacterCatalog.cs:52 | `new Material(...)` runs on every `OnPlayerDataChanged`, which means every money action, and the old material is never destroyed. A leak that grows over a long session. |
| R19 | **Low** | KenneyUiSprites.cs:20-24, 43 | Each property access runs `Sprite.Create`, so every panel, button and choice gets a new Sprite that is never cached or destroyed. |
| R20 | **Low** | BankInteraction.cs:137, HomeInteraction.cs:140, WorkInteraction.cs:82, ScenarioManager.cs:221; Bootstrap.cs:81, 99; WorldLocationWiring.cs:25-66; ScenarioWorldWiring.cs:68-73 | Objects found by name. All of them exist today (checked above) and every lookup handles null. But if `Mali` is renamed or missing, all money feedback silently disappears. If `MaliGo_Canvas` is missing, there is no HUD and no error. |
| R21 | **Low** | HUDController.cs:173 | `R{amount:0,0}` renders 0 as "R00" and 5 as "R05". Cash often hits exactly 0 because of the clamps in R12 and the ledger. Use `"#,##0"`, and decide on a culture (en-ZA uses a non-breaking-space group separator). |
| R22 | **Low** | HUDController.cs:128-132, 164-168 | Level and the XP bar never change, because `progression.xp` is never written. Only `financialStats.financialXP` goes up (ScenarioManager.cs:118, WorkInteraction.cs:68). A level display also sits awkwardly with the "no scores" rule. Recommend removing it from the HUD. |
| R23 | **Low** | HomeInteraction.cs:120-133 → MaliDialogueController.cs:52-55 | `DescribeNight` builds a **format template** that includes obligation labels, then runs `string.Format` on it. A `{` or `}` in any label (from content) throws a FormatException inside the button callback, after the save has happened, so Mali's line is lost. Format the name separately. |
| R24 | **Low** | Data/FinancialGoal.cs; BankInteraction.cs:117-123; MaliContextualDialogueSelector.cs:43 | There are three money pools (cash, savings, goal pot). Goal contributions leave cash+savings and live only in `goals[0].currentAmount`. The goal also starts with a pre-filled amount (CharacterCreationUI.cs:403-418: R1,500-12,000). Mali's "savings milestone" check compares `stats.savings` with the goal target and ignores the goal pot. The reveal has to pick a definition (see §5). |
| R25 | **Low** | MaliNpcController.cs:62, 83 | `FindWithTag("Player")` runs every frame. It's cheap, but unnecessary. |
| R26 | **Low** | no `Application.targetFrameRate` | Android defaults to 30 fps. Fine for this game, but it should be a decision rather than a default. |
| R27 | **Low** | Bootstrap.cs:119-127 | A returning player boots into CharacterCreation and is sent to the world in the same callback. Expect a one-frame flash of the CharacterCreation camera or clear colour, with no loading screen. |
| R28 | **Low** (UX) | WorkInteraction.cs:18, HomeInteraction.cs:23, BankInteraction.cs:20, ScenarioWorldWiring.cs:28-63, MaliCompanionInteraction.cs:188 | Prompts say "Press E…" on a touch device. |

Checked and found **not** to be a risk:
- **Double wiring on scene reload.** Each `Ensure*` checks with `FindFirstObjectByType` or by name. `sceneLoaded` is unsubscribed and re-subscribed once. `ScenarioManager.Instance` and `PlayerDataManager.Instance` clear themselves in `OnDestroy`.
- **Awake/Start/sceneLoaded order.** Components added during `sceneLoaded` get Awake and OnEnable immediately and Start before the first Update. The spawner (`[DefaultExecutionOrder(-150)]`) untags nothing in the current scene, because Mali is already untagged.
- **Legacy `Input`.** It is compiled out (see §2.8).
- **The static `ScenarioLibrary` cache of `CreateInstance` objects** should survive `UnloadUnusedAssets`, because Unity scans static fields. Not tested in a build.

---

## 4. Beta definition of done (MASTER_PROMPT §9): status

| Item | Status now | What's missing |
|---|---|---|
| Character creation → world | Works (CharacterCreationUI.cs:392-401, Bootstrap.cs:119-127) | Life-chapter dropdown offers Wealth Builder and Financial Independence (CharacterCreationUI.cs:183-203), which start with R15,000-25,000 cash, so R500 rent means nothing. For an 18-30 beta, lock or hide it. |
| A day of choices | 8 one-time scenarios, Work, Bank | No daily pacing. Everything is available on day 1 and runs out after 8 choices. Needs a scenario schedule per day. |
| Sleep | `HomeInteraction.SleepAndEndDay` | No confirmation; repeated taps skip days (R4). |
| **End-of-day screen (start, end, what moved it)** | **Missing.** Only a Mali line listing bill payments (HomeInteraction.cs:111-136) | Snapshot, event log and a reveal panel. See §5. |
| Next day | `currentDay++`, energy reset, bills settled | New day's scenarios; a "what happens tomorrow" hook. |
| Chapter ending with Mali's reflection | **Missing.** `LifeChapter` is a profile setting, not a run of days | Chapter length (number of days), an end-of-chapter trigger and a reflection line or screen. |
| Dialogue, choices, HUD restyled; phone-readable | Kenney brown panels, built-in Arial and legacy `Text` | Every runtime UI (R11, R14, R15). No Mali portrait in the dialogue (text "Mali" only, MaliDialogueView.cs:68-69). |
| Energy visible | Home panel text only | HUD element. |
| Bills visible | Home panel text only | HUD or upcoming-bill indicator. |
| First-run guidance | Mali `first_meeting`, and only if the player talks to her | Guided first steps (move, ACT, Work, Sleep). |
| Pause menu with reset save | Missing (`DeleteSave` has no caller) | Pause UI, back button (R8), reset → CharacterCreation. |
| Sound (UI click, ambience, Mali cue) | **None.** No `AudioSource` or `AudioClip` in any runtime script | Everything. |
| Compiles with zero errors | **Not verified** by this agent (read-only phase) | Run the csproj check in MASTER_PROMPT §7. |
| No private-strategy mentions anywhere the player can see | Runtime scripts are clean. "Bank" appears only as an in-world location. | Recheck new content. |

Design-rule notes found in passing (content, not runtime). These Mali lines lean toward praise or judgement,
which the brief rules out: MaliDialogueLibrary.cs:75 ("…becoming a habit"), :81 ("You actually chose your future over the
impulse"); ScenarioLibrary.cs:76 ("Nice treat"), :193 ("That's the move"), :266 ("…Nice."), :429 ("Sometimes walking away is the
whole trick"). The hidden behaviour scores do stay hidden, but they are turned into words in `financialProfile`
(ScenarioManager.cs:122-123, "impulsive", "careful"…). Nothing displays them today. Keep it that way.

---

## 5. The end-of-day reveal: required data and where it hooks in

### 5.1 Data the reveal needs

For the line "You started the day with R__. You ended it with R__. Here's what moved it":

1. **Day number** that is ending (`PlayerData.currentDay` before it is incremented).
2. **Start-of-day snapshot**: `cash`, `savings` and `goals[0].currentAmount`, and preferably total arrears
   (`ObligationLedger.TotalArrears`) and energy. These are taken when the day opens.
3. **End-of-day values**: the same fields, taken after tonight's bills are settled (recommended, because Home
   already says bills "come off tonight", HomeInteraction.cs:80-83).
4. **An ordered list of money events during the day**. Each event has: `day`, `kind` (Scenario | Work | BankDeposit |
   BankWithdraw | GoalContribution | Bill | BillShortfall), `sourceId` (scenarioId_choiceId, obligationId), a
   player-facing `label` ("Lunch: R20 meal", "Shift at work", "Rent"), and the **actual** `cashDelta`,
   `savingsDelta` and `goalDelta` after clamping. Optionally `stillOwed` for bills.
5. Invariant for logic checks: `start + Σ deltas == end` for each pool. Today this can't hold, because of R12 (clamping) and R13 (money created from nothing).

**Headline-number decision needed (for the design spec).** If the headline is cash only, a Bank deposit looks like
spending. If it's cash plus savings, a goal contribution looks like a loss, because the goal pot is a separate pool (R24). Recommendation:
headline = cash + savings + goal pot ("your money"), with the breakdown listing transfers between pools as neutral lines.

### 5.2 Is anything recording it now?

**No.** No field in `PlayerData` holds a day-start balance or an event list. The only per-day result is
`ObligationSettlement` (ObligationLedger.cs:16-25). It is created in `SleepAndEndDay` (HomeInteraction.cs:102-107),
used to build one Mali line, and thrown away. Scenario, Work and Bank changes are applied straight to
`FinancialStats` with nothing logged.

### 5.3 Hook points

The log has to be **saved in `PlayerData`**, because Android can kill the app mid-day. Suggested shape:
`[Serializable] DayRecord { int day; float startCash, startSavings, startGoal, startArrears; MoneyEvent[] events; }`
plus `PlayerData.today`. Use arrays rather than `List<>`, to match the existing JsonUtility style.

| Moment | File:line | What to do |
|---|---|---|
| Day 1 opens | CharacterCreationUI.cs:392-401 (before `SetPlayerData`) | Snapshot the start of day 1. |
| Old save loads | PlayerDataManager.cs:96-110 (`TryLoad` migration) | If `today == null` or `today.day != currentDay`, snapshot now. |
| Scenario choice | ScenarioManager.cs:111-127 (`ApplyConsequences`) | Measure cash, savings and goal before and after the mutation and log the **actual** delta with scenario title and choice label. Instalments added (:126) can be noted as "commits R120 × 3". |
| Work shift | WorkInteraction.cs:63-70 | Log +R150 Work. |
| Bank | BankInteraction.cs:65-71, 91-97, 117-123 | Log each transfer with its actual amount. |
| Bills overnight | ObligationLedger.cs:66-81 (per payment) or HomeInteraction.cs:104-109 from `settlement.payments` | Log each bill paid, and any shortfall. |
| Day closes and reveal is shown | HomeInteraction.cs:95-115 | Order: settle bills for the night → take end values → build the reveal from `today` → **show the reveal panel instead of `ShowMaliLine(DescribeNight)` (:111-114)** → then open the new day's `today` snapshot (after bills and the energy reset). Close the Home panel first (fixes R4). |
| App pause | new `PlayerDataManager.OnApplicationPause(true)` | `Save()` as a safety net. |

A small `DayLedger` static (pure C#, next to `ObligationLedger`) with `OpenDay(data)`, `Record(data, event)` and
`CloseDay(data)` keeps all of this testable outside Unity, as MASTER_PROMPT §7 requires.

---

## 6. Could not verify

- Whether the project compiles at this commit. No `dotnet build` was run: this phase is read-only, and the check writes temporary csproj files.
- Update order between Mali and the runtime-created triggers (R1), joystick behaviour after pause (R10), and emoji rendering (R11). These need an Editor or phone run.
- dp and sp figures in R14 assume a 2400×1080, about 400 dpi device. The real test phone isn't known.
- Whether a camera cutout actually overlaps the HUD and controls on the test phone (R7).
- `TEAM_TASKS.md` mentioned in the brief is not in the repo.
