using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>One parked car of MaliGoWorld: where the scene put it, where it is parked at runtime, and its
    /// collider footprint (half sizes in u, padded like <c>VehicleColliders</c>).</summary>
    public struct ParkedVehicle
    {
        public string Name;
        public float SceneX;
        public float SceneZ;
        public float X;
        public float Z;
        public float Yaw;
        public float HalfWidth;
        public float HalfLength;

        /// <summary>The box centre's offset along the car's own forward axis (u): the Kenney bodies are not
        /// quite centred on their pivot.</summary>
        public float CentreForward;

        public ParkedVehicle(string name, float sceneX, float sceneZ, float x, float z, float yaw,
            float halfWidth, float halfLength, float centreForward)
        {
            Name = name;
            SceneX = sceneX;
            SceneZ = sceneZ;
            X = x;
            Z = z;
            Yaw = yaw;
            HalfWidth = halfWidth;
            HalfLength = halfLength;
            CentreForward = centreForward;
        }

        /// <summary>True when the runtime spot differs from the scene's (the car is moved at load).</summary>
        public bool IsMoved => Mathf.Abs(X - SceneX) > 0.001f || Mathf.Abs(Z - SceneZ) > 0.001f;
    }

    /// <summary>A place the player uses (a scenario spot, a location, a door's prompt or exit point), on XZ.</summary>
    public struct InteractionSpot
    {
        public string Name;
        public float X;
        public float Z;

        public InteractionSpot(string name, float x, float z)
        {
            Name = name;
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// The parked cars of MaliGoWorld and the places the player interacts at, as pure data, so the logic tests can
    /// check that no car stands on a spot (pure maths, no Unity objects).
    ///
    /// The generator ("Add Parked Vehicles") parked the taxi across the Work spot (Work at (4.0, 0.3) was inside its
    /// box) and the taxi and the hatchback in the north lane of the main road, narrowing it for the drivable car; the
    /// delivery van stood across the Bank's door, and the neighbour's SUV nosed 0.23 u into the south lane (the road
    /// tile's asphalt is z -0.4..0.4). At load (<c>TownExpansion</c>) each moved car is put at its runtime spot here:
    /// the taxi and the hatchback on the north verge, just off the kerb, the van north of the Bank's door, the SUV
    /// backed up its driveway to the kerb. The generator parks them there too. The drivable car stays where it is:
    /// it noses over the pavement, but backing it up would put it on the Home exit, and it is the car that moves.
    ///
    /// Footprints: the car kit's model bounds (sedan 1.5 x 2.55, SUV 1.5 x 2.7, hatchback 1.3 x 2.85, taxi 1.5 x
    /// 2.75, van 1.5 x 3.25 model units) at the scene's 0.31 scale, plus the 0.012 u collider padding.
    /// </summary>
    public static class ParkedVehicles
    {
        public const string PlayerCar = "Vehicle_PlayerCar";

        /// <summary>How clear (u, beyond the player's radius) every interaction spot must be of every car.</summary>
        public const float SpotMargin = 0.03f;

        /// <summary>The player's capsule radius (PlayerCharacterSpawner).</summary>
        public const float PlayerRadius = 0.09f;

        public static readonly ParkedVehicle[] All =
        {
            new ParkedVehicle(PlayerCar, 2f, -0.6f, 2f, -0.6f, 0f, 0.2445f, 0.407f, -0.008f),
            // Backed up its driveway so its nose stops at the kerb instead of 0.23 u into the south lane.
            new ParkedVehicle("Vehicle_NeighborSUV", -4f, -0.6f, -4f, -0.85f, 0f, 0.2445f, 0.4305f, 0f),
            // On the north verge beside Neighbor 2's path, nose east, clear of the lane (z -0.5..0.5).
            new ParkedVehicle("Vehicle_MainRoad_1", -3f, 0.15f, -3f, 0.8f, 90f, 0.2135f, 0.4538f, -0.008f),
            // Off the Work spot: on the north verge east of it, nose west, clear of the lane.
            new ParkedVehicle("Vehicle_MainRoad_2", 4f, 0.15f, 5.05f, 0.8f, 270f, 0.2445f, 0.438f, -0.008f),
            // North of the Bank's west-facing door, not across it.
            new ParkedVehicle("Vehicle_DeliveryVan", -3.3f, 4.4f, -3.3f, 5.25f, 90f, 0.2445f, 0.516f, -0.008f)
        };

        /// <summary>
        /// Every place the player uses in the town: the six scenario spots (ScenarioWorldWiring: anchor + offset),
        /// Work (WorldLocationWiring), the Home and Bank street doors' prompts and the points their exits put the
        /// player on (BuildingInteriors), and the drivable car's Drive prompt (its own centre, checked against the
        /// other cars only).
        /// </summary>
        public static readonly InteractionSpot[] Spots =
        {
            new InteractionSpot("Scenario Corner", 0.6f, 1.2f),
            new InteractionSpot("Scenario Taxi", -1.8f, -0.5f),
            new InteractionSpot("Scenario Hub", 0f, 3.6f),
            new InteractionSpot("Scenario Shopfront", -1.5f, 3.1f),
            new InteractionSpot("Scenario Gate", 0.8f, -0.3f),
            new InteractionSpot("Scenario East", 2.6f, 0.9f),
            new InteractionSpot("Work", 4f, 0.3f),
            new InteractionSpot("Home door prompt", 2f, -1.51f),
            new InteractionSpot("Home exit", 2f, -1.14f),
            new InteractionSpot("Bank door prompt", -2.65f, 4.4f),
            new InteractionSpot("Bank exit", -3.02f, 4.4f),
            new InteractionSpot(PlayerCar, 2f, -0.6f)
        };

        /// <summary>The runtime parking for the car called <paramref name="name"/>; false if it is not listed.</summary>
        public static bool TryGet(string name, out ParkedVehicle vehicle)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Name == name)
                {
                    vehicle = All[i];
                    return true;
                }
            }

            vehicle = default;
            return false;
        }

        /// <summary>Distance (u) from (<paramref name="x"/>, <paramref name="z"/>) to the car's box footprint at its
        /// runtime spot; 0 inside it.</summary>
        public static float DistanceToBox(ParkedVehicle car, float x, float z)
        {
            InteriorMath.RotateY(0f, car.CentreForward, car.Yaw, out float offsetX, out float offsetZ);
            // Into the car's own frame (the inverse turn).
            InteriorMath.RotateY(x - (car.X + offsetX), z - (car.Z + offsetZ), -car.Yaw, out float localX, out float localZ);
            float outX = Mathf.Max(0f, Mathf.Abs(localX) - car.HalfWidth);
            float outZ = Mathf.Max(0f, Mathf.Abs(localZ) - car.HalfLength);
            return (float)System.Math.Sqrt(outX * outX + outZ * outZ);
        }

        /// <summary>The smallest distance (u) from the point to any parked car's box other than
        /// <paramref name="except"/>.</summary>
        public static float Clearance(float x, float z, string except = null)
        {
            float best = float.MaxValue;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Name == except)
                {
                    continue;
                }

                best = Mathf.Min(best, DistanceToBox(All[i], x, z));
            }

            return best;
        }
    }
}
