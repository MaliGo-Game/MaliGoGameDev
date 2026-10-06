# Build readiness: Android beta APK

Audit date: 3 Oct 2026. Branch `agents/beta-finish`. Read-only audit: no Unity run, no code changed.
Sources: `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`,
`ProjectSettings/QualitySettings.asset`, `ProjectSettings/GraphicsSettings.asset`,
`Assets/Settings/UniversalRP.asset`, `Packages/manifest.json`, `Packages/packages-lock.json`,
`Assets/Editor/MaliGo{AndroidSetup,BuildPipeline,ResourceBaker,PlayModeSetup}.cs`,
`BUILD_BETA_APK.bat`, `build.log` (headless run, 16 Sep 2026),
`%LOCALAPPDATA%\Unity\Editor\Editor-prev.log` (Editor-menu build, 22 Sep 2026),
`Builds/Android/MaliGo-Beta.apk` (on disk), React Native app `assets/images/*`.

---

## 1. Bottom line

The pipeline works: two previous builds succeeded with 0 errors (16 Sep headless, 101 min;
22 Sep from the Editor menu, 32.6 min). The current APK on disk is **64.2 MB**
(`Builds/Android/MaliGo-Beta.apk`, 64,200,189 bytes, 22 Sep 13:44).

What stops it being a *good* beta build is not the toolchain but packaging hygiene:

1. **An unused AI package ships ~105 MB of compute shaders.** `com.unity.ai.inference` 2.3.0
   (`Packages/manifest.json:11`) puts its shaders in a package `Resources/` folder, so they are
   always included. The build report puts `ConvGeneric.compute` alone at **100.7 MB uncompressed**
   ("Other Assets 107.4 mb, 87.5%" of user assets). No game script references it. It is also the
   most likely reason the builds are so slow.
2. **No app icon.** Every Android icon slot in `ProjectSettings.asset` (lines ~398-490) has
   `m_Textures: []`, so the phone shows Unity's default icon.
3. **The Unity splash screen is on** with a near-black background (`#231F20`), not the brand.
4. **The .bat can report a false success.** It checks only whether the APK file exists, and an
   older APK is already sitting there. A failed build also exits with code 0 (see 6.2).
5. **The Unity Editor is open on this project right now** (`Unity.exe` PID 5544, `Editor.log`
   started 3 Oct 13:02). A headless build will refuse to start until it is closed. Free RAM at
   audit time: **0.5 GB of 7.6 GB**.

---

## 2. Current settings, measured

