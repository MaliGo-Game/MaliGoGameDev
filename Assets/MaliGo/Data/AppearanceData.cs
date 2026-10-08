using System;

namespace MaliGo.Data
{
    [Serializable]
    public class AppearanceData
    {
        public string skinTone = "medium";
        public string hairstyle = "short";
        public string hairColor = "black";
        public string clothing = "casual";
        public string accessories = "none";
        public string genderPresentation = "neutral";
        public string bodyType = "average";

        /// <summary>The picked character (a <see cref="CharacterLooks.Outfits"/> id). Empty in saves from before
        /// outfits existed; <see cref="CharacterLooks.OutfitIndex"/> maps those to the look they had.</summary>
        public string outfit = "";

        public AppearanceData Clone()
        {
            return new AppearanceData
            {
                skinTone = skinTone,
                hairstyle = hairstyle,
                hairColor = hairColor,
                clothing = clothing,
                accessories = accessories,
                genderPresentation = genderPresentation,
                bodyType = bodyType,
                outfit = outfit
            };
        }
    }
}
