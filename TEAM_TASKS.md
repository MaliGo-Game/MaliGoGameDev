# MaliGo Unity — Remaining Dev Work

**Repo:** https://github.com/0geder/MaliGoGameDev (branch `main`)
**Read first:** `README.md` (what the game is and how it's built)

The game builds and runs. Choices now have lasting costs (one-time scenarios, one work
shift a day, weekly bills, pay-later instalments: see "Done" below), but there's no
real day cycle, chapter end or feedback yet. The remaining work is split into two
lanes that touch different files, so you can work in parallel without merge conflicts.

| | Lane | Focus |
|---|---|---|
| **Ubayd** | Core economy loop | A real day cycle and an end to the chapter |
| **Jaswin** | Device, UX & feedback | Prove it runs on a phone, make it understandable |

---

## Before you start (both)

- **Unity 6000.3.0f1** (6.3 LTS), URP. Use exactly this version.
- **New Input System only.** `UnityEngine.Input` (the legacy API) throws at runtime in
  this project. Use `UnityEngine.InputSystem` (`Keyboard.current`, etc.).
- **Don't edit `Assets/Scenes/MaliGoWorld.unity` unless you really have to.** Scene files
  merge badly. Almost everything in the world is wired at runtime from
  `Assets/Scripts/PlayerIdentity/MaliGoIdentityRuntimeBootstrap.cs` → `WireWorldScene()`.
  Add new systems there as a new `Step(...)` instead of placing objects in the scene.
- **Design rules from the research. These aren't up for debate:**
  - Never show the player a score, grade, streak, or leaderboard.
  - Every scenario choice gives the same XP (`financialXpDelta = 5`), so the reward
    never hints at a "correct" answer.
  - The behaviour scores (`spendingBehaviourScore`, `savingBehaviourScore`) stay hidden.
  - Mali reflects and doesn't judge. No "good job" or "bad choice" lines.
- **Scale:** the world uses about **0.27 Unity units per real metre**. New props need
  scaling to match (see `Assets/Editor/MaliGoScaleAudit.cs`).
- Open a branch per task (`ubayd/one-time-scenarios`, `jaswin/apk-device-test`, …) and
  open a PR into `main`.

---

## Done — U1–U3 (Sam)

On branch **`sam/core-loop-u1-u3`**, waiting for a play-test in the Editor before it
merges into `main`. Pull it before starting U4/J5, since both build on it.

- **U1 Scenarios play once.** `ScenarioTrigger` goes quiet once its scenario is in
  `completedScenarioIds`; `ScenarioManager` refuses completed scenarios.
- **U2 One work shift per day,** and a shift needs 20 energy (`WorkInteraction`).
- **U3 Bills.** `Obligation` data on `PlayerData` (rent R500 + airtime R60 weekly), settled
  by `Assets/Scripts/Economy/ObligationLedger.cs`: from cash only, unpaid amounts carry
  as arrears and add stress. Buy-now-pay-later creates 3 × R120 weekly instalments
  (`instalmentCount`/`instalmentAmount` on `ScenarioChoice`).
- **Minimal day boundary:** Home's "Sleep - end the day" (`HomeInteraction.SleepAndEndDay()`)
  advances `PlayerData.currentDay`, settles bills and restores energy. This is the hook
  U4 builds on.

---

## Ubayd — Core economy loop

### U4. Day cycle *(large, and the rest of the economy builds on it)*
**Already there (from U3):** `PlayerData.currentDay`, and sleeping at Home ends the day:
`HomeInteraction.SleepAndEndDay()` advances the day, calls
`ObligationLedger.SettleDue(...)` and restores energy.
**Do:** turn that into a proper day cycle. Move the end-of-day logic out of
`HomeInteraction` into its own system, raise an event when a day ends, and decide
which scenarios are available each day (right now all 8 are open on day 1 and each can
be played once).
**Naming:** e.g. `Assets/Scripts/DayCycle/DayCycle.cs` in namespace `MaliGo.Days`.
Don't call the folder or namespace `Time`: inside `MaliGo` it would hide Unity's `Time`
and break every `Time.deltaTime`. Also don't give a class the same name as its own
namespace.
**Expose this for Jaswin:** a C# event such as
`public static event Action<DaySummary> OnDayEnded;`. `DaySummary` should include the
`ObligationSettlement` that `SettleDue` already returns (bills paid, still owed, stress
added), plus the cash and savings change across the day.
**Files:** new `Assets/Scripts/DayCycle/`, `HomeInteraction.cs`
**Done when:** the day ends in one place, the event fires with a full summary, and the
scenarios on offer change from day to day.

### U5. Chapter end *(medium, after U4)*
**Problem:** after 8 decisions nothing happens. `currentLifeChapter` is set once
during character creation and never changes.
**Do:** define a chapter-end condition (e.g. X days played or all Chapter 1 scenarios
done). Show an end-of-chapter reflection from Mali that describes what the player
*did* without grading it.

---

## Jaswin — Device, UX & feedback

### J1. Build the APK and test on a real phone *(do this first, it unblocks everyone)*
The last device test found two bugs: no player character, and no on-screen controls
after character creation. Both are fixed in code (`MaliGoIdentityRuntimeBootstrap.cs`
now wires every scene load) but **no APK has been built since the fix.** The
touch joystick has also never been tested on a real touchscreen.

**Setup note:** `Assets/Editor/MaliGoAndroidSetup.cs` has the original dev machine's
paths hardcoded (`SdkPath`, `JdkPath`, `NdkPath`). Change them to your own before
building. You need Android SDK + NDK `27.2.12479018` + CMake `3.22.1` + **JDK 17**
(Unity rejects JDK 21). Build with **MaliGo → Build → Android Beta APK**.

**Check on the phone:**
- [ ] Character creation → world: the character appears
- [ ] Joystick moves the character; ACT button works
- [ ] Walking to a scenario trigger + ACT opens the choice panel
- [ ] Making a choice changes cash/savings on the HUD and Mali reacts
- [ ] Kill the app and reopen it: your progress is still there
- [ ] A finished scenario's prompt doesn't come back (also after reopening)
- [ ] Work pays once, then says you're done for today
- [ ] Sleep at Home: the day goes up, airtime comes off on day 2 and rent on day 3,
      and Mali says what was paid

Log anything broken as a GitHub issue. If something fails silently, run
`adb logcat -s Unity:V | grep -i maligo`; the world wiring logs every failure.

### J2. Show energy on the HUD *(small)*
Energy is tracked and choices lower it, but the player never sees it.
**Files:** `Assets/Scripts/PlayerIdentity/HUDController.cs`

### J3. First-time guidance *(medium)*
Nothing explains the controls, who Mali is, or what to do. Add a short first-run
walkthrough (Mali introduces herself, points at the joystick, ACT and the first
scenario) and save that it's been seen.
**Files:** `Assets/Scripts/UI/`

### J4. Pause / settings menu *(medium)*
Needs Resume, Volume, and **Reset save** (testers need this), plus Quit.
**Files:** `Assets/Scripts/UI/`

### J5. End-of-day summary screen *(after Ubayd's U4)*
Subscribe to `OnDayEnded` and show what the day did: money in and out, bills paid,
bills missed. Keep it factual and immediate, and cause → effect only, no grades.

### J6. Audio *(small–medium)*
The project has no sound at all. Add UI click sounds, ambient background audio and a
gentle cue when Mali speaks. Use free/CC0 sounds only (e.g. Kenney audio packs) and
keep them small for the APK.

### J7. Context-aware Mali emergency line *(small)*
In the Emergency Expense scenario Mali says the same line whether savings covered it
or not. Make the reaction depend on what actually happened.
**Files:** `Assets/MaliGo/Scenarios/ScenarioLibrary.cs`, `ScenarioManager.cs`

---

## Not assigned yet (after the above)

- **Make the hidden behaviour scores actually change the game.** Right now they only
  pick Mali's tone. Later they should shape which scenarios appear and what's offered.
- Chapter 2 content.

## Where the two lanes meet

| Ubayd delivers | Jaswin uses it for |
|---|---|
| U4 `OnDayEnded` event + `DaySummary` | J5 end-of-day screen |
| `ObligationSettlement` (already exists, from U3) | J5 (bills paid / still owed) |

Agree on the `DaySummary` fields before either of you starts U4/J5.