| Item | Current value | Source | Verdict |
|---|---|---|---|
| Scenes in build (order) | 0 `CharacterCreation.unity` (on), 1 `SampleScene` (off), 2 `MaliGoWorld.unity` (on), 3 `MaliGoIsometricWorld` (off) | `EditorBuildSettings.asset:8-19` | OK. Shipped order is CharacterCreation then MaliGoWorld. `MaliGoBuildPipeline.cs:34` also passes this exact list, so the build doesn't depend on the checkboxes |
| Product / company | `MaliGo` / `MaliGo` | `ProjectSettings.asset:15-16`, `MaliGoAndroidSetup.cs:42-43` | OK |
| Package id | `com.maligo.app` | `ProjectSettings.asset:170`, `MaliGoAndroidSetup.cs:41` | OK. The same id as the RN app would make the two overwrite each other on one phone. I couldn't check the RN app's package id (its `app.json` had no `android.package` line that I could see). If it matches, use `com.maligo.game` |
| Version / bundle code | `1.0` / `1` | `ProjectSettings.asset:146,178` | Change. `MaliGoAndroidSetup.cs:53-54` only sets these when they are empty or 0, so they stay at 1.0 / 1 forever |
| Orientation | `defaultScreenOrientation: 3` = Landscape Left (fixed) | `ProjectSettings.asset:11`, `MaliGoAndroidSetup.cs:47` | Works, but it is locked to one side. The autorotate flags (lines 62-65) only apply when orientation = AutoRotation (4) |
| Render outside safe area | `androidRenderOutsideSafeArea: 1` | `ProjectSettings.asset:71` | Risk. No script reads `Screen.safeArea` (grep of `Assets/Scripts`: 0 hits), so in landscape, HUD and dialogue can sit under a notch or punch-hole camera |
| App icon | none (all Android `m_Textures: []`, `m_BuildTargetIcons: []`) | `ProjectSettings.asset:299-490` | **Missing** |
| Splash | Unity splash + Unity logo on, bg `{r:0.137,g:0.122,b:0.125}`, no logos | `ProjectSettings.asset:19-42` | Off-brand. The Unity logo texture adds 2.7 MB (build report) |
| Min / target SDK | 26 (Android 8.0) / 0 = Auto (highest SDK installed) | `ProjectSettings.asset:179-180` | OK for sideloading |
| Scripting backend / arch | IL2CPP (`scriptingBackend: Android: 1`) / ARM64 only (`AndroidTargetArchitectures: 2`) | `ProjectSettings.asset:270,785-786` | OK. This Unity install ships no Mono player for Android (`MaliGoAndroidSetup.cs:9-12`). Very old 32-bit-only phones can't install it, which is acceptable |
| Managed stripping | `managedStrippingLevel: {}` = default (Minimal for IL2CPP), `stripEngineCode: 1` | `ProjectSettings.asset:184,790` | Leave as is for the beta. Stripping isn't what drives the size; the AI package is |
| Minify | Release 0 / Debug 0 | `ProjectSettings.asset:293-294` | OK |
| Signing | `androidUseCustomKeystore: 0`, keystore name empty | `ProjectSettings.asset:274-275,287` | Produces a **debug-signed** APK using `%USERPROFILE%\.android\debug.keystore` (exists, dated 12 Jul 2026). Fine for sideloading. Updates only install over the old version if they're signed by the same laptop's debug key |
| Input | `activeInputHandler: 1` (new Input System only) | `ProjectSettings.asset:879` | OK, matches the brief |
| Graphics API | `m_BuildTargetGraphicsAPIs: []` = automatic (Vulkan, then GLES3 fallback) | `ProjectSettings.asset:497` | OK |
| Frame pacing | `androidUseSwappy: 1` | `ProjectSettings.asset:72` | OK |
| Multithreaded rendering | `mobileMTRendering: Android: 1` | `ProjectSettings.asset:505-506` | OK |
| Colour space | Linear (`m_ActiveColorSpace: 1`) | `ProjectSettings.asset:50` | OK on API 26+ with GLES3/Vulkan |
| Quality default on Android | `m_PerPlatformDefaultQuality: Android: 2` = **Medium** (hard shadows, vSync 1, 1 pixel light) | `QualitySettings.asset:115-136,323` | Change to Low for low-end phones |
| URP asset (all six quality levels share it) | `Assets/Settings/UniversalRP.asset`: HDR **on**, MSAA off, render scale 1.0, main-light shadows on, shadow distance 50, SRP batcher on | `UniversalRP.asset:26-72` | HDR is wasted cost on phones. See the note on the 2D renderer below |
| Renderer | **`Renderer2D.asset` (URP 2D Renderer)** is the only renderer for a 3D Kenney world | `UniversalRP.asset:19-21`; Editor-prev.log: "Renderer2D ... Renderer2DData" | Works (3D Lit meshes draw through their Universal2D pass) but 3D lights and shadows are ignored. Don't switch to the Universal (Forward) renderer before the beta, because it would change the look the testers have seen. Flagged for after the beta |
| `Application.targetFrameRate` | not set anywhere (grep of `Assets/`: 0 hits) | n/a | Android defaults to 30 fps. That's fine for this game but should be explicit |
| `Screen.sleepTimeout` | not set (0 hits) | n/a | **The screen dims and locks while a player reads dialogue** or thinks about a choice. Set `SleepTimeout.NeverSleep` while the world scene is active |
| Data persistence | `Application.persistentDataPath/player_data.json` | `PlayerDataManager.cs:10,26,133` | On device: `/storage/emulated/0/Android/data/com.maligo.app/files/player_data.json`. Survives updates signed with the same key. Deleted on uninstall. No `OnApplicationPause` save hook exists (grep: 0 hits), so anything not yet written when Android kills the app is lost. I couldn't check from the code alone how often `Save()` is called |
| PlayerPrefs | not used | grep | OK |

