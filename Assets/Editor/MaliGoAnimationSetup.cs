using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Sets up the player's Animator controller on Kenney's real clips: "Idle" plays "Root|Idle", and "Locomotion"
/// is a 1D blend tree on the "Gait" parameter from "Root|Idle" (0) to "Root|Run" (1), so slow speeds take short,
/// walk-like steps instead of a slow-motion sprint (PlayerCharacterVisualController drives Gait and the playback
/// rate from MovementMath). Both clips loop; Idle and Locomotion crossfade on "Speed" with no exit time.
/// (History: the states once pointed at each file's first clip, "Root|0.Targeting Pose", a 0.03 s static pose,
/// and Kenney's clips import with looping off, so the character never animated.)
///
/// The checked-in PlayerCharacterAnimator.controller already matches this; run it again only if the controller
/// or the clips' import settings were rebuilt. It is idempotent.
/// Run headless: Unity -batchmode -quit -executeMethod MaliGoAnimationSetup.Run
/// </summary>
public static class MaliGoAnimationSetup
{
    const string ControllerPath = "Assets/MaliGo/Characters/PlayerCharacterAnimator.controller";
    const string AnimationFolder = "Assets/kenney_animated-characters-protagonists/Animations/";
    const string SpeedParameter = "Speed";
    const string GaitParameter = "Gait";
    const string LocomotionState = "Locomotion";

    /// <summary>"Speed" at which Idle and Locomotion crossfade (the visual sends 0 when standing).</summary>
    const float MovingThreshold = 0.05f;
    const float StartCrossfadeSeconds = 0.12f;
    const float StopCrossfadeSeconds = 0.15f;

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

        EnsureParameter(controller, SpeedParameter, 0f);
        EnsureParameter(controller, GaitParameter, 0.5f);

        AnimatorState idleState = null;
        AnimatorState moveState = null;
        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
        {
            if (child.state.name == "Idle")
            {
                idleState = child.state;
            }
            else if (child.state.name == LocomotionState || child.state.name == "Run")
            {
                moveState = child.state;
            }
        }

        if (idleState == null || moveState == null)
        {
            Debug.LogError($"[MaliGoAnimationSetup] Missing state: Idle={idleState != null} Locomotion={moveState != null}");
            ExitIfBatch(1);
            return;
        }

        idleState.motion = idle;
        moveState.name = LocomotionState;
        moveState.motion = BuildLocomotionTree(controller, moveState.motion as BlendTree, idle, run);

        ConfigureTransitions(idleState, moveState, AnimatorConditionMode.Greater, StartCrossfadeSeconds);
        ConfigureTransitions(moveState, idleState, AnimatorConditionMode.Less, StopCrossfadeSeconds);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[MaliGoAnimationSetup] Idle -> '{idle.name}' (loop {idle.isLooping}), Locomotion -> blend '{idle.name}'/'{run.name}' on {GaitParameter} (run loop {run.isLooping}).");
        ExitIfBatch(0);
    }

    static BlendTree BuildLocomotionTree(AnimatorController controller, BlendTree tree, AnimationClip idle, AnimationClip run)
    {
        if (tree == null)
        {
            tree = new BlendTree { hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
        }

        tree.name = LocomotionState;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = GaitParameter;
        tree.useAutomaticThresholds = false;
        tree.minThreshold = 0f;
        tree.maxThreshold = 1f;
        tree.children = new[]
        {
            new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f, directBlendParameter = GaitParameter },
            new ChildMotion { motion = run, threshold = 1f, timeScale = 1f, directBlendParameter = GaitParameter }
        };
        EditorUtility.SetDirty(tree);
        return tree;
    }

    static void ConfigureTransitions(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float duration)
    {
        AnimatorStateTransition transition = from.transitions.FirstOrDefault(t => t.destinationState == to)
            ?? from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.offset = 0f;
        transition.canTransitionToSelf = false;
        // A quick tap (start then stop) may turn back mid-crossfade instead of finishing it first.
        transition.interruptionSource = TransitionInterruptionSource.Destination;
        transition.conditions = new[]
        {
            new AnimatorCondition { mode = mode, parameter = SpeedParameter, threshold = MovingThreshold }
        };
    }

    static void EnsureParameter(AnimatorController controller, string name, float defaultValue)
    {
        if (controller.parameters.Any(p => p.name == name))
        {
            return;
        }

        controller.AddParameter(new AnimatorControllerParameter
        {
            name = name,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = defaultValue
        });
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
