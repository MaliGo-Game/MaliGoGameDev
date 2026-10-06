using System;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    [CreateAssetMenu(fileName = "PlayerCharacterCatalog", menuName = "MaliGo/Player Character Catalog")]
    public class PlayerCharacterCatalog : ScriptableObject
    {
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

        /// <summary>Resources folder of the six recoloured looks (DESIGN_SPEC §4.6, §6.3b).</summary>
        public const string RecolouredSkinFolder = "MaliGo/Skins/";

        /// <summary>
        /// The skin texture for <paramref name="appearance"/>. First the recoloured skater skins (§4.6):
        /// <c>MaliGo/Skins/skaterMaleA_{tone}</c> when <c>genderPresentation</c> is "masculine", else
        /// <c>skaterFemaleA_{tone}</c>, with tone light/medium/deep (anything else: medium). If that texture is not
        /// shipped, the existing <see cref="skinOptions"/> lookup.
        /// </summary>
        public Texture2D ResolveSkin(AppearanceData appearance)
        {
            Texture2D recoloured = Resources.Load<Texture2D>(RecolouredSkinPath(appearance));
            if (recoloured != null)
            {
                return recoloured;
            }

            if (appearance == null || skinOptions == null || skinOptions.Length == 0)
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

        public Material CreateRuntimeSkinMaterial(AppearanceData appearance)
        {
            if (baseSkinMaterial == null)
            {
                return null;
            }

            Material runtimeMaterial = new Material(baseSkinMaterial);
            Texture2D skin = ResolveSkin(appearance);
            if (skin != null)
            {
                if (runtimeMaterial.HasProperty("_BaseMap"))
                {
                    runtimeMaterial.SetTexture("_BaseMap", skin);
                }
                else if (runtimeMaterial.HasProperty("_MainTex"))
                {
                    runtimeMaterial.SetTexture("_MainTex", skin);
                }
            }

            return runtimeMaterial;
        }

        /// <summary>Resources path of the recoloured skin for <paramref name="appearance"/> (null = defaults).</summary>
        public static string RecolouredSkinPath(AppearanceData appearance)
        {
            string body = appearance != null && appearance.genderPresentation == "masculine" ? "skaterMaleA" : "skaterFemaleA";
            string tone = appearance != null ? appearance.skinTone : null;
            if (tone != "light" && tone != "medium" && tone != "deep")
            {
                tone = "medium";
            }

            return RecolouredSkinFolder + body + "_" + tone;
        }

        static string MapAppearanceToSkinId(AppearanceData appearance)
        {
            if (appearance.genderPresentation == "feminine")
            {
                return appearance.clothing == "smart" ? "cyborg_female" : "skater_female";
            }

            if (appearance.genderPresentation == "masculine")
            {
                return appearance.clothing == "smart" ? "criminal_male" : "skater_male";
            }

            return appearance.clothing == "sporty" ? "skater_male" : "skater_female";
        }
    }
}