### Resources baking (`MaliGoResourceBaker.cs`)

- Copies 5 Kenney UI PNGs into `Assets/Resources/MaliGoUI/` (all present, 0.3-0.6 KB each) and
  writes `Assets/Resources/PlayerCharacterCatalog.asset` (1.2 KB). `KenneyUiSprites.cs:28` and
  `KenneyRuntimeCatalogFactory.cs:19` load these, so the runtime does not rely on `AssetDatabase`
  (that is Editor-only and sits behind `#if UNITY_EDITOR`). Good.
- `Assets/MaliGo/Characters/PlayerCharacterAnimator.controller` and `PlayerSkinMaterial.mat` exist,
  so the "player will not animate" warning (`MaliGoResourceBaker.cs:126`) won't fire.
- `PlayerCharacterSpawner.cs:42` calls `Resources.Load<GameObject>("PlayerCharacter")`, but no
  such asset exists in `Assets/Resources`. It's a harmless null if the spawner builds the player
  from the catalog (it appears to). Worth a phone check: the player character should appear.
- **New runtime art must go through the same route.** Any Mali portrait, icon or sprite the
  build agents add and load by path must be either referenced from a scene/prefab or placed under
  `Assets/Resources/`. If not, it will work in the Editor and be missing on the phone.

### What actually goes into the APK (size drivers)

Only assets referenced by the two shipped scenes, plus everything in any `Resources/` folder
(project or package), get packed. The Kenney packs are **not** pulled in wholesale: there is only
one `Resources` folder in `Assets/`, and the build report counts meshes at 887.6 KB and textures
at 9.3 MB in total.

From the 16 Sep build report (`build.log:10798-10870`, same numbers on 22 Sep):

| Category | Uncompressed |
|---|---|
| Other Assets | 107.4 MB (87.5%), almost all `com.unity.ai.inference` compute shaders |
| Textures | 9.3 MB |
| Shaders | 4.7 MB |
| Meshes | 0.9 MB |
| Levels | 62 KB |
| "Complete build size" | 1.0 GB / 988.3 MB (includes IL2CPP and symbol intermediates, not the APK) |

Largest single items: `Packages/com.unity.ai.inference/.../ConvGeneric.compute` **100.7 MB**,
`Dense.compute` 1.7 MB, `ConvTranspose.shader` 1.5 MB, Unity splash logo 2.7 MB,
`Light2D.shader` 0.8 MB, `Assets/Sprites/Sam_Idle.png` 0.78 MB, 4 character skins at 0.5 MB each,
11 URP film-grain textures at 0.26 MB each.

`com.unity.ai.inference` also pulls in `com.unity.dt.app-ui` 2.1.1 (`packages-lock.json:102-155`).
That package adds `HapticFeedback.java` to the APK (`build.log:8993`), the `APP_UI_EDITOR_ONLY`
define (`ProjectSettings.asset:781`) and a config object (`EditorBuildSettings.asset:21`).
The folder `Assets/AI Toolkit/` contains only an empty `Temp/`. No script under `Assets/Scripts`
or `Assets/MaliGo` references `Unity.InferenceEngine` or `Unity.AppUI`.

Folder sizes on disk (none of them are a size driver, but they slow down imports):
nature-kit 28.1 MB, train-kit 17.7, furniture-kit 16.9, car-kit 13.8, food-kit 13.0,
city-kit-commercial 10.7, holiday-kit 10.1, city-kit-suburban 7.7, city-kit-roads 6.7,
mini-arcade 4.1, animated-characters 2.1, ui-pack-adventure 0.6 MB.
`Assets/Settings/Lit2DSceneTemplate.scenetemplate` is 3.9 MB (Editor-only).
`Library/` is 11.1 GB. Free disk on C: is 24.2 GB.

---

## 3. Prioritised fixes (file + exact change)

These are for the build phase. This audit changed nothing.

### P0: needed for a trustworthy build and a good first launch

