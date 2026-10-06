using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Android tooling + Player Settings configuration for the MaliGo beta.
///
/// This Unity install ships ONLY the il2cpp player variation (no mono variation exists at
/// PlaybackEngines/AndroidPlayer/Variations), so IL2CPP is the only viable backend - Mono
/// fails with "Mono2x library missing for the selected architecture". IL2CPP needs the NDK,
/// installed into the Android Studio SDK via sdkmanager rather than Unity's own copy.
/// </summary>
public static class MaliGoAndroidSetup
{
    // Android Studio installs the SDK under the current user's local app data folder
    // (%LOCALAPPDATA%\Android\Sdk); the NDK lives inside it.
    static readonly string SdkPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
    // Unity 6000.3 requires JDK 17 specifically. Android Studio's bundled JBR is Java 21,
    // which Unity rejects outright ("Incompatible Java version '21.0.10'"), so a dedicated
    // Temurin JDK 17 is installed here just for Unity's Gradle step.
    const string JdkPath = @"C:\JDK17";
    static readonly string NdkPath = System.IO.Path.Combine(SdkPath, "ndk", "27.2.12479018");

    // App version (DESIGN_SPEC §0): 0.2.0 / code 2 installs over the testers' 1.0 / code 1.
    public const string AppVersion = "0.2.0";
    public const int AppVersionCode = 2;

    // App icon layers (DESIGN_SPEC §6.6), made by tools/make_app_icon.py.
    const string IconBackgroundPath = "Assets/MaliGo/Branding/AppIcon_Background.png";
    const string IconForegroundPath = "Assets/MaliGo/Branding/AppIcon_Foreground.png";
    const string IconLegacyPath = "Assets/MaliGo/Branding/AppIcon_Legacy.png";

    // Splash background, brand forest green #0F5E2E (DESIGN_SPEC §7.13).
    static readonly Color32 SplashBackground = new Color32(0x0F, 0x5E, 0x2E, 0xFF);

    // Left behind by the removed com.unity.ai.inference / com.unity.dt.app-ui packages.
    const string AppUiDefine = "APP_UI_EDITOR_ONLY";
    const string AppUiConfigKey = "com.unity.dt.app-ui";

    [MenuItem("MaliGo/Android/Configure Tooling + Player Settings")]
    public static void Configure()
    {
        // Unity ignores custom tool paths while these "use embedded" flags are set, which is
        // why the JDK kept reverting to Unity's own (non-existent) bundled OpenJDK folder.
        EditorPrefs.SetBool("SdkUseEmbedded", false);
        EditorPrefs.SetBool("JdkUseEmbedded", false);
        EditorPrefs.SetBool("NdkUseEmbedded", false);

        // Setting these throws if a path fails Unity's validation. Don't let that abort the
        // whole build - a previously valid configuration can still carry it through.
        TrySetPath("SDK", SdkPath, p => AndroidExternalToolsSettings.sdkRootPath = p);
        TrySetPath("JDK", JdkPath, p => AndroidExternalToolsSettings.jdkRootPath = p);
        TrySetPath("NDK", NdkPath, p => AndroidExternalToolsSettings.ndkRootPath = p);

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.maligo.app");
        PlayerSettings.companyName = "MaliGo";
        PlayerSettings.productName = "MaliGo";

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Always set (never "keep if already set"), so every build is 0.2.0 / code 2.
        PlayerSettings.bundleVersion = AppVersion;
        PlayerSettings.Android.bundleVersionCode = AppVersionCode;

        ConfigureIcons();
        ConfigureSplash();
        RemoveAppUiLeftovers();

        Debug.Log($"[MaliGoAndroidSetup] SDK -> {AndroidExternalToolsSettings.sdkRootPath}\n" +
                  $"JDK -> {AndroidExternalToolsSettings.jdkRootPath}\n" +
                  $"NDK -> {AndroidExternalToolsSettings.ndkRootPath}\n" +
                  $"Package -> {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}\n" +
                  $"Scripting backend -> {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}\n" +
                  $"Architecture -> {PlayerSettings.Android.targetArchitectures}\n" +
                  $"Min SDK -> {PlayerSettings.Android.minSdkVersion}, Target SDK -> {PlayerSettings.Android.targetSdkVersion}\n" +
                  $"Version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode})");
    }

