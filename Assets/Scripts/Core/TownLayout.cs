using System.Collections.Generic;
using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>The Kenney city-kit road pieces the town is tiled with.</summary>
    public enum RoadPiece
    {
        None,
        Straight,
        Bend,
        T,
        Cross,
        End
    }

    /// <summary>One road tile: grid cell (1 u squares centred on whole numbers), piece and yaw (degrees).</summary>
    public struct RoadTile
    {
        public int X;
        public int Z;
        public RoadPiece Piece;
        public float Yaw;
    }

    /// <summary>A model placed in the expansion: catalog key, centre, yaw (degrees) and the half sizes of its
    /// footprint on the ground after the turn (u), used for its collider and for the layout checks.</summary>
    public struct TownPlacement
    {
        public string Model;
        public float X;
        public float Z;
        public float Yaw;
        public float HalfX;
        public float HalfZ;
        public bool IsTree;

        public TownPlacement(string model, float x, float z, float yaw, float modelHalfWidth, float modelHalfDepth, bool isTree)
        {
            Model = model;
            X = x;
            Z = z;
            Yaw = yaw;
            bool turned = Mathf.Abs(MovementMath.DeltaAngle(0f, yaw)) > 45f && Mathf.Abs(MovementMath.DeltaAngle(0f, yaw)) < 135f;
            HalfX = turned ? modelHalfDepth : modelHalfWidth;
            HalfZ = turned ? modelHalfWidth : modelHalfDepth;
            IsTree = isTree;
        }
    }

    /// <summary>
    /// The layout of the streets added around the original town (pure data and maths, so the logic tests can check it
    /// without Unity), built at runtime by <c>TownExpansion</c>.
    ///
    /// Grid: road tiles are 1 x 1 u (Kenney city-kit roads at scale 1, ~3.7 m), centred on whole-number X/Z, the same
    /// grid the generator used: the main road runs along z = 0 from x = -5 to 5, the connecting road up x = 0 from
    /// z = 1 to the cul-de-sac at z = 4.
    ///
    /// Added: a ring road around the whole town (x = -8..8 at z = -5 and z = 9; z = -4..8 at x = -8 and x = 8), the
    /// main road carried on to the ring at both ends (x = +/-6, +/-7, meeting it at T junctions), and the connecting
    /// road carried on north from the cul-de-sac to the ring (x = 0, z = 5..8; the cul-de-sac tile is swapped for a
    /// straight). Houses face the ring from outside it (suburban kit on the south, west and east, a row of shops
    /// from the commercial kit along the north), with trees between them and on the lawns inside the ring.
    ///
    /// Piece orientation (measured from the kit's meshes, with Unity's FBX import mirroring X): at yaw 0 a straight
    /// runs along X, a bend opens to +X and +Z, a T opens to +X, -X and +Z, a crossroad opens everywhere and a road end
    /// opens only to -X. A yaw of 90 degrees turns +Z to +X (Unity's convention).
    /// </summary>
    public static class TownLayout
    {
        /// <summary>Opening bits: toward +Z (north), +X (east), -Z (south), -X (west).</summary>
        public const int North = 1;
        public const int East = 2;
        public const int South = 4;
        public const int West = 8;

        public const int RingMinX = -8;
        public const int RingMaxX = 8;
        public const int RingMinZ = -5;
        public const int RingMaxZ = 9;

        /// <summary>The original main road (z = 0) and connecting road (x = 0) ends.</summary>
        public const int MainRoadMinX = -5;
        public const int MainRoadMaxX = 5;
        public const int ConnectingRoadMaxZ = 4;

        /// <summary>Half a road tile (u).</summary>
        public const float HalfTile = 0.5f;

        /// <summary>The original town as built by the generator (u, XZ), used to keep the additions clear of it.</summary>
        public const float CoreMinX = -5.6f;
        public const float CoreMaxX = 5.6f;
        public const float CoreMinZ = -2.9f;
        public const float CoreMaxZ = 6.2f;

        static readonly int[] PieceBaseMasks =
        {
            0,                          // None
            East | West,                // Straight
            East | North,               // Bend
            East | West | North,        // T
            North | East | South | West, // Cross
            West                        // End
        };

        // ---------------------------------------------------------------- road cells

        /// <summary>The road cells the scene already has (not built again).</summary>
        public static List<RoadTile> ExistingRoadCells()
        {
            var cells = new List<RoadTile>();
            for (int x = MainRoadMinX; x <= MainRoadMaxX; x++)
            {
                cells.Add(new RoadTile { X = x, Z = 0 });
            }

            for (int z = 1; z <= ConnectingRoadMaxZ; z++)
            {
                cells.Add(new RoadTile { X = 0, Z = z });
            }

            return cells;
        }

        /// <summary>How the scene's own tile at a cell opens (the generator's pieces), or 0 if it has none.</summary>
        public static int ExistingOpenings(int x, int z)
        {
            if (z == 0 && x >= MainRoadMinX && x <= MainRoadMaxX)
            {
                return x == 0 ? East | West | North : East | West;
            }

            if (x == 0 && z >= 1 && z <= ConnectingRoadMaxZ)
            {
                return z == ConnectingRoadMaxZ ? South : North | South;
            }

            return 0;
        }

        /// <summary>The road cells the expansion adds.</summary>
        public static List<RoadTile> NewRoadCells()
        {
            var cells = new List<RoadTile>();
            for (int x = RingMinX; x <= RingMaxX; x++)
            {
                cells.Add(new RoadTile { X = x, Z = RingMinZ });
                cells.Add(new RoadTile { X = x, Z = RingMaxZ });
            }

            for (int z = RingMinZ + 1; z <= RingMaxZ - 1; z++)
            {
                cells.Add(new RoadTile { X = RingMinX, Z = z });
                cells.Add(new RoadTile { X = RingMaxX, Z = z });
            }

            for (int x = MainRoadMaxX + 1; x < RingMaxX; x++)
            {
                cells.Add(new RoadTile { X = x, Z = 0 });
                cells.Add(new RoadTile { X = -x, Z = 0 });
            }

            for (int z = ConnectingRoadMaxZ + 1; z < RingMaxZ; z++)
            {
                cells.Add(new RoadTile { X = 0, Z = z });
            }

            return cells;
        }

        static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        static HashSet<long> AllRoadKeys()
        {
            var keys = new HashSet<long>();
            foreach (RoadTile cell in ExistingRoadCells())
            {
                keys.Add(Key(cell.X, cell.Z));
            }

            foreach (RoadTile cell in NewRoadCells())
            {
                keys.Add(Key(cell.X, cell.Z));
            }

            return keys;
        }

        /// <summary>Which neighbours of (x, z) are road, as opening bits.</summary>
        static int Openings(HashSet<long> roads, int x, int z)
        {
            int mask = 0;
            if (roads.Contains(Key(x, z + 1)))
            {
                mask |= North;
            }

            if (roads.Contains(Key(x + 1, z)))
            {
                mask |= East;
            }

            if (roads.Contains(Key(x, z - 1)))
            {
                mask |= South;
            }

            if (roads.Contains(Key(x - 1, z)))
            {
                mask |= West;
            }

            return mask;
        }

        /// <summary>Which neighbours of (x, z) are road in the finished network (old and new cells).</summary>
        public static int NetworkOpenings(int x, int z)
        {
            return Openings(AllRoadKeys(), x, z);
        }

        /// <summary>True when (x, z) is a road cell of the finished network.</summary>
        public static bool IsRoad(int x, int z)
        {
            return AllRoadKeys().Contains(Key(x, z));
        }

        /// <summary>
        /// The tiles to build: every new cell, plus every existing cell whose openings change (only the cul-de-sac,
        /// which becomes a straight), each with the piece and yaw that open exactly toward its road neighbours.
        /// </summary>
        public static List<RoadTile> TilesToBuild()
        {
            HashSet<long> roads = AllRoadKeys();
            var tiles = new List<RoadTile>();
            foreach (RoadTile cell in ExistingRoadCells())
            {
                int mask = Openings(roads, cell.X, cell.Z);
                if (mask != ExistingOpenings(cell.X, cell.Z) && TryPieceFor(mask, out RoadPiece piece, out float yaw))
                {
                    tiles.Add(new RoadTile { X = cell.X, Z = cell.Z, Piece = piece, Yaw = yaw });
                }
            }

            foreach (RoadTile cell in NewRoadCells())
            {
                int mask = Openings(roads, cell.X, cell.Z);
                if (TryPieceFor(mask, out RoadPiece piece, out float yaw))
                {
                    tiles.Add(new RoadTile { X = cell.X, Z = cell.Z, Piece = piece, Yaw = yaw });
                }
            }

            return tiles;
        }

        /// <summary>The existing cells whose tile the expansion replaces (their scene tile is hidden, not moved).</summary>
        public static List<RoadTile> ReplacedExistingCells()
        {
            HashSet<long> roads = AllRoadKeys();
            var cells = new List<RoadTile>();
            foreach (RoadTile cell in ExistingRoadCells())
            {
                if (Openings(roads, cell.X, cell.Z) != ExistingOpenings(cell.X, cell.Z))
                {
                    cells.Add(cell);
                }
            }

            return cells;
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>The openings of <paramref name="piece"/> turned by <paramref name="yaw"/> (a multiple of 90).</summary>
        public static int OpeningsOf(RoadPiece piece, float yaw)
        {
            int index = (int)piece;
            int mask = index >= 0 && index < PieceBaseMasks.Length ? PieceBaseMasks[index] : 0;
            int quarterTurns = ((Mathf.RoundToInt(yaw / 90f) % 4) + 4) % 4;
            for (int i = 0; i < quarterTurns; i++)
            {
                // +90 degrees: north -> east -> south -> west -> north.
                mask = ((mask << 1) | (mask >> 3)) & 15;
            }

            return mask;
        }

        /// <summary>The piece and yaw (0, 90, 180 or 270) whose openings are exactly <paramref name="mask"/>; false for
        /// a cell with no road neighbours.</summary>
        public static bool TryPieceFor(int mask, out RoadPiece piece, out float yaw)
        {
            for (int p = (int)RoadPiece.Straight; p <= (int)RoadPiece.End; p++)
            {
                for (int turn = 0; turn < 4; turn++)
                {
                    if (OpeningsOf((RoadPiece)p, turn * 90f) == mask)
                    {
                        piece = (RoadPiece)p;
                        yaw = turn * 90f;
                        return true;
                    }
                }
            }

            piece = RoadPiece.None;
            yaw = 0f;
            return false;
        }

        /// <summary>The catalog key of a road piece's model.</summary>
        public static string ModelOf(RoadPiece piece)
        {
            switch (piece)
            {
                case RoadPiece.Straight:
                    return "roads/road-straight";
                case RoadPiece.Bend:
                    return "roads/road-bend";
                case RoadPiece.T:
                    return "roads/road-intersection";
                case RoadPiece.Cross:
                    return "roads/road-crossroad";
                case RoadPiece.End:
                    return "roads/road-end";
                default:
                    return null;
            }
        }

        // ---------------------------------------------------------------- areas

        /// <summary>The rectangle (XZ) covered by every road tile, old and new: the area the car is kept in, and what
        /// the town-on-foot rectangle grows to include.</summary>
        public static void RoadBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = RingMinX - HalfTile;
            maxX = RingMaxX + HalfTile;
            minZ = RingMinZ - HalfTile;
            maxZ = RingMaxZ + HalfTile;
        }

        // ---------------------------------------------------------------- buildings and trees

        /// <summary>Houses and shops, facing the ring from outside. Half sizes are the models' measured footprints.</summary>
        public static readonly TownPlacement[] Buildings =
        {
            // South side: suburban houses, front doors (+Z) to the ring.
            new TownPlacement("suburban/building-type-c", -6.5f, -6.3f, 0f, 0.643f, 0.514f, false),
            new TownPlacement("suburban/building-type-e", -4f, -6.3f, 0f, 0.65f, 0.514f, false),
            new TownPlacement("suburban/building-type-g", -1.5f, -6.3f, 0f, 0.725f, 0.589f, false),
            new TownPlacement("suburban/building-type-h", 1.5f, -6.3f, 0f, 0.65f, 0.458f, false),
            new TownPlacement("suburban/building-type-p", 4f, -6.3f, 0f, 0.62f, 0.495f, false),
            new TownPlacement("suburban/building-type-s", 6.5f, -6.3f, 0f, 0.703f, 0.543f, false),

            // North side: a row of shops (commercial kit, doors on their -Z side) facing south to the ring.
            new TownPlacement("commercial/building-b", -6f, 10.25f, 0f, 0.485f, 0.47f, false),
            new TownPlacement("commercial/building-c", -4.8f, 10.25f, 0f, 0.442f, 0.545f, false),
            new TownPlacement("commercial/building-d", -3.6f, 10.25f, 0f, 0.42f, 0.45f, false),
            new TownPlacement("commercial/building-f", -2.4f, 10.25f, 0f, 0.42f, 0.515f, false),
            new TownPlacement("commercial/building-g", -1.2f, 10.25f, 0f, 0.485f, 0.461f, false),
            new TownPlacement("commercial/building-h", 1.2f, 10.25f, 0f, 0.442f, 0.504f, false),
            new TownPlacement("commercial/building-b", 2.4f, 10.25f, 0f, 0.485f, 0.47f, false),
            new TownPlacement("commercial/building-c", 3.6f, 10.25f, 0f, 0.442f, 0.545f, false),
            new TownPlacement("commercial/building-d", 4.8f, 10.25f, 0f, 0.42f, 0.45f, false),
            new TownPlacement("commercial/building-f", 6f, 10.25f, 0f, 0.42f, 0.515f, false),

            // West side: houses turned 90 degrees, front doors to the east (the ring).
            new TownPlacement("suburban/building-type-f", -9.3f, -3f, 90f, 0.714f, 0.703f, false),
            new TownPlacement("suburban/building-type-k", -9.3f, 0f, 90f, 0.46f, 0.51f, false),
            new TownPlacement("suburban/building-type-c", -9.3f, 3f, 90f, 0.643f, 0.514f, false),
            new TownPlacement("suburban/building-type-e", -9.3f, 6f, 90f, 0.65f, 0.514f, false),

            // East side: houses turned 270 degrees, front doors to the west (the ring).
            new TownPlacement("suburban/building-type-g", 9.3f, -3f, 270f, 0.725f, 0.589f, false),
            new TownPlacement("suburban/building-type-s", 9.3f, 0f, 270f, 0.703f, 0.543f, false),
            new TownPlacement("suburban/building-type-p", 9.3f, 3f, 270f, 0.62f, 0.495f, false),
            new TownPlacement("suburban/building-type-h", 9.3f, 6f, 270f, 0.65f, 0.458f, false)
        };

        const float TreeHalf = 0.12f;

        /// <summary>Trees: between the houses outside the ring, on its corners, and on the lawns inside it.</summary>
        public static readonly TownPlacement[] Trees =
        {
            // Outside the ring.
            new TownPlacement("suburban/tree-large", -9.4f, -6.4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 9.4f, -6.4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", -9.4f, 10.4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 9.4f, 10.4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", -5.25f, -6.1f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 0f, -6.1f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", 2.75f, -6.1f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 5.25f, -6.1f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 0f, 10.4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", -9.3f, -1.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", -9.3f, 1.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", -9.3f, 4.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 9.3f, -1.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", 9.3f, 1.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 9.3f, 4.5f, 0f, TreeHalf, TreeHalf, true),

            // Inside the ring, on the lawns around the old town.
            new TownPlacement("suburban/tree-large", -6.6f, -3.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", 6.6f, -3.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", -6.6f, 3f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 6.6f, 3f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", -6.6f, 6.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", 6.6f, 6.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", -3f, -4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 3.5f, -4f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", -4.5f, 7.5f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", -1.5f, 7.6f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-small", 1.5f, 7.6f, 0f, TreeHalf, TreeHalf, true),
            new TownPlacement("suburban/tree-large", 4.5f, 7.5f, 0f, TreeHalf, TreeHalf, true)
        };

        /// <summary>Every model key the expansion uses (what the baker puts in the catalog).</summary>
        public static List<string> ModelKeys()
        {
            var keys = new List<string>();
            for (int p = (int)RoadPiece.Straight; p <= (int)RoadPiece.End; p++)
            {
                AddUnique(keys, ModelOf((RoadPiece)p));
            }

            foreach (TownPlacement placement in Buildings)
            {
                AddUnique(keys, placement.Model);
            }

            foreach (TownPlacement placement in Trees)
            {
                AddUnique(keys, placement.Model);
            }

            return keys;
        }

        static void AddUnique(List<string> keys, string key)
        {
            if (!string.IsNullOrEmpty(key) && !keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        /// <summary>The kit FBX folder (relative to Assets/) for a catalog key's prefix, or null.</summary>
        public static string KitFolderOf(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (key.StartsWith("roads/", System.StringComparison.Ordinal))
            {
                return "kenney_city-kit-roads/Models/FBX format/";
            }

            if (key.StartsWith("suburban/", System.StringComparison.Ordinal))
            {
                return "kenney_city-kit-suburban_20/Models/FBX format/";
            }

            if (key.StartsWith("commercial/", System.StringComparison.Ordinal))
            {
                return "kenney_city-kit-commercial_2.1/Models/FBX format/";
            }

            return null;
        }

        /// <summary>The asset path of a catalog key's FBX ("Assets/.../road-straight.fbx"), or null.</summary>
        public static string AssetPathOf(string key)
        {
            string folder = KitFolderOf(key);
            if (folder == null)
            {
                return null;
            }

            int slash = key.IndexOf('/');
            return "Assets/" + folder + key.Substring(slash + 1) + ".fbx";
        }
    }
}