1. **Remove the unused AI package.** In `Packages/manifest.json`, delete line 11
   `"com.unity.ai.inference": "2.3.0",`. Let Unity re-resolve the lock file. Then delete the
   `com.unity.dt.app-ui:` line in `ProjectSettings/EditorBuildSettings.asset:21`, and remove
   `APP_UI_EDITOR_ONLY` from `scriptingDefineSymbols` (`ProjectSettings.asset:781-782`) via Player
   Settings > Other > Scripting Define Symbols. First do a compile check for any `using
   Unity.InferenceEngine` / `Unity.AppUI` (none found in `Assets/Scripts` or `Assets/MaliGo`).
   Expected effect: about 105 MB less uncompressed content and much less shader compilation. I
   couldn't measure the new APK size without a build. My estimate is the APK falls well below
   64 MB.
2. **Make failures fail.** In `Assets/Editor/MaliGoBuildPipeline.cs`, after the `LogError` at
   line 54, add `if (Application.isBatchMode) EditorApplication.Exit(1);`. Before line 42, delete
   any old APK: `if (File.Exists(ApkOutputPath)) File.Delete(ApkOutputPath);`.
   In `BUILD_BETA_APK.bat`, add `set RC=%ERRORLEVEL%` straight after the Unity line (line 19) and
   test `if %RC%==0 if exist ...apk` instead of checking only that the file exists.
3. **App icon from Mali.** Source: `mali2.png` (534x615, RN app `assets/images/`). It's the
   cleanest head-and-scarf artwork on a white or transparent background. **Do not use the RN
   `icon.png` or `android-icon-*.png`. They are the Expo template's blue placeholder, not
   MaliGo.** Make a 1024x1024 PNG: Mali's head centred inside the middle 66% (adaptive-icon safe
   zone) on a brand cream `#F9FFF6` or forest green `#087A18` background (`colors.ts:2,7`). Also
   make a separate background layer. Put them in e.g. `Assets/MaliGo/Branding/AppIcon_*.png`
   (texture type Default, no mipmaps, max size 1024). Then add to
   `MaliGoAndroidSetup.Configure()`: set `PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,
   AndroidPlatformIconKind.Adaptive, …)` with background and foreground layers, plus `Legacy` and
   `Round` kinds from the same 1024 image, so the icon survives any settings reset. Check the
   licence and ownership of the Mali artwork with the founder; it comes from the separate app.
4. **Keep the screen awake and set the frame rate.** In the runtime bootstrap
   (`MaliGoIdentityRuntimeBootstrap`, as one new `Step(...)`, without editing scenes), set
   `Screen.sleepTimeout = SleepTimeout.NeverSleep; Application.targetFrameRate = 30;`.
   Optionally return to `SleepTimeout.SystemSetting` on the end-of-day screen. 30 fps saves
   battery and heat on low-end phones and is plenty for a walking-and-dialogue game.
5. **Save on pause.** Add `OnApplicationPause(bool paused) { if (paused) Save(); }` to
   `PlayerDataManager.cs`. Android kills backgrounded apps without warning, and the day's money
   state is the core of the game.
6. **Close the Editor before building.** It is open now (see section 5).

### P1: first-launch polish and low-end phones

7. **Version.** In `MaliGoAndroidSetup.cs:53-54`, replace the "keep if set" logic with
   `PlayerSettings.bundleVersion = "0.9.0-beta";` and
   `PlayerSettings.Android.bundleVersionCode = <increment per build>`. Bump the code on every
   APK handed to testers so the install always counts as an update.
8. **Splash.** In `MaliGoAndroidSetup.Configure()`, set
   `PlayerSettings.SplashScreen.showUnityLogo = false;` and
   `PlayerSettings.SplashScreen.backgroundColor = new Color32(0x08,0x7A,0x18,0xFF);` (or cream
   `#F9FFF6`). Optionally add Mali's logo via `PlayerSettings.SplashScreen.logos`. Unity 6
   lets Personal licences turn off the Unity splash. I couldn't verify which licence tier is
   active. If it refuses, keep the splash and only change the colour.
9. **Low quality on Android.** In `ProjectSettings/QualitySettings.asset:323`, set
   `Android: 2` to `Android: 1` (Low: no shadows, vSync 0, no pixel lights). Or do it in code via
   `QualitySettings.SetQualityLevel(1)` on Android.
