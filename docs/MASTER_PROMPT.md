# MaliGo — Master Prompt: Finish the Beta

The brief every agent working on MaliGo follows. Written 3 Oct 2026.

---

## 1. What MaliGo is

Every day people make small money choices: lunch, the taxi, airtime, a friend's
request, something in a shop window. At the end of the day they have a different
amount than they started with, depending on those choices.

**MaliGo lets you live those decisions before they happen for real.** You play a day,
make the choices, and see where your money ends up, with Mali beside you reflecting
on what happened, never judging. When the same moment arrives in real life, you've
already been there, so you choose with more confidence.

The single moment the game must make unmissable:
**"You started the day with R___. You ended it with R___. Here's what moved it."**

## 2. Decisions already made (3 Oct 2026)

| Decision | Choice |
|---|---|
| Who builds | Agents build everything needed for a finished, packageable beta. Ubayd and Jaswin move to review, phone play-testing and fixes. |
| Player for this beta | Young adults, 18–30, South Africa. Rand. Current content (rent, taxi, stokvel, pay-later, family asking for help). |
| Art | Free **CC0** assets only (Kenney and similar). Anything under another licence is listed for the founder to approve, never added silently. |
| APK | Built on the founder's laptop once the work is done (8 GB RAM, so the Editor can't run alongside other apps). |
| Engine | Unity 6000.3.0f1 project in this repo. The React Native app is a separate product and out of scope, except as a brand reference. |

## 3. Non-negotiable design rules

These come from the 27 papers in `Behavioural Research/`. Breaking one is a bug.

- **No scores, grades, streaks or leaderboards shown to the player.** Streak and
  leaderboard mechanics drive anxiety and false confidence.
- **Every scenario choice gives the same XP** (`financialXpDelta = 5`), so the reward
  never hints at a "correct" answer.
- **The behaviour scores (`spendingBehaviourScore`, `savingBehaviourScore`) stay hidden.**
- **Mali reflects and doesn't judge.** No "good job", no "bad choice". She says what
  happened and what it means for the player's own goal.
- **Feedback is immediate, specific and causal:** this choice → this number moved.
- **Knowledge isn't behaviour.** Don't lecture. The player learns by living the
  consequence, not by reading a tip.

## 4. Look and feel

- **Warm, not childish.** Grown-up and calm, like a good banking app with personality,
  not a cartoon. Readable at a glance on a phone held in landscape.
- **Keep the existing identity.** Deep forest green, warm browns and golds, soft cream
  surfaces, the low-poly Kenney South African neighbourhood, and Mali as the face
  of the brand (see the React Native app's `assets/images/mali*.png` and
  `src/theme/colors.ts` for the brand palette). New UI must look like MaliGo, not
  like a stock asset pack.
- **The moat is the South African specificity and the non-judgmental mirror:** stokvels,
  minibus taxis, family obligations, pay-later offers, Rand amounts that feel real.
  Every new piece of content and UI should strengthen that, not dilute it.
- **Dialogue and choice panels must be clearly readable:** large text, strong
  contrast, generous tap targets (at least 48 dp), clear speaker (Mali's portrait and
  name), and choices whose consequences are easy to compare.

## 5. Workshop takeaways to apply

From the product-development and UI/UX workshops (partial transcripts):

- **Push and pull.** Push is the moment in someone's life that sends them looking
  (payday spent by the 20th, a declined application, a family request). Pull is the
  relief the product gives in that moment. Scenarios should be built around real push
  moments, and the end-of-day reveal is the pull.
- **Capability, motivation, opportunity.** Design so players can act (simple, clear
  controls and text), want to (the story of their own day and goal), and get the
  chance to (a decision every few minutes, never a dead world).
- **Habit without streaks.** Bring people back through an open question ("what happens
  tomorrow?") and their own goal, not loss-aversion mechanics.
- **Measure against a default.** Later A/B tests need a plain default experience to
  compare against, so keep features switchable rather than hard-wired.

## 6. Private strategy stays private

The repo is public. Future audiences, partners and commercial plans live in the founder's
private notes (`docs/internal/`, gitignored), never in game text, the README, store listings
or any committed file. Build so new content packs can be added later as data, but don't
build any now.

## 7. Technical rules

- Work on a branch off `sam/core-loop-u1-u3`. Open a PR into `main`; never push to `main`.
- **New Input System only.** `UnityEngine.Input` throws at runtime.
- **Don't edit `.unity` scene files** unless there's no alternative. Systems are added at
  runtime from `MaliGoIdentityRuntimeBootstrap.WireWorldScene()` as a `Step(...)`.
- UI is built at runtime in code (uGUI). Keep that approach for consistency.
- No folder or namespace called `Time` (it hides Unity's `Time`), and no class named
  the same as its own namespace.
- Every new asset or script gets a `.meta` file with a unique GUID (format: copy an
  existing `.meta`).
- **Compile check without opening Unity:** generate a temporary copy of
  `Assembly-CSharp.csproj` with every runtime `.cs` file under `Assets/` (excluding
  `/Editor/` folders) as `<Compile>` items and its `<Analyzer>` lines removed, then
  `dotnet build` it. Do the same for `Assembly-CSharp-Editor.csproj` if editor scripts
  change. Zero errors is required. Delete the temporary files afterwards.
- Pure logic (money, days, bills) gets scripted checks outside Unity.
- Commit messages end with the attribution line in `CLAUDE.md`.

## 8. Work plan

| Phase | What happens | Output |
|---|---|---|
| 1. Understand + research | Map every screen and system; research the market, behavioural design and mobile game UI; find CC0 assets that fit the theme | Reports in `docs/research/`, assets staged outside the repo |
| 2. Design | One design spec: the day loop and its reveal, scenario pacing per day, chapter end, first-run, the new UI kit (dialogue, choices, HUD, panels) | `docs/DESIGN_SPEC.md` |
| 3. Build | Agents implement in parallel on separate files, each compile-checked | Code on the branch |
| 4. Review | Independent reviewers try to break it: correctness, Unity runtime pitfalls, design rules, theme, phone readability. Fix what they find, re-check | Clean branch |
| 5. Package | Headless Android build on the founder's laptop, then phone test | `MaliGo-Beta.apk` |

## 9. Definition of done for the beta

- Character creation → world → a day of choices → sleep → an end-of-day screen showing
  start balance, end balance and what moved it → next day, across a whole chapter that
  ends with Mali's reflection.
- Dialogue, choice panels and HUD restyled to the new kit; readable on a phone.
- Energy visible; bills visible; first-run guidance; pause menu with reset save.
- Sound: UI clicks, ambience, Mali cue (CC0).
- Compiles with zero errors; logic checks pass; reviewers' findings fixed.
- No mention of future partners or other audiences anywhere a player can see.

## 10. Open items needing the founder

- **Notion OKR board:** not reachable until the Notion connector is authorised in
  claude.ai → Settings → Connectors. Its OKRs may change priorities.
- **RAM:** the 8 GB upgrade isn't installed; the APK build needs other apps closed.
- **Play-testing in the Editor/phone:** agents can compile and run logic checks, but only
  a person can confirm how it feels to play.
