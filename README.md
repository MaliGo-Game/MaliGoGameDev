# MaliGo (Unity)

A 2.5D life-simulation prototype that teaches financial behaviour through lived
consequences, built for a South African audience (currency: Rand). The player
makes ordinary daily decisions — what to eat, how to get around, whether to
cover an emergency from savings or cash, whether to join a stokvel — and Mali,
a non-judgemental companion, reflects the pattern back without ever showing a
score or grading a choice as "correct."

This is the design/prototype repo. **The app that actually ships to users is
the separate Expo/React Native project** (`Chiko-DK/MaliGo`) — this Unity
project's scenario data, consequence math, and Mali dialogue-selection logic
have been ported there natively (see `Assets/MaliGo/Scenarios/` and
`Assets/Scripts/Dialogue/` for the source of truth that was ported). Unity
stays useful here as the richer 3D prototype and as the place new scenario
design gets worked out before it's ported.

## What's actually in this project

- **Scenario decision loop** (`Assets/MaliGo/Scenarios/`,
  `Assets/Scripts/Scenarios/`) — 8 scenarios (food, transport, impulse
  purchase, emergency expense, windfall, family obligation, stokvel, credit/
  BNPL), each with 3 choices. Every choice carries a uniform XP reward so
  reward size never implies a "right" answer; consequences hit cash, savings,
  stress, and energy directly.
- **Hidden behaviour scoring** — two EMA-smoothed 0–1 scores (spending,
  saving) nudged by each choice's behaviour tag (`Neutral | Frugal |
  Discretionary | Deferred`), never shown to the player. They exist to pick
  Mali's tone, not to grade anyone.
- **Mali companion** (`Assets/Scripts/Characters/`,
  `Assets/Scripts/Dialogue/`) — a persistent NPC whose dialogue is chosen by
  a fixed priority cascade (first meeting → high stress → low cash →
  repeated discretionary spending → savings milestone → consistent saving →
  chapter-aware default), not randomly.
- **World** (`Assets/Scenes/MaliGoWorld.unity`) — a small walkable
  neighbourhood built from Kenney's City Kit (commercial, suburban, roads),
  Car Kit, and one animated-character pack, scaled to a measured ~0.27
  world-units-per-real-metre so people, cars, and buildings sit at
  consistent relative sizes (see `Assets/Editor/MaliGoScaleAudit.cs`, a
  read-only diagnostic — it changes nothing, only measures and logs).
- **Runtime self-healing bootstrap**
  (`Assets/Scripts/PlayerIdentity/MaliGoIdentityRuntimeBootstrap.cs`) — wires
  up the player character, mobile touch controls, scenario triggers, and
  world locations on every scene load (not just the first), since the app
  moves between `CharacterCreation` and `MaliGoWorld` at runtime via
  `SceneManager.LoadScene`.
- **Android build pipeline** (`Assets/Editor/MaliGoAndroidSetup.cs`,
  `MaliGoResourceBaker.cs`, `MaliGoBuildPipeline.cs`) — configures IL2CPP +
  ARM64 (this Unity install ships no Mono Android variation), bakes runtime
  resources, and builds `Builds/Android/MaliGo-Beta.apk`. Run via
  `BUILD_BETA_APK.bat` (Unity must be closed first) or **MaliGo → Build →
  Android Beta APK** from the Editor menu.
- **Behavioural Research/** — the 27 papers this design is actually built
  on (knowledge ≠ behaviour, streaks/leaderboards drive anxiety and false
  confidence over real ones, feedback must be immediate/specific/causal).
  The no-streaks, no-score, hidden-archetype design choices above are
  direct consequences of this research, not arbitrary.

## Versioning

MaliGo follows [Semantic Versioning 2.0.0](https://semver.org): `MAJOR.MINOR.PATCH`,
with a `-beta.N` pre-release tag on every tester build. The version lives in one
place, the [`VERSION.txt`](VERSION.txt) file at the repo root (e.g. `0.3.0-beta.1`). The
build reads it, the APK is also saved as `Builds/Android/MaliGo-<version>.apk`,
and the game shows `v<version>` at the bottom of the screen, so every screenshot
or bug report says which build it came from.

The file is `VERSION.txt`, not `VERSION`: Windows ignores case, so a file named
`version` in the project root replaces the C++ `<version>` header during the
IL2CPP build and the build fails.

**Rules**

1. **Every build that leaves your machine gets a new version.** Never send two
   different APKs with the same version. Bump before you build:
   `python tools/bump_version.py beta` (`0.3.0-beta.1` -> `0.3.0-beta.2`).
2. **PATCH** (`0.3.0` -> `0.3.1`): bug fixes only, nothing new for players.
   `python tools/bump_version.py patch`
3. **MINOR** (`0.3.x` -> `0.4.0`): new features or content (a new chapter,
   scenarios, screens). `python tools/bump_version.py minor`
4. **MAJOR** stays `0` while the game is in beta; anything can still change.
   `1.0.0` is the first public store release. After that, MAJOR goes up only
   when old saves stop working or the game changes in a way players must relearn.
5. A new PATCH, MINOR or MAJOR starts again at `-beta.1`. Dropping the tag
   (`python tools/bump_version.py release`) is for a build going to the store.
6. Commit the `VERSION.txt` change with the code it describes, and tag shipped
   builds: `git tag v0.3.0-beta.2`.

**Android versionCode** is worked out from the version, so it always goes up and
Android installs a new beta over an old one:
`major*1,000,000 + minor*10,000 + patch*100 + beta` (a release counts as beta 99,
so it sorts after its betas). `0.3.0-beta.1` -> `30001`. That is why MINOR,
PATCH and the beta number each stay below 100. `python tools/bump_version.py`
with no argument prints the current version and its code.

## Requirements

- Unity **6000.3.0f1** (Unity 6.3 LTS), URP, DX11
- New Input System only (`activeInputHandler: 1` in Project Settings) —
  this project does not support the legacy Input Manager at runtime
- For Android builds: Android SDK (cmdline-tools + platform-tools), NDK
  27.2.12479018, CMake 3.22.1, JDK 17 (Unity 6000.3 rejects JDK 21)

## Status

Chapter 1 vertical slice: character creation → world → 8 scenario
decisions → Mali reactions → persisted financial stats, playable on
Android. Known gaps: no day/night cycle, no recurring obligations, and the
hidden behaviour scores don't yet gate any content — they're logged and
used for Mali's tone but nothing else yet.