10. **HDR off.** In `Assets/Settings/UniversalRP.asset:26`, set `m_SupportsHDR: 0`. There is no
    bloom/HDR look to lose, and it saves bandwidth on cheap GPUs. Optionally set
    `m_MainLightShadowsSupported: 0`, since the 2D renderer doesn't draw 3D shadows anyway.
11. **Safe area.** In `ProjectSettings.asset:71`, set `androidRenderOutsideSafeArea: 0`, or set
    `PlayerSettings.Android.renderOutsideSafeArea = false` in `Configure()`. That keeps all UI
    clear of notches without touching the runtime UI code. An alternative is a safe-area panel
    in the new UI kit.
12. **Both landscape directions.** In `Configure()`, set
    `PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;` and keep the
    existing landscape-only autorotate flags (lines 48-51). A phone held either way up is then
    the right way up.

### P2: build speed and tidiness (optional before the beta)

13. Remove packages the game doesn't use from `Packages/manifest.json`. This cuts compile/import
    time and the risk of package `Resources/` content shipping. Check each with a compile first:
    `com.unity.2d.aseprite`, `com.unity.2d.psdimporter`, `com.unity.2d.spriteshape`,
    `com.unity.2d.tilemap.extras`, `com.unity.2d.tooling`, `com.unity.multiplayer.center`,
    `com.unity.visualscripting`, `com.unity.collab-proxy`. Don't remove `com.unity.2d.*`
    packages the 2D renderer needs (`2d.sprite` and `2d.animation` stay).
14. **Gradle heap.** Gradle starts with `-Xmx4096m` (`build.log`, near the end). On a 7.6 GB
    machine with Unity also resident, that can swap. A custom
    `Assets/Plugins/Android/gradleTemplate.properties` with `org.gradle.jvmargs=-Xmx2048m` is
    the safer value. I couldn't verify that Gradle succeeds at 2 GB; 4 GB has worked twice.
15. `Builds/Android/MaliGo-Beta_BackUpThisFolder_ButDontShipItWithYourGame` and
    `MaliGo_BurstDebugInformation_DoNotShip` are normal by-products. Never send them to testers.

---

## 4. What must be true for a clean headless build

- [ ] Unity Editor, Unity Hub-launched Editor and any `Unity.exe` for this project are closed
      (project lock). Check: `tasklist | findstr /i "Unity.exe"` returns nothing.
- [ ] Toolchain paths exist (verified 3 Oct): `C:\JDK17`,
      `%LOCALAPPDATA%\Android\Sdk`, `...\Sdk\ndk\27.2.12479018`. They're
      hard-coded in `MaliGoAndroidSetup.cs:16-21`; a missing one only logs a warning.
