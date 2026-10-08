// The streets added around the town (TownLayout): every tile's piece opens exactly toward its road neighbours, the
// network is one connected loop with no stray dead ends, it joins the old roads only where intended, nothing is built
// on the old town or on a road, and the areas fit the lawn.
using System;
using System.Collections.Generic;
using MaliGo.Core;

public static class TownLayoutTests
{
    public static void TestPieceOpeningsMatchTheKit()
    {
        // Measured from the kit's meshes (Unity mirrors X on import): straight along X, bend +X/+Z, T +X/-X/+Z,
        // road end -X. The scene's own tiles confirm it: the T at (0, 0), yaw 0, branches north; the cul-de-sac at
        // (0, 4), yaw 270, opens south.
        Assert.True(TownLayout.OpeningsOf(RoadPiece.Straight, 0f) == (TownLayout.East | TownLayout.West), "straight yaw 0");
        Assert.True(TownLayout.OpeningsOf(RoadPiece.Straight, 90f) == (TownLayout.North | TownLayout.South), "straight yaw 90");
        Assert.True(TownLayout.OpeningsOf(RoadPiece.T, 0f) == (TownLayout.East | TownLayout.West | TownLayout.North), "T yaw 0 (scene's T)");
        Assert.True(TownLayout.OpeningsOf(RoadPiece.End, 270f) == TownLayout.South, "end yaw 270 (scene's cul-de-sac)");
        Assert.True(TownLayout.OpeningsOf(RoadPiece.Bend, 0f) == (TownLayout.East | TownLayout.North), "bend yaw 0");
        Assert.True(TownLayout.OpeningsOf(RoadPiece.Bend, 90f) == (TownLayout.South | TownLayout.East), "bend yaw 90");
        Assert.True(TownLayout.OpeningsOf(RoadPiece.Cross, 180f) == 15, "crossroad");
    }

    public static void TestEveryMaskWithARoadHasAPiece()
    {
        for (int mask = 1; mask < 16; mask++)
        {
            Assert.True(TownLayout.TryPieceFor(mask, out RoadPiece piece, out float yaw), "mask " + mask);
            Assert.True(TownLayout.OpeningsOf(piece, yaw) == mask, "mask " + mask + " round trip");
            Assert.True(TownLayout.ModelOf(piece) != null, "model for " + piece);
        }

        Assert.True(!TownLayout.TryPieceFor(0, out _, out _), "an isolated cell has no piece");
    }

    public static void TestEveryBuiltTileOpensExactlyTowardItsNeighbours()
    {
        List<RoadTile> tiles = TownLayout.TilesToBuild();
        Assert.True(tiles.Count >= 60, "a real network: " + tiles.Count + " tiles");
        foreach (RoadTile tile in tiles)
        {
            int network = TownLayout.NetworkOpenings(tile.X, tile.Z);
            Assert.True(TownLayout.OpeningsOf(tile.Piece, tile.Yaw) == network,
                $"tile ({tile.X},{tile.Z}) {tile.Piece}@{tile.Yaw} matches its neighbours");
            Assert.True(tile.Piece != RoadPiece.End, $"no dead end at ({tile.X},{tile.Z})");
        }
    }

    public static void TestOldTilesStayExceptTheCulDeSac()
    {
        List<RoadTile> replaced = TownLayout.ReplacedExistingCells();
        Assert.True(replaced.Count == 1, "one old tile replaced, got " + replaced.Count);
        Assert.True(replaced[0].X == 0 && replaced[0].Z == TownLayout.ConnectingRoadMaxZ, "the cul-de-sac");
        Assert.True(TownLayout.NetworkOpenings(0, TownLayout.ConnectingRoadMaxZ) == (TownLayout.North | TownLayout.South),
            "it becomes a through road");

        // Every other old tile keeps exactly its openings (so the scene's pieces still fit).
        foreach (RoadTile cell in TownLayout.ExistingRoadCells())
        {
            if (cell.X == 0 && cell.Z == TownLayout.ConnectingRoadMaxZ)
            {
                continue;
            }

            Assert.True(TownLayout.NetworkOpenings(cell.X, cell.Z) == TownLayout.ExistingOpenings(cell.X, cell.Z),
                $"old tile ({cell.X},{cell.Z}) unchanged");
        }
    }

    public static void TestNewRoadsNeverOverlapOldOnes()
    {
        var old = new HashSet<string>();
        foreach (RoadTile cell in TownLayout.ExistingRoadCells())
        {
            old.Add(cell.X + "," + cell.Z);
        }

        var seen = new HashSet<string>();
        foreach (RoadTile cell in TownLayout.NewRoadCells())
        {
            string key = cell.X + "," + cell.Z;
            Assert.True(!old.Contains(key), "new road on an old one at " + key);
            Assert.True(seen.Add(key), "duplicate new road at " + key);
        }
    }

    public static void TestNetworkIsConnected()
    {
        var all = new HashSet<string>();
        foreach (RoadTile cell in TownLayout.ExistingRoadCells())
        {
            all.Add(cell.X + "," + cell.Z);
        }

        foreach (RoadTile cell in TownLayout.NewRoadCells())
        {
            all.Add(cell.X + "," + cell.Z);
        }

        // Flood fill from the player's driveway tile (2, 0).
        var reached = new HashSet<string> { "2,0" };
        var queue = new Queue<int[]>();
        queue.Enqueue(new[] { 2, 0 });
        int[][] steps = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
        while (queue.Count > 0)
        {
            int[] at = queue.Dequeue();
            foreach (int[] step in steps)
            {
                string key = (at[0] + step[0]) + "," + (at[1] + step[1]);
                if (all.Contains(key) && reached.Add(key))
                {
                    queue.Enqueue(new[] { at[0] + step[0], at[1] + step[1] });
                }
            }
        }

        Assert.True(reached.Count == all.Count, $"every road reachable from home: {reached.Count} of {all.Count}");
    }

