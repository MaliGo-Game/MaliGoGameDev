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
        // The Kenney adventure UI sprites are no longer baked or shipped (DESIGN_SPEC §7.13, D10): the new UI
        // kit draws its own surfaces.
        BakePlayerCharacterCatalog();
        BakeInteriorPieceCatalog();
        BakeTownPieceCatalog();
        // SaveAssets only, no AssetDatabase.Refresh(): everything above goes through AssetDatabase.CreateAsset /
        // SetDirty, which needs no refresh, and BuildAndroidBeta calls this right before BuildPlayer, where a
        // Refresh could pick up the scripting-define change from MaliGoAndroidSetup.Configure, start a script
        // recompile mid-build and fail the headless build.
        AssetDatabase.SaveAssets();
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

    /// <summary>
    /// The furniture-kit models the walk-in rooms use (InteriorRoomBuilder.PieceIds, about 16 small FBX meshes):
    /// referenced from a catalog in Resources so exactly these ship, and nothing else from the kit.
    /// </summary>
    static void BakeInteriorPieceCatalog()
    {
        string assetPath = ResourcesRoot + "/" + MaliGo.World.InteriorPieceCatalog.ResourceName + ".asset";
        var catalog = AssetDatabase.LoadAssetAtPath<MaliGo.World.InteriorPieceCatalog>(assetPath);
        bool isNew = catalog == null;
        if (isNew)
        {
            catalog = ScriptableObject.CreateInstance<MaliGo.World.InteriorPieceCatalog>();
        }

        string[] ids = MaliGo.World.InteriorRoomBuilder.PieceIds;
        var pieces = new MaliGo.World.InteriorPieceCatalog.Piece[ids.Length];
        int missing = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            string modelPath = MaliGo.World.InteriorPieceCatalog.FurnitureFolder + ids[i] + ".fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                missing++;
                Debug.LogWarning($"[MaliGoResourceBaker] Furniture model not found at {modelPath} - the room will use a plain box for it.");
            }

            pieces[i] = new MaliGo.World.InteriorPieceCatalog.Piece { id = ids[i], prefab = model };
        }

        catalog.pieces = pieces;
        if (isNew)
        {
            AssetDatabase.CreateAsset(catalog, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(catalog);
        }

        Debug.Log($"[MaliGoResourceBaker] Interior piece catalog: {ids.Length - missing} of {ids.Length} furniture models.");
    }

    /// <summary>
    /// The city-kit models the streets around the town are built from (MaliGo.Core.TownLayout.ModelKeys: five road
    /// pieces, the houses, shops and trees, about 21 small FBX meshes): referenced from a catalog in Resources so
    /// exactly these ship. The kits are not in git (see .gitignore); a checkout without them keeps the catalog as
    /// committed rather than emptying it.
    /// </summary>
    static void BakeTownPieceCatalog()
    {
        string assetPath = ResourcesRoot + "/" + MaliGo.World.TownPieceCatalog.ResourceName + ".asset";
        var catalog = AssetDatabase.LoadAssetAtPath<MaliGo.World.TownPieceCatalog>(assetPath);
        bool isNew = catalog == null;
        if (isNew)
        {
            catalog = ScriptableObject.CreateInstance<MaliGo.World.TownPieceCatalog>();
        }

        var keys = MaliGo.Core.TownLayout.ModelKeys();
        var pieces = new MaliGo.World.TownPieceCatalog.Piece[keys.Count];
        int missing = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            string modelPath = MaliGo.Core.TownLayout.AssetPathOf(keys[i]);
            GameObject model = modelPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) : null;
            if (model == null)
            {
                missing++;
                Debug.LogWarning($"[MaliGoResourceBaker] City-kit model not found at {modelPath} - it is left out of the streets.");
                model = isNew ? null : catalog.Find(keys[i]);
            }

            pieces[i] = new MaliGo.World.TownPieceCatalog.Piece { id = keys[i], prefab = model };
        }

        catalog.pieces = pieces;
        if (isNew)
        {
            AssetDatabase.CreateAsset(catalog, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(catalog);
        }

        Debug.Log($"[MaliGoResourceBaker] Town piece catalog: {keys.Count - missing} of {keys.Count} city-kit models.");
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