- [ ] At least about 10 GB free on C: (24.2 GB free now; `Library/` is already 11.1 GB).
- [ ] Code compiles with zero errors (agents' `dotnet build` check, per the brief §7).
      Known harmless warning: `CS8785 AttributeBasedFieldGenerator` (`build.log:311`).
- [ ] `Assets/Resources/` contains `PlayerCharacterCatalog.asset` and `MaliGoUI/*.png`.
      The pipeline rebakes them anyway.
- [ ] No private-strategy wording in any shipped text (brief §6). This is outside this
      audit's scope. Have a reviewer check it.

---

## 5. Exact steps: headless build on the 8 GB laptop

Measured: 101 min headless on 16 Sep (it included a platform switch and full import),
32.6 min from the Editor on 22 Sep (warm Library). **Expect 30-60 min** with a warm `Library/`.
Expect up to about 1 h 45 min if `Library/` was deleted or the platform needs switching.
Removing the AI package (fix 1) should shorten both, though I couldn't measure by how much.

1. **Save and close everything.** Close the Unity Editor (File > Exit), then **quit Unity Hub
   from the system tray** (it keeps helper processes alive). Also close the browser, Teams/
   WhatsApp desktop, OneDrive sync (pause it: tray > Pause syncing > 2 hours), VS Code/Rider and
   Android Studio. At audit time only 0.5 GB of 7.6 GB was free.
2. Check nothing Unity is left: in a terminal, `tasklist | findstr /i "Unity"`. If
   `Unity.exe` is listed, end it in Task Manager. Unity Hub entries are harmless but use RAM.
3. Plug the laptop into power and set Windows power mode to "Best performance". Turn off sleep
   for the duration (Settings > System > Power > Screen and sleep > Never).
4. Double-click `C:\Temp\MaliGoGameDev\BUILD_BETA_APK.bat`. That runs:
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe" -batchmode -quit ^
     -projectPath "C:\Temp\MaliGoGameDev" -buildTarget Android ^
     -executeMethod MaliGoBuildPipeline.BuildAndroidBeta -logFile "C:\Temp\MaliGoGameDev\build.log"
   ```
   Leave the window open. Don't use the laptop meanwhile.
5. **Watching progress (optional).** Opening `build.log` in Notepad is cheap. Milestones in
   order: `[MaliGoAndroidSetup] SDK ->`, `[MaliGoResourceBaker] Runtime resources baked`,
   `[MaliGoBuildPipeline] Starting Android build`, `DisplayProgressbar: Incremental Player
   Build`, `DisplayProgressbar: Building Gradle project`, `DisplayProgressbar: Moving output
   package(s)`.
6. **Success** looks like all of the following in `build.log`:
   - `Build Finished, Result: Success.`
   - `[MaliGoBuildPipeline] BUILD SUCCEEDED: C:/Temp/MaliGoGameDev/Builds/Android/MaliGo-Beta.apk`
     followed by `Size:`, `Time:`, `Warnings: N, Errors: 0`
   - last line `Application will terminate with return code 0`
   - and `Builds\Android\MaliGo-Beta.apk` has **today's** timestamp. Until fix 2 lands, the .bat's
     "BUILD SUCCEEDED" only means *an* APK exists, possibly the 22 Sep one.
7. **Failure:** search `build.log` for, in this order: `BUILD Failed`, `error CS`,
   `Build Finished, Result: Failed`, `CommandInvokationFailure`, `FAILURE: Build failed with an
   exception` (Gradle), `another Unity instance is running` (Editor still open; go back to
   step 1), `Incompatible Java version` (JDK path wrong; must be `C:\JDK17`), `OutOfMemory` or
   `Java heap space` (close more apps or apply fix 14).
8. Install on the phone: enable Developer options > USB debugging, then
   `"%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" install -r "C:\Temp\MaliGoGameDev\Builds\Android\MaliGo-Beta.apk"`.
   Or copy the APK to the phone and open it (allow "install unknown apps").
   If install fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, the phone has a build signed by
   another machine's debug key. Uninstall first; this erases the save.

## 6. First-launch checklist on the phone

- Icon is Mali, not the Unity cube (needs fix 3). Name under the icon reads "MaliGo".
- Splash is brand-coloured (needs fix 8). The app opens on Character Creation in landscape.
- The player character spawns in the world and UI panels show their Kenney art. That proves
  Resources baking worked.
- The screen doesn't dim during a long dialogue (needs fix 4).
- Nothing important is under the camera notch (needs fix 11).
- Background the app mid-day, swipe it away, reopen: the day's money and progress are kept
  (needs fix 5). The save file is at
  `/storage/emulated/0/Android/data/com.maligo.app/files/player_data.json`
  (`adb shell run-as` isn't needed for external files dir;
  `adb pull /storage/emulated/0/Android/data/com.maligo.app/files/player_data.json`).
- Smooth enough at 30 fps on the lowest-end tester phone, with no overheating after 15 min.

## 7. Could not verify

- New APK size and build time after removing `com.unity.ai.inference`. That needs a build.
- Which Unity licence tier is active, which decides whether the Unity splash can be turned off.
- How often `PlayerDataManager.Save()` is called during a day. I only read the file around
  lines 10-146.
- Whether the RN app uses the same Android package id `com.maligo.app`.
- How the 2D-renderer look compares on device with the Editor, and real fps on a low-end phone.
- Whether Vulkan on testers' specific phones has driver issues. Auto API order falls back to
  GLES3, but only on devices that lack Vulkan.
- Ownership and licence of the Mali artwork for use in the game icon (founder to confirm).

---

**Note:** nothing in the build settings, package id, product name or splash refers to private
strategy. Keep it that way when the icon and splash are added.