    /// <summary>
    /// Adaptive (background + foreground), Round and Legacy icons in every slot (DESIGN_SPEC §6.6).
    /// A missing texture only logs a warning, so the build still runs with the current icons.
    /// </summary>
    static void ConfigureIcons()
    {
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(IconBackgroundPath);
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(IconForegroundPath);
        var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(IconLegacyPath);
        if (background == null || foreground == null || legacy == null)
        {
            Debug.LogWarning("[MaliGoAndroidSetup] App icon textures missing under Assets/MaliGo/Branding " +
                             "(run tools/make_app_icon.py). Keeping the current icons.");
            return;
        }

        try
        {
            SetIconTextures(AndroidPlatformIconKind.Adaptive, background, foreground);
            // Unity 6.3 marks Round and Legacy obsolete (min SDK 26 means every phone uses Adaptive);
            // they are still filled, as DESIGN_SPEC §6.6 asks, so no slot shows Unity's default icon.
#pragma warning disable CS0618
            SetIconTextures(AndroidPlatformIconKind.Round, legacy);
            SetIconTextures(AndroidPlatformIconKind.Legacy, legacy);
#pragma warning restore CS0618
            Debug.Log("[MaliGoAndroidSetup] App icons set (Adaptive, Round, Legacy).");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MaliGoAndroidSetup] Could not set app icons: {ex.Message}");
        }
    }

    static void SetIconTextures(PlatformIconKind kind, params Texture2D[] layers)
    {
        PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        foreach (PlatformIcon icon in icons)
        {
            // Adaptive icons have two layers (0 = background, 1 = foreground); Round and Legacy have one.
            int count = Mathf.Min(icon.maxLayerCount, layers.Length);
            var textures = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                textures[i] = layers[i];
            }
            icon.SetTextures(textures);
        }
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
    }

    /// <summary>
    /// Turns the Unity splash off where the licence allows it, then logs what actually happened
    /// (the founder reports the "Splash shown:" line, H4). The background is brand green either way.
    /// </summary>
    static void ConfigureSplash()
    {
        try
        {
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.show = false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MaliGoAndroidSetup] Could not turn the splash screen off: {ex.Message}");
        }

        PlayerSettings.SplashScreen.backgroundColor = SplashBackground;
        Debug.Log($"[MaliGoAndroidSetup] Splash shown: {PlayerSettings.SplashScreen.show}");
    }

    /// <summary>
    /// Removes the define symbol and the config object the removed App UI package left behind.
    /// </summary>
    static void RemoveAppUiLeftovers()
    {
        RemoveDefine(NamedBuildTarget.Android);
        RemoveDefine(NamedBuildTarget.Standalone);
        if (EditorBuildSettings.RemoveConfigObject(AppUiConfigKey))
        {
            Debug.Log($"[MaliGoAndroidSetup] Removed config object '{AppUiConfigKey}'.");
        }
    }

    static void RemoveDefine(NamedBuildTarget target)
    {
        string current = PlayerSettings.GetScriptingDefineSymbols(target) ?? string.Empty;
        string[] symbols = current.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        var kept = new System.Collections.Generic.List<string>();
        foreach (string symbol in symbols)
        {
            string trimmed = symbol.Trim();
            if (trimmed.Length > 0 && trimmed != AppUiDefine)
            {
                kept.Add(trimmed);
            }
        }

        if (kept.Count != symbols.Length)
        {
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", kept));
            Debug.Log($"[MaliGoAndroidSetup] Removed {AppUiDefine} from {target.TargetName} define symbols.");
        }
    }

    static void TrySetPath(string label, string path, System.Action<string> setter)
    {
        if (!System.IO.Directory.Exists(path))
        {
            Debug.LogWarning($"[MaliGoAndroidSetup] {label} path does not exist: {path}");
            return;
        }

        try
        {
            setter(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MaliGoAndroidSetup] Could not set {label} path to '{path}': {ex.Message}\n" +
                             "Continuing with whatever is already configured.");
        }
    }

    [MenuItem("MaliGo/Android/Verify Tooling")]
    public static void Verify()
    {
        bool sdkOk = System.IO.Directory.Exists(AndroidExternalToolsSettings.sdkRootPath);
        bool jdkOk = System.IO.Directory.Exists(AndroidExternalToolsSettings.jdkRootPath);
        bool ndkOk = System.IO.Directory.Exists(AndroidExternalToolsSettings.ndkRootPath);
        Debug.Log($"[MaliGoAndroidSetup] SDK '{AndroidExternalToolsSettings.sdkRootPath}' exists: {sdkOk}\n" +
                  $"JDK '{AndroidExternalToolsSettings.jdkRootPath}' exists: {jdkOk}\n" +
                  $"NDK '{AndroidExternalToolsSettings.ndkRootPath}' exists: {ndkOk}");
    }
}
