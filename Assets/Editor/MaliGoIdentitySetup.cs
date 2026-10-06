using MaliGo.PlayerIdentity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MaliGoIdentitySetup
{
    const string CharacterCreationScenePath = "Assets/Scenes/CharacterCreation.unity";
    const string WorldScenePath = "Assets/Scenes/MaliGoWorld.unity";

    [MenuItem("MaliGo/Setup Player Identity System")]
    public static void SetupPlayerIdentitySystem()
    {
        CreateCharacterCreationScene();
        WireMaliGoWorldScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[MaliGo] Player Identity System setup complete.");
    }

    static void CreateCharacterCreationScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        scene.name = "CharacterCreation";

        var bootstrap = new GameObject("CharacterCreationBootstrap");
        bootstrap.AddComponent<CharacterCreationUI>();

        var manager = new GameObject("PlayerDataManager");
        manager.AddComponent<PlayerDataManager>();

        EditorSceneManager.SaveScene(scene, CharacterCreationScenePath);
    }

    static void WireMaliGoWorldScene()
    {
        var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

        EnsurePlayerDataManagerInScene();
        EnsureGameFlowController();
        EnsurePlayerCharacterSpawner();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void EnsurePlayerDataManagerInScene()
    {
        if (Object.FindFirstObjectByType<PlayerDataManager>() != null)
        {
            return;
        }

        var existing = GameObject.Find("PlayerDataManager");
        if (existing != null)
        {
            existing.AddComponent<PlayerDataManager>();
            return;
        }

        var go = new GameObject("PlayerDataManager");
        go.AddComponent<PlayerDataManager>();
    }

    static void EnsureGameFlowController()
    {
        if (Object.FindFirstObjectByType<GameFlowController>() != null)
        {
            return;
        }

        var go = GameObject.Find("MaliGo_Systems") ?? new GameObject("MaliGo_Systems");
        if (go.GetComponent<GameFlowController>() == null)
        {
            go.AddComponent<GameFlowController>();
        }
    }

    static void EnsurePlayerCharacterSpawner()
    {
        var go = GameObject.Find("MaliGo_Systems") ?? new GameObject("MaliGo_Systems");
        if (go.GetComponent<PlayerCharacterSpawner>() == null)
        {
            go.AddComponent<PlayerCharacterSpawner>();
        }
    }

    static void UpdateBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/CharacterCreation.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
            new EditorBuildSettingsScene(WorldScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/MaliGoIsometricWorld.unity", true)
        };

        EditorBuildSettings.scenes = scenes;
    }
}
