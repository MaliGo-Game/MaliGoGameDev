# MaliGo Beta — Design Spec (Chapter 1: "Seven days to payday")

The single document the build agents implement from. Written 3 Oct 2026 on `agents/beta-finish`
(after `ff80de05`). It elaborates the founder decisions and lead-developer resolutions D1–D24; it does
not reopen them. Where this spec and a research report disagree, **this spec wins**. Where this spec
and the code disagree about what exists today, the code wins and the builder flags it.

Read first: `docs/MASTER_PROMPT.md` (rules), then this file. Research reports in `docs/research/` are
background only. `ui-current-state.md` line numbers drift; every file:line reference in this spec was
checked against the code at `ff80de05`.

**Revision 2 (3–4 Oct 2026):** revised after an engineering critique and a player/design critique. Every
finding and what was done with it is in §11 (Revision log). Text widths quoted in this revision were
measured with fontTools on the staged Aileron fonts (advance widths, no kerning).

**Revision 3 (4 Oct 2026):** adds founder decisions A1–A5: the shift opens only after the day's first
scenario (A1, closes E6); every cheap option has a cost the player can see (A2, closes E7); a personal
spending profile from two taps in character creation shapes places, order and travel amounts (A3); the
first screen states the promise and the HUD shows today's start and current total (A4); E2 and E4 are
closed (A5). The economy was re-simulated with `tools/sim_chapter.py` (six play styles × 16 profiles);
its table is the new reference for `EconomySimTests` (§3.2, WP9). Every change is listed in §11.

**Revision 4 (6 Oct 2026):** founder decisions F1 and F2 and the player-experience review. The food week
gets a second food moment (`kota_run`, Day 5, food focus only) and Mali's N0 line needs R100 or more (F1);
walkers can keep walking when the fare rises, and the CC places line always shows the travel tap (F2); loan
days say so in the reveal pill; tags, ledger labels and several lines are reworded; the greeting names
places as "at home", "down the road". The economy was re-simulated (the food and walk rows of §3.2 change).
Every change is in §11, "Revision 4".

Contents
1. Player journey (first 3 minutes, a full day, the chapter end)
2. Data model, save, PlayerPrefs, money recorder, invariant, tests outside Unity
3. The chapter: schedule, economy, every scenario's final content
4. Copy templates (Mali lines, reflection, plans, first-run, notices, empty states)
5. UI kit: tokens, glyph policy, components with layout numbers
6. Assets: exact imports, Mali art prep, app icon, audio
7. Systems
8. Work packages
9. Resolution of every item in `phase1-critic.md`
10. Escalations (founder) and human tasks
11. Revision log
12. Later: bank-data source (not in the beta)

Conventions used in this document
- **u** = canvas unit on a 1920×1080 reference canvas with `matchWidthOrHeight = 1`. The canvas is
  always 1080 u tall; its width is 1080 × aspect (1920 u at 16:9, 2400 u at 20:9).
- Money is whole Rand. `R1 250` = one thousand two hundred and fifty Rand (plain space, see §5.2).
- "Night of Day N" = the moment the player sleeps at the end of Day N.
- Player-facing text in this spec is final copy. `{name}` etc. are tokens (§4.1).

---

## 0. Fixed numbers (one number for each thing)

Every other section uses these. If a number is needed that is not here, it is a bug in the spec:
ask, don't invent.

