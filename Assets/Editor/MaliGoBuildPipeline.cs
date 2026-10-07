using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the MaliGo Android beta APK. Configures Android tooling, ships only
/// CharacterCreation + MaliGoWorld (excludes the unused SampleScene and the
/// abandoned MaliGoIsometricWorld prototype without deleting either from the project),
/// and reports a clear pass/fail summary.
/// </summary>
public static class MaliGoBuildPipeline
{
    const string ApkOutputPath = "Builds/Android/MaliGo-Beta.apk";
    const string CharacterCreationScenePath = "Assets/Scenes/CharacterCreation.unity";
    const string WorldScenePath = "Assets/Scenes/MaliGoWorld.unity";

    [MenuItem("MaliGo/Build/Android Beta APK")]
    public static void BuildAndroidBeta()
    {
        MaliGoAndroidSetup.Configure();
        MaliGoResourceBaker.BakeAll();
        SetShippingScenes();

        string outputDir = Path.GetDirectoryName(ApkOutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Never let an older APK pass for this build's output.
        if (File.Exists(ApkOutputPath))
        {
            try
            {
                File.Delete(ApkOutputPath);
                Debug.Log($"[MaliGoBuildPipeline] Deleted the previous APK at {ApkOutputPath}.");
            }
            catch (System.Exception ex)
            {
                Fail($"[MaliGoBuildPipeline] BUILD FAILED: could not delete the previous APK at {ApkOutputPath}: {ex.Message}");
                return;
            }
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { CharacterCreationScenePath, WorldScenePath },
            locationPathName = ApkOutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log("[MaliGoBuildPipeline] Starting Android build - this can take several minutes on first run (Gradle setup).");

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            // A copy named after the version (e.g. MaliGo-0.3.0-beta.1.apk), so files sent to testers say
            // which build they are.
            string versioned = Path.Combine(outputDir ?? "", $"MaliGo-{PlayerSettings.bundleVersion}.apk");
            File.Copy(ApkOutputPath, versioned, true);
            Debug.Log($"[MaliGoBuildPipeline] Version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), copy at {versioned}");
            Debug.Log($"[MaliGoBuildPipeline] BUILD SUCCEEDED: {summary.outputPath}\n" +
                      $"Size: {summary.totalSize / (1024f * 1024f):0.0} MB\n" +
                      $"Time: {summary.totalTime}\n" +
                      $"Warnings: {summary.totalWarnings}, Errors: {summary.totalErrors}");
        }
        else
        {
            Fail($"[MaliGoBuildPipeline] BUILD {summary.result}: {summary.totalErrors} error(s), {summary.totalWarnings} warning(s). " +
                 "Check the Console above for the specific failure.");
        }
    }

    /// <summary>
    /// Logs the error and, in batch mode, exits Unity with code 1 so BUILD_BETA_APK.bat reports a failure.
    /// </summary>
    static void Fail(string message)
    {
        Debug.LogError(message);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }

    public static void SetShippingScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var scene in scenes)
        {
            bool shouldShip = scene.path == CharacterCreationScenePath || scene.path == WorldScenePath;
            scene.enabled = shouldShip;
        }
        EditorBuildSettings.scenes = scenes;

        bool hasCharacterCreation = scenes.Any(s => s.path == CharacterCreationScenePath);
        bool hasWorld = scenes.Any(s => s.path == WorldScenePath);
        if (!hasCharacterCreation || !hasWorld)
        {
            Debug.LogWarning("[MaliGoBuildPipeline] Expected scenes not found in Build Settings - add CharacterCreation and MaliGoWorld manually if the build fails to find them.");
        }
    }
}
