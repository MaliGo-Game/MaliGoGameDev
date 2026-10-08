using System;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// The Kenney furniture-kit models the walk-in rooms are furnished with (<see cref="InteriorRoomBuilder"/>), by
    /// model name. Baked into <c>Assets/Resources/InteriorPieceCatalog.asset</c> by <c>MaliGoResourceBaker</c> before
    /// every build (only the pieces in <see cref="InteriorRoomBuilder.PieceIds"/>, so the APK carries nothing else from
    /// the kit). In the Editor, before a bake, the models are loaded straight from the kit folder instead.
    /// </summary>
    public class InteriorPieceCatalog : ScriptableObject
    {
        public const string ResourceName = "InteriorPieceCatalog";

        /// <summary>The kit's FBX folder (Editor fallback and the baker).</summary>
        public const string FurnitureFolder = "Assets/kenney_furniture-kit/Models/FBX format/";

        [Serializable]
        public class Piece
        {
            public string id;
            public GameObject prefab;
        }

        public Piece[] pieces = new Piece[0];

        /// <summary>The model named <paramref name="id"/> (e.g. "bedSingle"), or null.</summary>
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