| Thing | Value | Constant (where) |
|---|---|---|
| Chapter length | 7 days. Day 1 is the first day; payday is "Day 8", which is never played in the beta | `ChapterConfig.ChapterLength = 7`, `ChapterConfig.PaydayDay = 8` |
| Start money (everyone, every run) | Cash R600, Savings R400 (total R1 000) | `ChapterConfig.StartCash = 600f`, `StartSavings = 400f` |
| Start hidden stress | 25 | `ChapterConfig.StartStress = 25f` |
| Energy | 100 at the start of every day; sleeping resets to 100; never below 0 or above 100 | `ChapterConfig.DailyEnergy = 100f` |
| Work shift | +R150 cash, needs ≥ 60 energy and uses 60, once per day. **Opens only after the day's first scheduled scenario is resolved** (A1; "first" = position 0 of that day in the player's schedule, §3.1). With the schedule flag off there is no gate | `ChapterConfig.ShiftPay = 150f`, `ShiftEnergyCost = 60f`; `WorkRules.State` (§7.3) |
| Energy of the free options | Skip lunch 45, walk there and back 45, free internet 45, walk today / keep walking (fare rise) 50, cold showers 10, cook at home (kota run) 20, fetch half a kota 10. Anything above 40 taken as the day's first choice leaves < 60, so it costs that day's shift. Energy still never blocks a choice | content, §3.2, §3.4 |
| Bill: Airtime | R60 (calls and messages; data is bought separately, §3.4.3), due Day 2, then every 7 days (Day 9, outside the chapter) | `ChapterConfig.AirtimeAmount = 60f`, `AirtimeDueDay = 2` |
| Bill: Rent | R500, due Day 3, then every 7 days (Day 10) | `ChapterConfig.RentAmount = 500f`, `RentDueDay = 3` |
| Pay-later (speaker) | R120 now + 2 × R130, every 2 days (R380 in all, R20 more than paying R360 now) | content, §3.4 |
| Day bundles | R15 now + R15 on each of the next 2 nights, never after Day 7 | content, §3.4.3 |
| Travel amounts (by travel mode, §3.4.0) | Trip across town (usual way): taxi R30, e-hailing R44, on foot R30, car R36. Daily commute from the fare rise: R34 / R50 / R34 / R40, paid that day and every night to Day 7. Lift club R120 for every mode. On foot (Revision 4) the fare rise's free option is "Keep walking, no fares" (50 energy, nothing later); the other modes walk today and pay from tomorrow. Bounds every profile stays inside: trip R30–R50, daily R34–R50 | `ScenarioLibrary` (WP3), §3.4.0 |
| Family follow-up | Your aunt calls back 2 days later (only if that day ≤ 7): after "Send R80 for now" she asks R120 (`family_callback`); after "Explain you can't this week" she asks the full R200 (`family_callback_full`) | content, §3.4.7b |
| Bra K loan | R400 → 1 × R600, or R200 → 1 × R300, due 2 days later | content, §3.4 |
| Payday commitments | Stokvel R200, Gym debit order R199, Geyser repair R350 (only after "cold showers"), all due Day 8 (never charged in the beta; shown as "Already promised for payday") | content, §3.4 |
| Spending profile (two taps in character creation) | Focus: `food` / `transport` / `data_social` / `home_family`; travel: `taxi` / `ehailing` / `walk` / `car`. Default `food` + `taxi` = the generic chapter | `SpendingProfile` (§2.7) |
| Bank transfer chips | R50, R100, R200, each direction | `BankRules.Amounts = {50,100,200}` |
| XP per scenario choice | 5 (all choices); XP is never shown | content |
| Hidden stress threshold for Mali's "stretched" tone | ≥ 60. In the Revision 3 simulation (`tools/sim_chapter.py`, every style × profile) the reference styles reach it only through arrears or lost shifts (the saver who skips lunch before work: 61–73; never works: 87–98). A player who keeps every shift and pays every bill can still reach it (up to 77) by stacking deferrals (e.g. skip lunch after the shift, day bundles, pay later, the new fare, can't this week then not this week either, not this time, cold showers, borrow R400 and repay it on night 7, leave the debit order: 77 with the `transport` focus, 72 with `food` (cook at home on Day 5), 70 with the other two foci, in every travel mode). That is intended: putting things off piles up worry. Don't tune it expecting otherwise; the script prints the highest hidden stress over every path it plays | `ChapterConfig.StretchedStress = 60f` |
| Arrears stress | +10 on any night something is still owed (existing) | `ObligationDefaults.MissedPaymentStress` |
| Save version | 2 (anything lower → fresh start). Revision 3 adds fields with initialisers and keeps 2: no version-2 save has ever shipped | `PlayerData.CurrentSaveVersion = 2` |
| Interaction radius | 0.7 world units for locations and scenario spots; 0.74 for Mali | arbiter |
| Spot spacing | every pair of interactables ≥ 1.4 world units apart | §3.3 |
| Touch target minimum | 144 u (≈ 48 dp on a 1600×720, 2.0-density phone) | `UiTheme.TargetMin` |
| Text sizes | dialogue 46 u, body 40 u, floor 32 u; full scale §5.1. Never scaled down to fit: copy is shortened instead, and fit is tested with Aileron metrics (§2.5) | `UiTheme` |
| Typewriter | 45 chars/s (Normal), 25 (Slow), Instant; 180 ms after `. ! ?`, 80 ms after `,` | `GameSettings` / dialogue |
| Reveal ledger | at most 7 rows, then one "Other (n)" row | §5.4.8 |
| Frame rate | `Application.targetFrameRate = 60`; `Screen.sleepTimeout = SleepTimeout.NeverSleep` | `AppLifecycle` |
| App version | `0.2.0`, bundle code `2`, package id `com.maligo.app` (unchanged). The build on testers' phones today is version name `1.0`, code `1`; 0.2.0 installs over it because code 2 > 1, and testers will see the name change from 1.0 to 0.2.0 | `MaliGoAndroidSetup` |
| Audio budget | < 6 MB of source files; this spec's list is 4 304 849 bytes. Unity re-encodes to Vorbis q0.5, so the size inside the APK is checked in the founder's build report (§10 H4) | §6.4 |
| Save fields that persist the night | `DayRecord.owedAtClose` (still owed after the night's bills), `Obligation.createdDay`, `PlayerData.morningLineDay`, `PlayerData.followUps`, `PlayerData.spendingProfile` (kept by "Live the week again", wiped by Start over) | §2.3, §2.7 |
| HUD | Day pill and a **Today pill under it** ("Today from R1 000" / → current total), Cash, Savings, Energy, bill, Pause (§5.4.1) | `HudView` |

---

## 1. Player journey

### 1.1 First three minutes (new player, Day 1)

| Time | What the player sees and does | System |
|---|---|---|
| 0:00 | Taps the Mali app icon. Unity splash only if the licence refuses to turn it off (§7.13). | `MaliGoAndroidSetup` |
| 0:02 | **Character creation, screen 1 of 5.** Forest backdrop, cream sheet. At the top, the promise in DisplayTitle 72 on two lines: *"Live the week before payday."* / *"See where your money goes."* Under it *"What should we call you?"* and a 144 u-tall name field with the Android keyboard up. If an old save was just discarded, a one-line banner reads *"MaliGo has been updated — your story starts fresh."* for 6 s. The calm music loop fades in. | `CharacterCreationUI`, `OnboardingCopy`, `AudioManager` |
| 0:12 | Reads the promise while the keyboard opens, types a name, taps **Next** (disabled while the name is empty). | |
| 0:13 | **Screen 2: "Pick your look."** A turning 3D preview of the character on the left, six style cards (3 × 2: two builds × three skin tones, §4.6) on the right. Default: Style 1. Taps one, taps **Next**. | look preview, §5.4.13 |
| 0:22 | **Screen 3: your money (A3).** Two rows of four large cards. *"Where does most of your money go?"* → taps **Food and takeaways**. *"How do you usually get around?"* → taps **Minibus taxi**. As soon as both are picked a line fades in under the rows: *"We've built your week around where your money goes."* and the places, *"Kota shop · Taxi rank · Shops by the bank"*. **Next** (disabled until both rows have a pick). About 10 s. | `SpendingProfile`, `ChapterSchedule.WeekPlaces` |
| 0:32 | **Screen 4: "What are you saving towards?"** Four goal cards: Emergency buffer R2 000 · December trip home R2 500 · New phone R3 000 · Side-hustle stock R1 500. Each card shows "R400 saved so far". Taps one, taps **Next**. | `GoalPresets` |
| 0:40 | **Screen 5: Mali.** Mali's portrait (left, 520 u) and her words typing at 45 cps, three paragraphs, each paginated at 3 lines (§4.6): *"Hi {name}, I'm Mali. It's the week before payday: R600 in your pocket and R400 in savings."* → *"You're saving for your {goalName}: R{goalTarget}. The R150 shift on the main road opens once the day's first thing is sorted. It takes 60 of your 100 energy."* → *"I won't tell you what to do. I'll show you where your money went, every night."* Taps **Let's go**. | `isCharacterCreated = true`, `hasMetMali = true`, `ChapterFlow.StartChapter`, save written |
| 0:58 | World loads (≈ 2–4 s). The township ambience bed fades in. | bootstrap |
| 1:01 | **Day 1** (11 s later than Revision 2: the two taps and the line ≈ 10 s, the promise ≈ 1 s; A3 allows ~10 s). HUD across the top: `Day 1 of 7 / Payday in 7 days` · `Cash R600` · `Savings R400 of R2 000` · `Energy 100` · `Airtime / R60 · tomorrow` · pause button, and under the Day pill the Today pill `Today from R1 000 / → R1 000` (§5.4.1; "/" = second line). The player stands at their front door; the Home prompt is visible at the bottom ("Home  [Tap]"). | `HudView`, arbiter |
| 1:02 | Mali's compact line (bottom, auto-hides): *"Day 1, {name}. Lunch first, then the shift. A trip across town too."* (≈ 1.5 s typing + ≈ 5 s reading). | `DayStarted` → Mali |
| 1:09 | When Mali's line has gone (`OnDialogueHidden`): **coach mark 1** — scrim with a hole around the joystick: *"Drag here to walk around the neighbourhood."* [Got it] [Skip tips]. The player drags; the mark closes once they have moved 1 world unit, or on Got it. | `FirstRunGuide` |
| 1:13 | Walks around the parked car towards the intersection (about 3.5 world units ≈ 1 s at 4.5 u/s; fumbling makes it 5–8 s). (A player who heads east to Work first sees the greyed prompt *"Shift opens after lunch"*; a tap shows Mali's line *"The shift opens after lunch. A shift needs 60 energy."*) | `WorkRules.State` = NotYet |
| 1:20 | The prompt changes to *"Lunch at the kota shop  [Tap]"*; the round action button reads **Look**. **Coach mark 2**: hole around the prompt: *"When something's nearby, its name shows here. Tap it to see what's going on."* | arbiter, `WorldPromptView` |
| 1:23 | Taps the prompt. **Choice sheet** rises (300 ms; taps ignored for 300 ms). Left: "Kota shop" · *What's for lunch?* · situation · Mali's small quote *"Midday already, {name}. What are you having?"* · *You have R600 cash · Savings R400 · Energy 100*. Right: three cards under the columns **Cash · Energy · Later**; *Skip lunch* shows Energy −45. | `ScenarioChoiceUI` |
| 1:36 | Taps **Vetkoek and mince (R20)**. Sheet closes; coin sound; the Cash pill counts R600 → R580 with a brown "−R20" tag under it for 1.6 s, and the Today pill counts → R980. Mali's box (blocking) types *"R20 for lunch, {name}, and you're sorted."* with a chip row `Cash −R20`. The joystick and action button are hidden while she talks. Lunch was the day's first scenario, so the shift is now open. | `ScenarioOutcome`, `MoneyRecorder` |
| 1:42 | Taps once (completes the line), taps again (closes). **Coach mark 3** around the Cash pill: *"This is your cash. Savings is next to it. Every choice shows up here straight away."* | |
| 1:48 | Walks west to the taxi rank. *"A trip across town  [Tap]"* → choice sheet *Getting across town* → taps **Minibus taxi (R15 each way)** → Cash R580 → R550, Today → R950, Energy 100 → 95, Mali: *"R30 for the taxi, {name}, there and back."* | |
| 2:13 | Stands still: prompt *"Talk to Mali"*. Optional tap → *"That's everything for today, {name}. Sleep at home when you're ready. There's a shift going at the far end of the main road."* | `MaliGreeting` |
| 2:23 | Walks east to Work: *"Take a shift (+R150)  [Tap]"*, action button **Work**. Taps. Cash R550 → R700 (green "+R150"), Today → R1 100, Energy 95 → 35. Mali (compact): *"R150 for the shift, {name}."* | `WorkRules` |
| 2:33 | Walks home. Home prompt appears → **coach mark 4** around it: *"Sleeping at home ends the day. Then you'll see where your money went."* Taps the prompt → **Home sheet**: status rows and **Sleep: end Day 1**. | |
| 2:40 | Taps Sleep → **confirm sheet**: *End Day 1?* · *Nothing is due tonight.* · [Not yet] [Sleep]. Taps **Sleep**. Both sheets close. | `DayFlowController` |
| 2:42 | **End-of-day reveal.** The world fades to forest green (400 ms), steel-pan chime. *"You started Day 1 with"* **R1 000** *cash + savings*. Rows stagger in: `↓ Vetkoek and mince −R20` · `↓ Taxi there and back −R30` · `↑ Shift at work +R150`. *"You ended it with"* counts R1 000 → **R1 100** *cash + savings*. Neutral pill *"+R100 today"*, *"Cash R700 · Savings R400"*. Mali types: *"The shift brought in the most today: R150. Tomorrow night, Airtime takes R60. You've got R700 in cash."* Coming up (under the rows): *"Day 2 night: Airtime R60"* and *"Tomorrow: no data, and a deal at the phone shop."* Button **On to Day 2**. Any tap before the end jumps to the final state. (↓ ↑ are arrow sprites, not glyphs.) | §5.4.8 |
| 2:58 | Taps **On to Day 2**. New-day jingle. HUD: `Day 2 of 7 / Payday in 6 days`, Today `Today from R1 100 / → R1 100`, Energy 100. Mali (compact): *"Day 2. Your phone's out of data, and the phone shop has a deal on."* | `DayStarted(2)` |
| 3:16 | Heads for the kota shop (the data scenario is Day 2's first, so it opens the shift). First run is complete (`maligo.firstRunDone = 1`). | |

### 1.2 A full day (Day 3, the "middle" path from §3.2)

Start of Day 3 (profile Food and takeaways + Minibus taxi): Cash R655, Savings R400, Energy 100. Owed: Speaker pay-later 2 × R130 (Day 4, Day 6), Day bundles R15 (nights 3 and 4). HUD bill pill: `Rent` / `R500 · tonight`; Today pill `Today from R1 055`.

1. Morning line (compact): *"Day 3. Getting to town costs more from today, and that hoodie's still there."* Work's prompt is greyed: *"Shift opens after the trip to town"* (the fare rise is Day 3's first scenario).
2. Taxi rank: *The fare went up* → **Pay the new fare (R34 a day)** (card: Cash −R34, Energy −5, Later "R34 × 4" / "Days 4–7") → Cash R621, Energy 95. Mali: *"R34 today, {name}. The fare comes off again on Days 4 to 7."* The shift is open now.
3. Work: shift → Cash R771, Energy 35, Today → R1 171.
4. Shop window (spot SHOPFRONT): *Something caught your eye* → **Walk away for now** → no money moves; Mali: *"Nothing spent, {name}. It's on special for a few more days if you still want it."*
5. Talk to Mali (optional): *"That's everything for today, {name}. Sleep at home when you're ready."*
6. Home → Sleep → confirm: *End Day 3?* · *Tonight: Rent R500* · *Tonight: Day bundles R15* · [Not yet] [Sleep].
7. Night: Rent R500 and Day bundles R15 are charged from cash (R771 → R256). Day 3 closes.
8. Reveal: started **R1 055** *cash + savings*, rows `−R34 Taxi at the new fare` · `+R150 Shift at work` · `−R500 Rent` · `−R15 Day bundles`; ended **R656**; pill *"−R399 today"*; *"Cash R256 · Savings R400"*; *"New promise: Taxi R34 × 4, Days 4–7"*. Mali: *"Rent took the most today: R500. Tomorrow night, Speaker takes R130. You've got R256 in cash."* Coming up: *"Day 4 night: Speaker R130"* · *"Tomorrow: a call from home, and a birthday."* → **On to Day 4**.

A day takes about 2.5–4 minutes: 2 scenarios (≈ 30–40 s each incl. walking and reading), a shift (≈ 10 s), sleep + reveal (≈ 30 s), plus wandering; a family call-back (§3.4.7b) adds ≈ 30 s on the day it comes. The chapter takes about 20–30 minutes.

### 1.3 The chapter end (night of Day 7)

1. Day 7: debit-order scenario at the gate, shift, Home → Sleep. Night of Day 7 settles anything due Day 7 (e.g. Bra K's R600) **before** the chapter closes.
2. Day 7 reveal as usual, but the button reads **On to payday** and Coming up says *"Tomorrow is payday."*
3. **Chapter end, screen A — the week.** Mali waving (left). *Seven days to payday.* *"On Day 1 you had"* **R1 000** *cash + savings*; *"Tonight you have"* **R515** *cash + savings* (amounts at 104 u), neutral pill *"−R485 this week"*, *"Cash R315 · Savings R200"*. Then, at Body size beside the total and never optional when non-zero: *"Still owed going into payday: R___"* (middle path: nothing owed, line omitted) and *"Already promised for payday: R399"* with the list *"Stokvel R200 · Gym R199"* under it. **What moved it most** — your choices, not the fixed bills: three cards (middle path: *Day 5 · Geyser repair from savings −R350*, *Day 6 · From the neighbour +R300*, *Day 2 · Speaker deposit −R120*; the Day 6 call-back's −R120 ties and loses to the earlier event), and under them *"Bills and repayments: −R986"* (airtime, rent, day bundles, speaker, taxi fares). **Next**.
4. **Screen B — what Mali noticed.** Mali waving (left), 2–3 reflective lines (§4.4), typed one after another. **Next**.
5. **Screen C — payday plan.** *"Tomorrow is payday."* *"If you could decide one thing about payday tonight, what would it be?"* Four plan cards (§4.5). Pick one → **Save my plan** (or **Not now**).
6. **Screen D — close.** Mali: *"Got it. Next time this week starts, I'll remind you what you said."* Caption: *"Chapter 2 starts on payday. It's coming in a future update."* Button **Live the week again** → a fresh run of Chapter 1 (same name, look, goal and spending profile; R600/R400; Day 1). Replaying after Day 7 is the beta's ending (E2 closed, A5). On the new Day 1 Mali opens with the plan quote (§4.2.4). A replay never shows the previous run's total anywhere, so the week's total can't become a high score to beat.

---

## 2. Data model, save and the money recorder

### 2.1 Money pools and what counts

- **Two pools only: cash and savings** (D4). The headline everywhere is **total = cash + savings**.
- The goal is a *target for savings*: `goal progress = savings / goal.targetAmount` (clamped 0–1).
  The separate goal pot (`FinancialGoal.currentAmount`) is retired: always 0, never read.
- A **transfer** (cash ↔ savings, same amount both ways) is neutral: it never changes the total and is
  never shown as a loss.
- **Still owed** (arrears) and **already promised** (obligations not yet due) are shown separately and
  are never part of the total.
- Energy, stress, XP are not money and are never recorded as money events.

### 2.2 Event kinds and categories

`MoneyEventKind` is derived by the recorder, never passed in:
- `In` if `cashDelta + savingsDelta > 0.005`
- `Out` if `cashDelta + savingsDelta < −0.005`
- `Transfer` otherwise (and at least one delta is non-zero)

Categories (string constants in `MoneyCategory`, `Assets/Scripts/Economy/MoneyCategory.cs`):
`Work`, `Food`, `Transport`, `Phone & data`, `Shopping`, `Family`, `Friends`, `Home`, `Bills`,
`Pay-later`, `Loan`, `Extra money`, `Savings move`.

### 2.3 Exact C# shapes (new and changed)

All in namespace `MaliGo.Data` unless stated. C# 9 (Unity `LangVersion 9.0`, `netstandard2.1`): no
records, no `init`, no file-scoped namespaces. Arrays, not `List<>`, in serialized types (JsonUtility).

**JsonUtility pitfalls every builder must respect**
1. `JsonUtility.FromJson` runs field initialisers, so a field missing from old JSON gets its
   initialiser value. `saveVersion` therefore has **no initialiser** (defaults to 0) and is set
   explicitly by `CreateNew()`/`StartChapter()`.
2. Serializable class fields are never null after loading; an "empty" `DayRecord` has `day == 0`.
   Treat `day == 0` as "no record".

```csharp
// Assets/MaliGo/Data/MoneyEvent.cs  (new)
namespace MaliGo.Data
{
    public enum MoneyEventKind { In = 0, Out = 1, Transfer = 2 }

    [Serializable]
    public class MoneyEvent
    {
        public string label = "";        // player-facing ledger text, <= 28 chars
        public string category = "";     // a MoneyCategory constant
        public float cashDelta;          // actual applied delta, never clamped afterwards
        public float savingsDelta;
        public MoneyEventKind kind;      // set by MoneyRecorder
        public string sourceId = "";     // "scenario:<scenarioId>/<choiceId>", "work:<day>", "bank:to_savings",
                                         // "bank:from_savings", "bill:<obligationId>"
        public float TotalDelta => cashDelta + savingsDelta;
    }
}

// Assets/MaliGo/Data/DayRecord.cs  (new)
[Serializable]
public class DayRecord
{
    public int day;                      // 0 = no record
    public float startCash;
    public float startSavings;
    public float endCash;                // valid when closed
    public float endSavings;
    public MoneyEvent[] events = Array.Empty<MoneyEvent>();
    public bool closed;
    public float owedAtClose;            // ObligationLedger.TotalArrears after the night's bills (set by
                                         // DayCycle.EndDay); "still owed" history survives a restart
    public float StartTotal => startCash + startSavings;
    public float EndTotal => endCash + endSavings;
}

// Assets/MaliGo/Data/ChapterRecord.cs  (new)
[Serializable]
public class ChoiceRecord
{
    public int day;
    public string scenarioId = "";
    public string choiceId = "";
    public string tag = "";              // ScenarioBehaviourTag name: "Neutral" | "Frugal" | "Discretionary" | "Deferred"
}

[Serializable]
public class ChapterRecord
{
    public int chapterNumber = 1;
    public int runNumber = 1;            // 1 on first play, +1 per "Live the week again"
    public float startCash;
    public float startSavings;
    public DayRecord[] days = Array.Empty<DayRecord>();       // closed days, in order (max 7)
    public ChoiceRecord[] choices = Array.Empty<ChoiceRecord>();
    public int shiftsWorked;
    public bool complete;                // set on the night of Day 7; while true the world always shows
                                         // the chapter end (screen A) until "Live the week again"
}

// Assets/MaliGo/Data/PlayerData.cs  (changed: add these fields; keep every existing field)
public const int CurrentSaveVersion = 2;
public int saveVersion;                              // NO initialiser (pitfall 1)
public DayRecord today = new DayRecord();            // the open day; day == currentDay while playing
public ChapterRecord chapter = new ChapterRecord();
public string paydayPlanId = "";                     // PaydayPlans id, survives "Live the week again"
public string paydayPlanText = "";                   // snapshot of the plan sentence
public int revealPendingForDay;                      // > 0: the reveal for that day has not been dismissed
public int morningLineDay;                           // the day whose morning line (and DayStarted) already ran
public SpendingProfile spendingProfile = new SpendingProfile();   // §2.7 (A3); kept by StartChapter, wiped only by Start over
public string[] followUps = Array.Empty<string>();   // follow-up scenarios set off by a choice, as keys "scenarioId@day"
                                                     // (ChapterSchedule.FollowUpKey, §3.1); appended by ScenarioOutcome.Apply,
                                                     // emptied by StartChapter. Plain strings so WP3 can read them in stage 1
public static bool ShouldReset(int saveVersion) => saveVersion < CurrentSaveVersion;   // used by TryLoad, tested
// CreateNew(): builds the object as today, then calls MaliGo.Economy.ChapterFlow.StartChapter(data, 1)
// before returning, so every new save is a valid Day-1 state (cash 600, savings 400, today opened,
// saveVersion = CurrentSaveVersion, spendingProfile = defaults "food" + "taxi", source "default"). This also keeps the branch runnable between stages 1 and 2.
// Other defaults unchanged (currentLifeChapter stays YOUNG_PROFESSIONAL; financialProfile defaults stay; D1, D2).

// Assets/MaliGo/Data/Obligation.cs  (changed: add)
public string shortLabel = "";   // <= 10 chars, HUD pill: "Rent", "Airtime", "Speaker", "Bra K", "Stokvel", "Gym",
                                 // "Geyser", "Data", "Taxi", "Rides", "Petrol"
public string category = "Bills";// MoneyCategory used when it is charged
public string kind = "bill";     // "bill" | "instalment" | "loan" | "commitment" | "repeat" (a cost that comes back
                                 // each night: day bundles, the daily fare; shown and charged like an instalment)
public int createdDay;           // day the scenario choice created it (0 for the base bills); feeds
                                 // "New promise" on that night's reveal

// Assets/MaliGo/Data/FinancialGoal.cs  (changed)
// goalId = preset id ("buffer" | "december" | "phone" | "hustle"); goalName = preset title;
// targetAmount = preset target; currentAmount and dailyTarget are retired (always 0).
// Default constructor = the "buffer" preset with currentAmount 0 (was 3800).
public float Progress(float savings)  => targetAmount > 0f ? Mathf.Clamp01(savings / targetAmount) : 0f;
public float Remaining(float savings) => Mathf.Max(0f, targetAmount - savings);
// Keep Deposit(), ProgressNormalized, ProgressPercentage, RemainingAmount until WP9 (still referenced).

// Assets/MaliGo/Data/GoalPresets.cs  (new)
public struct GoalPreset { public string id; public string title; public string shortName; public float target; }
public static class GoalPresets
{
    public static readonly GoalPreset[] All;   // in this order:
    // { "buffer",   "Emergency buffer",   "emergency buffer",   2000 }
    // { "december", "December trip home", "December trip home", 2500 }
    // { "phone",    "New phone",          "new phone",          3000 }
    // { "hustle",   "Side-hustle stock",  "side-hustle stock",  1500 }
    public static GoalPreset Get(string id);                 // unknown id -> "buffer"
    public static FinancialGoal CreateGoal(string id);       // goalId=id, goalName=title, target, current 0
    public static string ShortName(FinancialGoal goal);      // by goalId, falls back to goalName.ToLowerInvariant()
}

// Assets/MaliGo/Data/PaydayPlans.cs  (new)
public struct PaydayPlan { public string id; public string text; }
public static class PaydayPlans
{
    public static readonly PaydayPlan[] All;   // §4.5, in that order
    public static PaydayPlan Get(string id);   // unknown -> default(PaydayPlan) with empty id
}

// Assets/MaliGo/Data/ChapterConfig.cs  (new) — the constants in §0
public static class ChapterConfig
{
    public const int ChapterLength = 7, PaydayDay = 8;
    public const float StartCash = 600f, StartSavings = 400f, StartStress = 25f;
    public const float DailyEnergy = 100f, ShiftPay = 150f, ShiftEnergyCost = 60f;
    public const float AirtimeAmount = 60f, RentAmount = 500f;
    public const int AirtimeDueDay = 2, RentDueDay = 3, WeeklyIntervalDays = 7;
    public const float StretchedStress = 60f;
    public static int DaysToPayday(int day) => PaydayDay - day;  // 7 on Day 1, 1 on Day 7
}
```

`ObligationDefaults` (changed): airtime = `{ id "airtime_data", label "Airtime", shortLabel "Airtime",
amount 60, interval 7, nextDueDay 2, paymentsRemaining -1, category "Phone & data", kind "bill" }` (Phone &
data, so N0's total for "Data, airtime and going out" includes the airtime the player named, §4.4; nothing
else reads it: the bills line sums `bill:` events); rent = `{ id "rent",
label "Rent", shortLabel "Rent", amount 500, interval 7, nextDueDay 3, -1, "Bills", "bill" }`. Due days are
absolute (Day 2, Day 3), not "today + n". Keep `AddBaseObligations`, `AddObligation`, `MissedPaymentStress`.

### 2.4 The money recorder (the only writer of cash and savings)

```csharp
// Assets/Scripts/Economy/MoneyRecorder.cs  (new, pure: System + UnityEngine.Mathf/Debug only)
namespace MaliGo.Economy
{
    public static class MoneyRecorder
    {
        /// True if applying the deltas leaves both pools >= 0 (tolerance 0.005).
        public static bool CanAfford(PlayerData data, float cashDelta, float savingsDelta);

        /// Applies the deltas to data.financialStats, appends a MoneyEvent to data.today.events,
        /// queues GameEvents.MoneyChanged (see below), returns the event. Never clamps: if the result
        /// would make a pool negative it logs an error, changes nothing and returns null. Zero deltas -> null.
        /// Precondition: data.today.day == data.currentDay && !data.today.closed. Apply NEVER opens a
        /// day itself: if the precondition fails it logs an error, changes nothing and returns null.
        /// Days are opened only by ChapterFlow.StartChapter and DayCycle.EndDay.
        public static MoneyEvent Apply(PlayerData data, float cashDelta, float savingsDelta,
                                       string label, string category, string sourceId);

        /// Opens the record for data.currentDay if today.day != currentDay: startCash/startSavings =
        /// current stats, events empty, closed false.
        public static void OpenDay(PlayerData data);

        /// Sets endCash/endSavings from current stats, closed = true, appends a copy to
        /// data.chapter.days, returns it. No-op (returns null) if already closed.
        public static DayRecord CloseDay(PlayerData data);

        /// The invariant (2.5). Returns null if it holds, else a description of the first violation.
        public static string CheckInvariant(PlayerData data);
    }
}
```

**Every money change in the game goes through `MoneyRecorder.Apply`.** After this beta, a grep for
`\.cash\s*[-+]?=` or `\.savings\s*[-+]?=` outside `MoneyRecorder.cs`, `ChapterFlow.cs` (start of a run)
and `FinancialStats.CreateDefaults` must find nothing. Callers:

| Caller | Call |
|---|---|
| Scenario choice | `ScenarioOutcome.Apply` → `Apply(data, c.cashDelta, c.savingsDelta, c.ledgerLabel, s.moneyCategory, "scenario:"+s.scenarioId+"/"+c.choiceId)` |
| Work | `WorkRules.DoShift(data, gateScenarioId)` → `Apply(data, 150, 0, "Shift at work", "Work", "work:"+day)` |
| Bank | `BankRules.MoveToSavings(data, a)` → `Apply(data, −a, +a, "Moved to savings", "Savings move", "bank:to_savings")`; `TakeOut` → `Apply(data, +a, −a, "Taken out of savings", "Savings move", "bank:from_savings")` |
| Bills, instalments, loans | `ObligationLedger.SettleDue` → per obligation with `paid > 0`: `Apply(data, −paid, 0, label, o.category, "bill:"+o.obligationId)`; label is `o.label`, or `o.label + " (part)"` when something is still owed |
| Loan received | it is a scenario choice (`cashDelta = +400`), category `Loan` |

`ObligationLedger.SettleDue` keeps its signature and semantics (cash only, never savings; shortfall →
arrears; arrears paid first; +10 hidden stress on a night with anything still owed) and replaces
`stats.cash -= paid` with the recorder call. `paid = min(cash, owed)` is honest, not a discount: the
unpaid part is carried as arrears and shown as "still owed". On a night with no cash nothing is
recorded as a money event (zero deltas), which is why `DayRecord.owedAtClose` exists.

**Shared bill helpers** (pure, in `ObligationLedger`, WP1, tested in `CoreTests`). Every "due tonight",
"next bill", "still owed" and "promised" text in the game (HUD bill pill, Home sheet, sleep confirm,
reveal, chapter end) is built from these and nowhere else:

```csharp
public struct DueItem { public string obligationId, label, shortLabel, kind; public float amount; public int day; public int count; }
public static float TotalArrears(PlayerData d);                        // existing
public static Obligation LargestArrears(PlayerData d);                 // null if none
public static List<DueItem> DueOnNight(PlayerData d, int day);         // what SettleDue(d, day) would newly charge
                                                                       // tonight (arrears excluded), largest first
public static DueItem? NextDue(PlayerData d);                          // soonest nextDueDay >= currentDay with payments
                                                                       // left and nextDueDay <= 8; ties -> larger amount
public static List<DueItem> Promised(PlayerData d);                    // non-recurring (paymentsRemaining > 0) payments
                                                                       // falling on or after Day 8, one item per obligation
                                                                       // (amount x count), in creation order
public static List<DueItem> NewPromises(PlayerData d, int day);        // obligations with createdDay == day:
                                                                       // amount, count, first due day
```

**MoneyChanged is queued, not raised inside the mutator.** `MoneyRecorder.Apply` calls
`GameEvents.QueueMoneyChanged(e)`. `PlayerDataManager.UpdatePlayerData` runs the mutator, raises
`OnPlayerDataChanged`, saves (if asked), and only then calls `GameEvents.FlushMoneyChanged()`, which raises
`MoneyChanged` once per queued event in order. So subscribers always see the finished state (bills paid
*and* the day closed). Tests call `FlushMoneyChanged()` themselves. Rule for every subscriber of any
`GameEvents` event: never call `UpdatePlayerData` or `Save` synchronously from the handler (defer to the
next frame if a write is really needed).

### 2.5 The invariant and how it is tested

For every closed `DayRecord r` and for each pool P ∈ {cash, savings}:
`r.startP + Σ e.Pdelta == r.endP` (|error| ≤ 0.005), **and** continuity:
`days[i].endP == days[i+1].startP`, `days[0].startP == chapter.startP`. For the open day:
`today.startP + Σ == stats.P`.

Tests run **outside Unity** with `dotnet run --project tools/logic_tests` (exit code 0 = pass).

- **Shim** (`tools/logic_tests/UnityShim.cs`, namespace `UnityEngine`) provides exactly: `Mathf` (`Min`,
  `Max`, `Clamp`, `Clamp01`, `Abs`, `Sign`, `Floor`, `Ceil`, `Round`, `RoundToInt`, `FloorToInt`, `CeilToInt`,
  `Lerp`, `Approximately`, `Epsilon`, `PI`, `Infinity`), `Debug` (`Log`, `LogWarning`, `LogError`,
  `LogException` → console; `LogError`/`LogException` also count as a test failure unless the test expects
  it), `ScriptableObject` (`static T CreateInstance<T>() where T : new()`), and the attributes
  `SerializeField`, `Tooltip(string)`, `Header(string)`, `CreateAssetMenu` (`fileName`, `menuName`).
  The shim is owned by WP1 and frozen after stage 1 (only WP9 may add to it). Pure code that needs
  anything else uses `System.Math`/`System.MathF`, never a new Unity member.
- **No logic inside `#if !UNITY_EDITOR`** (or any `#if UNITY_ANDROID`-style block) anywhere in the
  project: `compile_check.py` compiles with the Editor defines, so such code is never checked. Platform
  differences are read at runtime (`Application.isEditor`, `Application.platform`).
- **Font metrics for fit tests**: `tools/make_font_metrics.py` (WP2, fontTools) writes
  `tools/logic_tests/Generated/AileronMetrics.cs` with
  `public static float Width(string text, string weight, float sizeU)` (advance widths per character
  for SemiBold/Bold/Black, UPM 1000; unknown characters throw). Stage-2 tests use it to prove copy fits
  its box (§8).
- **Logic file set** (compiled into the test project; every file in these folders must use only `System.*`
  and the shim): `Assets/MaliGo/Data/**`, `Assets/MaliGo/Scenarios/**`, `Assets/MaliGo/Dialogue/**`,
  `Assets/Scripts/Economy/**`, `Assets/Scripts/Core/**`, `Assets/Scripts/Copy/**`,
  `Assets/Scripts/Dialogue/**`, and the single file `Assets/Scripts/Scenarios/ScenarioOutcome.cs`.
  No `MonoBehaviour`, `GameObject`, `Vector3`, `Time`, `Application`, `PlayerPrefs`, `JsonUtility` there.
- **Project**: `tools/logic_tests/LogicTests.csproj`, `net10.0` console (SDK 10.0.401 is installed),
  `<LangVersion>9.0</LangVersion>`, `<Nullable>disable</Nullable>`, `<EnableDefaultCompileItems>false`,
  `<Compile Include>` with the globs above plus `UnityShim.cs`, `Program.cs`, `Tests/*.cs`,
  `Generated/*.cs`. `tools/logic_tests/bin/` and `tools/logic_tests/obj/` are added to `.gitignore` (WP1). Single files
  that a later package creates (`ScenarioOutcome.cs`) are included with `Condition="Exists(...)"`, and
  empty globs are fine, so the harness builds at every stage.
- **Runner** (`Program.cs`): reflects over every public static `void` method whose name starts with
  `Test` in every class whose name ends with `Tests`; a test fails by throwing; prints `PASS/FAIL name`
  and a total; exit code 1 on any failure. Helper `Assert` class: `True(bool, string)`,
  `Equal(float, float, string, float tol = 0.005f)`, `Equal(string, string, string)`.

The required tests are listed per work package (§8).

### 2.6 Save, reset and settings

**Save file**: `Application.persistentDataPath/player_data.json` (unchanged path).

| Rule | Spec |
|---|---|
| Version | `PlayerData.CurrentSaveVersion = 2`. `TryLoad`: if `PlayerData.ShouldReset(loaded.saveVersion)` → delete `player_data.json` and `player_data.json.bak`, `currentPlayer = PlayerData.CreateNew()`, set `PlayerDataManager.WasResetForUpdate = true` (static), return false. **Nothing is kept** (D3). |
| Notice | `CharacterCreationUI` shows *"MaliGo has been updated — your story starts fresh."* on its first screen when `WasResetForUpdate` is true, then clears the flag. |
| Atomic write | Serialize → write `player_data.json.tmp` → if the main file exists `File.Replace(tmp, main, main + ".bak")` else `File.Move(tmp, main)`. If `File.Replace` throws: `File.Copy(tmp, main, true)` then delete tmp. |
| Load fallback | If the main file fails to parse, try `.bak`; if both fail → `CreateNew()` (no notice). |
| When | Every `UpdatePlayerData(..., saveImmediately: true)` as today (call sites listed in systems-current-state §2.2), plus `OnApplicationPause(true)` and `OnApplicationQuit()` in `PlayerDataManager`. |
| Package id / key | Unchanged (`com.maligo.app`, the laptop's debug keystore) so 0.2.0 (code 2) installs over the current build (version name 1.0, code 1). |
| Reset save (pause menu) | `PlayerDataManager.DeleteSave()` (deletes main, `.bak`, `.tmp`), then `SceneManager.LoadScene("CharacterCreation")`. PlayerPrefs are **not** touched. `Time.timeScale` is set back to 1 before the load; the modal stack is cleared by the scene load (§7.2). |

**PlayerPrefs** (settings survive a save reset, D18). Owned by `GameSettings` (`Assets/Scripts/Settings/GameSettings.cs`):

| Key | Type | Default | Meaning |
|---|---|---|---|
| `maligo.sound` | int 0/1 | 1 | UI sounds, Mali cue, jingles, money sound, ambience |
| `maligo.music` | int 0/1 | 1 | Music loop |
| `maligo.textSpeed` | int 0/1/2 | 1 | Slow 25 cps / Normal 45 cps / Instant |
| `maligo.reduceMotion` | int 0/1 | 0 | Motion → 150 ms crossfades, numbers jump |
| `maligo.firstRunDone` | int 0/1 | 0 | All coach marks seen or skipped |
| `maligo.firstRunSteps` | int bitmask | 0 | bit 0..3 = coach mark 1..4 shown |

`GameSettings.Save()` calls `PlayerPrefs.Save()` after every change.

### 2.7 Spending profile (A3)

Every player's week is tethered to their own money habits by a small profile. In the beta it comes from
two taps in character creation (§4.6); later a bank-data source can fill the **same** object (§12), so no
game code changes then. Game code reads only `focus` and `travel`; the other fields are reserved.

```csharp
// Assets/MaliGo/Data/SpendingProfile.cs  (new, WP1, pure)
namespace MaliGo.Data
{
    [Serializable]
    public class SpendingProfile
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;       // shape version. A reader that meets a higher version keeps the
                                                   // fields it knows and ignores the rest (JsonUtility does this)
        public string source = "default";          // SpendingProfileSource: "default" | "onboarding" (the two taps);
                                                   // reserved: "bank_feed", "statement" (§12)
        public string focus = "food";              // SpendingFocus id: where most of the money goes
        public string travel = "taxi";             // TravelMode id: how the player usually gets around
        public long updatedUnixSeconds;            // 0 = never set by the player
        // Reserved for a summarised bank-data source (§12). Empty in the beta; no game code reads them.
        public CategoryShare[] categoryShares = Array.Empty<CategoryShare>();
        public RecurringDebit[] recurringDebits = Array.Empty<RecurringDebit>();
        public int paydayDayOfMonth;               // 0 = unknown
    }
    [Serializable] public class CategoryShare  { public string category = ""; public float share; public float typicalMonthlyAmount; }
    [Serializable] public class RecurringDebit { public string label = ""; public string category = ""; public float amount; public int dayOfMonth; }

    public struct ProfileOption { public string id; public string cardLabel; public string maliPhrase; }
    public static class SpendingFocus
    {
        public const string Food = "food", Transport = "transport", DataSocial = "data_social", HomeFamily = "home_family";
        public static readonly ProfileOption[] All;    // in this order (card label / Mali's phrase, §4.4 N0):
        // { "food",        "Food and takeaways",          "food and takeaways" }
        // { "transport",   "Getting around",              "getting around" }
        // { "data_social", "Data, airtime and going out", "data, airtime and going out" }
        // { "home_family", "Home and family",             "home and family" }
        public static string Normalize(string id);     // unknown/empty -> "food"
        public static string[] Categories(string id);  // money categories for N0: food -> {Food}; transport ->
                                                       // {Transport}; data_social -> {Phone & data, Friends};
                                                       // home_family -> {Home, Family}
    }
    public static class TravelMode
    {
        public const string Taxi = "taxi", EHailing = "ehailing", Walk = "walk", Car = "car";
        public static readonly ProfileOption[] All;    // { "taxi", "Minibus taxi" } { "ehailing", "E-hailing rides" }
                                                       // { "walk", "Mostly on foot" } { "car", "Own or shared car" }
                                                       // (maliPhrase = cardLabel in lower case)
        public static string Normalize(string id);     // unknown/empty -> "taxi"
    }
    public static class SpendingProfileSource { public const string Default = "default", Onboarding = "onboarding"; }
    public static class SpendingProfiles
    {
        /// The two taps: normalises both ids, sets source "onboarding" and updatedUnixSeconds. Pure.
        public static void SetFromOnboarding(SpendingProfile p, string focus, string travel, long nowUnixSeconds);
    }
}
```

**What the profile changes** (and nothing else; every amount stays inside bounds the simulation checks
for all 16 combinations, §3.2):

| Changes | How | Where |
|---|---|---|
| Places and their generic names | The corner shop is "the kota shop" (food), "the spaza shop" (home and family) or "the corner shop"; the transport spot is "the taxi rank" (taxi, on foot), "the pick-up point" (e-hailing) or "the petrol station" (car); the shop window is "the clothing shop" for data and going out. No real brand names anywhere | §3.3, `ChapterSchedule.SpotPlaceName/SpotPlaceLabel` |
| Order of the week | The focus picks one of four schedules, so the player's biggest money leak comes up on Day 1 (and the day's first scenario, which opens the shift, is always a need on Days 1–3). The food week also has a second food moment, the kota run on Day 5 (Revision 4) | §3.1 |
| Travel amounts | The usual way across town and the daily commute follow the travel mode, inside R30–R50 (trip) and R34–R50 (daily) | §3.4.0 |
| What Mali says | Reflectively, never as a verdict: the chapter end can say *"You said most of your money goes on getting around. This week that came to R200."* (§4.4 N0, only from R100) | §4.4 |
| What the player is shown | Right after the two taps: *"We've built your week around where your money goes."* and their three first places, always including the travel spot, so both taps change the line | §4.6, §5.4.13 |

`ChapterFlow.StartChapter` keeps the profile (like name, look and goal); `Start over` deletes the save, so
character creation asks the two questions again. Old or hand-edited values are normalised on read
(`SpendingFocus.Normalize`, `TravelMode.Normalize`), so an unknown id plays the default chapter.

---

## 3. The chapter

### 3.1 Day schedule

Data: `Assets/MaliGo/Scenarios/ChapterSchedule.cs` (pure). The player's **focus** (§2.7) picks one of four
schedules. A scenario is **active** on day D if it is scheduled for a day ≤ D in that schedule (or is a
follow-up whose day ≤ D, §3.4.7b) and not in `completedScenarioIds`. Unplayed scenarios carry over until
played. The day can end without playing anything. **The first scenario listed for a day is that day's
gate: the shift opens once it is resolved (A1, §7.3).** Carry-overs and follow-ups never gate, and
today's gate always comes first in its spot's queue, ahead of any carry-over (§3.3).

| Day | `food` (default) | `transport` | `data_social` | `home_family` | Night |
|---|---|---|---|---|---|
| 1 | `food_decision`, `transport_decision` | `transport_decision`, `food_decision` | `food_decision`, `group_chat_contribution` | `food_decision`, `family_obligation` | — |
| 2 | `data_runs_out`, `credit_bnpl` | same | same | same | Airtime R60 |
| 3 | `taxi_fare_rise`, `impulse_purchase` | same | same | same | Rent R500 (+ day bundles) |
| 4 | `family_obligation`, `group_chat_contribution` | same as `food` | `family_obligation`, `transport_decision` | `emergency_expense`, `group_chat_contribution` | (pay-later 1, fares) |
| 5 | `emergency_expense`, `mashonisa_offer`, `kota_run` | `emergency_expense`, `mashonisa_offer` | same as `transport` | `transport_decision`, `mashonisa_offer` | (fares) |
| 6 | `stokvel_decision`, `windfall` | same | same | same | (pay-later 2, fares) |
| 7 | `debit_order_check` | same | same | same | (Bra K repayment, fares) → chapter end |

Why these orders: the focus's own scenario comes up on Day 1 (lunch, the trip across town, the group-chat
birthday, the call from home); every day's gate on Days 1–3, and on Day 4 or 5 where the focus moved a
need there, is a need whose free option costs energy or leaves something for later (so A1 bites); and the
fixed anchors stay put: data on Day 2 (before the airtime night), the fare rise on Day 3 (so the commute
runs Days 4–7), Bra K on Day 5 (repaid on the night of Day 7), the stokvel and the neighbour on Day 6, the
debit order on Day 7. The family call-back (§3.4.7b) is added two days after the choice that sets it off.
The food week (Revision 4, F1) has a second food moment, `kota_run` on Day 5 at the kota shop (§3.4.14), so
the week that starts with lunch comes back to food mid-week, after the geyser and Bra K; it never gates
(the geyser is Day 5's gate) and no other focus sees it.

**Morning lines** (Passing, on `DayStarted`; keyed by the day's scenario list, so a line is shared by
every focus that has that list):

| Day's list | Morning line |
|---|---|
| food, transport | "Day 1, {name}. Lunch first, then the shift. A trip across town too." |
| transport, food | "Day 1, {name}. A trip across town first, then the shift, and lunch." |
| food, group chat | "Day 1, {name}. Lunch first, then the shift. The group chat's busy." |
| food, family | "Day 1, {name}. Lunch first, then the shift. Your phone will ring." |
| data, speaker | "Day 2. Your phone's out of data, and the phone shop has a deal on." |
| fare rise, hoodie | "Day 3. Getting to town costs more from today, and that hoodie's still there." |
| family, group chat | "Day 4. Your phone's going to ring today, and the group chat is busy." |
| family, transport | "Day 4. Your phone's going to ring today, and there's a trip across town." |
| geyser, group chat | "Day 4. Something's not right at home, and the group chat is busy." |
| geyser, Bra K | "Day 5. Something's not right at home this morning." |
| geyser, Bra K, kota run (food) | "Day 5. Something's not right at home, and your friends want a kota run." |
| transport, Bra K | "Day 5. There's a trip across town, and Bra K wants a word." |
| stokvel, neighbour | "Day 6. The stokvel meets at the shops today, and your neighbour's looking for you." |
| debit order | "Day 7. Last day before payday." |

**Reveal teasers** (shown the night before; keyed by the next day's list): data/speaker "Tomorrow: no
data, and a deal at the phone shop." · fare/hoodie "Tomorrow: town costs more, and that hoodie again." ·
family/group "Tomorrow: a call from home, and a birthday." · family/transport "Tomorrow: a
call from home, and a trip across town." · geyser/group "Tomorrow: something at home, and the group
chat." · geyser/Bra K "Tomorrow: something at home needs fixing." · geyser/Bra K/kota run "Tomorrow: something at
home, and a kota run." · transport/Bra K "Tomorrow: a trip
across town, and Bra K wants a word." · stokvel/neighbour "Tomorrow: the stokvel, and your neighbour."
· debit order "Tomorrow: the last day before payday." · after Day 7 "Tomorrow is payday." ·
a follow-up due tomorrow (either call-back, §3.4.7b): "Tomorrow: your aunt calls back." (§5.4.8 Coming up).

Fit rules (tested in `CopyTests` with `AileronMetrics`, for every focus): every morning line, with a
16-character name, fits 2 lines of Body 40 SemiBold in the compact box's 960 u text area; Days 2–7 are
tested with the stretched prefix "You seem stretched. " (§4.2.3), Day 1 lines without it (the prefix can
never show on Day 1's morning: `StartChapter` sets stress to 25 and nothing changes it before
`DayStarted`), and the test name is "Mmmmmmmmmmmmmmmm" (the widest 16 characters). Tightest measured: Day 2
with the prefix 958 + 625 u; stokvel 948 + 927 u; Day 1 transport-first 929 + 640 u. Every teaser fits one line of `Label` 36
**Bold** (the Coming-up lines use the `Label` role, §5.1) in 940 u (longest: transport/Bra K 913 u,
family/transport 871 u, geyser/group 871 u, fare/hoodie 870 u).

`grocery_stokvel` and anything needing a stokvel pot or conditional fees is Chapter 2 (D15).

```csharp
// Assets/MaliGo/Scenarios/ChapterSchedule.cs  (WP3, pure). Profile values and follow-ups come in as plain
// arguments (callers pass data.spendingProfile.focus / .travel and data.followUps), so WP3 needs no WP1 type.
namespace MaliGo.Scenarios
{
    public static class ChapterSchedule
    {
        public static IReadOnlyList<string> ScenariosForDay(int day, string focus);   // the table above
        public static int ScheduledDay(string scenarioId, string focus);              // 0 if unknown or a follow-up
        public static string GateScenario(int day, string focus);                     // ScenariosForDay(day, focus)[0];
                                                                                      // null outside Days 1-7
        public static string FollowUpKey(string scenarioId, int day);                 // "family_callback@6"
        public static bool TryParseFollowUp(string key, out string scenarioId, out int day);
        /// Active = (scheduled on a day <= data.currentDay in this focus's schedule, or every scheduled scenario
        /// when useSchedule is false) or (a followUps key whose day <= currentDay), and not completed.
        /// Order: today's gate (GateScenario(currentDay, focus)) first in its spot's queue, then by day, then
        /// position in the day; follow-ups after the scheduled scenarios of their day.
        public static List<string> ActiveScenarioIds(PlayerData data, bool useSchedule, string focus, string[] followUps);
        public static string ActiveScenarioAtSpot(PlayerData data, string spotId, bool useSchedule, string focus,
                                                  string[] followUps);                // spot queue, §3.3
        public static string MorningLine(int day, string focus);                      // tokens unfilled
        public static string TeaserForNight(int endedDay, string focus);              // teaser for endedDay + 1;
                                                                                      // "Tomorrow is payday." after 7
        public static string FollowUpTeaser(string scenarioId);                       // "Tomorrow: your aunt calls back."
                                                                                      // (family_callback and
                                                                                      // family_callback_full)
        public static string SpotFor(string scenarioId);                              // §3.3
        public static string SpotPlaceName(string spotId, string focus, string travel);   // "the kota shop", §3.3
        public static string SpotPlaceLabel(string spotId, string focus, string travel);  // "Kota shop", §3.3
        public static string[] WeekPlaces(string focus, string travel);   // labels of the first three distinct spots
                                                                          // in schedule order (CC screen 3, §4.6)
    }
}
```
Unknown `focus`/`travel` values are treated as `food`/`taxi` (the same rule as `SpendingFocus.Normalize`,
written out in WP3 so WP3 does not depend on WP1).

### 3.2 Economy and energy (what energy does)

**Energy, stated plainly** (the design rule; it is not shown as a help text): every day starts with 100
energy. A shift at work needs 60 and uses 60, and **it opens only once the day's first scenario is
resolved** (A1). Walking, waiting and skipping meals use energy; each choice card shows how much. Energy
never goes below 0 and never carries over: sleeping resets it to 100. Energy still never blocks a
scenario choice (a choice that costs more energy than you have still works and leaves you at 0) and never
costs money (D8 unchanged). So the trade-off comes back every day: the day's first scenario has to be
dealt with before work, and on Days 1–3 its free option (45–50 energy) leaves less than the 60 a shift
needs, so it costs that day's R150. Mali names the shift and its energy cost before Day 1 starts
(character creation, §1.1 0:40), the Day 1 morning line says "Lunch first, then the shift", the Work
prompt says what it is waiting for (*"Shift opens after lunch"*), and the energy column is on every card,
so the trade-off is never a hidden trap. After the shift the free options cost only energy; their other
costs are in the Later column (§3.4).

**Fair choices (A2).** Inside every scenario no option is at least as good as every other option on all
five things its card shows: money over the week (Cash + Savings + Later), cash kept today, savings kept
today, energy, and Later entries (payments, or a follow-up). The one exception is declining a want (the
speaker, the hoodie, the dinner, a second ask from family, the loan, the stokvel, the gym): what that
costs is going without it, which the card's label says. XP stays 5 on every choice and no card is marked.
Checked by `tools/sim_chapter.py` and by `ContentTests` (WP3) for every travel mode. The decline ids are:
`credit_bnpl/leave_it`, `impulse_purchase/walk_away`, `impulse_purchase/to_savings`,
`family_callback/not_this_week`, `family_callback_full/not_this_week`, `group_chat_contribution/not_this_time`, `mashonisa_offer/not_today`,
`stokvel_decision/not_for_now`, `debit_order_check/cancel_gym`.

**Reference play styles** (simulated with the content below by `tools/sim_chapter.py`; each day the
style resolves the day's first scenario, then works if it can, then plays the rest):

| Style | Choices |
|---|---|
| Saver | the free option every time, even before the shift: skip lunch, walk, free internet, leave the speaker, hoodie money to savings, walk today (on foot: keep walking), can't this week (then not this week either when she calls back asking R200), not this time, geyser from savings, no loan, cook at home (food week), join the stokvel, all to savings, cancel the gym |
| Always works | the saver's choices, except that the day's first choice never leaves less than 60 energy: there it takes the option that costs least over the week among those that keep the shift (vetkoek, day bundles, the lift club, the usual way across town) |
| Middle | vetkoek, the usual way across town, day bundles, pay-later, walk away, the new fare, R80 to family (then R120 when she calls back), gift only, geyser from savings, no loan, half a kota (food week), join, half and half, leave the debit order |
| Never works | the middle choices, never takes a shift |
| Comfort | kota, a ride, 1GB, speaker in full, hoodie, lift club, R200 to family\*, dinner\*, geyser from cash\*, no loan, the full kota (food week), join, keep the R300 as cash, move R199 to cash\* |
| Comfort + loan | as comfort, but borrow R400 on Day 5 |

\* when unaffordable the style takes the next affordable option in its own list, then the first
affordable one in authored order. The script's own fallback lists are: saver `impulse_purchase` to_savings →
walk_away; middle `family_callback` send_rest → from_savings and `emergency_expense` from_savings →
from_cash (never works uses the middle lists); every other style and scenario lists one option. With
today's content they give the same results as "first affordable in authored order".

**Default profile** (`food` + `taxi`, the generic chapter). Start R1 000 (cash R600, savings R400):

| Style | End cash | End savings | End total | Week | Lowest cash | Nights still owing | Shifts | Energy before each day's shift (Days 1–7) | Still owed / promised at payday |
|---|---|---|---|---|---|---|---|---|---|
| Saver | R384 | R470 | R854 | −R146 | R0 | 3 | 4 | 55, 55, 50 (no shift), then 100 | R0 / R200 |
| Always works | R785 | R470 | R1 255 | +R255 | R200 | — | 7 | 100 every day | R0 / R200 |
| Middle | R315 | R200 | R515 | −R485 | R97 | — | 7 | 100, 100, 95, then 100 | R0 / R399 |
| Never works | R0 | R350 | R350 | −R650 | R0 | 3, 4, 5, 6, 7 | 0 | — | R455 / R749 |
| Comfort | R549 | R1 | R550 | −R450 | R0 | 3, 4, 5 | 7 | 100, except 90 on Day 5 | R0 / R749 |
| Comfort + loan | R349 | R1 | R350 | −R650 | R0 | 3, 4 | 7 | 100, except 90 on Day 5 | R0 / R749 |

Revision 4 changed two kinds of rows: every `food` row (the kota run on Day 5: middle −R30, comfort and comfort +
loan −R65, the saver and "always works" cook at home) and every `walk` row's saver (+R136: the saver keeps
walking from Day 3 instead of paying R34 a night on Days 4–7; it still loses Day 3's shift). Everything else
is unchanged.

Read the totals with the last column: comfort ends above middle in cash + savings only because its geyser
fell to "cold showers" (R350 promised for payday) and it was short R335 on rent from night 3 to night 5.
The comfort path is tight on purpose: no cash on the night of Day 3, rent in arrears for three nights,
the geyser pushed to payday, and R749 already promised before pay lands.

**Every profile** (end totals in Rand, cash + savings on the night of Day 7; `EconomySimTests` locks this
table, WP9):

| Profile (focus / travel) | Saver | Middle | Comfort | Comfort + loan | Never works | Always works |
|---|---|---|---|---|---|---|
| food / taxi (default) | 854 | 515 | 550 | 350 | 350 | 1 255 |
| food / ehailing | 790 | 421 | 550 | 350 | 350 | 1 255 |
| food / walk | 990 | 515 | 550 | 350 | 350 | 1 255 |
| food / car | 830 | 479 | 550 | 350 | 350 | 1 255 |
| transport / taxi | 854 | 545 | 615 | 415 | 350 | 1 245 |
| transport / ehailing | 790 | 451 | 615 | 415 | 350 | 1 231 |
| transport / walk | 990 | 545 | 615 | 415 | 350 | 1 245 |
| transport / car | 830 | 509 | 615 | 415 | 350 | 1 239 |
| data_social / taxi | 854 | 545 | 615 | 415 | 350 | 1 255 |
| data_social / ehailing | 790 | 451 | 615 | 415 | 350 | 1 255 |
| data_social / walk | 990 | 545 | 615 | 415 | 350 | 1 255 |
| data_social / car | 830 | 509 | 615 | 415 | 350 | 1 255 |
| home_family / taxi | 704 | 425 | 205 | 50 | 200 | 1 225 |
| home_family / ehailing | 640 | 345 | 205 | 50 | 200 | 1 211 |
| home_family / walk | 840 | 425 | 205 | 50 | 200 | 1 225 |
| home_family / car | 680 | 395 | 205 | 50 | 200 | 1 219 |

What the simulation shows for all 16 profiles (rerun `python tools/sim_chapter.py`; exit 0 = all hold;
`--trace <style> <focus> <travel>` prints every money event of one play-through):
- **No stuck path.** Every scenario always has an option that costs no cash and no savings (always-
  available rule, §3.4), and 3 000 random play-throughs per profile (random choices, random shift timing,
  some "Not now") never meet a scenario with nothing affordable and never break the invariant.
- **No dominant style.** "Always works" has the best end total in every profile, but it is never best on
  everything: it goes without six wants, it leaves Gogo's call-back unanswered (a follow-up), and in 12
  of 16 profiles it also uses more choice energy (energy spent on choices only, not on shifts) than the
  middle path. Every other style is beaten on money.
- **The free option before work costs the shift.** The saver loses the shift on Days 1–3 (energy 55, 55,
  50 at shift time), is short on rent on the night of Day 3, and ends R401 below "always works", which
  pays R20 for lunch, R45 for day bundles and R120 for the lift club to keep its shifts.
- **Money is tight on the comfort path and never trivially easy.** Comfort ends below R1 000 in every
  profile; comfort + loan has a night with something still owed in every profile; the home-and-family
  week (R200 to family on Day 1, two days before rent) is the hardest (comfort R205, comfort + loan R50
  with R45 still owed); no style ends more than R255 up. The highest reachable end total is R1 605 (all
  seven shifts, every free option after the shift, cold showers), with R550 already promised for payday;
  random play-throughs ended at most R1 510 (Revision 4; R1 425 before).
- **Hidden stress.** The reference styles reach the stretched tone (60) only through arrears or lost
  shifts. A player who keeps every shift and pays every bill can still reach it, up to 77, by stacking
  deferrals (§0); that is intended, since putting things off piles up worry. The script prints the
  highest hidden stress over every path it plays (styles, a stacked-deferrals path and the random runs).

### 3.3 World positions (measured from `MaliGoWorld.unity`)

Measured world positions (x, z) of scene objects: `PlayerSpawnPoint` (2.0, −1.3), `Player_House`
(2.0, −2.2), `Local_Commercial_Hub` (0, 0) (an empty container), `Local_Bank_Building` (−2.0, 4.4),
`Road_T_Intersection` (0, 0), `Road_Crossing` (−2.0, 0), `Road_Connecting_2` (0, 2.0),
`Road_Connecting_End` (0, 4.0), `Road_Main_3` (3.0, 0), `Road_Main_4` (4.0, 0). The walkable world is
about 17 × 13 units; the player walks 4.5 u/s, so any two places are under 3 s apart.

Today the four hub triggers resolve to the origin (because `Local_Commercial_Hub` is at (0,0,0)) and sit
0.3–0.6 apart; the house triggers sit 0.4–0.7 apart. Both are replaced.

**Locations (unchanged positions):** Home (2.0, −1.2) = `Player_House` + (0, 0, 1.0); Bank (−1.7, 4.7) =
`Local_Bank_Building` + (0.3, 0, 0.3); Work (4.0, 0.3) = `Road_Main_4` + (0, 0, 0.3) (reachable from the
west side of the parked van).

**Scenario spots** (one trigger object per spot; `ScenarioSpot_<ID>`; anchor + offset, fallback = the
absolute position):

| Spot id | Anchor + offset | World (x, z) | Scenarios (default `food` queue order) |
|---|---|---|---|
| `CORNER` | `Road_T_Intersection` + (0.6, 0, 1.2) | (0.6, 1.2) | food_decision, data_runs_out, group_chat_contribution, kota_run (food week only) (today's gate always first, see below) |
| `TAXI` | `Road_Crossing` + (0.2, 0, −0.5) | (−1.8, −0.5) | transport_decision, taxi_fare_rise |
| `HUB` | `Road_Connecting_End` + (0, 0, −0.4) | (0.0, 3.6) | credit_bnpl, stokvel_decision |
| `SHOPFRONT` | `Road_Connecting_2` + (−1.5, 0, 1.1) | (−1.5, 3.1) | impulse_purchase |
| `GATE` | `Player_House` + (−1.2, 0, 1.9) | (0.8, −0.3) | family_obligation, family_callback / family_callback_full (follow-ups), emergency_expense, debit_order_check |
| `EAST` | `Road_Main_3` + (−0.4, 0, 0.9) | (2.6, 0.9) | mashonisa_offer, windfall |

**Place names follow the profile (A3).** `SpotPlaceName` (Mali's greeting, prompts) / `SpotPlaceLabel`
(choice-sheet eyebrow, CC summary). Generic names only, never a real brand:

| Spot | `food` | `transport` | `data_social` | `home_family` |
|---|---|---|---|---|
| `CORNER` | the kota shop / Kota shop | the corner shop / Corner shop | the corner shop / Corner shop | the spaza shop / Spaza shop |
| `SHOPFRONT` | the shop window up the road / Shop window | same | the clothing shop / Clothing shop | the shop window up the road / Shop window |
| `HUB` | the shops by the bank / Shops by the bank (every focus) | | | |
| `GATE` | home / Home (every focus) | | | |
| `EAST` | down the road / Down the road (every focus) | | | |

| Spot | `taxi` | `ehailing` | `walk` | `car` |
|---|---|---|---|---|
| `TAXI` | the taxi rank / Taxi rank | the pick-up point / Pick-up point | the taxi rank / Taxi rank | the petrol station / Petrol station |

`WeekPlaces(focus, travel)` = the labels of the first three distinct spots in that focus's schedule order:
`food` Kota shop · Taxi rank · Shops by the bank; `transport` Taxi rank · Corner shop · Shops by the bank;
`data_social` Corner shop · Shops by the bank · Taxi rank; `home_family` Spaza shop · Home · Taxi rank (the
transport spot's label follows the travel mode). Revision 4 (F2): the transport spot is always one of the
three; when it is not among the first three distinct spots it replaces the third, so the line reacts to the
travel tap for every focus (before, `home_family` showed "Shops by the bank" whatever the travel tap).
`SpotWhere(spot, focus, travel)` (new) is the greeting's form: "at " + the place name, except `EAST`
("down the road").

Closest pairs among the nine fixed interactables: Home–GATE 1.50 (the minimum), CORNER–GATE 1.51,
Work–EAST 1.52, HUB–SHOPFRONT 1.58; every pair ≥ 1.4 = 2 × radius 0.7. The player spawns 0.1 from Home,
so the Home prompt shows on the first frame (intended, §1.1). **Spot queue rule:** a spot shows only the
first active scenario in its queue order (the order of `ActiveScenarioIds`: **today's gate
(`ChapterSchedule.GateScenario(currentDay, focus)`) is first in its spot's queue**, then schedule day, then
position, then follow-ups after the scheduled scenarios of their day, so the queue follows the player's
focus); the next one appears when that is completed. Gate first, because the Work prompt names the gate
("Shift opens after …") and a carry-over at the same spot must not hide it (e.g. `food_decision` carried
to Day 2 waits behind `data_runs_out` at CORNER; a Day 1 `transport_decision` waits behind Day 3's
`taxi_fare_rise` at TAXI). So no two
scenario prompts ever share a place, and the arbiter (§7.1) shows one prompt at a time anyway. WP5
places the spots by these numbers; a person then walks to each spot in the Editor (human task H2, §10)
and, if a collider blocks one, moves it ≤ 0.3 along the road and records the new offset here.

### 3.4 Every scenario (final content)

Field rules (all scenarios): `financialXpDelta = 5` on every choice; `description` on choices is empty
(retired, D14); `requiredLifeChapters` empty; `instalmentIntervalDays` default 2. Reactions are shown
after the choice is applied, so `{cash}`/`{savings}` show the new values. Columns in the choice panel are
generated from the deltas (§5.4.5). "Stress" is hidden (D7). **Name rhythm:** `{name}` appears in at
most one reaction per scenario (the chip row already carries the amount), so the player's name is not
repeated on every line. Choice labels must fit 2 lines of ChoiceLabel 44 Bold in the card's label box
(373 u with a Savings column, 493 u without; tested with `AileronMetrics`, §8 WP6).

New fields used below: on `ScenarioDefinition` — `spotId`, `promptText`, `promptIcon`, `moneyCategory`,
`placeLabel` (eyebrow caption), `gateNoun` (what the Work prompt waits for when this scenario is the day's
first, e.g. "lunch"; §4.8), `isFollowUp` (true = never scheduled, only set off by a choice); on
`ScenarioChoice` — `ledgerLabel`, `instalmentFirstDueDay`, `instalmentShortLabel`, `instalmentCategory`,
`instalmentKind`, `instalmentLastDueDay` (0 = none; otherwise payments that would fall after this day are
dropped, so "the rest of the week" never runs past Day 7), `maliReactionNoLater` (used instead of the
reaction when `instalmentLastDueDay` leaves no payment, e.g. the fare chosen on Day 7), `followUpScenarioId`,
`followUpAfterDays`, `followUpLaterText` (the Later column's first line for a follow-up, e.g. "Call back").
`description` on the definition is the **situation text**; `introDialogue` is Mali's short line shown
inside the choice panel. In prompt and place strings, `[place]`/`[Place]` are filled by `ScenarioLibrary`
from `SpotPlaceName`/`SpotPlaceLabel` (§3.3), and `[trip]`/`[daily]` from §3.4.0, when it builds the
definition for a profile; `{...}` tokens are filled at runtime by `MaliText.Fill` (§4.1).

`ScenarioLibrary.Get(string id, string focus, string travel)` (WP3) returns the definition for that
profile (place names, travel amounts, travel labels and the authored order of the transport options);
`GetById(id)` = `Get(id, "food", "taxi")`. The choice ids are the same for every profile.

Format of each choice row: **id** · label · ledgerLabel · cash / savings / energy / stress · tag ·
instalments · Mali reaction.

#### 3.4.0 Travel modes (A3): the amounts that follow the profile

| Travel | Trip across town, the usual way (`transport_decision/usual`) | Daily commute after the fare rise (`taxi_fare_rise/pay_new_fare`) | Option order in `transport_decision` |
|---|---|---|---|
| `taxi` (Minibus taxi) | "Minibus taxi (R15 each way)", −R30, energy −5, ledger "Taxi there and back" | R17 each way: "Pay the new fare (R34 a day)", −R34 today, energy −5, then R34 a night to Day 7; obligation "Taxi fares" / short "Taxi"; ledger "Taxi at the new fare" | usual, walk, ride |
| `ehailing` (E-hailing rides) | "Shared ride both ways (R44)", −R44, energy 0, ledger "Shared ride there and back" | R25 each way: "Pay the new price (R50 a day)", −R50, energy 0, then R50 a night; "Shared rides" / "Rides"; ledger "Shared ride at the new price" | usual, walk, ride |
| `walk` (Mostly on foot) | as `taxi` | as `taxi`; the free option is "Keep walking, no fares" (Revision 4, §3.4.6) | walk, usual, ride |
| `car` (Own or shared car) | "Petrol for the car (R36)", −R36, energy 0, ledger "Petrol for the trip" | "Pay for petrol (R40 a day)", −R40, energy 0, then R40 a night; "Petrol" / "Petrol"; ledger "Petrol for the day" | usual, walk, ride |

Bounds (every profile, checked by `tools/sim_chapter.py` and `ContentTests`): trip R30–R50, daily
R34–R50; the lift club (R120), the private ride (R90) and the walking energy (45 for the trip, 50 for
"walk today") are the same for every mode. Inside these bounds every profile meets every check in §3.2.

#### 3.4.1 `food_decision` — Day 1 · spot CORNER · icon `shoppingBasket` · category Food · gate noun "lunch"
- Title: **What's for lunch?** · Place: "[Place]" · Prompt: "Lunch at [place]"
- Situation: "It's midday and you're hungry. There's a kota special, vetkoek and mince, or you could push through till supper."
- Mali intro: "Midday already, {name}. What are you having?"

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `kota` | Kota and a cold drink (R50) | Kota and a cold drink | −50 | 0 | 0 | −5 | Discretionary | "R50 for lunch. You're full till supper." |
| `vetkoek` | Vetkoek and mince (R20) | Vetkoek and mince | −20 | 0 | 0 | −2 | Frugal | "R20 for lunch. That keeps you going till supper." |
| `skip_lunch` | Skip lunch | — | 0 | 0 | −45 | +5 | Frugal | "No money out. Skipping lunch took 45 energy, and a shift needs 60." |

#### 3.4.2 `transport_decision` — Day 1 (`food`, `transport`), Day 4 (`data_social`), Day 5 (`home_family`) · TAXI · `car` · Transport · gate noun "the trip across town"
- Title: **Getting across town** · Place: "[Place]" · Prompt: "A trip across town"
- Situation (`taxi`, `walk`): "You've got an interview on the other side of town today, off your usual route. The minibus taxi is R15 each way. You could walk it, or book a ride." · (`ehailing`): "You've got an interview on the other side of town today, off your usual route. A shared ride is R22 each way, a ride of your own R45. Or you could walk it." · (`car`): "You've got an interview on the other side of town today, off your usual route. Petrol for the trip is about R36. You could walk it, or book a ride." (147 / 155 / 147 characters, 5 lines each of Body 40 in 613 u.) The interview is off the course route, so it is a separate trip from the daily commute of §3.4.6 even when it comes after the fare rise (`data_social` Day 4, `home_family` Day 5).
- Mali intro: "How are we getting there, {name}?"

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `usual` | per §3.4.0 | per §3.4.0 | −[trip] | 0 | per §3.4.0 | 0 | Neutral | `taxi`/`walk`: "R30 for the taxi, {name}, there and back." · `ehailing`: "R44 for the shared rides, {name}, there and back." · `car`: "R36 for petrol, {name}, there and back." |
| `walk` | Walk there and back | — | 0 | 0 | −45 | 0 | Frugal | "No fare today. The walk took 45 energy." |
| `ride` | Book a ride both ways (R90) | Ride there and back | −90 | 0 | 0 | −2 | Discretionary | "R90 for the rides. Door to door, no waiting." |

The Revision 2 ids `minibus_taxi` and `e_hailing` become `usual` and `ride`. Order per §3.4.0.

#### 3.4.3 `data_runs_out` (new) — Day 2 · CORNER · `phone` · Phone & data · gate noun "sorting your data" ("Shift opens after sorting your data", 640 u)
- Title: **Out of data** · Place: "Your phone" · Prompt: "Your phone's out of data"
- Situation: "Your data ran out this morning. You want to send a CV today and check for replies, and your next bundle only comes on payday."
- Mali intro: "Your data's gone, {name}, and the CV is still sitting on your phone."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `bundle_1gb` | Buy 1GB to last till payday (R85) | 1GB data bundle | −85 | 0 | 0 | 0 | Neutral | — | "R85 and you're online till payday. The CV can go." |
| `day_bundle` | Buy day bundles (R15 a day) | Day data bundle | −15 | 0 | 0 | +2 | Deferred | 2 × R15, interval 1, firstDue 0 (= tomorrow), lastDue 7, label "Day bundles", short "Data", category Phone & data, kind repeat | "R15 covers today, {name}. Day bundles carry on: R15 on {laterDays}." · no later: "R15 covers today, {name}, and that's the last day before payday." |
| `free_wifi` | Use the free internet at the mall | — | 0 | 0 | −45 | +3 | Frugal | — | "No money out. The walk to the mall and the wait took 45 energy." |

The Day-2 bill is now "Airtime" (calls and messages, R60, §0), not the data bundle, so buying data on
Day 2 no longer clashes with a renewal that night. The three options now trade off (A2): 1GB costs most
today and nothing later; day bundles cost least today and R30 more over the next two nights (Later
"R15 × 2" / "Days 3, 4"); the free internet costs no money and 45 energy, so as Day 2's first scenario it costs
the shift.

#### 3.4.4 `credit_bnpl` — Day 2 · HUB · `shoppingBasket` · Shopping · gate noun "the speaker"
- Title: **Pay now or pay later?** · Place: "Phone shop" · Prompt: "A deal at the phone shop"
- Situation: "The phone shop has a wireless speaker you've wanted for ages: R360. Pay it all now, or take it home today for R120 and pay R130 twice over the next few days."
- Mali intro: "Same speaker, {name}. Two ways to pay for it."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `pay_in_full` | Pay R360 now | Full price for the speaker | −360 | 0 | 0 | 0 | Neutral | — | "R360 today, and the speaker's paid off." |
| `pay_later` | Pay later: R120 now, then R130 twice | Speaker deposit | −120 | 0 | 0 | +3 | Deferred | count 2 × R130, interval 2, firstDue 0 (= today + 2), label "Speaker (pay-later)", short "Speaker", category Pay-later, kind instalment | "It's yours today, {name}. R130 comes off in two days, and again after that: R20 more in all." |
| `leave_it` | Leave it for now | — | 0 | 0 | 0 | 0 | Frugal | — | "Nothing spent. The speaker stays in the shop." |

Chosen on Day 2 → charged on the nights of Day 4 and Day 6 (both inside the chapter). Chosen later as a
carry-over, the second instalment may fall after Day 7; the chapter end then lists it under "Already
promised for payday" (`ObligationLedger.Promised`: every non-recurring payment due on or after Day 8). Two
instalments (not three) so that the Day-2 schedule lands both inside the week (D6). Pay-later now costs
R20 more than paying in full (A2): it keeps R240 more in your pocket today, and the card shows the price
of that ("R130 × 2" in Later against −R360 now).

#### 3.4.5 `impulse_purchase` — Day 3 · SHOPFRONT · `shoppingBasket` · Shopping · gate noun "the shop window"
- Title: **Something caught your eye** · Place: "[Place]" · Prompt: "[Place]" ("Shop window", or "Clothing shop" for `data_social`)
- Situation: "There's a hoodie in the window, R120 on special for a few more days. You don't need it, but you've been looking at it for weeks."
- Mali intro: "That's the one you keep looking at, isn't it, {name}?"

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `buy_it` | Buy it (R120) | Hoodie on special | −120 | 0 | 0 | −2 | Discretionary | "It's yours. That's R120 of today." |
| `walk_away` | Walk away for now | — | 0 | 0 | 0 | 0 | Frugal | "Nothing spent, {name}. It's on special for a few more days if you still want it." |
| `to_savings` | Move R120 to savings instead | Hoodie money to savings | −120 | +120 | 0 | 0 | Frugal | "R120 moved to savings. Your {goalName} is at R{savings} of R{goalTarget}." |

`to_savings` replaces `redirect_to_savings` and **moves** R120 cash → savings (the old choice created R120 from nothing, `ScenarioLibrary.cs:187-190`).

#### 3.4.6 `taxi_fare_rise` (new) — Day 3 · TAXI · `car` · Transport · gate noun "the trip to town"
- Title (`taxi`, `walk`): **The fare went up** · (`ehailing`): **Ride prices went up** · (`car`): **Petrol went up**
- Place: "[Place]" · Prompt: "News at [place]"
- Situation (`taxi`, `walk`): "The rank marshal says the fare is R17 each way from today. You're going to town and back every day till payday for a short course." · (`ehailing`): "Ride prices went up with petrol: a shared ride to town is R25 each way from today. You're going to town and back every day till payday for a short course." · (`car`): "Petrol went up again: your share is R40 a day from today. You're going to town and back every day till payday for a short course."
- Mali intro (`taxi`, `walk`): "R2 more each way, {name}, and it's every day till payday." · (`ehailing`): "R3 more each way, {name}, and it's every day till payday." · (`car`): "Petrol's up again, {name}, and it's every day till payday."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `pay_new_fare` | per §3.4.0 | per §3.4.0 | −[daily] | 0 | per §3.4.0 | 0 | Neutral | 4 × [daily], interval 1, firstDue 0 (= tomorrow), lastDue 7, label and short per §3.4.0, category Transport, kind repeat | `taxi`/`walk`: "R34 today, {name}. The fare comes off again on {laterDays}." · `ehailing`: "R50 today, {name}. The new price comes off again on {laterDays}." · `car`: "R40 today, {name}. Petrol comes off again on {laterDays}." · no later: "R[daily] today, {name}. That's the last trip before payday." |
| `lift_club` | Join a lift club (R120 till payday) | Lift club till payday | −120 | 0 | 0 | −3 | Neutral | — | "R120 for the lift club. Your trips to the course are covered till payday." |
| `walk_today` | `taxi`: Walk today, taxi from tomorrow · `ehailing`: Walk today, ride from tomorrow · `car`: Walk today, drive from tomorrow · `walk`: Keep walking, no fares (no instalments; reaction "No fares to pay. Walking to town and back took 50 energy today.") | — | 0 | 0 | −50 | +2 | Frugal | same as `pay_new_fare` | "Nothing spent today. That walk took 50 energy, and it's R[daily] a day again from tomorrow." · no later: "Nothing spent today. That walk took 50 energy." |

Transport is now a cost that comes back (A2, closes E7): from Day 3 the player is in town every day till
payday, so each option is a plan for the rest of the week. Chosen on Day 3 (`taxi`): the new fare is R34
today + R34 × 4 (Days 4–7) = R170; the lift club R120 once; walking today R0 now, 50 energy (it costs
Day 3's shift, since this is Day 3's first scenario) and R34 × 4 later = R136. The lift club wins over
the week but costs the most today; the fare costs least today but most over the week; the walk saves
R34 and costs the shift. A carry-over shortens the series (chosen on Day 5: Days 6–7), and on Day 7
there is nothing later. `walk_to_main_road` and `walk_all_the_way` of Revision 2 are replaced by
`walk_today`; the lift club now does cover the trips it promises.

**Walkers (Revision 4, F2).** For travel mode `walk` the third option keeps its id `walk_today` (choice ids
stay the same for every profile) but is "Keep walking, no fares": 0 cash, 50 energy, +2 stress, Frugal, no
payments later. Its cost is the energy on the card: as Day 3's first scenario it leaves 50, so it costs
that day's R150 shift (the same trade as every other gate). The game has no energy cost on later days, so
the card shows only today's 50 and Mali's line says "today". Fair choice holds: it costs least money but the
most energy; the fare and the lift club keep the shift.

#### 3.4.7 `family_obligation` — Day 4 (Day 1 for `home_family`) · GATE · `token_give` · Family · gate noun "the call from home"
- Title: **A call from home** · Place: "At home" · Prompt: "Your phone's ringing"
- Situation: "Your aunt calls. Gogo's chronic medication has run out and the clinic is out of stock. The pharmacy wants R200 to tide her over."
- Mali intro: "It's your aunt, {name}. It's about Gogo."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Follow-up | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `send_full` | Send R200 | Gogo's meds | −200 | 0 | 0 | 0 | Neutral | — | "R200 is on its way to Gogo. That's R200 of your week." |
| `send_part` | Send R80 for now | Towards Gogo's meds | −80 | 0 | 0 | +3 | Neutral | `family_callback` after 2 days, Later "Call back" | "R80 is on its way to Gogo, {name}. Your aunt will call back about the rest." |
| `from_savings` | Send R200 from savings | Gogo's meds from savings | 0 | −200 | 0 | 0 | Neutral | — | "R200 from savings, and Gogo has her meds. Savings is at R{savings} now." |
| `cant_this_week` | Explain you can't this week | — | 0 | 0 | 0 | +8 | Neutral | `family_callback_full` after 2 days, Later "Call back" | "That's a hard call to make. Your aunt says she'll try you again in two days." |

A follow-up is only added if its day is ≤ 7 (chosen on Day 6 or 7, nothing comes back inside the chapter
and the Later column shows "—"). Neither line asks the player to send more; the call-back simply happens.

#### 3.4.7b `family_callback` and `family_callback_full` (new, follow-ups only) · GATE · `token_give` · Family · `isFollowUp = true`

Two follow-ups, so that the call-back asks for what is still missing: `family_callback` comes 2 days after
`send_part` (R80 sent, she asks the other R120); `family_callback_full` comes 2 days after
`cant_this_week` (nothing sent, she asks the full R200). With one shared R120 call-back, "can't this week,
then R120" cost R120 and "R80 now, then R120" cost R200 for the same ending, so the card made
`cant_this_week` at least as good as `send_part` (A2).

**`family_callback`** — 2 days after `send_part`
- Title: **Your aunt calls back** · Place: "At home" · Prompt: "Your aunt's calling back"
- Situation: "Your aunt again. Gogo's meds are running low, and the clinic still has none. R120 would see her through to month-end."
- Mali intro: "It's your aunt again, {name}."
- Coming-up line the night before (§5.4.8): "Tomorrow: your aunt calls back."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `send_rest` | Send R120 | Rest of Gogo's meds | −120 | 0 | 0 | 0 | Neutral | "R120 is on its way, and Gogo's covered till month-end." |
| `from_savings` | Send R120 from savings | Gogo's meds from savings | 0 | −120 | 0 | 0 | Neutral | "R120 from savings, and Gogo's covered. Savings is at R{savings} now." |
| `not_this_week` | Not this week either | — | 0 | 0 | 0 | +6 | Neutral | "That's a hard one to say twice. Your week stays as it was." |

**`family_callback_full`** — 2 days after `cant_this_week`; the same title, place, prompt, Mali intro and
Coming-up line as `family_callback`
- Situation: "Your aunt again. Gogo's meds are running low, and the clinic still has none. R200 would see her through to month-end." (612 + 556 + 549 + 416 u, 4 lines in 613 u)

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `send_full` | Send R200 | Gogo's meds | −200 | 0 | 0 | 0 | Neutral | "R200 is on its way, and Gogo's covered till month-end." |
| `from_savings` | Send R200 from savings | Gogo's meds from savings | 0 | −200 | 0 | 0 | Neutral | "R200 from savings, and Gogo's covered. Savings is at R{savings} now." |
| `not_this_week` | Not this week either | — | 0 | 0 | 0 | +6 | Neutral | "That's a hard one to say twice. Your week stays as it was." |

No reference style pays after `cant_this_week` (the saver and "always works" answer "not this week
either"), so the §3.2 totals are unchanged.

Neither follow-up sets off anything further. The follow-up mechanic is data only: a choice names `followUpScenarioId`
and `followUpAfterDays`; `ScenarioOutcome.Apply` appends `ChapterSchedule.FollowUpKey(id, currentDay +
n)` to `PlayerData.followUps` when that day is ≤ 7; `ChapterSchedule.ActiveScenarioIds` makes it active
on that day at its spot (§3.3 queue). Owners: fields WP3, apply WP6, activation WP3, Later column WP6.

#### 3.4.8 `group_chat_contribution` (new) — Day 4 (Day 1 for `data_social`) · CORNER · `phone` · Friends · gate noun "the group chat"
- Title: **Birthday in the group chat** · Place: "Group chat" · Prompt: "The group chat is buzzing"
- Situation: "{friend}'s birthday dinner is on the weekend. The group chat says everyone's putting in R150 for the dinner and her gift."
- Mali intro: "{friend}'s birthday, {name}. The chat's asking who's in."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `in_for_dinner` | I'm in (R150) | Birthday dinner | −150 | 0 | 0 | −4 | Discretionary | "You're in. That's R150 of the week." |
| `gift_only` | Send R50 for the gift, skip the dinner | Birthday gift | −50 | 0 | 0 | +2 | Neutral | "R50 towards the gift, {name}. You'll miss the dinner, and R100 stays with you." |
| `not_this_time` | Not this time | — | 0 | 0 | 0 | +4 | Frugal | "That message is hard to send. Your week stays as it was." |

`{friend}` (§4.1) is "Thandi", or "Lerato" when the player's name starts with "Thandi", so the friend never
shares the player's name. Ledger labels carry no name.

#### 3.4.9 `emergency_expense` — Day 5 (Day 4 for `home_family`) · GATE · `home` · Home · gate noun "the geyser"
- Title: **The geyser broke** · Place: "At home" · Prompt: "Something's up at home"
- Situation: "The geyser element has burnt out: no hot water. Sipho from two doors down does plumbing on the side. He can fix it today for R350, parts included."
- Mali intro: "Cold water this morning, {name}. Sipho can fix it today."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `from_savings` | Pay from savings (R350) | Geyser repair from savings | 0 | −350 | 0 | 0 | Neutral | — | "R350 from savings, and there's hot water tonight. Savings is at R{savings} now." |
| `from_cash` | Pay from cash (R350) | Geyser repair | −350 | 0 | 0 | 0 | Neutral | — | "R350 from cash, and there's hot water tonight. You've got R{cash} in cash." |
| `cold_showers` | Cold showers until payday | — | 0 | 0 | −10 | +12 | Deferred | 1 × R350, firstDueDay 8, label "Geyser repair", short "Geyser", category Home, kind commitment | "No money out today. It's cold water for now, and the R350 repair is promised for payday." |

R350 (not R600) so the savings option is affordable from the R400 start (R600 could never be paid from
savings); a neighbour who does plumbing on the side makes R350 believable (a call-out plumber in SA costs
more). "Cold showers" defers the cost rather than erasing it: it shows as "R350 / on payday" in the Later
column and under "Already promised for payday" at the chapter end.

#### 3.4.10 `mashonisa_offer` (new) — Day 5 · EAST · `hand_token` · Loan · gate noun "Bra K"
- Title: **Till payday** · Place: "Down the road" · Prompt: "Bra K wants a word"
- Situation: "Bra K from down the road lends to anyone till payday, no forms. Whatever you take, you pay back half as much again in two days."
- Mali intro: "Bra K's offering a loan, {name}."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `borrow_400` | Borrow R400 (R600 back in two days) | Loan from Bra K | +400 | 0 | 0 | +6 | Deferred | 1 × R600, interval 2, label "Bra K loan repayment", short "Bra K", category Loan, kind loan | "R400 in your hand. In two days R600 goes back to Bra K, and R200 of that is what the loan costs." |
| `borrow_200` | Borrow R200 (R300 back in two days) | Loan from Bra K | +200 | 0 | 0 | +3 | Deferred | 1 × R300, same fields | "R200 now. In two days R300 goes back, and R100 of that is what the loan costs." |
| `not_today` | Not today | — | 0 | 0 | 0 | 0 | Neutral | — | "Nothing borrowed, and nothing owed to Bra K." |

Taken on Day 5 → repaid on the night of Day 7, before the chapter closes. Always offered (no cash gating, §9 G13).
A loan is money in but not income: the reveal and chapter end draw it in the neutral `MoneyTransfer`
colour with its repayment beside it (§5.4.8).

#### 3.4.11 `stokvel_decision` — Day 6 · HUB · `tokens_stack` · Bills · gate noun "the stokvel meeting"
- Title: **Joining a stokvel** · Place: "Shops by the bank" · Prompt: "Stokvel meeting at the shops"
- Situation: "Mam' Dlamini's stokvel is taking new members. Everyone puts in R200 each payday, and each month one member gets the whole pot. Two members sign for the account."
- Mali intro: "The stokvel's meeting today, {name}."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `join` | Join: R200 from payday | — | 0 | 0 | 0 | −2 | Frugal | 1 × R200, firstDueDay 8, label "Stokvel contribution", short "Stokvel", category Bills, kind commitment | "You're in, {name}. Your first R200 goes to the stokvel on payday." |
| `not_for_now` | Not this one, for now | — | 0 | 0 | 0 | 0 | Neutral | — | "Not this one. Mam' Dlamini says the door's open if you change your mind." |

No money moves in Chapter 1 (no stokvel pot, D4/D15); the commitment shows as "Already promised for
payday". The old lines framing stokvels as risky ("not worth the risk", "better to sit this one out than
regret it", "knowing who holds the money matters most") are gone.

#### 3.4.12 `windfall` — Day 6 · EAST · `coin` · Extra money · gate noun "your neighbour"
- Title: **Unexpected money** · Place: "Down the road" · Prompt: "Your neighbour's waving you over"
- Situation: "Your neighbour pays you R300 for helping her move furniture last weekend. You weren't counting on it."
- Mali intro: "Some money you weren't expecting, {name}."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `all_to_savings` | Put all R300 in savings | Neighbour's money to savings | 0 | +300 | 0 | 0 | Frugal | "R300 into savings. Your {goalName} is at R{savings} of R{goalTarget}." |
| `keep_cash` | Keep it as cash | From the neighbour | +300 | 0 | 0 | −5 | Discretionary | "R300 in your pocket, {name}. Yours to use." |
| `half_half` | R150 cash, R150 savings | From the neighbour | +150 | +150 | 0 | −2 | Neutral | "R150 each way. Some for now, some for your {goalName}." |

#### 3.4.13 `debit_order_check` (new, simplified) — Day 7 · GATE · `notepad` · Bills · gate noun "the gym SMS"
- Title: **It goes off on payday** · Place: "At home" · Prompt: "An SMS about your gym"
- Situation: "Your gym debit order of R199 goes off on payday. Pay sometimes lands later that day, and a bounced debit order costs a fee on top."
- Mali intro: "Tomorrow's payday, {name}, and the gym goes off too."

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Instalments | Mali reaction |
|---|---|---|---|---|---|---|---|---|---|
| `move_to_cash` | Move R199 to cash | Gym money from savings | +199 | −199 | 0 | −3 | Neutral | 1 × R199, firstDueDay 8, label "Gym debit order", short "Gym", category Bills, kind commitment | "R199 moved across. It'll clear on payday. Savings is at R{savings}." |
| `cancel_gym` | Cancel the gym, run in the park | — | 0 | 0 | 0 | 0 | Frugal | — | "Cancelled, {name}. From payday, that R199 stays with you." |
| `leave_it` | Leave it, hope pay comes first | — | 0 | 0 | 0 | +6 | Deferred | same as `move_to_cash` | "It goes off on payday either way. If pay lands after it, the bank adds a fee." |

No conditional fee is modelled (D15); it is named, not charged.

#### 3.4.14 `kota_run` (Revision 4, F1) — Day 5, food focus only · CORNER · `shoppingBasket` · Food · gate noun "the kota run" (never a gate)
- Title: **Kota run tonight** · Place: "[Place]" (Kota shop) · Prompt: "Your friends want a kota run"
- Situation: "Your friends are doing a kota run tonight and want to know if you're in. A full kota and chips is R65, delivered. You could split one and fetch it, or cook at home."
- Mali intro: "Your friends are getting kotas tonight, {name}. Are you in?"

| id | Label | Ledger | Cash | Sav | Energy | Stress | Tag | Mali reaction |
|---|---|---|---|---|---|---|---|---|
| `full_kota` | Full kota and chips (R65) | Kota run with friends | −65 | 0 | 0 | −4 | Discretionary | "R65 for supper, and you ate it with your friends." |
| `share_kota` | Split one and fetch it (R30) | Half a kota | −30 | 0 | −10 | −1 | Neutral | "R30 for your half, {name}. Fetching it took 10 energy." |
| `cook_home` | Cook with what's at home | — | 0 | 0 | −20 | +2 | Frugal | "No money out. Cooking supper took 20 energy." |

Every option has a cost on its card (A2): the full kota costs the most money and no energy; half a kota
costs R30 and 10 energy (fetching it); cooking costs no money and 20 energy. None is a decline: each is
supper. With the R100 N0 rule (§4.4) a food player who takes the kota at lunch and the full kota (R115) is
told so at the chapter end; one who takes the vetkoek and half a kota (R50) is not. It never gates, so the
energy costs only bite if it is played before the shift on a later day as a carry-over.

**Always-available rule** (tested): every scenario has at least one choice with `cashDelta ≥ 0` and
`savingsDelta ≥ 0`, so a scenario can never be fully disabled and the day's first scenario can always be
resolved (so A1 can never lock the shift away): skip lunch, walk, free internet, leave it, walk away, walk
today, can't this week, not this week either, not this time, cold showers, every loan option, both
stokvel options, every windfall option, cancel the gym and leave it.

**Fair-choice rule** (A2, tested in `ContentTests` for every travel mode and by `tools/sim_chapter.py`):
§3.2. Summary of what each cheap option now costs, all visible on its card:

| Scenario | Cheapest on money | What it costs instead |
|---|---|---|
| Lunch | Skip lunch | 45 energy: as the day's first choice it costs the shift |
| Trip across town | Walk | 45 energy (same) |
| Out of data | Free internet | 45 energy (same) |
| | Day bundles | Later "R15 × 2": R45 in all, paid over three days |
| Speaker | Pay later | Later "R130 × 2": R20 more than paying in full |
| Fare rise | Walk today (on foot: keep walking) | 50 energy (costs Day 3's shift) and Later "R34 × 4" (on foot: nothing later) |
| | Lift club | the most cash today (R120) |
| Call from home | Can't / R80 for now | Later "Call back": your aunt calls again in two days, asking R200 after "can't", R120 after R80 |
| Geyser | Cold showers | Later "R350 on payday" |
| Kota run (food week) | Cook at home / half a kota | 20 energy / R30 and 10 energy |
| Bra K | Not today | nothing: borrowing is the costly side (Later R600 / R300) |
| Hoodie, dinner, stokvel, gym, second call | Declining | going without it (the card's label) |

### 3.5 Behaviour signal (hidden)

Unchanged maths (`ScenarioManager.cs:135-164` moves into `ScenarioOutcome`): lerp spending/saving scores
toward the tag's signal (α 0.25 / 0.20); the words in `financialProfile` stay hidden. Each choice also
appends a `ChoiceRecord` for the chapter reflection.

---

## 4. Copy templates

### 4.1 Tokens (`MaliText.Fill`, `Assets/Scripts/Core/MaliText.cs`)

| Token | Value | Example |
|---|---|---|
| `{name}` (and legacy `{0}`) | `characterName`, or "friend" if empty | Thandi |
| `{cash}` `{savings}` `{total}` | `MoneyFormat.Rand` without the R sign (template writes `R{cash}`) | `R{cash}` → R1 250 |
| `{goalName}` | `GoalPresets.ShortName(goal)` | emergency buffer |
| `{goalTarget}` `{goalLeft}` | target, `Remaining(savings)` (digits as above) | 2 000 |
| `{day}` | `currentDay` | 3 |
| `{paydayWhen}` | "in N days" (N = `DaysToPayday`), or "tomorrow" when N = 1 | in 4 days |
| `{energy}` | energy, whole number | 35 |
| `{plan}` | `paydayPlanText` | |
| `{friend}` | "Thandi", or "Lerato" when `characterName` starts with "Thandi" (ordinal, ignore case) | Thandi |
| Any other `{key}` | supplied by the caller's `extra` dictionary (`label`, `amt`, `owed`, `places`, `count`, `due`, `repay`, `laterDays`, `gate`, `focus`) | |
| `{laterDays}` (extra) | the days of the choice's later payments as words, computed for today (§4.8): "Days 4 to 7" (three or more in a row), "Days 3 and 4" (two), "Days 4 and 6", "Day 7" (one) | Days 4 to 7 |
| `{gate}` (extra) | `gateNoun` of today's first scenario (§3.4) | lunch |
| `{focus}` (extra) | `SpendingFocus` Mali phrase (§2.7) | getting around |

Amount tokens produce digits with the group space and no sign; templates write the `R` (`R{amt}`).
Unknown tokens are left visible and logged (the content test fails on them).

### 4.2 Mali in the world

**4.2.1 Talk to Mali (`MaliGreeting.Build(PlayerData data, bool useSchedule, out Dictionary<string,string>
extra)`, WP6 since Revision 3).** It reads the profile and follow-ups from `data` and asks
`ChapterSchedule` (§3.1) and `WorkRules.State` (§7.3) itself. Returns the template with tokens unfilled; `{places}` and `{count}` are already
substituted (plain text), and `{owed}`/`{label}` are returned in `extra` as raw values (`owed` = the
amount as a float string with invariant culture, `label` = shortLabel). The caller (WP6) formats `owed`
with `MoneyFormat.Digits` and fills everything with `MaliText.Fill(template, data, extra)`, so WP3 needs
no money formatter. Built as: [stretched prefix] + waiting sentence + [one suffix].
- Stretched prefix (hidden stress ≥ 60): "You seem stretched, {name}. "
- Waiting sentence, by number of active scenarios today (spot queue heads only, names from §3.3):
  - 0: "That's everything for today, {name}. Sleep at home when you're ready."
  - 1: "One thing is waiting today, {places}."
  - 2: "Two things are waiting today, {places}."
  - 3+: "{count} things are waiting today, {places}." (`{count}` as a digit)
  - `{places}` joins where-phrases: "A", "A and B", "A, B and C", e.g. "Two things are waiting today, at the
    spaza shop and at home." (Revision 4: before, "…: your gate and down the road." read oddly.)
- Suffix, first that applies: arrears > 0 → " R{owed} is still owed for {label}."; shift state NotYet →
  " The shift opens after {gate}." (`gate` returned raw in `extra`); not worked and energy ≥ 60 →
  " There's a shift going at the far end of the main road."; not worked and energy < 60 →
  " You're too tired for a shift today."
- `{places}` uses `ChapterSchedule.SpotWhere(spot, focus, travel)` ("at the kota shop", "at home", "down the
  road"), so the names follow the profile.
- First meeting (only if `hasMetMali` is false; CC sets it true on **Let's go**, WP8): "Hi {name}, I'm Mali. I'll be
  with you all week, all the way to payday." This is the only entry left in `MaliDialogueLibrary`
  (`first_meeting`); every other legacy entry is deleted (WP3, §8).

**4.2.2 Work (Passing).** Shift done: "R150 for the shift, {name}." · Already worked (shown by
`OnDisabledTap`, §7.1): "That's today's shift done. There's another one tomorrow." · Too tired (shown by
`OnDisabledTap`): "A shift needs 60 energy, and you've got {energy}. Sleep brings it back to 100." · Not
yet open (A1, shown by `OnDisabledTap`): "The shift opens after {gate}. A shift needs 60 energy." (longest,
"…after the trip across town…": 2 lines of Dialogue 46 in 1 180 u; it never says which option to pick).

**4.2.3 Morning lines** (Passing, on `DayStarted`): §3.1 table. With the prefix "You seem stretched. "
(no name, so the line still fits two lines of the compact box) when stress ≥ 60.

**4.2.4 First day of a replay** (Blocking, `runNumber > 1`, Day 1):
- With a plan: "Same week, another go, {name}. Last time, the night before payday, you said: \"{plan}\""
- Without: "Same week, another go, {name}. R600 in your pocket, R400 in savings, seven days to payday."

### 4.3 End-of-day reveal line (`RevealLineBuilder.Build(PlayerData data, NightResult night)`, WP7)

Line = [prefix] + A + " " + B. Prefix when hidden stress ≥ 60: "You seem stretched, {name}. ".
Every rule reads only persisted data (`chapter.days`, `today`, obligations, stats) and works when
`night.settlement == null` (a reveal rebuilt after the app was killed, §7.3). Overflow: the line has at
most 3 lines of Body 40 in the band's text width; if it is longer, drop the prefix first, then paginate
(tap advances, like the blocking box).

**Biggest mover** = the non-transfer event of the closed day with the largest |TotalDelta|; tie → the
later event.

**A — what moved it most** (first that matches):
| # | Condition | Template |
|---|---|---|
| A1 | no events at all | "Nothing moved your money today. You ended where you started: R{total}." |
| A2 | only transfers | "Money moved between cash and savings today, and your total stayed at R{total}." |
| A3 | mover In, category Work | "The shift brought in the most today: R{amt}." |
| A4 | mover In, category Loan | "The R{amt} from Bra K was the biggest change today. R{repay} goes back on Day {due}." |
| A5 | mover In, other | "{label}: R{amt} in. That was the biggest change today." |
| A6 | mover Out | "{label} took the most today: R{amt}." |

(`{label}` = event label with " (part)" removed; `{repay}`/`{due}` from the matching loan obligation.)
Ledger labels are noun phrases with no amount and no comma (Revision 4, tested), so A6 never repeats the
amount: "Gogo's meds took the most today: R200.", not "R200 for gogo's meds took the most today: R200."

**B — what's next** (first that matches; "new day" = the day after the closed one):
| # | Condition | Template |
|---|---|---|
| B1 | closed day is 7 | "Tomorrow is payday." |
| B2 | any arrears now | "R{owed} is still owed for {label}. It comes off tomorrow night from your cash, and the Bank can move savings into cash before then." (`{label}` = label of `LargestArrears`; arrears are charged at night, from cash only, Revision 4) |
| B3 | `ObligationLedger.DueOnNight(data, day + 1)` is not empty | "Tomorrow night, {label} takes R{amt}. You've got R{cash} in cash." (`{label}` = shortLabel; if several, the largest) |
| B4 | the new day is 7 | "Tomorrow is the last day before payday." |
| B5 | any savings delta today ≠ 0 | "Savings is at R{savings} of your R{goalTarget} {goalName}." |
| B6 | otherwise | "Payday is {paydayWhen}." (computed for the new day) |

Banned in every Mali line, reaction, reveal and reflection (tested, case-insensitive, whole words): good,
nice, great, smart, careful, should, mistake, well done, bad, wrong, proud, wise, smart move,
responsible, too much, overspent, treat yourself, unfortunately, risk, and "!".

### 4.4 Chapter end: "What Mali noticed" (`ChapterReflection.Noticed(PlayerData)`, WP7)

Built from `chapter.choices` (tags + ids), `chapter.days` (money events) and obligations. Evaluate in order,
keep the first **three** that match; if fewer than two match, append F1 then F2 until there are two.
Count words: 2 → "Twice", 3 → "Three times", 4 → "Four times", n ≥ 5 → "{n} times".

| # | Rule | Line |
|---|---|---|
| N0 | `spendingProfile.source == "onboarding"` and Σ outflow (bills included) in `SpendingFocus.Categories(focus)` ≥ R100 (Revision 4; `ChapterReflection.N0Minimum`) | "You said most of your money goes on {focus}. This week that came to R{amt}." |
| N1 | chose `emergency_expense/from_savings` | "When the geyser broke, you reached for savings first." |
| N1b | chose `emergency_expense/from_cash` | "When the geyser broke, you paid for it from cash the same day." |
| N1c | chose `emergency_expense/cold_showers` | "When the geyser broke, you waited it out with cold water." |
| N2 | Σ outflow in categories Family + Friends ≥ R50 | "You made room for other people this week: R{support} to family and friends." |
| N3 | Deferred tag count ≥ 2 | "{CountWord} you moved a cost to later." |
| N4 | Frugal tag count ≥ 3 | "{CountWord} you held on to your money rather than spend it." |
| N5 | Discretionary tag count ≥ 2 | "{CountWord} you chose the option that made the day easier." |
| N6 | net savings movement (Σ savings deltas of all events, withdrawals included; Revision 4) ≥ R100 | "R{saved} went into savings over the week." |
| N7 | any `chapter.days[i].owedAtClose > 0.005`, and `TotalArrears > 0.005` now | "Some nights there wasn't enough cash for everything due. R{owed} is still owed going into payday." |
| N7b | any `chapter.days[i].owedAtClose > 0.005`, nothing owed now | "Some nights there wasn't enough cash for everything due. It carried over and got paid." |
| F1 | fallback | `shiftsWorked` ≥ 2: "You took a shift on {shifts} days this week." · 1: "You took a shift on one day this week." · 0: "You didn't take a shift this week." |
| F2 | fallback | "You lived the whole week, one choice at a time." |

Observations of actions, never traits ("you reached for savings", never "you are a saver"). No scores,
no ratios ("x of 7"), no comparisons with other players or with an earlier run. N0 only puts the
player's own answer next to the week's number; it never says whether that is a lot (A3). Under R100 it is
skipped (Revision 4), so a small amount never reads as contradicting what the player said.

**What moved it most** (`ChapterReflection.Summary`): the top three events of the chapter whose
`sourceId` starts with `scenario:` (scenario choices, loans included), transfers excluded, ranked by
|TotalDelta| desc, ties → earlier. Shifts and bills are not ranked: bills get one line of their own,
"Bills and repayments: −R{amt}" (Σ of events whose `sourceId` starts with `bill:`; omitted when
0), so the reflection is about the player's choices, not the fixed rent.

### 4.5 Payday plans (implementation intentions)

Prompt: "Tomorrow is payday." / "If you could decide one thing about payday tonight, what would it be?"

| id | Text (stored in `paydayPlanText`, quoted back) |
|---|---|
| `save_first` | "When pay lands, R200 goes into savings before I buy anything." |
| `owed_first` | "When pay lands, I pay what I owe first, then everything else." |
| `family_set` | "When pay lands, I set aside one amount for family, and stick to it." |
| `wait_a_day` | "When pay lands, I wait one day before any shopping." |

After saving: Mali "Got it. Next time this week starts, I'll remind you what you said." After **Not now**:
"That's the week, {name}." (Plan fields stay as they were.)

### 4.6 Character creation copy

All strings below except the Mali paragraphs live in `OnboardingCopy` and `SpendingFocus`/`TravelMode`
(`Assets/MaliGo/Data/`, WP1) so their fit can be tested outside Unity.

| Screen | Title | Body / controls |
|---|---|---|
| 1 | Promise (DisplayTitle 72, two lines): "Live the week before payday." / "See where your money goes." | Question (Body 40 Bold) "What should we call you?"; field placeholder "Your name" (max 16 chars, trimmed). Button **Next** |
| 2 | Pick your look | Cards "Style 1"…"Style 6", each with a 48 u skin-tone swatch. **Back** **Next** |
| 3 | (no title; two questions) | "Where does most of your money go?" cards: "Food and takeaways" · "Getting around" · "Data, airtime and going out" · "Home and family". "How do you usually get around?" cards: "Minibus taxi" · "E-hailing rides" · "Mostly on foot" · "Own or shared car". Nothing preselected. Once both rows have a pick: "We've built your week around where your money goes." and under it `ChapterSchedule.WeekPlaces(focus, travel)` joined with " · " (e.g. "Kota shop · Taxi rank · Shops by the bank"). **Back** **Next** (Next disabled until both rows have a pick). On Next: `SpendingProfiles.SetFromOnboarding` |
| 4 | What are you saving towards? | Cards: title, "R{target}", caption "R400 saved so far". **Back** **Next** |
| 5 | (no title; Mali) | Three paragraphs (§1.1 0:40), each paginated at 3 lines by `UiTextLayout.Paginate`; tap advances. Button **Let's go** (sets `isCharacterCreated = true`, `hasMetMali = true`, runs `ChapterFlow.StartChapter`, saves) |

The promise is the first thing a new player reads (A4). Measured (Aileron Black 72): "Live the week before
payday." 1 041 u, "See where your money goes." 1 014 u, both inside the 1 380 u text width of the sheet.
The two questions are plain and neutral: the cards name categories, not habits, and there is no "right"
card. The places line describes, it does not advise.

Look mapping. The stock Kenney skins all share one light peach skin colour, and two of them are a
"criminal" and a "cyborg"; neither fits the players. Only the two skater skins are used, each recoloured
into three skin tones (§6.3b), giving six looks. Two `AppearanceData` fields drive the model:

| Card | `genderPresentation` | `skinTone` | Texture (`Resources/MaliGo/Skins/`) |
|---|---|---|---|
| Style 1 | feminine | light | `skaterFemaleA_light` |
| Style 2 | feminine | medium | `skaterFemaleA_medium` |
| Style 3 | feminine | deep | `skaterFemaleA_deep` |
| Style 4 | masculine | light | `skaterMaleA_light` |
| Style 5 | masculine | medium | `skaterMaleA_medium` |
| Style 6 | masculine | deep | `skaterMaleA_deep` |

`PlayerCharacterCatalog.ResolveSkin` (WP8) first tries `Resources.Load<Texture2D>("MaliGo/Skins/" +
(genderPresentation == "masculine" ? "skaterMaleA" : "skaterFemaleA") + "_" + (skinTone is light/medium/deep ?
skinTone : "medium"))`, and falls back to the existing `skinOptions` lookup. `clothing` and the other
fields keep their defaults. The criminal and cyborg skins are never used. The 8 financial-profile
questions are removed from the UI; their data keeps defaults (D2).

### 4.7 First-run guide (coach marks)

| # | Fires when | Target anchor | Line | Ends when |
|---|---|---|---|---|
| 1 | Mali's morning line hides (`MaliDialogueController.OnDialogueHidden`) while no modal is open; or, if no Mali line is showing 2.0 s after the world loads, then | `controls.joystick` | "Drag here to walk around the neighbourhood." | moved ≥ 1 unit, or Got it |
| 2 | after 1, first time a prompt is visible | `world.prompt` | "When something's nearby, its name shows here. Tap it to see what's going on." | Got it, or prompt tapped |
| 3 | after 2, first `MoneyChanged` whose `sourceId` does not start with `bill:`, once no modal is open | `hud.cash` | "This is your cash. Savings is next to it. Every choice shows up here straight away." | Got it |
| 4 | after 3, the Home prompt is visible and today has ≥ 1 event or a played scenario | `world.prompt.home` | "Sleeping at home ends the day. Then you'll see where your money went." | Got it, or the prompt is tapped (the first `UiModal.Changed` with `IsAnyOpen` while the mark is up) |

Buttons on every mark: **Got it** (primary) and **Skip tips** (text button; sets `firstRunDone = 1`).
Coach marks are not modal, but `FirstRunGuide` **hides its current mark whenever `UiModal.IsAnyOpen`**
and re-evaluates on `UiModal.Changed`, so a mark never sits over a sheet, the reveal or Pause. A mark
that was hidden by a modal comes back when the modal closes, unless its end condition was met.

### 4.8 Other fixed strings

| Where | Text |
|---|---|
| Old-save notice | "MaliGo has been updated — your story starts fresh." |
| Prompt chip | "Tap" (touch) / "E" (keyboard, Editor/desktop) |
| Action button verbs | Home "Open" · Bank "Open" · Work "Work" · scenario "Look" · Mali "Talk" |
| Work prompt | "Take a shift (+R150)" · not yet open (A1): "Shift opens after {gate}" (e.g. "Shift opens after lunch"; longest "Shift opens after the stokvel meeting" 693 u of the prompt's 776 u text width, §5.4.2) · done: "Shift done for today" · tired: "Too tired for a shift" |
| Home / Bank prompts | "Home" · "Bank" |
| Mali prompt | "Talk to Mali" |
| Choice panel | "You have R{cash} cash" · "Savings R{savings} · Energy {energy}" · columns "Cash", "Savings", "Energy", "Later" · disabled "Not enough cash" / "Not enough savings" · button "Not now" |
| Later column | 1 payment: "R{amt}" + small line "on Day {due}" or "on payday" (due ≥ 8); n payments: "R{amt} × {n}" + small line with the actual due days, "Days 4, 6" (all ≤ 7; "Days 4–7" when three or more fall on consecutive days), or "from Day {first}" when any falls after Day 7. Due days and n are computed for today (`firstDueDay > 0 ? firstDueDay : currentDay + interval`, then dropping any after `instalmentLastDueDay`); n = 0 shows "—". A follow-up (no instalments): `followUpLaterText` ("Call back") + "on Day {d}", or "—" when d > 7 |
| HUD Today pill (A4) | line 1 "Today from R{start}" (start = `today.StartTotal`), line 2 arrow sprite + "R{now}" (cash + savings) |
| Mali chip row | as Revision 2 ("Cash −R20", "Energy −45", "Later R34 × 4"), plus "Call back Day {d}" for a follow-up |
| Home sheet | Title "Home". Rows "Day", "Cash", "Savings", "Energy", "Tonight", "Still owed", "Next". Button "Sleep: end Day {day}" |
| Sleep confirm | Title "End Day {day}?" · "Tonight: {label} R{amt}" per item of `DueOnNight(day)`, or "Nothing is due tonight." · "Still owed: R{owed} ({label}). It comes off first." · "One thing is still waiting today. It'll carry over to tomorrow." / "{n} things are still waiting today. They'll carry over to tomorrow." · not worked and energy ≥ 60: "You haven't taken today's shift." · not worked and energy < 60: "You were too tired for a shift today." · buttons **Not yet** / **Sleep** |
| Bank sheet | Title "Bank". Rows "Cash", "Savings", "Goal" (title + R target), progress bar. Groups "Move to savings", "Take out of savings"; chips "R50" "R100" "R200"; disabled caption "Not enough cash" / "Not enough savings" |
| Reveal | "You started Day {day} with" · "You ended it with" · "cash + savings" (caption beside each headline amount) · "+R210 today" / "−R85 today" / "R0 today", and on a day with a loan "+R166 today · R400 borrowed" (Revision 4) · "Cash R{cash} · Savings R{savings}" · "Still owed: R{owed}" plus " (R{owedBefore} this morning)" when the morning figure differs · "New promise: {shortLabel} R{amt} on Day {due}" / "… on payday" / "New promise: {shortLabel} R{amt} × {n}, Days {d1}, {d2}" (three or more consecutive days: "Days {d1}–{dn}", e.g. "New promise: Taxi R34 × 4, Days 4–7") (at most 2 lines; a third and more → "+{n} more new promises") · "What moved it" · "Other ({n})" · loan rows: "{label} · R{repay} back Day {due}" · "Coming up" · "Day {n} night: {label} R{amt}" · "Tomorrow: your aunt calls back." (a follow-up due tomorrow, §5.4.8) · button "On to Day {n}" / "On to payday" |
| Chapter end A | "Seven days to payday" · "On Day 1 you had" · "Tonight you have" · "cash + savings" · "+R440 this week" · "Still owed going into payday: R{owed}" · "Already promised for payday: R{sum}" + list "Stokvel R200 · Gym R199" · "What moved it most" · "Bills and repayments: −R{amt}" · **Next** |
| Chapter end B | "What Mali noticed" · **Next** |
| Chapter end D | "Chapter 2 starts on payday. It's coming in a future update." · **Live the week again** (the beta's ending: a replay of the same week, E2 closed) |
| Pause | "Paused" · **Resume** · "Sound" · "Music" · "Text speed" (Slow / Normal / Instant) · "Reduce motion" · toggles read "On"/"Off" · **Start over…** |
| Reset confirm | "Start over?" · "This deletes your week and your progress. Your settings stay. You can't undo this." · **Delete and start over** (left, attention text) · **Keep playing** (right, primary) |

### 4.9 Empty states

| Where | State | Shows |
|---|---|---|
| HUD bill pill | no obligation due on or before Day 8 and nothing owed | line 1 "No bills", line 2 "till payday" |
| Reveal ledger | no events | caption "Nothing moved today." in the ledger area |
| Chapter end moments | < 3 ranked scenario events in the chapter (§4.4) | as many cards as exist; none → "None of your choices moved money this week." |
| Chapter end promises / still owed | nothing promised / nothing owed | that line is omitted |
| Mali greeting | nothing waiting | 4.2.1 "That's everything for today…" |
| Bank | a chip unaffordable | chip disabled at 45% + group caption |

---

## 5. UI kit

### 5.1 Tokens (`UiTheme`, `Assets/Scripts/UI/Kit/UiTheme.cs`)

**Colour** (from `ui-ux-direction.md` §4.1; contrast ratios there):

| Token | Hex | Use |
|---|---|---|
| `Paper` | `#FBF6EC` | sheets, dialogue box, CC sheet |
| `Card` | `#FFFFFF` | choice cards, ledger card |
| `Warm` | `#FFF4E9` | Mali's box, coach bubble |
| `Sunken` | `#F2EDDC` | status cards, chips, delta pill, toggles off |
| `Tint` | `#EEF8E8` | selected card fill |
| `Inverse` | `#0F5E2E` (alpha 0.94 on HUD pills) | HUD pills, reveal backdrop (alpha 1), CC backdrop |
| `Scrim` | `#102217` alpha 0.55 (0.25 behind Mali's blocking box) | behind sheets |
| `TextPrimary` | `#101010` | text on paper/card |
| `TextSecondary` | `#344330` | supporting text |
| `TextMuted` | `#52604E` | labels, captions (≥ 32 u) |
| `TextOnInverse` | `#F9FFF6` | text on forest |
| `TextOnInverseMuted` | `#C9EFB4` | labels on forest |
| `AccentPrimary` / `Pressed` | `#087A18` / `#075F15` | primary buttons, progress, toggles on |
| `Gold` | `#DFA464` | Mali's name tag, focus ring alt, gold circle; never text on paper |
| `Coin` | `#F4C84A` | energy bar, coin icon tint, focus ring |
| `MoneyIn` | `#087A18` | "+" amounts and in-arrows on paper |
| `MoneyOut` | `#8A5633` | "−" amounts and out-arrows on paper (deliberately not red) |
| `MoneyTransfer` | `#344330` | transfers |
| `MoneyInOnInverse` / `MoneyOutOnInverse` | `#C9EFB4` / `#F8E4D6` | HUD delta tags |
| `Attention` | `#A8432F` | **only** arrears ("still owed") and the reset confirm |
| `AttentionDot` | `#E85F48` | non-text dot on the HUD bill pill when something is owed |
| `BorderSubtle` | `#EAD7CB` | dividers |
| `BorderControl` | `#B47B41` | outlines of secondary buttons and toggles (4 u) |
| `Shadow` | `#102217` | all shadows |

Colour never carries meaning alone: every amount has a sign and an arrow sprite.

**Type** (Aileron, three weights; sentence case everywhere; never shrink below the role size; floor 32 u):

| Role | Size u | Weight | Line spacing | Use |
|---|---|---|---|---|
| `DisplayAmount` | 104 | Black | 1.0 | reveal and chapter-end amounts |
| `DisplayTitle` | 72 | Black | 1.1 | "Seven days to payday" |
| `Title` | 56 | Black | 1.15 | sheet and scenario titles |
| `Dialogue` | 46 | SemiBold | 1.3 | Mali, blocking box |
| `ChoiceLabel` | 44 | Bold | 1.15 | choice labels |
| `Button` | 44 | Bold | 1.0 | buttons |
| `HudValue` / `LedgerValue` | 44 | Black / Bold | 1.0 | numbers |
| `Body` | 40 | SemiBold | 1.3 | situations, sheet copy, compact Mali box |
| `Label` | 36 | Bold | 1.2 | ledger header, name tag, short labels |
| `Caption` | 32 | SemiBold | 1.25 | the floor: HUD labels, column headers, hints. Outside the HUD, a caption that carries information found nowhere else ("Not enough cash", "cash + savings", the Later due days excepted) uses `Label` 36 |

**Shape and spacing**: radius button 28, card 36, sheet 48, pill = height/2. Spacing 10/20/30/40/60/80.
Sheet padding 48. Gap between tappables ≥ 20. HUD margin 24 inside the safe area; other screens 40.
Touch target ≥ 144 × 144 (an invisible padding rect may make up the difference). Stroke 4.
Shadows: card (0, −12) blur 32 at 10%; sheet (0, −20) blur 56 at 18% (procedural, §5.3).

**Motion** (all on `Time.unscaledDeltaTime`; reduce motion → every transition is a 150 ms crossfade and
numbers jump):

| Token | ms | Easing | Use |
|---|---|---|---|
| `Press` | 100 | ease-out | scale 0.97 + darken |
| `Fade` | 200 | standard | prompts, tags |
| `SheetIn` / `SheetOut` | 300 / 200 | decelerate / accelerate | sheets rise 40 u |
| `Count` | 700 (max 1 200) | decelerate | number count-ups |
| `Stagger` | 120 per row | | ledger rows |
| `Backdrop` | 400 | standard | reveal backdrop |
| `CueBob` | 1 200 loop, 6 u | sine | dialogue continue cue |
| `DeltaTag` | hold 1 600, fade 200 | | HUD delta tags |

No bounce, shake, confetti or flashing.

**Sort orders** (one canvas per layer; every canvas: Screen Space Overlay, `ScaleWithScreenSize`
1920×1080, `matchWidthOrHeight = 1`, content under a `SafeAreaFitter` child):

| Order | Layer |
|---|---|
| 0 | Character creation |
| 10 | World prompt |
| 20 | HUD |
| 25 | Mobile controls |
| 40 | Mali dialogue (blocking and compact) |
| 50 | Sheets: choice, Home, Bank |
| 55 | Sleep confirm |
| 60 | End-of-day reveal, chapter end |
| 70 | Pause |
| 75 | Reset confirm |
| 80 | Coach marks |
| 90 | Notices |

### 5.2 Glyph policy (verified)

Checked with fontTools 4.59.2 on `C:\Temp\MaliGoAssetsStaging\dotcolon_aileron\Aileron-{Regular,SemiBold,
Bold,Heavy,Black}.otf` (337 cmap entries; identical cmap in every weight; UPM 1000, ascent 970,
descent −230):

- **Present:** all printable ASCII; U+2212 − (minus); U+2014 — ; U+2013 – ; U+2018/2019 ‘ ’ ; U+201C/201D “ ” ;
  U+2026 … ; U+00B7 · ; U+00D7 × ; U+2022 • ; U+00E9 é, U+00EA ê, U+00EB ë, U+00F4 ô, U+0161 š, U+014B ŋ and
  the rest of Latin-1 plus most of Latin Extended-A (enough for Afrikaans, isiZulu, isiXhosa names); U+20AC €.
- **Missing:** U+00A0 no-break space, U+2009, U+202F, all arrows U+2190–2194, ▲ U+25B2, ▼ U+25BC, ▶ U+25B6,
  ✓ U+2713, ✕ U+2715, ‖ U+2016, ● U+25CF, ★, ½, U+FFFD, every emoji.

**Rules**
1. Player-visible strings may contain only printable ASCII plus `− — – ‘ ’ “ ” … · × •` and Latin-1/
   Latin Extended-A letters. `ContentTests` checks every string in content and copy.
2. Money: `MoneyFormat.Rand(1250) = "R1 250"` (plain U+0020 between groups of three from 1 000),
   `Rand(5) = "R5"`, `Rand(0) = "R0"`, `Signed(-45) = "−R45"` (U+2212), `Signed(150) = "+R150"`,
   `Signed(0) = "R0"`. Rounded half away from zero. Never `ToString("N")` or CurrentCulture.
3. Because the separator is a breakable space, every wrapped text that may contain money goes through
   `UiTextLayout.WrapKeepingAmounts`, which pre-breaks lines so `R1 250` never splits (§5.3).
4. Arrows, ticks, crosses, pause bars, chevrons, energy: sprites (§6.2) or procedural, never glyphs.
5. The staged Aileron files are OpenType-CFF (a `CFF ` table, no `glyf`), which legacy `UnityEngine.UI.Text`
   has never been tested with in this project. WP2 therefore converts the three weights to TrueType
   outlines with fontTools (`tools/convert_fonts.py`: `cu2qu` via `Cu2QuPen` + `TTGlyphPen`, tolerance 1.0
   font unit; checked on 3 Oct: the converted Bold keeps all 337 cmap entries including U+2212). CC0 allows
   the conversion. Only the `.ttf` files are imported.
6. `UiFonts` self-tests each weight on first use: `font.RequestCharactersInTexture("R0123456789 −·", 44)`
   then `GetCharacterInfo` for every one of those characters; if the font is null or any lookup fails it
   logs a warning once and uses `LegacyRuntime.ttf` (built-in; not checked here, so rules 1–4 still apply).

### 5.3 Procedural surfaces (`UiKit`)

- **Rounded rect:** one white 132×132 texture with a 64 px corner radius and a 1.5 px anti-aliased edge,
  `Sprite.Create(..., border = 64 all sides)`, `Image.type = Sliced`, tinted by `Image.color`. Radius r u
  is set with `image.pixelsPerUnitMultiplier = 64f / r`. Cached (fixes the per-access `Sprite.Create` leak
  in `KenneyUiSprites.cs:43-50`). **Every** sprite `UiKit` makes (rounded rect, shadow, circle, white,
  bolt, each icon, Mali art) is created once and cached by name; a cached entry that has become
  Unity-null (`== null`) is recreated.
- **Soft shadow:** one 192×192 texture, rounded rect radius 48 with a 40 px Gaussian-like alpha falloff,
  sliced with border 88; placed behind the element with the shadow offset.
- **Circle:** 128 px anti-aliased disc (same technique as `MobileControlsUI.CreateCircleSprite`).
- **White:** 1×1 for scrims, dividers, bars (`Image.Type.Filled` for progress).
- **Energy bolt:** 128 px texture, 6-point polygon (0.58,1.00) (0.18,0.45) (0.46,0.45) (0.36,0.00)
  (0.84,0.58) (0.54,0.58) in unit coordinates, filled white, tinted `Coin`.
- **Kenney adventure panels** (`panel_brown`, `button_brown`, `panel_grey_green`) are retired: no new code
  uses `KenneyUiSprites`.

### 5.4 Components (layout numbers on a 1080-u-tall canvas; widths given for 16:9 = 1920 u)

Anchors are relative to the safe-area root. "safeWidth" = width of the safe root in u.

#### 5.4.1 HUD (`HudView`, sort 20)

```
+----------------------------------------------------------------------------------------------------+
| [Day 3 of 7       ] [(c) Cash ] [(p) Savings  of R2 000] ... [(z) Energy] [Rent          ]   (||)  |
| [Payday in 5 days ] [    R655 ] [    R400  [=====    ] ]     [ ==-   35 ] [R500 · tonight]          |
| [Today from R1 055]   -R34  (delta tag under the Cash pill, 1.6 s)                                  |
| [ >  R1 021       ]                                                                                  |
+----------------------------------------------------------------------------------------------------+
```

| Element | Anchor | Size | Contents (all measured with Aileron metrics at 16:9; widths are the text area inside 24 u padding) |
|---|---|---|---|
| Row | top, 24 u below safe top; left group from x = 24, right group ends at safeWidth − 24 | pills 112 tall, gaps 16 | no background behind the row |
| Day pill | left 1 | 328 × 112, no icon (text 280) | line 1 "Day 3 of 7" Bold 40 `TextOnInverse` (185 u); line 2 "Payday in 5 days" / "Payday tomorrow" Caption `TextOnInverseMuted` (245 / 260 u) |
| **Today pill** (A4) | directly under the Day pill: x = 24, top 152 u below the safe top (24 + 112 + 16) | 328 × 112, no icon (text 280) | from `HudCopy.TodayPill(data)` (pure, §7.4). Line 1 "Today from R{start}" Caption `TextOnInverseMuted` ("Today from R9 999" = 279 u), start = `data.today.StartTotal` (cash + savings when the day opened); line 2 `arrowRight` sprite 36 u tinted `TextOnInverse` + 10 gap + "R{now}" HudValue `TextOnInverse` (now = cash + savings; "R9 999" = 154 u, so 200 u). Counts over `Count` like the other values. It says "Today: started R1 000 → now R___" in two short lines. Line 1 is the number the reveal starts from. The reveal's end is line 2 minus whatever the night charges (the sleep confirm lists it) |
| Cash pill | left 2 | 280 × 112 | icon `coin` 56 u tinted `Coin` at x 24; text from x 96 (160 u): "Cash" Caption muted; value HudValue `TextOnInverse` ("R1 315" = 154 u) |
| Savings pill | left 3 | 392 × 112 | icon `pouch` 56; text from x 96 (272 u): line 1 "Savings" + right-aligned "of R2 000" (Caption muted; 117 + 140 u); line 2 value HudValue (154 u) + 16 gap + progress bar 102 × 12 (`Coin` on 25% cream) = savings / target |
| Energy pill | right 3 | 300 × 112 | procedural bolt 56; text from x 96 (180 u): "Energy" Caption; bar 80 × 14 (`Coin` on 25% cream) + 12 gap + number HudValue ("100" = 87 u) |
| Bill pill | right 2 | 340 × 112, no icon (text 292) | from `HudCopy.BillPill(data)` (pure, §7.4). Due: line 1 `{shortLabel}` Bold 36 `TextOnInverse` ("Speaker" 142 u); line 2 "R{amt} · {when}" Caption muted, when = "tonight" / "tomorrow" / "Day {n}" / "payday" ("R60 · tomorrow" 245 u, "R9 999 · tomorrow" 269 u). Arrears: line 1 "{shortLabel} owed" Bold 36 + 20 u `AttentionDot` 8 u after it ("Speaker owed" 244 + 28 u, the longest; "Petrol owed" 205 u); line 2 "R{owed}" Caption `TextOnInverse` (not `Attention`: it fails contrast on forest; the dot and the word "owed" carry it). Empty: "No bills" / "till payday" |
| Pause | right 1 | 144 × 144 circle, top 16 u below safe top | `Inverse` 94%, icon `pause` 56 `TextOnInverse` |

- Total natural width of the top row: 328+280+392 + 300+340+144 + 5 × 16 (incl. the minimum middle gap)
  + 2 × 24 = 1 912 u, the same as Revision 2, so the row still fits a 16:9 screen with no cutout: the Day
  pill grew 8 u (to hold "Today from R9 999" in the pill below it at the same width) and the savings bar
  gave up 8 u. The Today pill adds no width; it adds a second row on the left only. **Nothing is ever
  scaled down.** If the top row does not fit `safeWidth` (a cutout on a 16:9 phone, 4:3 or foldable
  widths), the right group (Energy, Bill, Pause) moves to the second row (top 152 u below the safe top),
  right-aligned; it then shares that row with the Today pill (328 + 816 u, no overlap at any width
  ≥ 1 240 u); at 20:9 (2 400 u) one row always fits.
- Bindings (§7.4). Value changes count over `Count`; Cash and Savings pills show a delta tag 8 u below the
  pill: `MoneyInOnInverse` "+R150", `MoneyOutOnInverse` "−R20", transfers `TextOnInverse`. The cash tag
  starts at x = 368, 16 u right of the Today pill. When the right group has wrapped, the delta tags sit
  below the second row instead.
- Never shown: name, level, XP, stress, emoji (D7, D9).
- Anchors registered: `hud.cash`, `hud.savings`, `hud.today`, `hud.energy`, `hud.bill`, `hud.pause`.
- Fit test (`CopyTests`, WP7): every string `HudCopy` can produce (Day, Today, Bill), with amounts up to
  R9 999 and every shortLabel in the content, fits its text width above.

#### 5.4.2 World prompt (`WorldPromptView`, sort 10) and mobile controls (sort 25)

```
                       +--------------------------------------------+
                       | (icon) Lunch at the corner shop     [Tap]  |   <- tappable, bottom 330 u
                       +--------------------------------------------+
   ( joystick )                                                          ( Look )
```

| Element | Anchor | Size | Notes |
|---|---|---|---|
| Prompt pill | bottom-centre, bottom edge 330 u above safe bottom | visible 120 tall, width auto 480–1000; hit rect 160 tall | `Paper` + card shadow; icon 56 tinted `AccentPrimary` (Mali prompt: 56 u Mali portrait); text Body Bold `TextPrimary`; chip "Tap"/"E" Caption Black ink on `Gold` pill 88 × 56. Text width ≤ 1000 − 24 − 56 − 16 − 16 − 88 − 24 = 776 u (every prompt, incl. "Shift opens after {gate}", is tested against it, WP7). Disabled (Work not yet open/done/tired): text `TextMuted`, no chip; a tap (or the action button, or E) calls `OnDisabledTap()`, which shows the §4.2.2 line explaining why |
| Joystick base | bottom-left, centre (190, 190) | 240 diameter; handle 104 | base `Inverse` 45%, handle `TextOnInverse` 75% |
| Action button | bottom-right, centre (−190, 190) | 168 diameter | `Gold` 90%; verb Button 36 Bold ink; dimmed to 40% with no label when there is no current interactable |

- One prompt only (the arbiter's current interactable). Fades over `Fade` when `Current` changes.
  `WorldPromptView` re-reads `PromptText`, `PromptIcon`, `ActionVerb` and `IsEnabled` from `Current`
  **every frame** (they change while `Current` stays the same: Work after a shift, a spot's queue head
  after a choice) and updates the text only when it differs.
- Controls hide (alpha 0, not interactable) while any modal is open (§7.2) and reset the joystick on
  `OnApplicationPause(true)`, `OnApplicationFocus(false)`, `OnDisable` and when hidden.
- Anchors: `world.prompt` (while visible), `world.prompt.home` (while visible and current is Home),
  `controls.joystick`, `controls.act`.

#### 5.4.3 Mali dialogue — blocking box (sort 40)

```
         +--------+
         |  Mali  |  portrait 280, rises 72 above the box
     +---|  art   |---------------------------------------------------------------+
     |   |        |  [ Mali ]  <- gold tag 60 tall                                 |
     |   +--------+  R350 from savings, and there's hot water tonight.             |
     |               Savings is at R50 now.                                        |
     |               (chips sit in the tag row: [Mali] [Savings -R350])        (v)  |
     +-----------------------------------------------------------------------------+
       bottom 40 u above safe bottom; width min(1640, safeWidth - 96); height 300
```

- Box `Warm`, radius 48, sheet shadow, 6 u top edge in `Gold`. Scrim 25% over the world. Controls hidden.
- Portrait `MaliPortrait` 280 × 280, left inset 24; its bottom edge sits 92 u above the box's bottom edge, so its top is 72 u above the box.
- Name tag at (330, −24) from the box's top-left: pill `Gold`, "Mali" Label 36 Black ink, padding 24.
- Text: Dialogue 46 SemiBold `TextPrimary`; area left inset 330, right inset 120, top inset 84, bottom
  inset 24 (1 180 u wide at 16:9 ≈ 54 characters a line). **Maximum 3 lines a page**; longer text is
  paginated at sentence boundaries (else word boundaries) by `UiTextLayout.Paginate`; never shrunk.
- Example of a reaction in this box: *"R350 from savings, and there's hot water tonight. Savings is at R50 now."*
- Chip row (after a choice only): pills 56 tall on `Sunken`, Caption 32, gaps 12, e.g. "Cash −R20",
  "Energy −45", "Later R130 × 2", placed in the name-tag row to the right of the tag (from x = tag right
  + 20); the text keeps its 3 lines.
- Typewriter: lay out the page first, reveal characters by wrapping the hidden tail in
  `<color=#00000000>`…`</color>` (rich text) so words never jump lines. Speed from `GameSettings`.
- Input: a full-screen transparent catcher on this canvas. First tap completes the page, next tap
  advances/closes; 150 ms lockout after every page change. Continue cue `down` 48 u `AccentPrimary`,
  right 48, bottom 36, bobbing (`CueBob`) once the page is complete; on the last page the cue is
  `checkmark`. Android back = tap.
- Anchor `mali.dialogue` while visible.

#### 5.4.4 Mali dialogue — compact (passing) box (sort 40)

- Bottom 40 u above safe bottom, height 220, width `clamp(safeWidth − 720, 1000, 1640)` centred (1 200 u at
  16:9), so it sits between the joystick and the action button. `Warm`, radius 48, card shadow.
- Portrait 160 inside the box (left 24, vertically centred); gold tag at (208, −20); Body 40 SemiBold, max
  **2 lines** (text area left 208, right 32). A passing line that needs more than 2 lines is shown in the
  blocking box instead.
- No scrim, no input blocking; controls stay. Tap on the box dismisses. Auto-hides after typing ends +
  `clamp(1.5 s + 0.05 s × characters, 3 s, 8 s)`.

#### 5.4.5 Scenario choice sheet (`ScenarioChoiceUI`, sort 50)

```
+--------------------------------------- min(2000, safeWidth-120) x 940 -------------------------------+
| Corner shop                                 |                         Cash     Energy    Later   [Not now]|
| What's for lunch?                           | +------------------------------------------------------+ |
|                                             | | Kota and a cold drink (R50)      -R50       -        -   | |
| It's midday at the corner shop and you're   | +------------------------------------------------------+ |
| hungry. There's a kota special, vetkoek     | +------------------------------------------------------+ |
| and mince, or you could push through till   | | Vetkoek and mince (R20)          -R20       -        -   | |
| supper.                                     | +------------------------------------------------------+ |
|  (Mali 120) "Midday already, Thandi.        | +------------------------------------------------------+ |
|              What are you having?"          | | Skip lunch                         -       -20       -   | |
| You have R600 cash                          | +------------------------------------------------------+ |
| Savings R400 · Energy 100                   |                                                          |
+-------------------------------------------------------------------------------------------------------+
```

- Sheet `Paper`, radius 48, sheet shadow, scrim 55%. Inner padding 48. Left column 36% of the inner width,
  gap 40, right column the rest (16:9: left 613, right 1 051).
- Left, top to bottom: place caption (Caption muted), title (Title 56, ≤ 2 lines), situation (Body 40,
  ≤ 6 lines, ≤ 190 chars), Mali mini portrait 120 + intro line (Label 36 SemiBold `TextSecondary`,
  ≤ 3 lines), then at the bottom "You have R{cash} cash" (Body Bold) and "Savings R{savings} · Energy
  {energy}" (Caption muted).
- Right: header row 56 tall with column titles (Caption muted, right-aligned in their columns); then 2–4
  cards, each 150 tall, 20 apart, `Card`, radius 36, card shadow. Card: label (ChoiceLabel 44, ≤ 2 lines,
  left 24) and value columns right-aligned. Columns: **Cash | Energy | Later**, plus **Savings** between
  Cash and Energy when any choice in the scenario has a savings delta. Column widths: 3 columns Cash 160 ·
  Energy 130 · Later 200 (label box 493 u); 4 columns Cash 150 · Savings 150 · Energy 120 · Later 190
  (label box 373 u). Label box = 1 051 − 24 − columns − 24 − 20 gap. Values: Cash/Savings
  `MoneyFormat.Signed` HudValue 44 ("—" when 0; "+R400" = 139 u); Energy "−20" ("—" when 0); Later per §4.8
  ("—" when none): first line Label 36 Bold ("R130 × 2" 188 u, "R34 × 4" 165 u; it must fit the column), small line Caption 32 ("Days 4, 6",
  "on payday" ≤ 155 u; "Days 4–7" 134 u; every small line must fit its Later column, 200 or 190 u, and
  "from Day 5" measures 159 u). Colours: out `MoneyOut`, in `MoneyIn`, transfer choices (cash and savings deltas
  cancel) both `MoneyTransfer`, loans (category Loan) `MoneyTransfer`, energy and Later `TextPrimary`.
- **Never shown:** XP, stress, the behaviour tag, any "recommended" mark. Authored order kept.
- Disabled (`ScenarioOutcome.Availability` ≠ Available): label and values at 60% alpha, not interactable,
  and under the label a `Label` 36 line "Not enough cash" / "Not enough savings" at full opacity in
  `TextSecondary` (45% alpha failed contrast: 2.05:1).
- **Not now** (top-right, 280 × 144 text button with `BorderControl` outline) and Android back close the
  sheet without choosing; the scenario stays active.
- Opening: `SheetIn`, 300 ms input lockout. Pressing a card: `Press` + 6 u `Coin` ring; on release the
  sheet closes (`SheetOut`) and the choice resolves (§7.6).

#### 5.4.6 Home and Bank sheets (`ActionPanelUI`, sort 50)

```
+------------------------------ 1560 x 860 ------------------------------+
| Home                                                             ( X ) |
| +------------- status 700 wide (Sunken) ---+  +--- actions 640 wide --+ |
| | Day                                    3 |  | [ Sleep: end Day 3  ] | |
| | Cash                                R771 |  |   (primary, 144 tall) | |
| | Savings                             R400 |  |                       | |
| | Energy                                35 |  |                       | |
| | ---------------------------------------- |  |                       | |
| | Tonight                        Rent R500 |  |                       | |
| | Next                      Speaker · Day 4 |  |                       | |
| +------------------------------------------+  +-----------------------+ |
+-------------------------------------------------------------------------+
```

- Sheet `Paper`, radius 48, scrim 55%, centred. Title Title 56 at (48, −48). Close button 144 × 144 at the
  top-right, icon `cross` 56 `TextPrimary`. Android back closes.
- Status card: rows 72 tall; label Caption muted left, value HudValue right-aligned; divider 2 u
  `BorderSubtle`. "Still owed" row value in `Attention` (from `LargestArrears`/`TotalArrears`), with a
  Caption under it: "The Bank is up the road." "Tonight" = `DueOnNight(currentDay)`, "Next" = `NextDue`
  after tonight (§2.4 helpers; the old `nextDueDay - currentDay <= 1` test is gone).
- Home actions: one primary button "Sleep: end Day {day}" (640 × 144, icon `hourglass`) → sleep confirm.
- Bank: status rows Cash, Savings, Goal ("{goalTitle} R{target}") and a 600 × 16 progress bar; actions:
  "Move to savings" (Label) + three chips R50/R100/R200 (196 × 144 each, gaps 20, `Sunken` +
  `BorderControl`, icon `pouch_add`), then "Take out of savings" + three chips (icon `pouch_remove`). A chip
  is disabled when unaffordable (no partial moves). The panel stays open; values count in place. No Mali
  line.
- Never shown: stress, level, XP (D7).

#### 5.4.7 Sleep confirm (sort 55)

1100 × 640 sheet: title "End Day {day}?" (Title), lines from §4.8 (Body, ≤ 5 lines), buttons **Not yet**
(secondary 360 × 144, left) and **Sleep** (primary 360 × 144, right). Back = Not yet.

#### 5.4.8 End-of-day reveal (`EndOfDayRevealView`, sort 60)

```
backdrop Inverse 100% (full bleed, outside the safe root)
+----------------------------- min(1840, safeWidth-80) x 1000, Paper -----------------------------+
| You started Day 5 with                         | What moved it                                    |
| R497   cash + savings        (104 u)           |  v  Geyser repair from savings          -R350    |
| You ended it with                              |  ^  Shift at work                       +R150    |
| R633   cash + savings        (104 u, counts)   |  ^  Loan from Bra K · R600 back Day 7   +R400    |  <- MoneyTransfer colour
| ( +R136 today · R400 borrowed )  (pill)        |  v  Half a kota                          -R30    |
| Cash R583 · Savings R50                        |  v  Taxi fares                           -R34    |
|                                                | Coming up                                        |
| New promise: Bra K R600 on Day 7               | Day 6 night: Speaker R130                        |
|                                                | Tomorrow: your aunt calls back.                  |
|------------------------------------------------------------------------------------------------- |
| (Mali 160) [Mali] The R400 from Bra K was the biggest change today. R600   [ On to Day 6 ]       |
|                   goes back on Day 7. Tomorrow night, Speaker takes R130.  (400 x 144)           |
+--------------------------------------------------------------------------------------------------+
```

(Example: the middle path's Day 5, profile `food` + `taxi`, if the player had also borrowed R400. The
geyser comes first because it is Day 5's first scenario and opens the shift.)

- Card radius 48, padding 40. Two columns: left 44% (757 u at 16:9), right 56% (963 u), gap 40. Bottom
  band 200 tall below a 24 u gap and a 2 u `BorderSubtle` divider.
- Left column (heights in brackets): "You started Day {d} with" Body muted (52) · amount DisplayAmount 104
  with "cash + savings" `Label` 36 `TextSecondary` beside it on the same baseline (112) · "You ended it
  with" (52) · amount 104 counting, same caption (112) · 16 gap · delta pill 80 tall, HudValue
  `TextPrimary` on `Sunken` (neutral, D4) · 12 gap · split line "Cash R{cash} · Savings R{savings}"
  Caption muted (44) · still-owed line, `Label` 36 `Attention`, only when `TotalArrears > 0`: "Still owed:
  R{owed}" + " (R{owedBefore} this morning)" when it differs, where owedBefore = the previous closed day's
  `owedAtClose` (0 on Day 1) (44) · new promises, `Label` 36 `TextSecondary`, from
  `ObligationLedger.NewPromises(data, day)`, at most 2 lines (88). Total ≤ 612 of 696.
- Right column: "What moved it" Label Bold (44); rows 70 tall: arrow sprite 40 (`arrowUp` `MoneyIn` /
  `arrowDown` `MoneyOut` / `arrow_horizontal` `MoneyTransfer`), label Body SemiBold, amount LedgerValue
  right-aligned (`Signed`, coloured by kind; transfers read "R120 to savings" / "R199 from savings").
  Chronological. **Display grouping** (events stay separate in the record): all `bank:to_savings` events
  of the day form one row "Moved to savings" at the position of the first, likewise `bank:from_savings`
  "Taken out of savings". **Loans** (category Loan, kind In): arrow `arrowUp` and amount tinted
  `MoneyTransfer`, not `MoneyIn`, and the label gets " · R{repay} back Day {due}" from the matching
  obligation (or from the choice's instalment fields if the obligation is already gone). At most 7 rows;
  if more, rows 1–6 then "Other ({n})" with the summed total of the rest. Empty: Caption "Nothing moved
  today." Then 20 gap, "Coming up" Label Bold (44) and up to 2 lines `Label` 36 **Bold** (the role's
  weight, §5.1; each one line in 940 u, §3.1) (88), the first two that
  exist of: the first item of `ObligationLedger.DueOnNight(data, day + 1)` ("Day {n} night: {shortLabel}
  R{amt}"); `ChapterSchedule.FollowUpTeaser(id)` for a follow-up due on day + 1 (either call-back: "Tomorrow: your
  aunt calls back."); the teaser (§3.1). Built by `RevealLineBuilder.ComingUp` (WP7). Total ≤ 686 of 696.
- Bottom band: Mali portrait 160, gold tag, line (Body 40, ≤ 3 lines, width = band − 160 − 24 − 440;
  overflow per §4.3) typing at the text speed; primary button "On to Day {n}" / "On to payday" 400 × 144
  at the right.
- **Sequence:** 0 ms backdrop fades in (`Backdrop`) and the view calls
  `GameEvents.RaiseSoundRequested("day_end_chime")` itself (so a reveal rebuilt after a restart chimes
  too); 400 card rises (`SheetIn`); 700 "You started…" + start amount fade in; 900 rows stagger
  (`Stagger`, no sound per row); after the last row: "You ended it with" + count-up from start to end
  total (`Count`); +700 delta pill, split, still owed and new promises fade in; +200 Mali's line types;
  when typed: Coming up and the button fade in. ≈ 4 s for 3 rows. **Any tap before the end jumps to the
  final state** (line fully typed); the button then continues. 300 ms input lockout on open. Back = tap.
  Reduce motion: everything appears at once.
- Data: built only from the persisted `DayRecord`, obligations and stats (`DayCycle.Rebuild`, §7.3), so a
  fresh reveal and one rebuilt after the app was killed are identical.
- Never on this screen: XP, level, stress, grades, "great day", red flashes, sad sounds.

#### 5.4.9 Chapter end (`ChapterEndView`, sort 60), four screens

```
Screen A (the week)                                   Screen B (noticed)       Screen C (plan)          Screen D (close)
+---------------+-----------------------------------+ +--------+-------------+ +-------------------+ +-----------------+
| (MaliWave on  | Seven days to payday              | |MaliWave| What Mali   | | Tomorrow is payday| | (MaliWave)      |
|  gold circle) | On Day 1 you had  R1 000 cash+sav | |        | noticed     | | If you could ...  | | [Mali] Got it.  |
|               | Tonight you have  R515  cash+sav  | |        | line 1      | | ( ) When pay ...  | | Next time ...   |
|               | ( -R485 this week ) Cash · Sav    | |        | line 2      | | ( ) ...           | | Chapter 2 ...   |
|               | Still owed going into payday: R0  | |        | (line 3)    | | ( ) ...           | | [Live the week  |
|               | Already promised for payday: R399 | |        |             | | ( ) ...           | |  again]         |
|               |   Stokvel R200 · Gym R199         | |        |             | | [Not now] [Save]  | |                 |
|               | What moved it most                | |        |      [Next] | |                   | |                 |
|               | [Day 5 Geyser][Day 6 Neighb][D2 ] | +--------+-------------+ +-------------------+ +-----------------+
|               | Bills and repayments: -R986 [Next]|
+---------------+-----------------------------------+
```

- Full screen `Paper`, 40 u margins inside the safe area. Left 34%: `MaliWave` 640 u tall on a `Gold` 25%
  circle (diameter 720). Right column ≈ 1 187 u wide at 16:9.
- **Screen A** right column, top to bottom (height budget, total ≤ 1 000 = 1 080 − 2 × 40):
  DisplayTitle (80) · 16 gap · "On Day 1 you had" Body label + DisplayAmount 104 + "cash + savings"
  `Label` 36 beside it (112) · "Tonight you have" row, same (112) · delta pill 72 tall ("±R this week",
  neutral) with the split "Cash R{cash} · Savings R{savings}" Caption beside it (72) · 16 gap ·
  "Still owed going into payday: R{owed}" Body 40, amount in `Attention`, omitted when 0 (52) ·
  "Already promised for payday: R{sum}" Body 40 (52) and under it the list from
  `ObligationLedger.Promised`, "{shortLabel} R{amt}" joined with " · ", Caption 32 (40); both omitted when
  nothing is promised · 20 gap · "What moved it most" Label Bold (44) · 12 gap · up to three cards
  360 × 200 (`Card`, radius 36; "Day {d}" Caption, label Label Bold ≤ 2 lines, amount HudValue coloured
  by kind, loans in `MoneyTransfer`), chosen per §4.4 (200) · 12 gap · bottom row: "Bills and repayments:
  −R{amt}" Body 40 left (≤ 704 u; "−R1 650", the most the chapter can now charge (rent, airtime, speaker, Bra K, e-hailing fares, day bundles), measures 554 u, as does "−R9 999") and **Next** (primary 320 × 144) right (144). Sum 984. The
  still-owed and promised lines are the same size as the body text next to the total, never a footnote.
- **Screen B — What Mali noticed:** Title "What Mali noticed" (Title 56), then the 2–3 lines from
  `ChapterReflection.Noticed`, Dialogue 46 SemiBold, each its own paragraph (30 gap), typed one after the
  other at the text speed (tap completes); **Next** (primary 320 × 144) bottom-right once typed.
- **Screen C — payday plan:** four plan cards (full right-column width, 144 tall, radio state = `Tint`
  fill + 6 u `AccentPrimary` (#087A18) ring + a checkmark icon 48 u; plan-card text inset 96 u); **Save my plan** (primary, disabled until one is picked) and **Not now** (text
  button).
- **Screen D — close:** Mali line (Dialogue box style inline), caption, **Live the week again** (primary
  480 × 144).
- No screen ever shows a previous run's total.
- Audio: the view calls `GameEvents.RaiseSoundRequested("day_end_chime")` when screen A appears (no
  separate chapter jingle).

#### 5.4.10 Pause (`PauseMenuView`, sort 70)

1500 × 760 sheet, scrim 55%, `Time.timeScale = 0` while open (UI runs on unscaled time). Padding 48.
- Top row (144): title "Paused" (Title) left; **Resume** (primary 360 × 144) right.
- 30 gap, then two columns 660 u wide each, 84 apart:
  - Left: Sound (toggle, icons `audioOn`/`audioOff`), Music (toggle, `musicOn`/`musicOff`), Reduce motion
    (toggle); rows 144 tall, 20 apart (472).
  - Right: "Text speed" Label 36 (44), 12 gap, segmented control of three 200 × 144 segments (Slow /
    Normal / Instant); at the bottom, aligned with the left column's last row, **Start over…**
    (secondary 360 × 144, text `Attention`, `BorderControl` outline).
- Height: 48 + 144 + 30 + 472 + 48 = 742 ≤ 760.
- Toggle: 120 × 72 track (on `AccentPrimary`, off `Sunken` + outline) with "On"/"Off" text beside it; the
  whole 660 × 144 row is the hit target. Reset confirm: 1000 × 600 sheet at sort 75 (§4.8 copy).

#### 5.4.11 Coach mark (`FirstRunGuide`, sort 80)

Scrim 55% drawn as four rects around **one** target rect (+24 u padding) and a 6 u `Coin` ring around the
hole; the hole passes input through to the control underneath. Bubble 900 wide, `Warm`, radius 36: Mali
120 portrait, Body 40 line, buttons **Got it** (primary 240 × 144) and **Skip tips** (text 240 × 144).
The bubble sits above the target if the target is in the lower half, else below; clamped to the safe area.
Hidden (alpha 0, not blocking) whenever `UiModal.IsAnyOpen` (§4.7).

#### 5.4.12 Notice banner (sort 90)

Top-centre, 24 u below the safe top, pill 96 tall, `Inverse`, Body `TextOnInverse`, `information` icon 48.
Shows 6 s or until tapped.

#### 5.4.13 Character creation (`CharacterCreationUI`, sort 0)

- Backdrop `Inverse` full bleed; sheet 1500 × 880 `Paper` centred; step dots (5 circles 20 u, current
  `AccentPrimary`, others `BorderSubtle`) at the top-centre of the sheet, 40 u from its top; Title 56 at
  (60, −60) on screens 2 and 4. Text width inside the sheet: 1 380 u.
- **Screen 1 (promise + name, A4):** the promise in `DisplayTitle` 72 Black `TextPrimary`, two lines
  (1 041 and 1 014 u, line height 79), top at 90 u; 40 gap; the question "What should we call you?" Body
  40 Bold `TextSecondary` (52); 12 gap; name input 900 × 144, Body 46, `Sunken` fill + 4 u
  `BorderControl`, placeholder `TextMuted`. Bottom of the field at 496 u, clear of the buttons (top 696 u).
- Look: left preview 560 × 560 `RawImage` showing a 512 × 512 `RenderTexture` from a dedicated camera
  pointed at a model instance created from `PlayerCharacterCatalog.characterModelPrefab` at world
  (0, −500, 0), skin from `CreateRuntimeSkinMaterial`, rotating 20°/s (static when reduce motion). The
  model is the bare FBX, so `LookPreview` adds an `Animator` with `catalog.animatorController` (idle, no
  T-pose). In `OnDestroy` it destroys the runtime skin material and the model instance and calls
  `RenderTexture.Release()` + `Destroy` on the texture. The CharacterCreation scene, like the world, uses
  `Renderer2D` with only a `Light2D`; the preview camera needs no 3D light, so don't add one. Six cards
  246 × 160 in a 3 × 2 grid (gaps 20; row 1 the feminine build in light/medium/deep, row 2 the masculine
  build), each with a 48 u circle swatch of its tone and "Style {n}" Label Bold. If the catalog or model
  is missing: no preview, cards only.
- **Screen 3 (your money, A3):** two rows, each a question in Body 40 Bold `TextPrimary` (52; 689 and
  607 u) and, 12 u under it, four cards 330 × 144 (gaps 20; 4 × 330 + 3 × 20 = 1 380), `Card`, radius 36,
  card shadow, label Label 36 Bold centred, ≤ 2 lines in 250 u ("Data, airtime and going out" 215 + 237 u,
  "Own or shared car" 247 + 54 u). Selected = `Tint` fill + 6 u `AccentPrimary` (#087A18) ring + a checkmark
  icon 36 u (as the plan cards, whose check is 48 u), one per row. Vertical budget: dots 80 · question 1 52 · 12 · cards 144 · 30 · question 2 52 · 12 · cards 144 · 24
  · summary 2 lines (Body 40 SemiBold "We've built your week around where your money goes." 1 004 u, then
  the places in Label 36 Bold `TextSecondary`, ≤ 846 u: 96) = 646 ≤ 696 (button top). The summary fades in
  (`Fade`) when both rows have a pick and updates if a pick changes; no icons on the cards.
- Goal: four cards 560 × 220 (2×2): title Label Bold, "R{target}" HudValue, caption "R400 saved so far".
- Mali: portrait 520 left, text Dialogue 46 typing; **Let's go** primary 400 × 144.
- **Back** (secondary) / **Next** (primary) 320 × 144 at the bottom corners of the sheet (40 u margin);
  screen 1 has no Back.

---

## 6. Assets

All copies are **from** `C:\Temp\MaliGoAssetsStaging\` unless stated, **to** the given path; every new
file and folder gets a `.meta` with a fresh GUID (`python -c "import uuid;print(uuid.uuid4().hex)"`).
Templates: `.cs` → two lines like `Assets/Scripts/UI/MobileInputBridge.cs.meta`; folders → like
`Assets/Resources/MaliGoUI.meta`; PNG → copy `Assets/Resources/MaliGoUI/panel_brown.png.meta` and then:
new guid, `internalIDToNameTable: []`, `spriteMode: 1`, in `spriteSheet` set `sprites: []` and
`nameFileIdTable: {}` (the source has `spriteMode: 2` and a `panel_brown_0` entry that must not be
copied), `maxTextureSize: 1024` (icons 128); fonts and audio → templates in §6.5. Code loads textures
with `Resources.Load<Texture2D>` and makes sprites itself, so importer type is not critical; mipmaps must
be off (UI) or on (skin textures, §6.3b).

**Shared folders and Git LFS (WP2 owns both).**
- WP2 creates every shared folder and its `.meta`: `Assets/Resources/MaliGo`, `Assets/Resources/MaliGo/Audio`
  (empty, for WP4), `Assets/Resources/MaliGo/{Fonts,Icons,Mali,Skins}`, `Assets/MaliGo/Licenses`. WP4 only
  adds files inside `Audio/` and `Licenses/` and never writes those folder metas (two packages writing the
  same folder meta with different GUIDs is a merge conflict). WP4 owns `Assets/MaliGo/Branding` alone.
- `.gitattributes` routes `*.otf`, `*.ttf`, `*.ogg`, `*.mp3` through LFS, and the account's LFS budget is
  used up (commit 47e4c844). Before any font or audio file is added, WP2 removes those four `lfs` lines
  and appends `*.ttf`, `*.otf`, `*.ogg`, `*.mp3` with `!text !filter !merge !diff` under the existing
  PNG/PDF exception block (same approach and comment style), then checks
  `git check-attr filter -- a.ttf a.ogg a.mp3` prints `unspecified`.

### 6.1 Fonts (WP2)

| Source | Destination |
|---|---|
| `dotcolon_aileron\Aileron-SemiBold.otf` → `tools/convert_fonts.py` | `Assets/Resources/MaliGo/Fonts/Aileron-SemiBold.ttf` |
| `dotcolon_aileron\Aileron-Bold.otf` → converted | `Assets/Resources/MaliGo/Fonts/Aileron-Bold.ttf` |
| `dotcolon_aileron\Aileron-Black.otf` → converted | `Assets/Resources/MaliGo/Fonts/Aileron-Black.ttf` |
| `dotcolon_aileron\License.txt` | `Assets/MaliGo/Licenses/Aileron-License.txt` |

`tools/convert_fonts.py` (WP2) converts CFF → TrueType outlines (§5.2 rule 5), drops `CFF `/`VORG`, builds
`glyf`/`loca`, sets `maxp` 1.0, `post` format 2 and sfnt version 0x00010000, and prints for each output
the glyph count, the cmap size (337) and whether U+2212 is mapped. Loaded as
`Resources.Load<Font>("MaliGo/Fonts/Aileron-Bold")` (dynamic font) with the §5.2 rule 6 self-test. No OFL
fonts (D10). The same script (or `tools/make_font_metrics.py`) writes `tools/logic_tests/Generated/
AileronMetrics.cs` (§2.5).

### 6.2 Icons (WP2) → `Assets/Resources/MaliGo/Icons/<same name>.png`

| From | Files | Use |
|---|---|---|
| `kenney_game-icons\PNG\White\2x\` (100 px) | `down.png`, `checkmark.png`, `cross.png`, `pause.png`, `home.png`, `phone.png`, `shoppingBasket.png`, `wrench.png`, `information.png`, `audioOn.png`, `audioOff.png`, `musicOn.png`, `musicOff.png`, `arrowUp.png`, `arrowDown.png`, `arrowRight.png` | continue cue, last page, close, pause, Home/Home-category, phone scenarios, shopping/food, Work, notice, sound, music, in/out arrows, the HUD Today pill's arrow (A4; verified on disk 4 Oct) |
| `kenney_game-icons-expansion\PNG\White\2x\` (100 px) | `coin.png`, `car.png`, `flag.png` | cash, transport, goal |
| `kenney_board-game-icons\PNG\Double (128px)\` | `pouch.png`, `pouch_add.png`, `pouch_remove.png`, `notepad.png`, `hourglass.png`, `token_give.png`, `hand_token.png`, `tokens_stack.png`, `arrow_horizontal.png` | savings / Bank prompt, move to savings, take out, bills, sleep/day, family, loan, stokvel, transfer arrow |
| licences | `kenney_game-icons\license.txt`, `kenney_game-icons-expansion\license.txt`, `kenney_board-game-icons\License.txt` | `Assets/MaliGo/Licenses/kenney_<pack>-License.txt` |

All white; tinted at runtime. Rendered ≥ 40 u. Never used: `star`, `trophy`, `medal*`, `leaderboards*`,
`dollar`, `warning` (verdict-like). Procedural: energy bolt (§5.3). No Kenney panel or button PNGs are
imported (D10).

### 6.3 Mali art (WP2, script `tools/prepare_mali_art.py`, PIL)

The founder approved the Mali artwork (`mali2.png` and the waving Mali) for use inside the app and for
the app icon (A5, closes E4). It is brand art, not CC0, so it stays out of any asset-pack licence file.

| Output | Source | Preparation |
|---|---|---|
| `Assets/Resources/MaliGo/Mali/MaliPortrait.png` | `mali2.png` from the React Native app's `assets/images/` (534 × 615 RGBA), or a local copy of `mali2.png` (MD5 `09c1e7891c32a254d60a32d528337a47`); verify the MD5 before use. | Alpha is clean (body alpha 254). Crop the alpha bbox (93, 63)–(433, 536) padded 16 px → (77, 47)–(449, 552) (372 × 505); scale uniformly to 512 tall (377 × 512, Lanczos); paste centred horizontally, bottom-aligned, on a 512 × 512 transparent canvas. |
| `Assets/Resources/MaliGo/Mali/MaliWave.png` | `Assets/MaliGo Pitch Deck.png` (779 × 779 RGBA, full-body wave) | Crop alpha bbox (168, 75)–(620, 683) padded 16 → (152, 59)–(636, 699) = 484 × 640; paste centred on a 512 × 640 transparent canvas. The original file stays where it is (it is not under Resources, so it never ships). |
| — | `Assets/Mali Dumbfound.png` | **Not used** (mirrored "R" on the coin). |

### 6.3b Skin tones (WP2, script `tools/make_skin_tones.py`, PIL)

The four stock skins share one light peach skin (base colour (245, 140, 106) with a gradient of shades).
The script writes six recoloured copies of the two skater skins, which stay CC0:

| Input (`Assets/kenney_animated-characters-protagonists/Skins/`) | Outputs (`Assets/Resources/MaliGo/Skins/`) |
|---|---|
| `skaterFemaleA.png` | `skaterFemaleA_light.png`, `skaterFemaleA_medium.png`, `skaterFemaleA_deep.png` |
| `skaterMaleA.png` | `skaterMaleA_light.png`, `skaterMaleA_medium.png`, `skaterMaleA_deep.png` |

- Skin pixels: R ∈ [243, 245], G ∈ [115, 155], B ∈ [85, 122] (the face/hands gradient and the ear, nose
  and lip accents; checked on 3 Oct: shirt colours such as (242, 101, 76) and (228, 120, 62) fall outside).
- New colour per channel = round(target × pixel / base), clamped to 255, base (245, 140, 106); alpha kept.
  Targets: light (198, 140, 100), medium (150, 96, 62), deep (96, 60, 40).
- The script prints the number of pixels changed per file and writes `tools/out/skin_preview.png`
  (all six side by side, not committed; WP1 adds `tools/out/` to `.gitignore` with its other lines) for the human check H3 (§10).
- `.meta`: PNG template, `textureType: 0`, `maxTextureSize: 1024`, **mipmaps on**, `sRGBTexture: 1`.

### 6.4 Audio (WP4) → `Assets/Resources/MaliGo/Audio/`

| Destination | Source | Bytes | Role |
|---|---|---|---|
| `ui_click.ogg` | `kenney_interface-sounds\Audio\click_001.ogg` | 4 876 | every kit button |
| `mali_cue.ogg` | `kenney_interface-sounds\Audio\glass_001.ogg` | 6 103 | Mali starts a line |
| `money_moved.ogg` | `kenney_rpg-audio\Audio\handleCoins2.ogg` | 13 094 | any money event, same sound in and out |
| `day_end_chime.ogg` | `kenney_music-jingles\Audio\Steel jingles\jingles_STEEL01.ogg` | 21 019 | reveal and chapter end |
| `new_day.ogg` | `kenney_music-jingles\Audio\Pizzicato jingles\jingles_PIZZI01.ogg` | 16 579 | `DayStarted` |
| `music_calm.ogg` | `oga_feel-good-slow-ambient_annandistance\ambienttrack.ogg` | 1 861 922 | music loop (CC and world) |
| `ambience_township.mp3` | `freesound_soweto-township-day_jackaTTackeditor\soweto-township-day-ambience.mp3` | 2 381 256 | world ambience bed |
| **Total** | | **4 304 849** | < 6 MB |

Licences: each source folder's `License.txt`/`license.txt` → `Assets/MaliGo/Licenses/<folder>-License.txt`.
All CC0 (asset-manifest §2). No trimming tool is installed (no ffmpeg), so files are imported as-is and
the loop seam is handled at runtime (§7.11). A person must audition all seven before the APK goes to
testers (§10).

### 6.5 Import-setting templates

Font `.meta`:
```
fileFormatVersion: 2
guid: <new>
TrueTypeFontImporter:
  externalObjects: {}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontNames:
  - Aileron
  fallbackFontReferences: []
  customCharacters: 
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```
Audio `.meta` (music and ambience: `loadType: 2` Streaming; short clips: `loadType: 0`, `preloadAudioData: 1`):
```
fileFormatVersion: 2
guid: <new>
AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.5
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 1
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```
Unity rewrites these on first import; only the GUID and the listed values matter.

### 6.6 App icon (WP4, script `tools/make_app_icon.py`, PIL)

| Output (not in Resources) | Spec |
|---|---|
| `Assets/MaliGo/Branding/AppIcon_Background.png` | 1024 × 1024 solid `#FBF6EC` |
| `Assets/MaliGo/Branding/AppIcon_Foreground.png` | 1024 × 1024 transparent; `mali2.png` cropped to (88, 58)–(438, 478) (head, scarf and coin), scaled to fit 640 × 640, centred at (512, 530), so the art stays inside the adaptive-icon safe circle (diameter 676 = 66%) |
| `Assets/MaliGo/Branding/AppIcon_Legacy.png` | foreground composited on the background, 1024 × 1024 opaque |

Texture `.meta`: copy of the PNG template, `textureType: 0`, `maxTextureSize: 1024`, mipmaps off.
`MaliGoAndroidSetup.Configure()` sets: `AndroidPlatformIconKind.Adaptive` (background + foreground),
`.Round` and `.Legacy` (Legacy png), all slots, via `PlayerSettings.GetPlatformIcons/SetPlatformIcons
(NamedBuildTarget.Android, kind, icons)`, loading textures with `AssetDatabase.LoadAssetAtPath<Texture2D>`.

---

## 7. Systems

### 7.1 Interaction arbitration (D12)

```csharp
// Assets/Scripts/World/IInteractable.cs
namespace MaliGo.World
{
    public enum InteractPriority { World = 0, Companion = 1 }
    public interface IInteractable
    {
        Vector3 InteractPosition { get; }
        float InteractRadius { get; }           // 0.7, Mali 0.74
        InteractPriority Priority { get; }
        bool IsAvailable { get; }               // e.g. spot has an active scenario; own panel closed
        bool IsEnabled { get; }                 // false = shown greyed with a reason (Work done / too tired)
        string PromptText { get; }
        string ActionVerb { get; }              // "Open", "Look", "Work", "Talk"
        string PromptIcon { get; }              // icon name in Resources/MaliGo/Icons, or "mali"
        void Interact();                        // called when IsEnabled
        void OnDisabledTap();                   // called instead when !IsEnabled: shows the reason line
                                                // (Work: §4.2.2); a no-op for everything else
    }
}

// Assets/Scripts/World/InteractionArbiter.cs  (MonoBehaviour singleton on MaliGo_Systems)
public class InteractionArbiter : MonoBehaviour
{
    public static InteractionArbiter Instance { get; }
    public static InteractionArbiter Ensure(GameObject host);
    public static void Register(IInteractable i);   // static registry: works before any arbiter exists
    public static void Unregister(IInteractable i);
    public static void RequestInteract();          // touch: action button or prompt tap
    public IInteractable Current { get; }
    public event Action<IInteractable> CurrentChanged;
}
```

**Registry and scene loads.** The registry is a **static** list on `InteractionArbiter`, not instance state:
scene objects such as `MaliCompanionInteraction` run `OnEnable` before the bootstrap's `sceneLoaded`
handler creates the arbiter, so `Register` must work with no instance. `Register` ignores duplicates.
Every `IInteractable` MonoBehaviour calls `Register` in `OnEnable` and **`Unregister` in `OnDisable`**.
`Update` first removes Unity-null entries (`(i as UnityEngine.Object) == null`), so a destroyed object
left behind by a scene load is never a candidate. The request frame lives on the instance (reset in
`Awake`), so a tap from the previous scene never fires in the next. All statics are also reset in a
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method (Editor
play mode without domain reload). `UiModal` does the same.

Every frame (`Update`):
1. If `UiModal.IsAnyOpen` or no player → `Current = null`.
2. Else candidates = registered, `IsAvailable`, flat distance to the player ≤ `InteractRadius`.
   Pick the nearest `World` candidate. If none, pick Mali only if the player has been still (flat speed
   < 0.05 u/s) for ≥ 0.6 s.
3. Input: `requested = (Time.frameCount − requestFrame ≤ 1)` or (`Keyboard.current != null &&
   Keyboard.current.eKey.wasPressedThisFrame`). If requested and `Current != null`: `Current.IsEnabled ?
   Current.Interact() : Current.OnDisabledTap()`. The request is cleared after this check whether or
   not anything consumed it (fixes the stale-flag bug, systems R3).
**Work** (`WorkInteraction`, WP5) reads `WorkRules.State(data, gate)` every frame, with `gate =
MaliGoFeatures.ChapterSchedule ? ChapterSchedule.GateScenario(data.currentDay, data.spendingProfile.focus) :
null`: Open → enabled, "Take a shift (+R150)"; NotYet → disabled, "Shift opens after {gate}" (the gate
scenario's `gateNoun`, §4.8); Done / Tired → disabled, as before. `OnDisabledTap` shows the matching §4.2.2
line. The prompt is visible in every state, so the player can always find Work and read why it is shut.
`MobileInputBridge.RequestInteract()` forwards to `InteractionArbiter.RequestInteract()` (kept for
compatibility). `ScenarioTrigger`, `ProximityInteraction` and `MaliCompanionInteraction` stop polling
input and stop building prompt canvases; they implement `IInteractable`, register in `OnEnable` and
unregister in `OnDisable`.

### 7.2 Modals, back button, movement gate

```csharp
// Assets/Scripts/UI/Kit/UiModal.cs
public static class UiModal
{
    public static void Push(object owner, Action onBack);   // onBack null = back ignored for this modal
    public static void Pop(object owner);
    public static bool IsAnyOpen { get; }
    public static object Top { get; }
    public static event Action BackWithNoModal;             // pause opens on this
    public static event Action Changed;
    public static void ClearAll();                          // empties the stack, raises Changed
    public static void EnsureRouter();                      // creates a DontDestroyOnLoad router that
                                                            // polls Keyboard.current.escapeKey (Android back)
}
```
Back: top modal's `onBack`, else `BackWithNoModal`. The router reads back as `Keyboard.current != null &&
Keyboard.current.escapeKey.wasPressedThisFrame` (on Android the back key arrives as Escape). Modals:
blocking Mali box, choice sheet, Home/Bank, sleep confirm, reveal, chapter end, pause, reset confirm. Not
modal: compact Mali box, coach marks, prompt, HUD. `MaliGoPlayerController.GetMovementInput()` returns
`Vector2.zero` while `UiModal.IsAnyOpen`.

**The stack never outlives a scene.** "Live the week again" loads the world while the chapter end is
pushed, and Start over loads CharacterCreation while Pause and the reset confirm are pushed; a stale
entry would leave `IsAnyOpen` true for good (no prompt, no movement, controls hidden). Three rules:
1. `Pop(owner)` removes that owner wherever it is in the stack (a no-op if absent) and raises `Changed`.
2. Every modal view calls `UiModal.Pop(this)` in **both** `OnDisable` and `OnDestroy`.
3. The router subscribes to `SceneManager.sceneLoaded` and calls `ClearAll()` (and sets
   `Time.timeScale = 1`). Because the bootstrap's own `sceneLoaded` handler may run first and create
   views, **no view pushes in `Awake`/`OnEnable`**: the earliest a view may push is `Start` (the pending
   reveal and the chapter end are shown from `DayFlowController.Start`, §7.3).

### 7.3 Day cycle

```csharp
// Assets/Scripts/Economy/DayCycle.cs (pure, namespace MaliGo.Economy)
public class NightResult
{
    public int endedDay;
    public DayRecord record;                 // the closed day
    public ObligationSettlement settlement;  // what the night charged; NULL when rebuilt from the save
    public bool chapterComplete;
    public int newDay;                       // == endedDay when chapterComplete
}
public static class DayCycle
{
    /// Null (and no change) if today is already closed, the chapter is complete, or
    /// today.day != currentDay. Otherwise, in this order:
    /// 1. settlement = ObligationLedger.SettleDue(data, data.currentDay)  // D6: due Day N -> night of Day N
    /// 2. record = MoneyRecorder.CloseDay(data); record.owedAtClose = ObligationLedger.TotalArrears(data)
    ///    (set on both data.today and the copy appended to chapter.days)
    /// 3. data.revealPendingForDay = endedDay
    /// 4. if currentDay >= ChapterConfig.ChapterLength: chapter.complete = true (currentDay stays 7)
    ///    else: currentDay += 1; energy = 100; MoneyRecorder.OpenDay(data)
    public static NightResult EndDay(PlayerData data);

    /// Rebuilds the result of an earlier night from the save alone: record = the last chapter.days entry
    /// with day == endedDay, settlement = null, chapterComplete = data.chapter.complete && endedDay >=
    /// ChapterConfig.ChapterLength, newDay = chapterComplete ? endedDay : endedDay + 1. Null if no record.
    /// Changes nothing.
    public static NightResult Rebuild(PlayerData data, int endedDay);
}
// Assets/Scripts/Economy/ChapterFlow.cs (pure)
public static class ChapterFlow
{
    /// New run: cash 600, savings 400, energy 100, stress 25, behaviour scores 0.5, obligations =
    /// defaults (airtime Day 2, rent Day 3), completedScenarioIds empty, followUps empty, currentDay 1,
    /// lastWorkedDay 0, revealPendingForDay 0, morningLineDay 0, chapter = { chapterNumber 1, runNumber,
    /// startCash 600, startSavings 400 }, today opened, saveVersion = 2. Keeps name, appearance, goals,
    /// paydayPlanId/Text, hasMetMali and spendingProfile.
    public static void StartChapter(PlayerData data, int runNumber);
    public static void RestartChapter(PlayerData data);   // StartChapter(data, chapter.runNumber + 1)
}
// Assets/Scripts/Economy/WorkRules.cs, BankRules.cs (pure)
public enum ShiftState { Open, NotYet, Tired, Done }
public static class WorkRules
{
    public static bool HasWorkedToday(PlayerData d);
    public static bool HasEnergy(PlayerData d);           // energy >= 60
    /// A1. gateScenarioId = today's first scheduled scenario (ChapterSchedule.GateScenario, passed in by the
    /// caller so WP1 does not depend on WP3), or null/empty when there is no gate (schedule flag off).
    /// First that applies: Done (worked today) > NotYet (gate not in completedScenarioIds) > Tired
    /// (energy < 60) > Open.
    public static ShiftState State(PlayerData d, string gateScenarioId);
    public static MoneyEvent DoShift(PlayerData d, string gateScenarioId); // null unless State == Open; +150 via
                                                          // recorder, energy -60, xp +5, lastWorkedDay, shiftsWorked++
}
public static class BankRules
{
    public static readonly float[] Amounts = { 50f, 100f, 200f };
    public static MoneyEvent MoveToSavings(PlayerData d, float amount); // null if cash < amount
    public static MoneyEvent TakeOut(PlayerData d, float amount);       // null if savings < amount
}

// Assets/Scripts/Core/GameEvents.cs (pure static events, namespace MaliGo.Core; subscribers MUST
// unsubscribe in OnDestroy and MUST NOT call UpdatePlayerData/Save synchronously, §2.4)
public static class GameEvents
{
    public static event Action<MoneyEvent> MoneyChanged;   // raised by FlushMoneyChanged, after the mutator
                                                           // has finished and the save is written (§2.4)
    public static void QueueMoneyChanged(MoneyEvent e);    // called only by MoneyRecorder.Apply
    public static void FlushMoneyChanged();                // called by PlayerDataManager.UpdatePlayerData (in a
                                                           // finally block, so a throwing mutator still empties
                                                           // the queue) and by tests
    public static event Action SleepRequested;             // sleep confirm -> DayFlowController
    public static event Action<NightResult> NightEnded;    // after EndDay, before the reveal (no sound hook)
    public static event Action<int> DayStarted;            // once per in-game day (see morningLineDay below)
    public static event Action ChapterCompleted;
    public static event Action PauseRequested;             // HUD pause button
    public static event Action MaliSpoke;                  // Mali starts any line (audio cue)
    public static event Action<string> SoundRequested;     // a view asks for a one-off clip by name
    public static void RaiseSleepRequested();              // ...and one Raise method per event
}
```

**`DayFlowController`** (MonoBehaviour, `MaliGo.World`, WP7) owns the sequence:
1. On `SleepRequested`: ignore if `busy`; `busy = true`. `UpdatePlayerData(d => result = DayCycle.EndDay(d),
   saveImmediately: true)`. If `result == null` → `busy = false`, stop (double-trigger guard, with the
   closed-day check in `EndDay`). Raise `NightEnded`.
2. If `MaliGoFeatures.EndOfDayReveal`: show the reveal for `result`. Else (plain default): a blocking Mali
   line "Day {d} is done. You started with R{start} and ended with R{end}." via the reveal view's plain mode.
3. On dismiss: `revealPendingForDay = 0`, save. Then read **`data.chapter.complete`** (never `result`,
   which does not exist after a restart): if true → raise `ChapterCompleted`, show the chapter end. Else
   `StartDay(currentDay)`. `busy = false`.
4. On world load (`Start`, never earlier, §7.2): if `revealPendingForDay > 0` → `night =
   DayCycle.Rebuild(data, revealPendingForDay)` and show that reveal (if `night == null`: clear
   `revealPendingForDay`, save, continue below); else if `chapter.complete` → chapter end (screen A; the
   world always shows it until "Live the week again"); else if `morningLineDay != currentDay` → after
   1.0 s `StartDay(currentDay)`; else nothing (a reopened app on a day that has started gets no morning
   line, no jingle and no plan quote again).
5. `StartDay(n)`: `UpdatePlayerData(d => d.morningLineDay = n, saveImmediately: true)`, then raise
   `DayStarted(n)`. This is the only place `DayStarted` is raised, so the morning line, the replay's plan
   quote (§4.2.4) and the new-day jingle happen once per in-game day.
6. Chapter end "Live the week again": `ChapterFlow.RestartChapter` (which replaces `chapter`, so
   `complete` is false again and `morningLineDay` is 0), save, `SceneManager.LoadScene("MaliGoWorld")`. The
   modal stack is cleared by the load (§7.2).

**Every reveal and Mali rule works with `settlement == null`.** `RevealLineBuilder`, the reveal view and
`ChapterReflection` read only `chapter.days` (including `owedAtClose`), `today`, obligations and stats;
`settlement` may be used for nothing that is not also in the record. A fresh reveal and one rebuilt after
the app was killed are therefore identical (WP7 test: build both for the same night and compare).

### 7.4 HUD binding

`HudView` subscribes to `PlayerDataManager.OnPlayerDataChanged` (refresh all values) and
`GameEvents.MoneyChanged` (delta tags). Day pill from `currentDay`; Cash/Savings from stats;
progress = `goal.Progress(savings)`; Energy; Bill pill = `HudCopy.BillPill(data)` →
`(line1, line2, attention)` (`Assets/Scripts/Copy/HudCopy.cs`, pure, `MaliGo.Copy`, WP7), built only from
the §2.4 helpers: `LargestArrears` if any ("{shortLabel} owed" / "R{owed}", attention), else `NextDue`
("{shortLabel}" / "R{amt} · {when}"), else "No bills" / "till payday" (§5.4.1, §4.9). `HudCopy.DayPill(data)`
gives "Day {n} of 7" / "Payday in {n} days" or "Payday tomorrow". `HudCopy.TodayPill(data)` → `(line1,
nowText)`: "Today from R{today.StartTotal}" and "R{cash + savings}" (A4; refreshed on every
`OnPlayerDataChanged`, counted like the other values). Pause → `GameEvents.RaisePauseRequested()`. Built in code; the scene
`MaliGo_Canvas` is hidden at runtime (`SetActive(false)`) by the bootstrap and `HUDController` is no longer
added (D9).

### 7.5 Schedule activation

Six `ScenarioTrigger`s, one per spot. Each frame (cheap) or on `OnPlayerDataChanged`, a trigger asks
`ChapterSchedule.ActiveScenarioAtSpot(data, spotId, MaliGoFeatures.ChapterSchedule,
data.spendingProfile.focus, data.followUps)`; null → unavailable. The definition comes from
`ScenarioLibrary.Get(id, focus, travel)` (§3.4), so prompt text, place label and amounts follow the
profile. With the flag off every scheduled scenario is active from Day 1 (spot queues still apply; follow-
ups still need their day) and the shift has no gate. The chapter still ends after Day 7.

### 7.6 Scenario resolution

`ScenarioManager.TryBeginScenario(ScenarioDefinition)` (signature kept): refuse if in progress, completed,
or `ScenarioOutcome.Availability` is NotEnough for every choice (cannot happen, §3.4). Opens the choice
sheet (Mali's intro is inside the sheet, not in the dialogue box). On a choice:
`ScenarioOutcome.Apply(data, s, c)` inside `UpdatePlayerData(..., saveImmediately: true)`:

```csharp
// Assets/Scripts/Scenarios/ScenarioOutcome.cs (pure)
public enum ChoiceAvailability { Available, NotEnoughCash, NotEnoughSavings }
public static class ScenarioOutcome
{
    public static ChoiceAvailability Availability(PlayerData d, ScenarioChoice c);
    /// False (no change) if not Available or the scenario is completed. Otherwise: money via
    /// MoneyRecorder (only if a delta is non-zero), energy clamp 0..100, stress clamp 0..100, xp += 5,
    /// behaviour lerp + profile words, completedScenarioIds += id, chapter.choices += record,
    /// instalments -> Obligation { id "<scenarioId>_<choiceId>", label instalmentLabel, shortLabel,
    /// category, kind, amount, intervalDays, nextDueDay = first = (firstDueDay > 0 ? firstDueDay :
    /// currentDay + interval), paymentsRemaining = LaterCount(d, c), createdDay = currentDay } when
    /// LaterCount > 0; follow-up -> appends ChapterSchedule.FollowUpKey(followUpScenarioId, currentDay +
    /// followUpAfterDays) to d.followUps when that day <= 7.
    public static bool Apply(PlayerData d, ScenarioDefinition s, ScenarioChoice c);
    /// count, or with instalmentLastDueDay > 0: payments due on first, first + interval, ... that are
    /// <= instalmentLastDueDay (at most count). Used by Apply, the Later column and {laterDays}.
    public static int LaterCount(PlayerData d, ScenarioChoice c);
    public static int[] LaterDays(PlayerData d, ScenarioChoice c);   // the due days themselves
    public static string LaterDaysWords(int[] days);                 // "Days 4 to 7", "Days 3 and 4", "Day 7"
    /// The reaction to show: maliReactionNoLater when instalments exist but LaterCount == 0 and the
    /// field is set, else maliReactionLine. Callers fill it with extra { laterDays }.
    public static string ReactionFor(PlayerData d, ScenarioChoice c);
}
```
`s` is always the profile's definition (`ScenarioLibrary.Get(id, focus, travel)`), which the trigger
passes to `TryBeginScenario`.
Then Mali's reaction (blocking, chip row from the deltas, tokens filled after the change).

### 7.7 Mali dialogue

```csharp
// MaliDialogueController (kept class, new members; existing signatures kept)
public enum MaliLineMode { Passing, Blocking }
public void Say(string text, MaliLineMode mode, Action onClosed = null, string[] chips = null);
public void ShowLine(string line);                         // = Say(line, Passing)
public void ShowFormatted(string template, params object[] args); // MaliText.Fill(template, data) for {0}/{name}
public void ShowGreeting();                                // MaliGreeting.Build -> Blocking
public bool IsShowingDialogue { get; }                     // kept
public bool IsBlocking { get; }
public void Hide();                                        // kept
public event Action<string> OnDialogueShown; public event Action OnDialogueHidden; // kept
```
Queue: blocking lines queue FIFO; a passing line replaces a passing line; a blocking line supersedes a
showing passing line. Subscribes to `GameEvents.DayStarted` → morning line (or §4.2.4 on a replay's Day 1).
`DayStarted` is raised once per in-game day (§7.3 step 5), so the morning line never repeats when the app
is reopened. Raises `GameEvents.MaliSpoke` on every line. Mali's 3D `TALK` state as today.
`ShowGreeting` calls `MaliGreeting.Build(data, MaliGoFeatures.ChapterSchedule, out extra)` (WP6, §4.2.1),
formats `extra["owed"]` with `MoneyFormat.Digits`, then `MaliText.Fill(template, data, extra)`. The morning
line is `ChapterSchedule.MorningLine(day, data.spendingProfile.focus)`. The legacy
`MaliContextualDialogueSelector.SelectLine` path at `MaliDialogueController.cs:60` is replaced by this.

### 7.8 Reveal and chapter end

Data: the reveal reads the `NightResult` from `EndDay` or `DayCycle.Rebuild` (its `record` is the
closed day; `settlement` may be null and is never needed, §7.3), obligations for Coming up and still owed
(§2.4 helpers), `RevealLineBuilder` for Mali, `RevealLineBuilder.ComingUp` (bill line, follow-up line,
`ChapterSchedule.TeaserForNight(day, focus)`, §5.4.8). The chapter end
reads `ChapterReflection.Summary(data)` (start/end totals, split, still owed, promised list, top-3
moments) and `ChapterReflection.Noticed(data)`. Both pure (`Assets/Scripts/Copy/`).

### 7.9 Pause

`PauseMenuView` opens on `GameEvents.PauseRequested` or `UiModal.BackWithNoModal`; back closes it.
`Time.timeScale = 0` while open (restored on close and in `OnDestroy`). Settings write `GameSettings`
immediately (sound/music apply live). Start over → confirm → `DeleteSave()` → load CharacterCreation.

### 7.10 First run

`FirstRunGuide` (only when `MaliGoFeatures.FirstRunGuide && !GameSettings.FirstRunDone`) runs §4.7 using
`UiAnchors` (`UiAnchors.Register(id, rect)`, `Unregister`, `TryGet(id, out RectTransform)`) and
`GameEvents.MoneyChanged`, `UiModal.Changed` and `MaliDialogueController.OnDialogueHidden`. It hides its
mark while `UiModal.IsAnyOpen` and re-evaluates on `UiModal.Changed` (§4.7). Each shown step sets its bit in
`maligo.firstRunSteps`; step 4 done or Skip → `firstRunDone = 1`.

### 7.11 Audio (D20)

`AudioManager` (`MaliGo.Sound`, `DontDestroyOnLoad` singleton, created by the bootstrap in both scenes
when `MaliGoFeatures.Audio`). On creation and on every `sceneLoaded`: if
`FindFirstObjectByType<AudioListener>() == null`, add one to `Camera.main` (MaliGoWorld has none).
Sources: 1 UI one-shot, **2 music** and **2 ambience** (each loop alternates between its pair for the
crossfade). Levels: SFX 0.8, music 0.30, ambience 0.35; × 1 or 0 from `GameSettings.SoundOn` / `MusicOn`
(ambience follows Sound). Music plays in both scenes; ambience only in MaliGoWorld. **Loop seam:** each
loop schedules its next copy on the other source of its pair with `AudioSource.PlayScheduled(dspStart)`,
where `dspStart = AudioSettings.dspTime` at the current copy's start + `clip.length − 2.0`, and fades the
pair over 2.0 s using `AudioSettings.dspTime` / `Time.unscaledDeltaTime` (never `Time.time`,
`Time.deltaTime` or `WaitForSeconds`): Pause sets `Time.timeScale = 0`, which would freeze a scaled
scheduler and let the music run out. The audio itself keeps playing while paused. If a person confirms
the music loops cleanly (H1), music may use one source with `loop = true` instead. Hooks:
`UiKit.ButtonClicked` → ui_click; `GameEvents.MaliSpoke` → mali_cue; `MoneyChanged` → money_moved (≥ 250 ms
apart); `GameEvents.SoundRequested(name)` → that clip (the reveal and chapter-end screen A request
`day_end_chime` themselves, §5.4.8/§5.4.9, so a reveal rebuilt after a restart chimes too; `NightEnded`
has **no** sound hook, so the chime never plays twice); `DayStarted` → new_day (once per day, §7.3).
Clips via `Resources.Load<AudioClip>("MaliGo/Audio/<name>")`; a missing clip is skipped silently.

### 7.12 Feature flags (D19)

```csharp
// Assets/Scripts/Core/MaliGoFeatures.cs (pure)
public static class MaliGoFeatures
{
    public static bool EndOfDayReveal  = true;  // off: plain Mali line instead of the reveal
    public static bool Typewriter      = true;  // off: text appears instantly
    public static bool Audio           = true;  // off: AudioManager not created
    public static bool ChapterSchedule = true;  // off: all scenarios active from Day 1
    public static bool FirstRunGuide   = true;  // off: no coach marks
    public static bool ProfileTaps     = true;  // off: CC skips screen 3; the default profile (food + taxi) is kept
}
```
Read by the bootstrap (Audio, FirstRunGuide), `DayFlowController` (reveal), dialogue (typewriter),
triggers and Work (schedule; with it off there is also no shift gate), character creation (ProfileTaps). Static fields, not consts, so a later A/B layer can set them before the bootstrap runs.

### 7.13 App lifecycle and packaging

- `AppLifecycle.Apply()` (bootstrap, both scenes): `Application.targetFrameRate = 60`,
  `Screen.sleepTimeout = SleepTimeout.NeverSleep`.
- `MaliGoAndroidSetup.Configure()`: version `0.2.0`, code `2` (always set, not "keep if set"); icons (§6.6);
  splash: `PlayerSettings.SplashScreen.showUnityLogo = false; PlayerSettings.SplashScreen.show = false;`
  inside try/catch, then read back and log `Splash shown: <bool>`; background colour `#0F5E2E` either way;
  remove `APP_UI_EDITOR_ONLY` from the Android and Standalone define symbols
  (`PlayerSettings.GetScriptingDefineSymbols`/`SetScriptingDefineSymbols`); `EditorBuildSettings.
  RemoveConfigObject("com.unity.dt.app-ui")`. Orientation, safe-area setting, URP renderer and quality
  levels unchanged.
- `Packages/manifest.json`: delete the `"com.unity.ai.inference": "2.3.0",` line.
  `Packages/packages-lock.json`: delete the `com.unity.ai.inference` and `com.unity.dt.app-ui` blocks
  (leave `com.unity.burst`/`com.unity.collections`; Unity re-resolves depths on the next open).
- `MaliGoBuildPipeline.BuildAndroidBeta`: delete an existing APK before building; on any result other
  than `Succeeded` log the error and, in batch mode, `EditorApplication.Exit(1)`. Remove the
  `MaliGoResourceBaker.BakeUiSprites()` call from `BakeAll` (keep `BakePlayerCharacterCatalog`), and
  delete `Assets/Resources/MaliGoUI/` with its five PNGs and metas once WP9 has removed every
  `KenneyUiSprites` use, so the Kenney adventure panels no longer ship (D10). Until then the folder stays.
- `BUILD_BETA_APK.bat`: **delete `Builds\Android\MaliGo-Beta.apk` itself before starting Unity** (so a Unity
  run that dies before `executeMethod` and still exits 0 cannot report a stale APK); `set RC=%ERRORLEVEL%`
  right after the Unity line; success only if `RC == 0` **and** the APK exists; on failure print the log
  hints and `exit /b 1` (after `pause`).
- Target SDK stays Auto (resolves to 36) with `androidPredictiveBackSupport: 0`. Whether the back key still
  reaches the game as Escape on Android 16 is checked on a phone (H5, §10); if it does not, a one-line
  change in `MaliGoAndroidSetup` pins `PlayerSettings.Android.targetSdkVersion` to 35.

### 7.14 Safe area (D11)

`SafeAreaFitter` (on the content root of every canvas): in `Update`, if `Screen.safeArea`, `Screen.width`,
`Screen.height` or `Screen.orientation` changed, set the RectTransform's anchors to the safe rect
normalised by the screen size (pixels, bottom-left origin). Full-bleed backdrops (reveal, CC, scrims) sit
outside the fitter. `androidRenderOutsideSafeArea` stays `1`. Character creation and every runtime canvas
use it (via `UiCanvasFactory.Create`).

### 7.15 Save robustness

§2.6. Plus: `PlayerDataManager` handles `OnApplicationPause(true)` and `OnApplicationQuit` → `Save()`.

---

## 8. Work packages

**Rules for every package**
- Branch from `agents/beta-finish` (it contains `sam/core-loop-u1-u3`). `TEAM_TASKS.md` does not exist;
  don't look for it. Don't commit unless told to; don't edit `.unity` scenes or `ProjectSettings/`.
- Only touch the files your package owns. **Never remove or rename a public member** that other code
  uses; obsolete members are removed only by WP9.
- New `.cs` files and folders get `.meta` files (§6). No folder or namespace called `Time`, `Audio`
  (use `Sound`) or the same as a class in it. Shared folders under `Assets/Resources/MaliGo` and
  `Assets/MaliGo/Licenses` are created (with their metas) by WP2 only (§6).
- Use the namespaces in the table below; don't invent others. `MaliGo.Environment` already exists, so
  inside any `MaliGo.*` namespace write `System.Environment`, never `Environment.X`.
- Acceptance for every package: `python tools/compile_check.py --root <your checkout>` → 0 errors
  (baseline today: 0). Packages touching `Assets/Editor` also run `--editor`. Logic packages run
  `dotnet run --project tools/logic_tests` → all pass (stage 1 packages other than WP1 run it once WP1 is
  merged).
- UI timing on unscaled time; static event subscribers unsubscribe in `OnDestroy` and never write the
  save from a handler (§2.4); null-safe when `PlayerDataManager.Instance` is missing; modal views follow
  §7.2 (push from `Start` at the earliest, `Pop` in `OnDisable` and `OnDestroy`).
- Build agents cannot run the Unity Editor (8 GB laptop, MASTER §2/§10). Acceptance is compile + logic
  tests + inspection; anything that needs Play mode or a phone is a human task in §10 (H1–H8).
- Merge order inside a stage does not matter, except that WP2 is merged before WP4's audio files are
  staged (Git LFS, §6).

**Namespaces**

| Namespace | Types |
|---|---|
| `MaliGo.Data` | everything in `Assets/MaliGo/Data/` (existing, plus `MoneyEvent`, `MoneyEventKind`, `DayRecord`, `ChoiceRecord`, `ChapterRecord`, `GoalPreset(s)`, `PaydayPlan(s)`, `ChapterConfig`, `SpendingProfile`, `CategoryShare`, `RecurringDebit`, `ProfileOption`, `SpendingFocus`, `TravelMode`, `SpendingProfileSource`, `SpendingProfiles`, `OnboardingCopy`) |
| `MaliGo.Economy` | `Assets/Scripts/Economy/`: `MoneyRecorder`, `MoneyCategory`, `MoneyFormat`, `DayCycle`, `NightResult`, `ChapterFlow`, `WorkRules`, `ShiftState`, `BankRules`, `ObligationLedger` (existing), `DueItem` |
| `MaliGo.Core` | `Assets/Scripts/Core/`: `GameEvents`, `MaliGoFeatures`, `MaliText` |
| `MaliGo.Scenarios` | existing, plus `ChapterSchedule`, `ScenarioOutcome`, `ChoiceAvailability` |
| `MaliGo.Dialogue` | existing (`MaliDialogueLibrary`, `MaliContextualDialogueSelector`), plus `MaliGreeting` (WP6) |
| `MaliGo.Copy` | `Assets/Scripts/Copy/`: `RevealLineBuilder`, `ChapterReflection`, `ChapterSummary`, `HudCopy` |
| `MaliGo.UI.Kit` | `Assets/Scripts/UI/Kit/`: `UiTheme`, `UiFonts`, `UiFontWeight`, `UiKit`, `UiTween`, `UiTextLayout`, `UiCanvasFactory`, `SafeAreaFitter`, `UiModal`, `UiAnchors` |
| `MaliGo.UI` | existing, plus `HudView`, `EndOfDayRevealView`, `ChapterEndView`, `WorldPromptView`, `PauseMenuView`, `FirstRunGuide`, `NoticeBanner`, `LookPreview` |
| `MaliGo.World` | existing, plus `IInteractable`, `InteractPriority`, `InteractionArbiter`, `SleepConfirmView`, `DayFlowController` |
| `MaliGo.Settings` | `GameSettings` |
| `MaliGo.Sound` | `AudioManager` |
| `MaliGo.App` | `AppLifecycle` |
| `MaliGo.Characters`, `MaliGo.PlayerIdentity` | existing only (`PlayerCharacterCatalog` is in `MaliGo.PlayerIdentity` although its file is under `Assets/MaliGo/Characters/`) |
| global | `MaliGoPlayerController` stays in the global namespace (it adds `using MaliGo.UI.Kit;`) |

### Stage 1 (no dependencies between these)

**WP1 — Core money logic and save** (pure logic + save manager)
- Goal: one recorder for all money, the day/chapter cycle, the shared bill helpers, the save version and
  the test harness.
- Creates: `Assets/MaliGo/Data/{MoneyEvent,DayRecord,ChapterRecord,GoalPresets,PaydayPlans,ChapterConfig,SpendingProfile,OnboardingCopy}.cs`;
  `Assets/Scripts/Economy/{MoneyRecorder,MoneyCategory,MoneyFormat,DayCycle,ChapterFlow,WorkRules,BankRules}.cs`;
  `Assets/Scripts/Core/{GameEvents,MaliGoFeatures,MaliText}.cs` (+ folder meta); `tools/logic_tests/
  {LogicTests.csproj,UnityShim.cs,Program.cs,Tests/CoreTests.cs}` and an empty `tools/logic_tests/Generated/`
  (`.gitkeep`).
- Modifies: `Assets/MaliGo/Data/{PlayerData,FinancialGoal,Obligation,ObligationDefaults}.cs` (incl.
  `spendingProfile`, `followUps`, the "repeat" kind, the airtime label "Airtime" and the airtime category
  "Phone & data"; rent stays "Bills"),
  `Assets/Scripts/Economy/ObligationLedger.cs` (recorder call + §2.4 helpers),
  `Assets/Scripts/PlayerIdentity/PlayerDataManager.cs` (`ShouldReset` in `TryLoad`, atomic write, `.bak`,
  pause/quit save, `FlushMoneyChanged` after the save, `WasResetForUpdate`, `DeleteSave`), `.gitignore`
  (`tools/logic_tests/bin/`, `tools/logic_tests/obj/`, `tools/out/`), and **one interim edit** to
  `Assets/Scripts/World/HomeInteraction.cs`: the body of `SleepAndEndDay`'s mutator becomes
  `result = DayCycle.EndDay(data)` and the existing night line is fed `result?.settlement`. Without it,
  the old order (increment the day, then settle) makes every bill fail the recorder's precondition until
  WP5 lands. WP5 then replaces the whole flow.
- Exposes: everything in §2.3 (incl. `PlayerData.ShouldReset`), §2.4 (incl. `DueOnNight`, `NextDue`,
  `Promised`, `NewPromises`, `LargestArrears`), §2.7 (`SpendingProfile` and its option tables,
  `SpendingProfiles.SetFromOnboarding`), `OnboardingCopy` (const strings: `PromiseLine1` "Live the week before
  payday.", `PromiseLine2` "See where your money goes.", `NameQuestion`, `SpendQuestion` "Where does most of your
  money go?", `TravelQuestion` "How do you usually get around?", `WeekBuiltLine` "We've built your week around
  where your money goes."), §7.3 (`DayCycle.EndDay/Rebuild`, `NightResult`, `ChapterFlow`, `WorkRules` incl.
  `ShiftState`, `State(d, gate)`, `DoShift(d, gate)`, `BankRules`, `GameEvents`), §7.12, `MoneyFormat.Rand(float)`, `MoneyFormat.Signed(float)`,
  `MoneyFormat.Digits(float)`, `MaliText.Fill(string template, PlayerData data, IDictionary<string,string>
  extra = null)`, `PlayerDataManager.WasResetForUpdate`, `PlayerDataManager.DeleteSave()` (also deletes
  .bak/.tmp).
- Consumes: nothing new.
- Tests (`CoreTests`): recorder invariant over 200 random applies; `Apply` refuses a negative result and
  returns null; `Apply` with `today.day != currentDay` returns null and opens nothing; kinds
  In/Out/Transfer; `MoneyChanged` is not raised until `FlushMoneyChanged`; bill timing (nights 1/2/3
  charge 0/60/500); arrears paid and carried; a zero-cash night records no event but sets `owedAtClose`;
  pay-later chosen Day 2 charged nights 4 and 6; loan Day 5 charged night 7 before completion; chapter
  completes after night 7 with 7 continuous records and `currentDay == 7`; second `EndDay` on a closed day
  returns null; `Rebuild(d, n)` returns the same record, `chapterComplete` and `newDay` as `EndDay` did,
  with `settlement == null`; `DueOnNight`/`NextDue`/`Promised`/`NewPromises` on constructed states (incl.
  a Day-8 commitment counted as promised, never as due); `ShouldReset(saveVersion 0/1) == true`, `(2) ==
  false`; `MoneyFormat` cases (0→"R0", 5→"R5", 1250→"R1 250", 12450→"R12 450", 1000000→"R1 000 000",
  −45→"−R45", +150→"+R150", 49.5→"R50"); work needs 60 energy, once per day; bank refuses partial moves;
  `StartChapter` values (incl. `morningLineDay == 0`); `RestartChapter` keeps name/goal/plan and bumps
  `runNumber`; `MaliText.Fill` tokens incl. `{paydayWhen}` and `{friend}`. **Revision 3:** a new
  `SpendingProfile` is `food`/`taxi`/source `default`/version 1 with empty reserved arrays; `Normalize` maps
  each of the four focus ids and four travel ids to itself and anything else (null, "", "FOOD ", "bus") to
  the default; `SetFromOnboarding` for all 16 combinations stores both ids, source `onboarding` and the
  time; `SpendingFocus.Categories` for each of the four ids (§2.7); `StartChapter` keeps the profile and
  empties `followUps`; `CreateNew` gives the default profile; `WorkRules.State` is NotYet before the gate
  is completed (also when energy is 100), Open after it, Tired below 60, Done after a shift, and never
  NotYet with a null/empty gate; `DoShift` returns null and changes nothing unless Open; a "repeat"
  obligation is charged nightly like an instalment.

**WP2 — UI kit, settings, fonts, icons, Mali art, skins**
- Goal: everything other packages need to draw the new UI and the six looks.
- Creates: `Assets/Scripts/UI/Kit/{UiTheme,UiFonts,UiKit,UiTween,UiTextLayout,UiCanvasFactory,SafeAreaFitter,
  UiModal,UiAnchors}.cs`; `Assets/Scripts/Settings/GameSettings.cs`; every shared folder and its meta (§6);
  assets §6.1, §6.2, §6.3, §6.3b with metas and licences; `tools/prepare_mali_art.py`,
  `tools/convert_fonts.py`, `tools/make_font_metrics.py` (→ `tools/logic_tests/Generated/AileronMetrics.cs`),
  `tools/make_skin_tones.py`.
- Modifies: `.gitattributes` (**first**, before any font file is added: §6 LFS rule).
- Exposes: §5.1 tokens as `UiTheme` constants/colours; `UiFonts.Get(UiFontWeight)` with the §5.2 rule 6
  self-test; `UiKit` (`RoundedRect`, `SoftShadow`, `Circle`, `White`, `EnergyBolt`, `Icon(string name)`,
  `MaliPortrait`, `MaliWave`, `Panel(RectTransform parent, string name, Color c, float radius, bool shadow)`,
  `Label(...)`, `PrimaryButton/SecondaryButton/TextButton(RectTransform parent, string label, Action onClick,
  float w, float h = 144)`, `IconImage(...)`, `SetRadius(Image, float)`, `event Action ButtonClicked`; every
  sprite cached, §5.3); `UiTween.Fade/Move/Scale/CountUp/Delay` (unscaled, reduce-motion aware);
  `UiTextLayout.WrapKeepingAmounts(Text, string, float width)`, `UiTextLayout.Paginate(Text, string, float
  width, int maxLines)`; `UiCanvasFactory.Create(string name, int sortOrder, Transform parent, out
  RectTransform safeRoot)`; §7.2 `UiModal` (incl. `ClearAll`, the `sceneLoaded` router, owner-anywhere
  `Pop`); §7.10 `UiAnchors`; `GameSettings` (§2.6 keys; `SoundOn`, `MusicOn`, `TextSpeed`, `ReduceMotion`,
  `FirstRunDone`, `FirstRunSteps`, `static int CharsPerSecond`, `event Action Changed`); `AileronMetrics.Width`
  for tests.
- Revision 3: also imports `arrowRight.png` (§6.2) for the HUD Today pill.
- Acceptance: compile; `git check-attr filter -- a.ttf a.otf a.ogg a.mp3` prints `unspecified` for all
  four; `convert_fonts.py` prints 337 cmap entries and U+2212 mapped for each weight; `prepare_mali_art.py`
  prints the output sizes (512×512 and 512×640) and the source MD5; `make_skin_tones.py` prints the changed
  pixel count per file (non-zero) and writes the preview; every file in §6.1–6.3b exists under Resources
  with a `.meta`; PNG metas follow the §6 template (no `internalIDToNameTable` entries).

**WP3 — Scenario content, schedule and Mali's words**
- Goal: the chapter's content exactly as §3, for every spending profile, and Mali's fixed lines.
- Modifies: `Assets/MaliGo/Scenarios/{ScenarioChoice,ScenarioDefinition,ScenarioLibrary}.cs`,
  `Assets/MaliGo/Dialogue/MaliDialogueLibrary.cs` (delete every entry except `first_meeting`, rewritten to
  §4.2.1; this removes the judging lines and `default_greeting_student`).
- Creates: `Assets/MaliGo/Scenarios/ChapterSchedule.cs`, `tools/logic_tests/Tests/ContentTests.cs`.
  (`MaliGreeting` and the selector change moved to WP6 in Revision 3: the greeting needs WP1's
  `WorkRules.State` and `Obligation.shortLabel`, which a stage-1 package cannot use.)
- Exposes: new fields (§3.4, incl. `gateNoun`, `isFollowUp`, `instalmentLastDueDay`, `maliReactionNoLater`,
  `followUpScenarioId`, `followUpAfterDays`, `followUpLaterText`) with defaults (`instalmentIntervalDays = 2`,
  others empty/0/false); ids as `ScenarioLibrary` constants for all 16 scenarios (13 scheduled for every
  focus, `kota_run` for the food focus (Revision 4) and the 2 follow-ups, `family_callback` and `family_callback_full`); `ScenarioLibrary.Get(id, focus, travel)` (§3.4.0 variants, place names), `GetById`, `AllIds`;
  §3.1 `ChapterSchedule` with the Revision 3 signatures (profile and follow-ups as plain arguments).
- Consumes: only existing types (no WP1 types; uses literal category strings from §2.2, literal 7/8, and
  writes out the focus/travel ids and their defaults itself).
- Tests (`ContentTests`): 16 scenarios; 2–4 choices each; every choice xp = 5; always-available rule;
  schedule lists each id exactly once; spot queues match §3.3; all strings pass the glyph whitelist; the
  full §4.3 banned list absent from every reaction, intro, situation, morning line, greeting and every
  `MaliDialogueLibrary.AllEntries` line; `MaliDialogueLibrary.AllEntries` has exactly one entry; ledger
  labels ≤ 28 chars and present whenever a delta is non-zero; choice labels ≤ 44 chars, situations ≤ 190,
  reactions ≤ 150 (cheap guards; the real fit tests with font metrics are in WP6/WP7); `{name}` in at most
  one reaction per scenario; only known tokens; categories are §2.2 strings. **Every profile option
  (Revision 3):** for each of the 4 foci: the 13 scheduled ids each appear exactly once over Days 1–7 and
  `family_callback`/`family_callback_full` never; `send_part` names `family_callback` and `cant_this_week`
  names `family_callback_full`; **gate first in the spot queue (§3.3):** with `food_decision` carried to
  Day 2, `ActiveScenarioAtSpot(CORNER)` = `data_runs_out`; Day 2 starts with `data_runs_out`, Day 3 with `taxi_fare_rise`,
  `mashonisa_offer` is on Day 5 and Day 7 is `debit_order_check` alone; the gates of Days 1–3 are needs
  with a free option using ≥ 45 energy; `MorningLine` and `TeaserForNight` exist for Days 1–7; each focus's
  Day 1 holds its own scenario (food, transport, group chat, family). For each of the 4 travel modes:
  `Get` keeps the choice ids of `GetById`; trip R30–R50 and daily R34–R50; lift club R120, ride R90, walk
  energy 45/50; the walk option is first only for `walk`. For all 16 profiles: every spot has a place
  name and label; `WeekPlaces` matches §3.3; no `[place]`, `[trip]` or `[daily]` left in any string; the
  **fair-choice rule** (§3.2: no non-decline option at least as good as every other on money over the
  week, cash today, savings today, energy, Later entries; decline ids as listed); **no brand names**: a
  whole-word, case-insensitive list (Uber, Bolt, Shoprite, Checkers, Pick n Pay, Spar, Woolworths, KFC,
  Nando's, Steers, Vodacom, MTN, Telkom, Cell C, Absa, Capitec, FNB, Nedbank, Standard Bank, Takealot,
  PEP, Mr Price, Engen, Shell, Sasol, Caltex, Virgin Active, Planet Fitness) is absent from every
  player-facing string. `FollowUpKey`/`TryParseFollowUp` round-trip; `ActiveScenarioIds` makes a
  follow-up active on its day (not before), after the scheduled scenarios at its spot, and never makes
  one active that is not in `followUps`; `GateScenario(d, focus)` = first of `ScenariosForDay`.

**WP4 — Packaging, app icon, audio files**
- Goal: a trustworthy 0.2.0 (code 2) build that installs over the testers' 1.0 (code 1) build.
- Modifies: `Assets/Editor/MaliGoAndroidSetup.cs`, `Assets/Editor/MaliGoBuildPipeline.cs`,
  `Assets/Editor/MaliGoResourceBaker.cs` (stop baking the Kenney UI sprites, §7.13), `BUILD_BETA_APK.bat`,
  `Packages/manifest.json`, `Packages/packages-lock.json`.
- Creates: `tools/make_app_icon.py`, `Assets/MaliGo/Branding/` (folder meta) and its PNGs (§6.6), the audio
  files §6.4 inside WP2's `Audio/` folder and their licences inside WP2's `Licenses/` folder (file metas
  only, never those folder metas). The audio files are added only once WP2's `.gitattributes` change is in
  the checkout (merge WP2 first): `git check-attr filter -- Assets/Resources/MaliGo/Audio/ui_click.ogg`
  must print `unspecified` before anything is staged.
- Acceptance: compile with `--editor`; manifest/lock contain no `ai.inference` or `dt.app-ui`; grep shows no
  `Unity.InferenceEngine`/`Unity.AppUI` usage; the .bat deletes the old APK first and returns 1 when the
  APK is missing; icon PNGs are 1024² with the foreground inside the safe circle (script prints the bbox);
  audio source total < 6 MB (the in-APK size is H4).

### Stage 2 (consume stage 1; no file shared between these four)

**WP5 — World interaction, locations, controls**
- Goal: one prompt, one tap target, the right thing happens (D12); Home/Bank/Work on the recorder.
- Creates: `Assets/Scripts/World/{IInteractable,InteractionArbiter}.cs`, `Assets/Scripts/UI/WorldPromptView.cs`,
  `Assets/Scripts/World/SleepConfirmView.cs`.
- Modifies: `Assets/Scripts/World/{ProximityInteraction,HomeInteraction,BankInteraction,WorkInteraction,
  WorldLocationWiring}.cs`, `Assets/Scripts/Scenarios/{ScenarioTrigger,ScenarioWorldWiring}.cs`,
  `Assets/Scripts/Characters/MaliCompanionInteraction.cs`, `Assets/Scripts/UI/{MobileControlsUI,
  MobileInputBridge,ActionPanelUI}.cs`, `Assets/Scripts/MaliGoPlayerController.cs`,
  `Assets/Scripts/PlayerIdentity/PlayerIdentityBridge.cs` (destroy the previous runtime material; reapply
  only when `appearance` changed). Revision 3: `WorkInteraction` uses `WorkRules.State/DoShift(d, gate)`
  (§7.1) and `ScenarioTrigger` passes the profile and follow-ups to `ChapterSchedule` and builds the
  definition with `ScenarioLibrary.Get(id, focus, travel)` (§7.5).
- Exposes: §7.1 (static registry, `OnDisabledTap`); `ScenarioWorldWiring.EnsureAllScenarioTriggers()` (now
  six spot triggers, §3.3); `WorldLocationWiring.EnsureLocations()` (unchanged signature);
  `ActionPanelUI.Show(...)` keeps its signature plus `ShowRows(string title, Func<IReadOnlyList<(string
  label, string value, bool attention)>> rows, IReadOnlyList<ActionButton> actions)`; `WorldPromptView.Ensure()`.
- Consumes: WP1 (`WorkRules`, `BankRules`, `GameEvents.RaiseSleepRequested`, `ChapterConfig`, the §2.4
  helpers for the Home sheet and the sleep confirm), WP2 (kit, `UiModal`, `UiAnchors`), WP3
  (`ChapterSchedule`, scenario fields), existing `ScenarioManager.TryBeginScenario` and
  `MaliDialogueController.ShowGreeting/ShowFormatted`.
- Acceptance (compile + inspection, stated in the report): every `IInteractable` registers in `OnEnable` and
  unregisters in `OnDisable`; the arbiter skips Unity-null entries; one `Current` at a time; Mali only when
  standing still with nothing else near; `Keyboard.current` null-checked; the request is cleared every
  frame; a disabled Work prompt calls `OnDisabledTap` (§4.2.2 lines); on **Sleep** the confirm and the Home
  sheet both close (and pop) before `SleepRequested` is raised; movement and controls gated on
  `UiModal.IsAnyOpen`; no "Press E" string anywhere; the Home sheet and sleep confirm use only
  `DueOnNight`/`NextDue`/`LargestArrears`; Work shows "Shift opens after {gate}" until today's gate
  scenario is completed and its tap shows the NotYet line (§4.2.2); no `ChapterSchedule` call without
  `data.spendingProfile.focus` and `data.followUps`. Play-mode checks are H2.

**WP6 — Mali dialogue and the choice sheet**
- Goal: D13 and D14.
- Modifies: `Assets/Scripts/Characters/{MaliDialogueController,MaliDialogueView}.cs`,
  `Assets/Scripts/Scenarios/{ScenarioManager,ScenarioChoiceUI}.cs`,
  `Assets/Scripts/Dialogue/MaliContextualDialogueSelector.cs` (keep it pure; it now delegates to
  `MaliGreeting`; moved here from WP3 in Revision 3).
- Creates: `Assets/Scripts/Scenarios/ScenarioOutcome.cs`, `Assets/Scripts/Dialogue/MaliGreeting.cs` (moved
  from WP3), `tools/logic_tests/Tests/ScenarioOutcomeTests.cs`.
- Exposes: §7.6 (incl. `LaterCount`, `LaterDays`, `LaterDaysWords`, `ReactionFor`), §7.7,
  `MaliGreeting.Build(PlayerData data, bool useSchedule, out Dictionary<string,string> extra) → string`
  (§4.2.1: `{places}`, `{count}` substituted; `{owed}`, `{label}`, `{gate}` returned raw in `extra`);
  anchor `mali.dialogue`.
- Consumes: WP1 (recorder, `MaliText`, `MoneyFormat`, `GameEvents`, `ChapterConfig`, `WorkRules.State`,
  `SpendingProfile`), WP2 (kit, `UiModal`, `UiTextLayout`, `GameSettings`, portrait, `AileronMetrics`), WP3
  (fields, `ScenarioLibrary.Get`, `ChapterSchedule`).
- Tests: every §3.4 choice applied to a fresh Day-N state gives the listed deltas exactly; unaffordable →
  `Apply` false and no change; instalment obligations have the §3.4 due days and `createdDay`; cold showers
  creates the Day-8 geyser commitment; the choice record is appended; `to_savings` is a transfer (total
  unchanged); **fit with `AileronMetrics`**: every choice label ≤ 2 lines of ChoiceLabel 44 Bold in its
  scenario's label box (373 u with a Savings column, 493 u without), every situation ≤ 6 lines of Body 40
  in 613 u, every Later small line within its Later column (200 u, or 190 u with a Savings column),
  every reaction ≤ 3 lines of Dialogue 46 in 1 180 u (16-character name). **Revision 3, for every travel
  mode and every focus:** each choice of `ScenarioLibrary.Get(id, focus, travel)` applied on its scheduled
  day gives the §3.4/§3.4.0 deltas; `LaterDays`: day bundles chosen Day 2 → [3, 4], Day 6 → [7], Day 7 →
  [] (no obligation, `ReactionFor` = the no-later line); the fare chosen Day 3 → [4, 5, 6, 7] ("Days 4 to
  7", Later small line "Days 4–7"), Day 5 → [6, 7]; pay-later chosen Day 2 → R130 on Days 4 and 6;
  `send_part` on Day 4 adds "family_callback@6" and `cant_this_week` on Day 4 adds
  "family_callback_full@6"; on Day 6 either adds nothing (Later "—"); neither follow-up sets off anything; Later column text for a follow-up is "Call back" / "on Day 6"; the
  greeting gives the NotYet suffix before the gate and not after; the fits above for every profile
  variant (labels, situations, reactions with `{laterDays}` = "Days 4 to 7"), and every scenario prompt
  of every profile ≤ 776 u (Body 40 Bold, §5.4.2).
- Acceptance: compile; 3-line pagination; first tap completes, second advances; passing lines auto-hide;
  no description strings, XP, stress or tags on the sheet; disabled cards say "Not enough cash".

**WP7 — HUD, day flow, reveal, chapter end**
- Goal: D9, D16, D17.
- Creates: `Assets/Scripts/UI/{HudView,EndOfDayRevealView,ChapterEndView}.cs`,
  `Assets/Scripts/World/DayFlowController.cs`, `Assets/Scripts/Copy/{RevealLineBuilder,ChapterReflection,
  HudCopy}.cs` (+ folder meta), `tools/logic_tests/Tests/CopyTests.cs`.
- Exposes: `HudView.Ensure()`, `DayFlowController.Ensure()`, `RevealLineBuilder.Build(PlayerData, NightResult)`,
  `RevealLineBuilder.ComingUp(PlayerData, int endedDay) → string[]` (bill line, follow-up line, teaser),
  `HudCopy.BillPill/DayPill/TodayPill`,
  `ChapterReflection.Summary(PlayerData) → ChapterSummary { startTotal, endTotal, endCash, endSavings,
  stillOwed, string[] promised, MoneyEvent[] top, int[] topDays, float billsTotal }`,
  `ChapterReflection.Noticed(PlayerData) → string[]`; anchors `hud.*`.
- Consumes: WP1 (incl. `SpendingFocus` for N0), WP2 (incl. `AileronMetrics` in tests, `arrowRight`), WP3
  (`TeaserForNight`, `MorningLine`, `FollowUpTeaser`, `GateScenario`, `gateNoun`), `PaydayPlans` (WP1).
- Tests (`CopyTests`): each A/B rule fires on a constructed day; tie-break; bank-move grouping; "Other (n)"
  beyond 7; every N rule (N7/N7b from `owedAtClose` only) and the fallbacks (F1 wording); never fewer than 2
  or more than 3 noticed lines; banned words absent; top-3 holds only `scenario:` events and excludes
  transfers; the bills line sums `bill:` events; `Build` gives the same line for a fresh `NightResult` and
  for `DayCycle.Rebuild` of the same night; **fit with `AileronMetrics`**: every `HudCopy` string (amounts
  up to R9 999, every shortLabel) in its §5.4.1 width, every morning line (name "Mmmmmmmmmmmmmmmm", the widest 16
  characters; Days 2–7 with the stretched prefix, Day 1 without it, §3.1) in 2 lines of 960 u, every
  teaser and every Coming-up line in one line of `Label` 36 Bold in 940 u, the reveal's still-owed and new-promise lines in
  the left column, the chapter-end bills line in 704 u. **Revision 3:** `TodayPill` gives "Today from
  R1 000" / "R1 100" for a Day-1 state after +R100, and line 1 fits 280 u up to R9 999; `ComingUp` puts
  "Tomorrow: your aunt calls back." second when either follow-up is due tomorrow and the teaser otherwise; N0
  fires for each of the four foci with the right categories and amount (e.g. `transport` middle path
  R200), and never when `source` is `default`; morning lines and teasers of **every focus** fit (above);
  "Shift opens after {gate}" for every `gateNoun` fits 776 u (Body 40 Bold) and the NotYet Mali line fits 2
  lines of 1 180 u; the bills line "Bills and repayments: −R9 999" fits 704 u.
- Acceptance (compile + inspection): double Sleep produces one reveal (`busy` + `EndDay` null); the pending
  reveal is shown from `Start` via `Rebuild`; the dismiss path reads `data.chapter.complete`; `DayStarted`
  only from `StartDay`; the reveal's numbers equal the DayRecord; total = cash + savings; transfers
  neutral; button text "On to payday" on Day 7. Kill-and-reopen on a phone is H2.

**WP8 — Shell: character creation, pause, first run, audio, lifecycle**
- Goal: D2, D3 notice, D18 (settings use), D20, D21, D22, D23 runtime part.
- Modifies: `Assets/Scripts/PlayerIdentity/CharacterCreationUI.cs` (rewrite; keep the class name),
  `Assets/MaliGo/Characters/PlayerCharacterCatalog.cs` (`ResolveSkin` tries the recoloured skins first,
  §4.6).
- Creates: `Assets/Scripts/UI/{PauseMenuView,FirstRunGuide,NoticeBanner,LookPreview}.cs`,
  `Assets/Scripts/Sound/AudioManager.cs`, `Assets/Scripts/App/AppLifecycle.cs` (+ folder metas; not under
  `Assets/Scripts/Core/`, which is reserved for pure code), `tools/logic_tests/Tests/ShellCopyTests.cs`.
- Exposes: `PauseMenuView.Ensure()`, `FirstRunGuide.Ensure()`, `AudioManager.Ensure()`, `AppLifecycle.Apply()`,
  `NoticeBanner.Show(string)`.
- Consumes: WP1 (`ChapterFlow.StartChapter`, `GoalPresets`, `GameEvents`, `WasResetForUpdate`, `MaliText`,
  `SpendingFocus`, `TravelMode`, `SpendingProfiles`, `OnboardingCopy`), WP2 (everything), WP3
  (`ChapterSchedule.WeekPlaces`), WP4 audio files (by Resources name only).
- Tasks to note: CC has five screens (§4.6, §5.4.13): the promise above the name field; screen 3's two
  rows of cards, the summary line from `WeekPlaces`, Next only when both rows have a pick, and
  `SpendingProfiles.SetFromOnboarding` on Next; **Let's go** sets `isCharacterCreated = true` and
  `hasMetMali = true`, runs `ChapterFlow.StartChapter(data, 1)` and saves (§4.6); six look cards (§4.6) and `LookPreview` per §5.4.13
  (animator applied, material/model/RenderTexture released in `OnDestroy`, no 3D light); Pause in two
  columns (§5.4.10); coach marks hide under any modal (§4.7); audio per §7.11 (two sources per loop,
  dspTime scheduling, `SoundRequested`).
- Tests (`ShellCopyTests`, Revision 3): each promise line fits one line of DisplayTitle 72 Black in
  1 380 u; both questions fit one line of Body 40 Bold in 1 380 u; every one of the 8 card labels fits 2
  lines of Label 36 Bold in 250 u; `WeekBuiltLine` fits one line of Body 40 SemiBold in 1 380 u; the
  joined `WeekPlaces` of all 16 profiles each fit one line of Label 36 Bold in 1 380 u; all strings pass
  the glyph whitelist and the brand list (WP3's list).
- Acceptance (compile + inspection): CC is 5 screens, no questions beyond the two profile taps, name
  required, the promise is the first text on screen 1, both profile rows required; old save →
  notice; reset → CharacterCreation with settings kept and `Time.timeScale` 1; back opens/closes pause;
  sound/music toggles stored in PlayerPrefs; coach marks once and skippable; a listener is added in
  MaliGoWorld; targetFrameRate 60.

### Stage 3

**WP9 — Integration (the only package that edits the bootstrap)**
- Goal: wire every system into both scenes, lock the economy numbers, remove what is obsolete.
- Modifies: `Assets/Scripts/PlayerIdentity/MaliGoIdentityRuntimeBootstrap.cs`; may delete obsolete members
  (e.g. `FinancialGoal.Deposit`, `KenneyUiSprites` and its uses) once nothing references them, then deletes
  `Assets/Resources/MaliGoUI/` (§7.13); may append members to `UnityShim.cs`.
- Creates: `tools/logic_tests/Tests/EconomySimTests.cs`: the six §3.2 play styles × the 16 profiles
  through `ScenarioLibrary.Get`, `ScenarioOutcome`, `WorkRules` (with the gate) and `DayCycle`, played the
  way `tools/sim_chapter.py` plays them (each day: the gate scenario, then the shift if the style works
  and it is Open, then the other active scenarios in `ActiveScenarioIds` order, then sleep); expected end
  totals = the §3.2 "Every profile" table (default profile: saver 854, always works 1 255, middle 515,
  never works 350, comfort 550, comfort + loan 350; Revision 4), the default-profile details of §3.2 (cash, savings,
  nights still owing, shifts), and the invariant every night. If the C# and the script disagree, the
  spec's content is the referee; fix whichever is wrong and rerun both.
- Bootstrap steps, world scene, in this order, each in `Step(label, ...)`: `app lifecycle`
  (`AppLifecycle.Apply`), `ui router` (`UiModal.EnsureRouter`), `hide scene HUD`
  (`GameObject.Find("MaliGo_Canvas")?.SetActive(false)`), `player spawner` (existing), `interaction`
  (`InteractionArbiter.Ensure(systems)`, `WorldPromptView.Ensure()`), `mobile controls` (existing), `game flow`
  (existing), `HUD` (`HudView.Ensure()`; the `HUDController` step is removed), `scenarios` (existing calls),
  `world locations` (existing), `day flow` (`DayFlowController.Ensure()`), `pause` (`PauseMenuView.Ensure()`),
  `audio` (if `MaliGoFeatures.Audio`: `AudioManager.Ensure()`), `first run` (if `MaliGoFeatures.FirstRunGuide
  && !GameSettings.FirstRunDone`: `FirstRunGuide.Ensure()`).
  CharacterCreation scene: `app lifecycle`, `ui router`, `audio`, then the existing redirect/CC logic.
- Acceptance: compile (runtime and `--editor`); all logic tests pass; `python tools/sim_chapter.py`
  exits 0; grep checks: no `Press E`, no `ACT`
  label, no `financialStress`/`Level`/`XP` in any UI string, no `KenneyUiSprites` in runtime code, no
  `.cash`/`.savings` writes outside the allowed files (§2.4), no `Environment.` without `System.`, no
  `#if !UNITY_EDITOR` logic, no emoji; a reviewer greps all player-facing text for the private-strategy
  rule (MASTER_PROMPT §6). The play-through checklist §1.1–1.3 at three resolutions is H2.

---

## 9. Resolution of every item in `phase1-critic.md`

Gaps
1. **End-of-day data model** → §2.2–2.5: `DayRecord`/`ChapterRecord`/`MoneyEvent`, two pools, transfers neutral, categories, Coming up = tomorrow night's charges + teaser (§4.3/§5.4.8), chapter top-3 by |total delta|, invariant + continuity tested outside Unity.
2. **Testability outside Unity** → §2.5: logic file set, a shim for the `Mathf`/`Debug` members listed there, `ScriptableObject.CreateInstance` and four attributes (frozen after stage 1 except WP9 appends); `FinancialStats`/`FinancialGoal`/`ObligationLedger` compile against it unchanged.
3. **Baseline compile state** → `tools/compile_check.py` reports 0 errors at the start of the build phase; every package re-runs it (§8 rules).
4. **No AudioListener / audio architecture** → §7.11: listener added to `Camera.main`, UI one-shot plus two sources each for music and ambience (dspTime-scheduled crossfade), levels, PlayerPrefs toggles. Zone-switched ambience (CBD clip at the rank) is deferred to Chapter 2: one bed is enough for a 17×13 world and keeps the budget.
5. **Replacing the scene HUD** → §7.4/WP9: `MaliGo_Canvas` hidden at runtime, `HudView` built in code, `HUDController` no longer added (file removable later).
6. **Settings/first-run persistence** → §2.6: PlayerPrefs keys survive a reset; the payday plan lives in `PlayerData` (it survives "Live the week again" and is wiped only by Start over).
7. **Save migration** → §2.6: `saveVersion` 2, older → fresh start with the notice; same id and key.
8. **Energy economy** → §3.2: shift needs and uses 60 of a daily 100 and opens only after the day's first scenario (Revision 3, A1); the free option of that scenario (45–50 energy on Days 1–3) costs the shift; energy blocks nothing else (stated plainly).
9. **Stress** → hidden (D7): no number anywhere; only Mali's "You seem stretched, {name}." at ≥ 60 (§4.2, §4.3).
10. **Goal model** → §2.1/§2.3: one player-chosen preset; target for savings; goal pot retired; Bank = transfers.
11. **Character creation vs fixed Chapter 1** → §4.6: promise and name, look, two spending-profile taps (Revision 3), goal, Mali; life stage stays default in data.
12. **Pacing vs geography** → §3.3 measured positions, ≥ 1.4 spacing, spot queues; walking is < 3 s between any two places; a day ≈ 2.5–4 min, the chapter ≈ 20–30 min (§1.2).
13. **Scoping new logic** → beta: instalments with absolute first due day (stokvel, gym), instalments capped at Day 7 (fares, day bundles), loans, carry-over schedule, a data-driven follow-up mechanic with two call-back scenarios (Revision 3, 3a), the spending profile from two taps (Revision 3). Later (Chapter 2): stokvel/grocery pot, conditional fees, cash-gated offers (mashonisa is always offered), dated calendar (HUD uses "Day N of 7"), free-text or adjustable plan amounts (presets only), Mali remembering specific earlier choices beyond the morning lines, reaction tokens and the plan quote.
14. **Feature flags** → §7.12 `MaliGoFeatures`.
15. **Reflection copy authoring** → §4.3 (A/B rules), §4.4 (N rules + fallbacks), §4.2 greetings; tested in `CopyTests`/`ContentTests`.
16. **Icons and glyphs** → §5.2 fontTools results; every missing glyph replaced by a named sprite (§6.2) or procedural shape. `LegacyRuntime.ttf` is built into Unity and not on disk to check; it is only a fallback and the same whitelist applies.
17. **Mali art preparation** → §6.3 (`MaliPortrait`, `MaliWave`, exact crops, MD5); Dumbfound unused.
18. **Package removal / settings needing the Editor** → §7.13: manifest and lock edited as text; the define symbol and config object are removed in `MaliGoAndroidSetup.Configure()` (code, runs in the headless build). QualitySettings Low and URP HDR are not changed (D23: leave the renderer; revisit only if the phone test shows poor frame rate).
19. **Branch and TEAM_TASKS.md** → noted in §8 rules.
20. **Notion OKRs unread** → Escalation E1.

Contradictions
1. **Headline number** → total = cash + savings; transfers neutral; commitments shown separately as "Already promised for payday"; no goal pot, no stokvel pot.
2. **Choice consequence text** → generated columns from deltas (ui-ux), description strings retired (overrides market B1).
3. **Font** → Aileron (CC0), plain-space grouping, U+2212 verified present.
4. **Procedural vs Kenney panels** → procedural; no Kenney panel/button PNGs imported.
5. **Reveal colour and glyphs** → ui-ux tokens (in green, out warm brown, transfer neutral, delta pill neutral), arrows as sprites (not glyphs), sentence case, no all-caps header.
6. **HUD content** → D9 list (§5.4.1): Day N of 7 + payday, the Today pill under it (A4), Cash, Savings with goal progress, Energy, next bill, Pause.
7. **Mock-up numbers** → every number in this spec comes from §0/§3; the ui-ux mock-ups (R350 shift, R1 200 rent, phone bill, "Eat at home") are not used.
8. **Bill timing off by one** → D6: due Day N = charged on the night ending Day N (settle before incrementing). Airtime night 2, rent night 3.
9. **Safe area approach** → `SafeAreaFitter` on every canvas reacting to changes; `androidRenderOutsideSafeArea` unchanged.
10. **Unit → dp conversion** → worst case: 144 u targets (≈ 48 dp at 2.0 density on 720p).
11. **Dialogue vs controls overlap** → sort orders §5.1; controls hidden under the blocking box and every modal; the compact box is sized to sit between the controls.
12. **Chapter-end art naming** → `MaliWave.png`; the "Pitch Deck" file never ships.
13. **Save cadence** → systems §2.2 lists every immediate save; plus pause/quit saves and atomic writes.

Unsupported claims
1. **ui-current-state line numbers** → treated as approximate; this spec's references were re-checked.
2. **"PDFs copied into docs/research"** → false; the 27 PDFs are tracked under `Behavioural Research/`. Not needed by builders.
3. **Market bill-night timing and hand economy check** → superseded by D6 and the scripted reference paths (§3.2, `EconomySimTests`).
4. **"Saturday has no shift pressure"** → there is no weekday concept; every day has one shift.
5. **0.40 dp per unit** → not relied on; sizes use the 144 u worst case.
6. **Target SDK ≥ 35** → Auto resolves to the highest installed (android-36); the fitter works whether or not edge-to-edge is enforced. The back key on Android 16 is checked on a phone (H5).
7. **AI package is the slowness/size cause** → treated as likely, not proven; WP4 removes it and the founder's build log will show the new size and time.
8. **renderOutsideSafeArea = 0 alone keeps UI clear** → not relied on (fitter).
9. **Audio role picks unauditioned; OTF dynamic font untested; 9-slice estimates** → audition is E3/H1; the OTFs are converted to TrueType and self-tested, falling back to LegacyRuntime (§5.2, H6); 9-slices are procedural (no estimates needed).
10. **Staged icon names unverified** → verified on disk 3 Oct (listed in §6.2); icons are rendered at ≥ 40 u (≈ 13+ dp, as glyph-like marks next to text, not tap targets).
11. **Statistics from weak sources** → no statistic appears in player-facing text; fares, fees and prices are in-game values.
12. **Motion values from search summaries** → adopted as design choices (§5.1), not cited as facts.
13. **R1/R10/R11 inferred** → fixed by design regardless (arbiter, joystick reset on pause/focus/hide, no emoji anywhere).
14. **HUD generator line ranges** → not relied on; the scene HUD is hidden, not edited.

---

## 10. Escalations (founder) and human tasks

### Escalations

E1. **Notion OKRs** are still unread (connector not authorised). They may change priorities; this spec
    assumes they don't.
E2. **Closed (Revision 3, A5).** Replaying the same week after Day 7 ("Live the week again") is the beta's
    ending; no Chapter 2 opener is needed for testers.
E3. **Moved to human task H1** (Revision 3). Nobody has listened to the seven audio files yet; the audition
    and the loop check happen before the APK goes out.
E4. **Closed (Revision 3, A5).** The founder approved the Mali artwork (`mali2.png`, the waving Mali) for use
    in the app and for the app icon (§6.3, §6.6).
E5. **Moved to human task H7** (Revision 3). The loan and family scenes (§3.4.7, §3.4.7b, §3.4.10) are
    written to be non-judging; the founder reads them before testers see them.
E6. **Closed (Revision 3, A1, option a).** The shift opens only after the day's first scenario is
    resolved (§0, §3.2, §7.3), and the free options of the gates on Days 1–3 use 45–50 energy, so the
    shift is at stake every day it can be. The simulation (§3.2) shows energy at shift time of 55/55/50 for
    the saver on Days 1–3 and the cost: three lost shifts and a short night on rent.
E7. **Closed (Revision 3, A2, "visible cost").** Every cheap option now pays something the card shows
    (§3.4 fair-choice table): energy that costs the shift, a Later entry (fares and day bundles for the rest
    of the week, R20 more for pay-later, a call-back from family, the geyser on payday), or going without a
    want. The lift club now wins over the week (R120 against R170) and loses on cash today, the 1GB bundle
    is the "nothing later" option against day bundles, pay-later costs R20 more than paying in full, and
    borrowing stays the costly side of Bra K's offer. Checked by `tools/sim_chapter.py` and `ContentTests`.

### Human tasks (agents cannot do these: no Editor alongside other work, no phone)

One owner per task. **Order:** H2, H3 (step 1, the Editor on the founder's laptop) → H4 (step 2, the
founder builds the APK) → H1, H5, H6, H8 (step 3, on a phone with that APK) → H7 (step 4) → APK to
testers. Ubayd owns the Editor work, Jaswin the phone work, the founder the build and the tone read.

| # | Order | Owner | Task | Needed before |
|---|---|---|---|---|
| H1 | 3 | Jaswin | Audition all seven §6.4 clips on headphones and on the phone speaker; say whether `music_calm` loops cleanly (if it does, music may use `loop = true`, §7.11) and whether the township bed sounds right (was E3) | APK to testers |
| H2 | 1 | Ubayd | Editor play-through on the founder's laptop: walk to each of the six spots, Home, Bank and Work (§3.3; if a collider blocks one, move it ≤ 0.3 along the road and record the offset in §3.3); the §1.1–1.3 checklist at 1920×1080, 2400×1080 and 1600×720 with a simulated cutout on each side; kill the app after sleeping and reopen (the pending reveal shows, identical); reopen on Day 1 of a replay (no second plan quote or jingle); Start over and Live the week again (the game stays playable: prompts, joystick, movement) | APK to testers |
| H3 | 1 | Ubayd | Look at `tools/out/skin_preview.png` and the six looks in the CC preview: the three tones read as light/medium/deep brown and no clothing colour changed | APK to testers |
| H4 | 2 | Founder | Build the APK for steps 3–4 and report from the headless build: APK size, size of the audio inside the APK (Unity re-encodes to Vorbis q0.5), build time, and the `Splash shown:` log line | APK to testers |
| H5 | 3 | Jaswin (needs an Android 16 phone; if Jaswin has none, it goes to whoever has one) | On an Android 16 phone: the back key opens/closes Pause and closes sheets (targetSdk 36, predictive back off; if not, pin targetSdk 35, §7.13); music and ambience crossfade without stutter (Streaming clips on two sources) | APK to testers |
| H6 | 3 | Jaswin | On the phone: Aileron renders in CC, HUD, sheets and the reveal (no boxes; `−` and `·` visible); logcat shows no `UiFonts` fallback warning; text is readable at arm's length on the smallest test phone | APK to testers |
| H7 | 4 | Founder | Read the family call (§3.4.7), both call-backs (§3.4.7b) and Bra K (§3.4.10) as a player would, for tone (was E5) | APK to testers |
| H8 | 3 | Jaswin | Play the first two days with two different profile picks (e.g. Food + Minibus taxi, Home and family + E-hailing): the places, the Day 1 order, the travel amounts and the CC summary line change as §2.7 says; the Work prompt reads "Shift opens after …" until the day's first scenario is done; the Today pill's "from" equals the reveal's start, and its bedtime total minus the sleep confirm's "Tonight" items equals the reveal's end | APK to testers |

---

## 11. Revision log

Revision 2 answers two reviews of revision 1: an engineering critique (EB = blocking, EI = important,
EM = minor) and a player/design critique (PB, PI, PM). A first reviser run applied part of this revision
and was cut off before §7–§11; this log covers both runs, and every number touched was re-checked across
the whole document. Text widths were measured with fontTools on the staged Aileron fonts.

### Engineering critique

| # | Finding | Outcome |
|---|---|---|
| EB1 | `UiModal`/arbiter static state survives scene loads; scene objects register before the arbiter exists | **Fixed.** §7.2: owner-anywhere `Pop`, every modal pops in `OnDisable`/`OnDestroy`, the router clears the stack (and resets `timeScale`) on `sceneLoaded`, and no view pushes before `Start`. §7.1: static registry, `Unregister` in `OnDisable`, Unity-null entries skipped, request frame per instance, statics reset at `SubsystemRegistration`. WP2/WP5 acceptance updated |
| EB2 | WP2 and WP4 both create `Assets/Resources/MaliGo` and `Licenses` folder metas | **Fixed.** §6 and §8: WP2 owns every shared folder meta (incl. an empty `Audio/`); WP4 adds only files and owns `Branding/` alone |
| EB3 | No interface for a reveal built from the save | **Fixed.** §7.3 `DayCycle.Rebuild(data, endedDay)` (settlement null); every reveal/Mali rule works with `settlement == null`; the dismiss path reads `data.chapter.complete`; WP1 and WP7 tests compare fresh and rebuilt |
| EB4 | Arrears history not persisted (N7/N7b, B2) | **Fixed.** §2.3 `DayRecord.owedAtClose`, set by `EndDay` step 2 (§7.3); N7/N7b read it (§4.4); WP1 tests a zero-cash night |
| EB5 | Git LFS still routes fonts and audio | **Fixed.** §6 LFS rule; WP2 edits `.gitattributes` first and checks `git check-attr`; WP4 stages audio only after WP2 is merged |
| EI1 | Aileron is CFF; untested with legacy Text | **Fixed.** §5.2 rules 5–6: WP2 converts to TrueType (`tools/convert_fonts.py`) and `UiFonts` self-tests glyphs, falling back to LegacyRuntime; H6 checks on the phone |
| EI2 | Namespaces unspecified | **Fixed.** §8 namespace table; `System.Environment` rule; WP9 grep |
| EI3 | `ShouldReset` undefined | **Fixed.** §2.3 `PlayerData.ShouldReset`, used by `TryLoad` (§2.6), tested in WP1 |
| EI4 | `MaliGreeting` would need a money formatter in stage 1 | **Fixed.** §4.2.1 and WP3: `Build(data, useSchedule, out extra)` returns `{owed}`/`{label}` raw; WP6 formats and fills (§7.7) |
| EI5 | Coach marks over modals; mark 3 fired by a bill | **Fixed.** §4.7/§5.4.11/§7.10: marks hide while `UiModal.IsAnyOpen`; step 3 ignores `bill:` events |
| EI6 | Pause sheet does not fit | **Fixed.** §5.4.10: two columns, 1 500 × 760, height 742 |
| EI7 | Chapter-end screen A overflows | **Fixed.** §5.4.9 height budget 984 ≤ 1 000; "What Mali noticed" is screen B. Revision 2b also shortened the bills line ("Bills and repayments: −R{amt}"), because "…this week: −R1 400" measured 737 u against its 704 u box |
| EI8 | Work's "already worked"/"too tired" lines unreachable | **Fixed.** `IInteractable.OnDisabledTap()` (§7.1) shows the §4.2.2 line; §5.4.2 |
| EI9 | "Due tonight"/"next bill" computed in four places | **Fixed.** §2.4 shared helpers (`DueOnNight`, `NextDue`, `Promised`, `NewPromises`, `LargestArrears`), used by the HUD (`HudCopy`, §7.4), Home sheet, sleep confirm and reveal; tested in WP1 |
| EI10 | Music has one source; scaled-time scheduler freezes under Pause | **Fixed.** §7.11: two sources per loop, `PlayScheduled` on `dspTime`, fades on unscaled time |
| EI11 | Interim state: bills fail between stages; does `Apply` open a day? | **Fixed.** §2.4: `Apply` never opens a day. WP1 makes a one-line interim change to `HomeInteraction.SleepAndEndDay` (call `DayCycle.EndDay`), and `CreateNew` starts a valid Day 1 |
| EI12 | `MoneyChanged` raised inside the mutator | **Fixed.** §2.4/§7.3: queued and flushed after the mutator and the save; handlers never write the save |
| EM1 | Wrong minimum-spacing sentence | **Fixed.** §3.3 (Home–GATE 1.50 is the minimum) |
| EM2 | "Installs over 0.1.x" is wrong (testers have 1.0, code 1) | **Fixed.** §0, §2.6, WP4 goal |
| EM3 | Frozen shim breaks stage-2 pure code | **Fixed.** §2.5: shim widened (Sign, Floor, Ceil, Round, LogException, Epsilon …), frozen after stage 1 except WP9 appends; pure code otherwise uses `System.Math` |
| EM4 | Code under `#if !UNITY_EDITOR` is never compile-checked | **Fixed.** §2.5 rule; WP9 grep |
| EM5 | HUD scale-down breaks the 32 u floor | **Fixed.** §5.4.1: nothing scales; the right group wraps to a second row |
| EM6 | Prompt text changes while `Current` stays the same | **Fixed.** §5.4.2: `WorldPromptView` re-reads every frame |
| EM7 | Coach mark 2 has two targets; mark 4 has no end event; mark 1 races `DayStarted` | **Fixed.** §4.7: one target (the prompt); mark 4 ends on the first `UiModal.Changed` with a modal open; mark 1 fires on `OnDialogueHidden` (2.0 s fallback) |
| EM8 | Nobody sets `hasMetMali` | **Fixed.** §4.6 and WP8 tasks: **Let's go** sets `isCharacterCreated` and `hasMetMali` |
| EM9 | `endSeen` does nothing; `DayStarted` repeats on every load; chime hooks | **Fixed.** `endSeen` removed (`chapter.complete` alone drives the chapter end; `RestartChapter` replaces it); `PlayerData.morningLineDay` and `StartDay` raise `DayStarted` once per day (§7.3); reveal and screen A request the chime via `SoundRequested`, `NightEnded` has no sound (§7.11) |
| EM10 | PNG meta template copies a sprite table; Kenney panels still baked | **Fixed.** §6 template (clear `internalIDToNameTable`, `spriteMode: 1`); §7.13 + WP4 stop `BakeUiSprites`; WP9 deletes `Resources/MaliGoUI` once unused |
| EM11 | Audio budget measured on source files | **Fixed.** §0 states it; the in-APK size is H4; two-source Streaming playback is H5 |
| EM12 | .bat should delete the stale APK itself | **Fixed.** §7.13, WP4 acceptance |
| EM13 | Editor/phone checks assigned to build agents | **Fixed.** §8 rule; checks moved to H2–H6 (§10) |
| EM14 | `Keyboard.current` can be null; Android 16 back | **Fixed.** Null checks in §7.1/§7.2; H5 checks the back key, with the targetSdk 35 fallback in §7.13 |
| EM15 | `LookPreview` T-pose, leaks, light | **Fixed.** §5.4.13: animator applied, material/model/RenderTexture released, no 3D light |
| EM16 | `.gitignore` for test output; cache every sprite | **Fixed.** §2.5/WP1 (`bin/`, `obj/`, `tools/out/`); §5.3 caches every sprite and recreates Unity-null ones |
| EM17 | Items verified correct | No change needed |

### Player/design critique

| # | Finding | Outcome |
|---|---|---|
| PB1 | The four stock looks: one light skin, a "criminal" and a "cyborg" | **Fixed.** §4.6/§6.3b/§5.4.13: two skater builds × three recoloured tones (`tools/make_skin_tones.py`), mapped through `genderPresentation` + `skinTone`; criminal and cyborg never used. Base skin colour re-measured on 4 Oct: (245, 140, 106) is the most common skin pixel in all four stock skins |
| PB2 | Cold showers erases a R350 cost | **Fixed.** §3.4.9: 1 × R350 Day-8 commitment ("Geyser"); §0; no reference path takes it, so the §3.2 totals stand |
| PB3 | Headline total hides debt (loans read as gains, arrears invisible) | **Fixed.** §5.4.8: still owed with the morning figure, "New promise" lines, loan rows in `MoneyTransfer` with their repayment; A4 names the repayment; §5.4.9 still owed at Body size beside the total |
| PB4 | HUD copy does not fit the pills | **Fixed.** §5.4.1: two-line short copy ("Rent" / "R500 · tonight", "{label} owed" / "R{owed}", "No bills" / "till payday"), widths measured; `HudCopy` fit test in WP7. §7.4's stale "No bills before payday" removed in 2b |
| PB5 | F1 "x of 7 shifts" reads as a grade | **Fixed.** §4.4 F1 |
| PB6 | Stokvel "not for now" line still lectures | **Fixed.** §3.4.11 |
| PI1 | Simulation output | Used: it confirmed the five §3.2 totals and fed E6/E7 and §0's stress note |
| PI2 | Energy hardly matters; §3.2 contradicts itself | **Partly fixed, escalated.** §3.2 reworded (Mali mentions energy; it changes nothing else); D8 is binding, so the design question is E6 |
| PI3 | Choices that win on money; lift club; 1GB bundle; pay-later | **Escalated (E7).** The fixes change money deltas and reference totals; this revision changed copy only. Mali no longer promises the lift club covers the week (§3.4.6). "Renews in three days" **rejected**: the bundle that renews tonight is the airtime bill (D6 timing), so it stays consistent |
| PI4 | Day 1 energy trap | **Fixed.** CC page 2 and the Day 1 morning line name the shift and its 60 energy (§1.1, §3.1); sleep confirm says "You were too tired for a shift today." (§4.8) |
| PI5 | Where did R1 000 come from | **Fixed.** "cash + savings" beside every headline amount (§1.1, §4.8, §5.4.8, §5.4.9) |
| PI6 | Choice cards overflow | **Fixed.** Shorter labels; Later column 200/190 u with real due days; label boxes 493/373 u; WP6 fit test with font metrics. Re-measured in 2b: every label fits 2 lines |
| PI7 | The Bank is never introduced | **Fixed.** B2 names the Bank; the Home sheet's "Still owed" row says "The Bank is up the road." |
| PI8 | Legacy judging Mali lines | **Fixed.** §4.2.1: only `first_meeting` remains; WP3 deletes the rest (incl. `default_greeting_student`) and `ContentTests` checks `AllEntries`; §7.7 replaces the selector path |
| PI9 | "What moved it most" is always rent | **Fixed.** §4.4: only scenario choices and loans ranked; bills get one line |
| PI10 | Reactions that lean | **Fixed.** keep_cash, bundle_1gb, in_for_dinner, send_part, mashonisa situation (§3.4) |
| PI11 | Stress mostly flags arrears | **Fixed (stated).** §0 |
| PM1 | Disabled cards fail contrast | **Fixed.** §5.4.5: 60% label, full-opacity `Label` 36 reason |
| PM2 | Information captions at 32 u are ~10.7 dp | **Partly fixed.** Outside the HUD such captions use `Label` 36 (§5.1). **Rejected for the HUD**: at 36 u "Payday in 7 days" (≈ 276 u) no longer fits the 272 u Day pill, and the 16:9 row has no spare width; the HUD keeps 32 u and short copy |
| PM3 | First-minute timing; long names overflow the compact box | **Fixed.** §1.1 times recomputed; §3.1 fit rule with a 16-character name; the stretched prefix for morning lines has no name (§4.2.3). Re-measured: the longest case wraps to 2 lines of 960 u |
| PM4 | Reveal line overflow | **Fixed.** §4.3: drop the prefix, then paginate |
| PM5 | Bank moves crowd the ledger | **Fixed.** §5.4.8 groups same-direction bank moves per day |
| PM6 | "Thandi's birthday, Thandi." | **Fixed.** `{friend}` token (§4.1, §3.4.8) |
| PM7 | Day 2 "nearly out" vs "ran out" | **Fixed.** Both say out of data (§3.1, §3.4.3) |
| PM8 | Geyser price; weekly rent | Geyser **fixed** (a neighbour who does plumbing, §3.4.9). Rent wording **rejected**: D6 names it rent; "Rent" is also the short HUD/ledger label and the Mali template subject ("Rent took the most today"), and chapter-compressed time already explains the timing. The founder can rename it later as content |
| PM9 | The name on every line | **Fixed.** §3.4 name rhythm rule, tested in WP3 |
| PM10 | More banned words | **Fixed.** §4.3 list; WP3 tests use the full list |
| PM11 | Replay totals as a high score | **Fixed.** §1.3, §5.4.9 |
| PM12 | D24 check; `default_greeting_student` | Passed; the id is deleted with the legacy entries (WP3) |
| PM13 | Readability checks that passed | No change needed |

### Repairs to the first run's half-finished edits (2b)

- §7.1–§7.4, §7.7–§7.11, §7.13 still described revision 1 (no `Rebuild`, `endSeen`, one music source,
  chime on `NightEnded`, "No bills before payday", no `OnDisabledTap`); now consistent with §2–§6.
- §8 still listed revision-1 signatures and tests (`MaliGreeting.Build` without `extra`, the short banned
  list, "installs over 0.1.x", Editor checks as acceptance) and lacked the `.gitattributes`, `.gitignore`,
  font, skin and catalog files; rewritten.
- §0, §3.2, §3.3, §3.4.3, §3.4.6 and §6.3b referred to E6, E7, H2, H3, H4 and this section, which did not
  exist; all now exist (§10, §11).
- Two teasers did not fit their own 940 u test ("…and a neighbour owes you." = 940.03 u; "…and a hoodie on
  special." = 939.2 u, no margin): now "Tomorrow: the stokvel, and a neighbour who owes you." (911 u) and
  "Tomorrow: news at the taxi stop, and that hoodie again." (910 u).
- §9 items 2, 4, 9 and unsupported 6 updated to the widened shim, the four-source audio and H1/H5/H6.

### Revision 3 (founder decisions A1–A5, 4 Oct 2026)

Every number below was re-simulated with `tools/sim_chapter.py` (new; six play styles × 16 profiles,
3 000 random play-throughs per profile) and every new string was measured with fontTools on the staged
Aileron fonts, the same method as Revision 2.

| # | Decision / finding | What changed |
|---|---|---|
| A1 | Energy: the shift opens only after the day's first scenario (closes E6, option a) | §0 work row; §3.1 gate = first scenario of the day in the player's schedule (carry-overs and follow-ups never gate; flag off = no gate); §3.2 rewritten; §7.3 `ShiftState`, `WorkRules.State(d, gate)`, `DoShift(d, gate)`; §7.1 and §4.8 the Work prompt "Shift opens after {gate}" (≤ 693 u of 776 u) and §4.2.2 the NotYet line; §4.2.1 greeting suffix; §1.1 timings; WP1/WP5/WP7 tests |
| A1 | Energy alone would not bite after Day 1 (free options of 15–30) | Free options of the day gates use 45–50 energy (skip lunch, walk, Wi-Fi 45; walk today 50), so on Days 1–3 the free option as the first choice costs the shift. Energy still never blocks a choice (D8 kept), so after the shift the free options cost only energy; their other costs are in Later |
| A2 | Fair choices, visible cost (closes E7) | §3.2 fair-choice rule (five axes, decline ids) and §3.4 table; tested in `ContentTests` for every travel mode and in the script. Content: the fare rise is now a commute to Day 7 (`instalmentLastDueDay`), so the lift club (R120) wins over the week and the walk saves R34 but costs the shift; `walk_to_main_road`/`walk_all_the_way` → `walk_today`; day bundles R15 + 2 × R15 against 1GB R85 "till payday"; pay-later R120 + 2 × R130 (R20 more than paying now); "R80 for now" and "can't this week" bring a call-back (follow-up, §3.4.7b) asking R120 |
| A2 | Follow-up mechanic (small, data-driven) | Choice fields `followUpScenarioId`, `followUpAfterDays`, `followUpLaterText`; `PlayerData.followUps` as "id@day" strings (so WP3 can read them in stage 1); `ChapterSchedule.FollowUpKey/TryParseFollowUp/FollowUpTeaser`; one follow-up in the beta (`family_callback`). Owners: fields and activation WP3, apply and Later column WP6, Coming up WP7 |
| A2 | The Day-2 bill contradicted buying data | The bill is now "Airtime" (calls and messages, same R60, same night); the data scenario no longer says "renews tonight" |
| A2 | Uniform XP, no correct answer | XP 5 everywhere, no marks; new reactions state amounts and days only ("Your aunt will call back about the rest."), checked against the banned list |
| A3 | Personal spending profile | §2.7 `SpendingProfile` (versioned, defaults = the generic chapter, reserved fields for a bank source); two taps on CC screen 3 (§4.6, §5.4.13; ≈ 10 s); four schedules by focus (§3.1); place names by focus and travel, generic only (§3.3); travel amounts in bounds (§3.4.0); N0 in "What Mali noticed" (§4.4); the "We've built your week…" line with `WeekPlaces`; WP1 data and copy, WP3 content, WP8 screens and `ShellCopyTests`; tests cover each of the 4 + 4 options and all 16 combinations |
| A3 | Later: bank-data source | §12, separated from the beta and written neutrally |
| A4 | First 10 seconds | Screen 1 opens with "Live the week before payday." / "See where your money goes." (DisplayTitle 72: 1 041 / 1 014 u of 1 380 u). HUD Today pill under the Day pill ("Today from R1 000" / → current total; §5.4.1): the top row still measures 1 912 u at 16:9 (Day pill +8 u, savings bar −8 u); anchor `hud.today`; `arrowRight` icon added (WP2) |
| A5 | E2, E4 closed; E3, E5 kept as human tasks | §10: E2 and E4 closed; E3 → H1, E5 → H7; H8 added for the profile play check |
| R3-1 | WP3 could not build `MaliGreeting` in stage 1 (it needs `WorkRules.State` and `Obligation.shortLabel`, both WP1) | `MaliGreeting.cs` and the selector change moved to WP6 (stage 2); no file is owned by two packages in a stage |
| R3-2 | Small fixes found while re-measuring | "R60 · tomorrow" measures 245 u, not 224 u (still fits 292 u); "from Day 5" measures 159 u, over the old 155 u small-line rule, so the rule is now "fits its Later column" (200/190 u); the chapter can now charge up to R1 650 and the bills line still measures 554 u |
| R3-3 | Reference totals | §3.2 tables replace the Revision 2 paths (1 440 / 1 290 / 851 / 505 / 305). Default profile: saver 854, always works 1 255, middle 545, never works 350, comfort 615, comfort + loan 415; WP9 locks all 96 totals. §1.2, §1.3, §5.4.1, §5.4.8 and §5.4.9 examples recomputed from the middle path |

### Revision 3a (independent verification of Revision 3)

An independent verifier re-simulated the chapter from the spec text (all 96 totals agreed) and found 11
important issues. All are fixed below; the 96 reference totals are unchanged (`tools/sim_chapter.py`
re-run, exit 0, table identical) and every changed string was re-measured with fontTools on the staged
Aileron fonts, the same method as before.

| # | Finding | Outcome |
|---|---|---|
| I1 | Human tasks had no single owner ("Ubayd or Jaswin") and no order; §8 said "(H1–H6)" | **Fixed.** §10: one owner per task (Ubayd H2, H3; Jaswin H1, H5, H6, H8, with H5 going to whoever has an Android 16 phone; founder H4, H7) and an Order column: H2, H3 → H4 (founder builds the APK) → H1, H5, H6, H8 on a phone with that APK → H7 → APK to testers. §8 now says "(H1–H8)" |
| I2 | The Today pill was said to match the reveal's start and end, which is false on any night with a bill | **Fixed.** §5.4.1: line 1 is the reveal's start; the reveal's end is line 2 minus the night's charges (listed by the sleep confirm). §10 H8 reworded the same way |
| I3 | A carried-over scenario at the same spot could hide today's gate, while the Work prompt named it | **Fixed.** §3.3 spot-queue rule and §3.1 (`ActiveScenarioIds` order): today's gate is first in its spot's queue, then schedule day, position, follow-ups. WP3 `ContentTests`: with `food_decision` carried to Day 2, `ActiveScenarioAtSpot(CORNER)` = `data_runs_out`. Totals unchanged (the script already plays the gate first) |
| I4 | "Can't this week" then R120 cost less than "R80 now" then R120 for the same ending | **Fixed.** §3.4.7/§3.4.7b: new follow-up `family_callback_full` (set off only by `cant_this_week`, asks R200: send R200 / R200 from savings / not this week either; situation 612 + 556 + 549 + 416 u in 613 u); `family_callback` (R120) stays for `send_part`. §0 family row, §3.2 decline ids, §3.3 GATE queue, WP3 counts (15 = 13 scheduled + 2 follow-ups), WP6/WP7 tests, `FollowUpTeaser` (either call-back), `tools/sim_chapter.py` |
| I5 | The lift club "covers your trips till payday", but a later trip across town still charged | **Fixed.** §3.4.6 lift club reaction "…Your trips to the course are covered till payday." (1 008 + 412 u); §3.4.2 situation now starts "You've got an interview on the other side of town today, off your usual route." (147 / 155 / 147 characters, 5 lines each in 613 u) |
| I6 | "No path that keeps its shifts and pays its bills reaches 60" was false (stacked deferrals reach 70–77) | **Fixed (accepted as intended).** §0 and §3.2 reworded: the reference styles reach 60 only through arrears or lost shifts; a player who keeps every shift and pays every bill can reach up to 77 by stacking deferrals, which is intended. `tools/sim_chapter.py` now plays a stacked-deferrals path per profile (not a reference style) and prints the highest hidden stress over every path it plays, random runs included (100 overall; 77 on paths with all 7 shifts and no night owing) |
| I7 | "Random play-throughs end at most R425 up" read as a ceiling | **Fixed.** §3.2: the highest reachable end total is R1 605 (with R550 already promised for payday); random play-throughs ended at most R1 425 |
| I8 | Coming-up lines use `Label` (Bold 36) but the fit rule measured SemiBold; the family/group teaser was 941.9 u of 940 u in Bold | **Fixed.** §3.1, §1.2: "Tomorrow: a call from home, and a birthday." (744 u Bold); the stokvel teaser (935.7 u Bold, within 10 u) is now "Tomorrow: the stokvel, and your neighbour." (744 u). §3.1, §5.4.8 and the WP7 test state Coming-up lines are `Label` 36 Bold; longest teaser now transport/Bra K 913 u |
| I9 | With the stretched prefix and the widest 16-character name, the Day 1 morning lines wrap to 3 lines | **Fixed.** §3.1 and WP7: Day 1 lines are tested without the prefix (it can never show on Day 1's morning); the test name is "Mmmmmmmmmmmmmmmm". Every line passes (tightest: Day 2 with the prefix, 958 of 960 u) |
| I10 | Airtime was category "Bills", so N0 for "Data, airtime and going out" left out the airtime | **Fixed.** §2.3 and WP1 `ObligationDefaults`: airtime category "Phone & data"; rent stays "Bills" |
| I11 | A personal local path in §6.3; §12 "never sent to MaliGo" conflicted with an aggregator route | **Fixed.** §6.3: "a local copy of `mali2.png` (MD5 …); verify the MD5 before use"; no other personal path in the spec. §12: "never stored by MaliGo; processed on the device where the route allows". (The untracked PDFs in the repo root are outside the spec; the founder moves or ignores them) |
| Minor | Verifier's minor notes | §9 item 6 lists the Today pill; the data gate noun is "sorting your data" ("Shift opens after sorting your data", 640 u); §3.2 says "choice energy only" for the 12-of-16 claim and lists the script's fallback lists |

### Revision 4 (founder decisions F1–F2 and the player-experience review, 6 Oct 2026)

The content and economy batch. `tools/sim_chapter.py` was updated to match and re-run (exit 0: no stuck
path, no dominant style, fair-choice rule holds in every travel mode, comfort ends below R1 000 in every
profile and comfort + loan is short at least one night); `EconomySimTests` and the §3.2 tables carry the new
totals. Every new or changed string passes the existing `AileronMetrics` fit tests.

| # | Item | Outcome |
|---|---|---|
| F1 | Food players got almost the default week, and N0 could quote R20 | **New scenario `kota_run`** (§3.4.14), food focus only, Day 5 at the kota shop: full kota R65 / half a kota R30 + 10 energy / cook at home 20 energy; XP 5 on each; ledger labels ≤ 28 chars; no brands. New Day 5 morning line and teaser for the food list (§3.1). **N0 needs ≥ R100** (§4.4). Food rows of §3.2: middle −R30, comfort and comfort + loan −R65; saver and "always works" cook at home (unchanged totals) |
| F2a | Walkers were always booked onto taxi fares | §3.4.6: for `walk` the free option (id `walk_today`) is "Keep walking, no fares": 50 energy (it costs Day 3's shift as the gate), nothing later. Walk rows: saver +R136 (854 → 990, 704 → 840 for `home_family`); other styles unchanged |
| F2b | The CC places line ignored the travel tap for `home_family` | `WeekPlaces` always includes the transport spot (it replaces the third place when missing): `home_family` now reads "Spaza shop · Home · Taxi rank" (or the travel mode's label) |
| R1 | A loan day's pill read as a good day | `DeltaPill`: "+R136 today · R400 borrowed" (`RevealLineBuilder.Borrowed`); fit: "−R9 999 today · R400 borrowed" is within the 757 u column less the pill's 60 u padding |
| R2 | Tags misdescribed careful choices in N3/N5 | `pay_in_full` Discretionary → Neutral; `lift_club` Discretionary → Neutral; `skip_lunch` Deferred → Frugal. (Tags never change money; totals unaffected) |
| R3 | "R200 for gogo's meds took the most today: R200." | Ledger labels carry no amount and no comma (tested): "Gogo's meds", "Towards Gogo's meds", "Rest of Gogo's meds", "Gogo's meds from savings", "Geyser repair from savings", "Full price for the speaker", "Shared ride at the new price", "From the neighbour" (half and half). A6 template kept |
| R4 | Tone | Removed "You showed up for her, {name}, just not at the table." (now "R50 towards the gift, {name}. You'll miss the dinner, and R100 stays with you."); vetkoek's line no longer the only warm named one ("R20 for lunch. That keeps you going till supper."); B2 says arrears come off at night from cash; N6 uses the net savings movement; "Gogo" capitalised everywhere; "Bluetooth" → "wireless", "Wi-Fi" → "internet" |
| R5 | "Two things are waiting today: your gate and down the road." | Greeting templates use a comma and where-phrases from the new `ChapterSchedule.SpotWhere` ("at the kota shop", "at home", "down the road"); `SpotPlaceName(GATE)` is "home" |
| R6 | Hidden stress numbers | The never-works style now peaks at 98 with the food focus (96 before; it is in arrears from night 3 either way); the stacked-deferrals path reaches 72 with the food focus (cooking at home +2). Random play-throughs ended at most R1 510; the highest reachable total stays R1 605 |
| UI | Selected-card style (UI batch, other branch) | §5.4.9 plan cards and §5.4.13 CC cards: `Tint` fill + 6 u `AccentPrimary` (#087A18) ring + a checkmark icon (36 u on CC cards, 48 u on plan cards; plan-card text inset 96 u), replacing the 6 u `Coin` ring |

Not changed here (owned by the UI batch): the sleep confirm's "It comes off first." (`SleepConfirmView`).

---

## 12. Later: bank-data source (not in the beta)

**Nothing in this section is built for the beta.** It records how the profile in §2.7 could later be
filled from a player's real transactions, so that today's design does not block it.

- **Today there is no public, self-serve transaction API for Absa** (or most South African banks) that an
  app like this can simply sign up to. The realistic routes are: a consent-based account-data aggregator
  (for example Stitch), a direct partnership with a bank, or the player uploading a bank statement (PDF or
  CSV) that is read on the phone.
- **Consent (POPIA).** Personal financial information is only processed with the player's explicit,
  specific and informed consent, for a stated purpose ("build your week from your spending"), and the
  player can withdraw it at any time in the app, which deletes the derived profile and stops any refresh.
  The default stays the two taps; a bank source is opt-in only.
- **Keep the summary, never the transactions.** What is stored is only the summarised profile: spending
  categories with their share and typical amounts, the payday (day of the month), and recurring debits
  (label, category, amount, day). Raw transactions are never stored by MaliGo; they are processed on the device where the route
  allows, and discarded once the summary is made.
- **Same object, no game changes.** The bank route fills the same `SpendingProfile` (§2.7): `source =
  "bank_feed"` or `"statement"`, the reserved `categoryShares`, `recurringDebits` and `paydayDayOfMonth`, and
  from them the two fields the game reads, `focus` (the largest of the four spending groups) and `travel`
  (from the transport spending). Amounts still come from the bounded ranges in §3.4.0, so every
  profile, however it was filled, plays a week the simulation has checked. `SpendingProfile.version`
  lets a later shape add fields without breaking older saves.
