using System;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// The Kenney city-kit models the streets added around the town are built from (<see cref="TownExpansion"/>), by
    /// catalog key ("roads/road-straight", "suburban/building-type-c", ...: see <c>TownLayout.ModelKeys</c>). Baked into
    /// <c>Assets/Resources/TownPieceCatalog.asset</c> by <c>MaliGoResourceBaker</c> before every build (only these
    /// models, so the APK carries nothing else from the kits). In the Editor, before a bake, the models are loaded
    /// straight from the kit folders instead.
    /// </summary>
    public class TownPieceCatalog : ScriptableObject
    {
        public const string ResourceName = "TownPieceCatalog";

        [Serializable]
        public class Piece
        {
            public string id;
            public GameObject prefab;
        }

        public Piece[] pieces = new Piece[0];

        /// <summary>The model with catalog key <paramref name="id"/>, or null.</summary>
        public GameObject Find(string id)
        {
            if (pieces == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < pieces.Length; i++)
            {
                Piece piece = pieces[i];
                if (piece != null && piece.id == id)
                {
                    return piece.prefab;
                }
            }

            return null;
        }
    }
}
