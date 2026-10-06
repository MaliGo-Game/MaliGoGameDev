using System.IO;
using UnityEditor;
using UnityEngine;
using MaliGo.PlayerIdentity;

/// <summary>
/// Bakes everything the RUNTIME needs into Assets/Resources so it survives a player build.
/// Without this the APK ships broken: KenneyRuntimeCatalogFactory falls back to AssetDatabase,
/// which is Editor-only, so in a build the player character never spawns.
/// </summary>
public static class MaliGoResourceBaker
{
    const string ResourcesRoot = "Assets/Resources";
    const string CatalogAssetPath = "Assets/Resources/PlayerCharacterCatalog.asset";

    const string KenneyRoot = "Assets/kenney_animated-characters-protagonists";
    const string ModelPath = KenneyRoot + "/Model/characterMedium.fbx";
    const string AnimatorPath = "Assets/MaliGo/Characters/PlayerCharacterAnimator.controller";
    const string MaterialPath = "Assets/MaliGo/Characters/PlayerSkinMaterial.mat";

    [MenuItem("MaliGo/Build/Bake Runtime Resources")]
    public static void BakeAll()
    {
        EnsureFolder(ResourcesRoot);
        // The Kenney adventure UI sprites are no longer baked (DESIGN_SPEC §7.13, D10): the new UI kit
        // draws its own surfaces. Assets/Resources/MaliGoUI stays until WP9 removes every KenneyUiSprites
        // use and deletes the folder.
        BakePlayerCharacterCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MaliGoResourceBaker] Runtime resources baked into Assets/Resources.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void BakePlayerCharacterCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(CatalogAssetPath);
        bool isNew = catalog == null;
        if (isNew)
        {
            catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
        }

        catalog.characterModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        catalog.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
        catalog.baseSkinMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (catalog.baseSkinMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var runtimeMaterial = new Material(shader) { name = "PlayerSkinMaterial" };
            EnsureFolder("Assets/MaliGo/Characters");
            AssetDatabase.CreateAsset(runtimeMaterial, MaterialPath);
            catalog.baseSkinMaterial = runtimeMaterial;
        }

        catalog.skinOptions = new[]
        {
            MakeSkin("skater_male", KenneyRoot + "/Skins/skaterMaleA.png"),
            MakeSkin("skater_female", KenneyRoot + "/Skins/skaterFemaleA.png"),
            MakeSkin("criminal_male", KenneyRoot + "/Skins/criminalMaleA.png"),
            MakeSkin("cyborg_female", KenneyRoot + "/Skins/cyborgFemaleA.png")
        };

        if (isNew)
        {
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }
        else
        {
            EditorUtility.SetDirty(catalog);
        }

        if (catalog.characterModelPrefab == null)
        {
            Debug.LogError($"[MaliGoResourceBaker] Character model NOT found at {ModelPath} - the build will have no player.");
        }

        if (catalog.animatorController == null)
        {
            Debug.LogWarning($"[MaliGoResourceBaker] Animator controller not found at {AnimatorPath} - player will not animate. Enter Play Mode once in the Editor to auto-generate it.");
        }
    }

    static PlayerCharacterCatalog.SkinOption MakeSkin(string id, string texturePath)
    {
        return new PlayerCharacterCatalog.SkinOption
        {
            optionId = id,
            skinTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
        };
    }
}
