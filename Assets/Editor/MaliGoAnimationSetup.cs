using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Points the player's Idle and Run states at Kenney's real "Root|Idle" and "Root|Run" clips and makes both
/// loop. The controller had both states on each file's first clip, "Root|0.Targeting Pose" (a 0.03 s static
/// pose), and Kenney's clips import with looping off, so the character never animated.
/// Run headless: Unity -batchmode -quit -executeMethod MaliGoAnimationSetup.Run
/// </summary>
public static class MaliGoAnimationSetup
{
    const string ControllerPath = "Assets/MaliGo/Characters/PlayerCharacterAnimator.controller";
    const string AnimationFolder = "Assets/kenney_animated-characters-protagonists/Animations/";

    [MenuItem("MaliGo/Characters/Fix Player Animation Clips")]
    public static void Run()
    {
        AnimationClip idle = PrepareClip(AnimationFolder + "idle.fbx", "Root|Idle");
        AnimationClip run = PrepareClip(AnimationFolder + "run.fbx", "Root|Run");

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || idle == null || run == null)
        {
            Debug.LogError($"[MaliGoAnimationSetup] Missing asset: controller={controller != null} idle={idle != null} run={run != null}");
            ExitIfBatch(1);
            return;
        }

        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state.name == "Idle")
            {
                child.state.motion = idle;
            }
            else if (child.state.name == "Run")
            {
                child.state.motion = run;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MaliGoAnimationSetup] Idle -> '{idle.name}' (loop {idle.isLooping}), Run -> '{run.name}' (loop {run.isLooping}).");
        ExitIfBatch(0);
    }

    static AnimationClip PrepareClip(string path, string clipName)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[MaliGoAnimationSetup] No ModelImporter at " + path);
            return null;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations != null && importer.clipAnimations.Length > 0
            ? importer.clipAnimations
            : importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.name == clipName && !clip.loopTime)
            {
                clip.loopTime = true;
                changed = true;
            }
        }

        if (changed || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
    }

    static void ExitIfBatch(int code)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(code);
        }
    }
}
