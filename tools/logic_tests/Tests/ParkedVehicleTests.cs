// Parked cars (ParkedVehicles): no car stands on a place the player uses, none narrows the main road's lane, and
// none overlaps another or the streets added around the town.
using System;
using MaliGo.Core;

public static class ParkedVehicleTests
{
    public static void TestEveryInteractionSpotIsClearOfEveryParkedCar()
    {
        float needed = ParkedVehicles.PlayerRadius + ParkedVehicles.SpotMargin;
        foreach (InteractionSpot spot in ParkedVehicles.Spots)
        {
            // The drivable car's own prompt is at its centre: checked against the other cars only.
            string except = spot.Name == ParkedVehicles.PlayerCar ? ParkedVehicles.PlayerCar : null;
            float clearance = ParkedVehicles.Clearance(spot.X, spot.Z, except);
            Assert.True(clearance >= needed, $"{spot.Name} at ({spot.X},{spot.Z}) is {clearance:0.000} u from a car, needs {needed}");
        }
    }

    public static void TestTheOldParkingReallyBlockedWorkAndTheBank()
    {
        // The regression this guards: at the scene's spots the taxi stood on Work and the van on the Bank's exit.
        ParkedVehicles.TryGet("Vehicle_MainRoad_2", out ParkedVehicle taxi);
        taxi.X = taxi.SceneX;
        taxi.Z = taxi.SceneZ;
        Assert.Equal(0f, ParkedVehicles.DistanceToBox(taxi, 4f, 0.3f), "Work was inside the taxi");

        ParkedVehicles.TryGet("Vehicle_DeliveryVan", out ParkedVehicle van);
        van.X = van.SceneX;
        van.Z = van.SceneZ;
        Assert.Equal(0f, ParkedVehicles.DistanceToBox(van, -3.02f, 4.4f), "the Bank exit was inside the van");
    }

    public static void TestMainRoadIsClearOfParkedCars()
    {
        foreach (ParkedVehicle car in ParkedVehicles.All)
        {
            if (car.Name == ParkedVehicles.PlayerCar)
            {
                // The drivable car noses over the pavement from its driveway (it cannot back up further without
                // standing on the Home exit) and is the one car that moves anyway.
                continue;
            }

            // Sample the main road's carriageway (the tile's asphalt is z -0.4..0.4; kerb and pavement beyond) along
            // x -7.5..7.5 with its new extensions, on a fine grid.
            for (float x = -7.5f; x <= 7.5f; x += 0.05f)
            {
                for (float z = -0.4f; z <= 0.4f; z += 0.05f)
                {
                    Assert.True(ParkedVehicles.DistanceToBox(car, x, z) > 0f, $"{car.Name} is on the main road at ({x:0.00},{z:0.00})");
                }
            }
        }
    }

    public static void TestCarsDoNotOverlapEachOtherOrTheNewStreets()
    {
        foreach (ParkedVehicle car in ParkedVehicles.All)
        {
            // Car centres well apart from every other car's box.
            foreach (ParkedVehicle other in ParkedVehicles.All)
            {
                if (other.Name == car.Name)
                {
                    continue;
                }

                Assert.True(ParkedVehicles.DistanceToBox(other, car.X, car.Z) > car.HalfLength, $"{car.Name} too close to {other.Name}");
            }

            // Not on a new road tile, building or tree.
            Assert.True(!TownLayout.IsRoad((int)Math.Round(car.X), (int)Math.Round(car.Z))
                        || ((int)Math.Round(car.Z) == 0 && Math.Abs(car.X) <= TownLayout.MainRoadMaxX)
                        || car.Name == ParkedVehicles.PlayerCar,
                $"{car.Name} on a new road");
            foreach (TownPlacement p in TownLayout.Buildings)
            {
                Assert.True(Math.Abs(p.X - car.X) > p.HalfX + car.HalfLength || Math.Abs(p.Z - car.Z) > p.HalfZ + car.HalfLength,
                    $"{car.Name} overlaps {p.Model}");
            }

            foreach (TownPlacement p in TownLayout.Trees)
            {
                Assert.True(ParkedVehicles.DistanceToBox(car, p.X, p.Z) > p.HalfX, $"{car.Name} overlaps a tree at ({p.X},{p.Z})");
            }
        }
    }

    public static void TestBoxDistanceFollowsTheTurn()
    {
        // A car turned 90 degrees is long along X.
        var car = new ParkedVehicle("t", 0f, 0f, 0f, 0f, 90f, 0.25f, 0.5f, 0f);
        Assert.Equal(0f, ParkedVehicles.DistanceToBox(car, 0.45f, 0f), "inside along X");
        Assert.Equal(0.05f, ParkedVehicles.DistanceToBox(car, 0f, 0.3f), "0.05 past the side along Z", 0.0001f);
        Assert.Equal(0.1f, ParkedVehicles.DistanceToBox(car, 0.6f, 0f), "0.1 past the end along X", 0.0001f);
    }
}
