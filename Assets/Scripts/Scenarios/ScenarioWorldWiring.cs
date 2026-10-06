using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// Places the six scenario spots into MaliGoWorld at runtime (DESIGN_SPEC §3.3), the same way
    /// MaliGoIdentityRuntimeBootstrap patches in other systems. Keeps the scene file untouched. One trigger object
    /// per spot, <c>ScenarioSpot_&lt;ID&gt;</c>, at anchor + offset (or the measured absolute position when the
    /// anchor is missing). Every pair of interactables is at least 1.4 world units apart (2 x radius 0.7).
    /// </summary>
    public static class ScenarioWorldWiring
    {
        const float GroundY = 0.05f;

        struct Spot
        {
            public string Id;
            public string Anchor;
            public Vector3 Offset;
            public Vector2 FallbackXZ;

            public Spot(string id, string anchor, Vector3 offset, Vector2 fallbackXZ)
            {
                Id = id;
                Anchor = anchor;
                Offset = offset;
                FallbackXZ = fallbackXZ;
            }
        }

        static readonly Spot[] Spots =
        {
            new Spot(ChapterSchedule.SpotCorner, "Road_T_Intersection", new Vector3(0.6f, 0f, 1.2f), new Vector2(0.6f, 1.2f)),
            new Spot(ChapterSchedule.SpotTaxi, "Road_Crossing", new Vector3(0.2f, 0f, -0.5f), new Vector2(-1.8f, -0.5f)),
            new Spot(ChapterSchedule.SpotHub, "Road_Connecting_End", new Vector3(0f, 0f, -0.4f), new Vector2(0.0f, 3.6f)),
            new Spot(ChapterSchedule.SpotShopfront, "Road_Connecting_2", new Vector3(-1.5f, 0f, 1.1f), new Vector2(-1.5f, 3.1f)),
            new Spot(ChapterSchedule.SpotGate, "Player_House", new Vector3(-1.2f, 0f, 1.9f), new Vector2(0.8f, -0.3f)),
            new Spot(ChapterSchedule.SpotEast, "Road_Main_3", new Vector3(-0.4f, 0f, 0.9f), new Vector2(2.6f, 0.9f)),
        };

        public static void EnsureScenarioManager(GameObject systemsRoot)
        {
            if (Object.FindFirstObjectByType<ScenarioManager>() == null)
            {
                systemsRoot.AddComponent<ScenarioManager>();
            }
        }

        /// <summary>Kept for compatibility: ensures the CORNER spot, where lunch is offered.</summary>
        public static void EnsureFoodDecisionTrigger()
        {
            EnsureSpot(Spots[0]);
        }

        /// <summary>Places all six spot triggers (§3.3). Safe to call more than once.</summary>
        public static void EnsureAllScenarioTriggers()
        {
            foreach (Spot spot in Spots)
            {
                EnsureSpot(spot);
            }
        }

        static void EnsureSpot(Spot spot)
        {
            string objectName = "ScenarioSpot_" + spot.Id;
            if (GameObject.Find(objectName) != null)
            {
                return;
            }

            GameObject anchor = GameObject.Find(spot.Anchor);
            Vector3 position = anchor != null
                ? anchor.transform.position + spot.Offset
                : new Vector3(spot.FallbackXZ.x, GroundY, spot.FallbackXZ.y);

            var triggerObject = new GameObject(objectName);
            triggerObject.transform.position = position;
            var trigger = triggerObject.AddComponent<ScenarioTrigger>();
            trigger.ConfigureSpot(spot.Id);
        }
    }
}