    public static void TestBuildingsAndTreesStayOffRoadsAndTheOldTown()
    {
        var placements = new List<TownPlacement>(TownLayout.Buildings);
        placements.AddRange(TownLayout.Trees);

        foreach (TownPlacement p in placements)
        {
            // Not on any road tile (old or new), with a little pavement to spare.
            const float spare = 0.05f;
            for (int x = TownLayout.RingMinX - 1; x <= TownLayout.RingMaxX + 1; x++)
            {
                for (int z = TownLayout.RingMinZ - 1; z <= TownLayout.RingMaxZ + 1; z++)
                {
                    if (!TownLayout.IsRoad(x, z))
                    {
                        continue;
                    }

                    bool overlaps = Math.Abs(p.X - x) < TownLayout.HalfTile + p.HalfX + spare
                                    && Math.Abs(p.Z - z) < TownLayout.HalfTile + p.HalfZ + spare;
                    Assert.True(!overlaps, $"{p.Model} at ({p.X},{p.Z}) is on the road at ({x},{z})");
                }
            }

            // Not inside the original town.
            bool inCore = p.X + p.HalfX > TownLayout.CoreMinX && p.X - p.HalfX < TownLayout.CoreMaxX
                          && p.Z + p.HalfZ > TownLayout.CoreMinZ && p.Z - p.HalfZ < TownLayout.CoreMaxZ;
            Assert.True(!inCore, $"{p.Model} at ({p.X},{p.Z}) is inside the old town");
        }

        // No two placements overlap.
        for (int i = 0; i < placements.Count; i++)
        {
            for (int j = i + 1; j < placements.Count; j++)
            {
                TownPlacement a = placements[i];
                TownPlacement b = placements[j];
                bool overlaps = Math.Abs(a.X - b.X) < a.HalfX + b.HalfX && Math.Abs(a.Z - b.Z) < a.HalfZ + b.HalfZ;
                Assert.True(!overlaps, $"{a.Model} ({a.X},{a.Z}) overlaps {b.Model} ({b.X},{b.Z})");
            }
        }
    }

    public static void TestHousesFaceTheRing()
    {
        foreach (TownPlacement p in TownLayout.Buildings)
        {
            bool commercial = p.Model.StartsWith("commercial/", StringComparison.Ordinal);

            // Suburban doors are on the model's +Z side, the shops' on -Z.
            float localDoorZ = commercial ? -1f : 1f;
            double radians = p.Yaw * Math.PI / 180.0;
            float doorX = (float)(localDoorZ * Math.Sin(radians));
            float doorZ = (float)(localDoorZ * Math.Cos(radians));

            // The door points toward the nearest ring side.
            float toRingX = p.X < TownLayout.RingMinX ? 1f : p.X > TownLayout.RingMaxX ? -1f : 0f;
            float toRingZ = p.Z < TownLayout.RingMinZ ? 1f : p.Z > TownLayout.RingMaxZ ? -1f : 0f;
            Assert.True(toRingX != 0f || toRingZ != 0f, $"{p.Model} at ({p.X},{p.Z}) is outside the ring");
            Assert.True(doorX * toRingX + doorZ * toRingZ > 0.9f, $"{p.Model} at ({p.X},{p.Z}) faces the ring");
        }
    }

    public static void TestAreasFitTheLawnAndContainTheOldTown()
    {
        TownLayout.RoadBounds(out float minX, out float maxX, out float minZ, out float maxZ);
        Assert.True(minX < TownLayout.CoreMinX && maxX > TownLayout.CoreMaxX
                    && minZ < TownLayout.CoreMinZ && maxZ > TownLayout.CoreMaxZ, "the roads surround the old town");

        // The scene's lawn is 35 x 35 u centred on the origin; everything built stays well inside it.
        const float lawnHalf = 17.5f;
        Assert.True(Math.Max(Math.Abs(minX), Math.Abs(maxX)) < lawnHalf - 5f, "road X within the lawn");
        Assert.True(Math.Max(Math.Abs(minZ), Math.Abs(maxZ)) < lawnHalf - 5f, "road Z within the lawn");
        foreach (TownPlacement p in TownLayout.Buildings)
        {
            Assert.True(Math.Abs(p.X) + p.HalfX < lawnHalf - 4f && Math.Abs(p.Z) + p.HalfZ < lawnHalf - 4f,
                $"{p.Model} within the lawn");
        }
    }

    public static void TestModelKeysAreKnownKitPaths()
    {
        List<string> keys = TownLayout.ModelKeys();
        Assert.True(keys.Count >= 15 && keys.Count <= 30, "a small, fixed set of models: " + keys.Count);
        foreach (string key in keys)
        {
            string path = TownLayout.AssetPathOf(key);
            Assert.True(path != null && path.StartsWith("Assets/kenney_city-kit-", StringComparison.Ordinal)
                        && path.EndsWith(".fbx", StringComparison.Ordinal), "path for " + key + ": " + path);
        }

        Assert.Equal("Assets/kenney_city-kit-roads/Models/FBX format/road-straight.fbx",
            TownLayout.AssetPathOf("roads/road-straight"), "road path");
        Assert.True(TownLayout.AssetPathOf("furniture/bed") == null, "unknown kit");
    }
}
