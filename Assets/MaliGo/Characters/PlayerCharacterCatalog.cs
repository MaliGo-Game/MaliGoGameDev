using System;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    [CreateAssetMenu(fileName = "PlayerCharacterCatalog", menuName = "MaliGo/Player Character Catalog")]
    public class PlayerCharacterCatalog : ScriptableObject
    {
        const string BaseMapProperty = "_BaseMap";
        const string MainTexProperty = "_MainTex";

        [Header("Kenney Assets")]
        public GameObject characterModelPrefab;
        public RuntimeAnimatorController animatorController;
        public Material baseSkinMaterial;

        [Header("Skin Options")]
        public SkinOption[] skinOptions = Array.Empty<SkinOption>();

        [Serializable]
        public struct SkinOption
        {
            public string optionId;
            public Texture2D skinTexture;
        }

        /// <summary>
        /// The stock Kenney skin closest to <paramref name="appearance"/>'s outfit, from <see cref="skinOptions"/>.
        /// Only a fallback: the look normally comes from <see cref="CharacterSkins"/> (every outfit in every tone,
        /// <see cref="CharacterLooks"/>).
        /// </summary>
        public Texture2D ResolveSkin(AppearanceData appearance)
        {
            if (skinOptions == null || skinOptions.Length == 0)
            {
                return null;
            }

            string requested = MapAppearanceToSkinId(appearance);

            foreach (SkinOption option in skinOptions)
            {
                if (option.optionId == requested)
                {
                    return option.skinTexture;
                }
            }

            return skinOptions[0].skinTexture;
        }

        /// <summary>
        /// A new material showing <paramref name="appearance"/>: its outfit recoloured into its skin tone
        /// (<see cref="CharacterSkins"/>; <paramref name="thumbnail"/> uses the half-size copy), or the stock skin if
        /// that texture is missing. Free it with <see cref="ReleaseRuntimeSkinMaterial"/>, which also releases the
        /// shared texture.
        /// </summary>
        public Material CreateRuntimeSkinMaterial(AppearanceData appearance, bool thumbnail = false)
        {
            if (baseSkinMaterial == null)
            {
                return null;
            }

            Material runtimeMaterial = new Material(baseSkinMaterial);
            Texture2D skin = CharacterSkins.Acquire(appearance, thumbnail);
            if (skin == null)
            {
                skin = ResolveSkin(appearance);
            }

            if (skin != null)
            {
                if (runtimeMaterial.HasProperty(BaseMapProperty))
                {
                    runtimeMaterial.SetTexture(BaseMapProperty, skin);
                }
                else if (runtimeMaterial.HasProperty(MainTexProperty))
                {
                    runtimeMaterial.SetTexture(MainTexProperty, skin);
                }
            }

            return runtimeMaterial;
        }

        /// <summary>Destroys a material made by <see cref="CreateRuntimeSkinMaterial"/> and releases its runtime skin
        /// texture (stock textures are left alone).</summary>
        public static void ReleaseRuntimeSkinMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            Texture skin = null;
            if (material.HasProperty(BaseMapProperty))
            {
                skin = material.GetTexture(BaseMapProperty);
            }
            else if (material.HasProperty(MainTexProperty))
            {
                skin = material.GetTexture(MainTexProperty);
            }

            CharacterSkins.Release(skin);
            Destroy(material);
        }

        static string MapAppearanceToSkinId(AppearanceData appearance)
        {
            switch (CharacterLooks.OutfitOf(appearance).id)
            {
                case CharacterLooks.StreetId:
                case CharacterLooks.WorkwearId:
                    return "skater_male";
                case CharacterLooks.SmartId:
                    return "criminal_male";
                case CharacterLooks.NightOutId:
                    return "cyborg_female";
                default:
                    return "skater_female";
            }
        }
    }
}
