# MaliGo Unity — Team Roles for the Beta

**Repo:** https://github.com/MaliGo-Game/MaliGoGameDev
**Working branch:** `agents/beta-finish` (the beta build). `main` gets it once the phone test passes.
**Read first:** `docs/MASTER_PROMPT.md` (what MaliGo is and the design rules), then your part of
`docs/DESIGN_SPEC.md`.

The beta's features are built: a 7-day chapter before payday, a personal spending profile from two
onboarding taps, the end-of-day "you started with / ended with" screen, the chapter end with Mali's
reflection, the new UI, sound, pause menu and first-run guide. It compiles with zero errors and passes
104 logic tests plus a week-long money simulation.

**What's left can only be done by people: playing it, on real phones.** That's your job now. Please
don't start new features. Report problems instead, so fixes don't collide.

| | Role | Focus |
|---|---|---|
| **Ubayd** | Money and content | Does the week feel real for a 22-year-old in SA? Do the profiles change the world? |
| **Jaswin** | Phone and screens | Does it run, read and feel right on real (and budget) Android phones? |
| **Founder** | Builds the APK; reads the tone | Testers' APKs come only from the founder's laptop (signing key). |

---

## Setup (both)

1. Install **Unity 6000.3.0f1**. Clone the repo and check out `agents/beta-finish`.
2. Open the project once (Unity generates the `.csproj` files), then run
   `python tools/compile_check.py --editor`. It should end with `RESULT: OK`.
3. Optional: `dotnet run --project tools/logic_tests` (all pass) and `python tools/sim_chapter.py`.
4. Install the founder's APK on your phone for the phone checks.

## Ubayd: money and content

- Run `python tools/sim_chapter.py` and `python tools/sim_chapter.py --trace middle food taxi` (try other
  styles and profiles). Tell us if any week feels too easy, too harsh or unrealistic.
- **Play check (H8):** play the first two days twice with different answers to the two onboarding
  questions. The shops, the order of situations and travel prices should change; food players get the
  kota run on Day 5; walkers get "Keep walking, no fares" when the fare goes up.
- **Sound check (H1):** listen to every sound on headphones and on the phone speaker. Does the music loop
  cleanly? Does the township ambience sound right? Is the end-of-day chime calm, not celebratory?
- **Editor checks (H2, H3):** walk to every spot, Home, Bank and Work; check the six character looks
  read as light, medium and deep skin.

## Jaswin: phone and screens

- **First run:** the first screen reads "Live the week before payday."; Mali's intro shows a bouncing
  continue arrow; the selected card has a green ring and a tick; the first end-of-day screen comes in
  about 3 minutes.
- **Controls (H5):** the back button goes back in onboarding, opens and closes Pause in the world, and
  closes panels. Use an Android 16 phone if anyone has one.
- **Readability (H6):** text readable at arm's length on the smallest phone; the font shows properly (no
  boxes, the minus sign and "·" visible); nothing hidden under the notch.
- **Sound after restart:** Pause → Start over → the new-day jingle plays and the world has sound.
- **Loans:** take a loan. There should be no green "+R400" on the HUD, and the reveal says
  "· R400 borrowed".

## How to report

- One GitHub issue per problem: what you did, what you expected, what happened, phone model and Android
  version, and a screenshot or screen recording if you can.
- Fix only what you're assigned, on your own branch (`ubayd/...`, `jaswin/...`), with a pull request into
  `agents/beta-finish`. Never push to `main` directly.
- The repo is **public**: nothing about business plans, partners or investors in issues, commits or code.
- Commit messages carry no AI attribution line (see `CLAUDE.md`).
