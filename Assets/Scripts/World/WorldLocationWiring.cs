using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Places Home/Bank/Work into the already-built MaliGoWorld scene at runtime, the same
    /// self-healing pattern as ScenarioWorldWiring - no manual Editor step required. Home and Bank are used inside
    /// their walk-in rooms (beside the bed, at the teller counter: <see cref="BuildingInteriors"/>, wired just before
    /// this); their street positions (DESIGN_SPEC §3.3: Home (2.0, -1.2), Bank (-1.7, 4.7)) are only the fallback if
    /// the rooms could not be built. Work (4.0, 0.3) is used from the street.
    /// </summary>
    public static class WorldLocationWiring
    {
        const string PlayerHouseAnchor = "Player_House";
        const string CommercialHubAnchor = "Local_Commercial_Hub";
        const string BankBuildingAnchor = "Local_Bank_Building";
        const string EastRoadAnchor = "Road_Main_4";

        public static void EnsureLocations()
        {
            EnsureHome();
            EnsureBank();
            EnsureWork();
            EnsureInteraction();
        }

        /// <summary>
        /// The locations are useless without the arbiter and the prompt. The bootstrap creates both in its own
        /// "interaction" step; this idempotent call only makes sure they exist if that step has not run.
        /// </summary>
        static void EnsureInteraction()
        {
            InteractionArbiter.Ensure(GameObject.Find("MaliGo_Systems"));
            MaliGo.UI.WorldPromptView.Ensure();
        }

        static void EnsureHome()
        {
            if (GameObject.Find("Location_Home") != null)
            {
                return;
            }

            // Inside the home room, beside the bed (BuildingInteriors); at the house's front door only if the room
            // could not be built.
            Vector3 position;
            if (!BuildingInteriors.TryGetInteractPoint(BuildingInteriors.HomeId, out position, out float radius))
            {
                GameObject anchor = GameObject.Find(PlayerHouseAnchor);
                position = anchor != null
                    ? anchor.transform.position + new Vector3(0f, 0f, 1.0f)
                    : new Vector3(2.0f, 0.05f, -1.2f);
                radius = 0f;
            }

            var obj = new GameObject("Location_Home");
            obj.transform.position = position;
            HomeInteraction home = obj.AddComponent<HomeInteraction>();
            if (radius > 0f)
            {
                home.SetInteractRadius(radius);
            }
        }

        static void EnsureBank()
        {
            if (GameObject.Find("Location_Bank") != null)
            {
                return;
            }

            // Inside the Bank room, at the teller counter (BuildingInteriors); at the building only if the room could
            // not be built.
            Vector3 position;
            if (!BuildingInteriors.TryGetInteractPoint(BuildingInteriors.BankId, out position, out float radius))
            {
                GameObject anchor = GameObject.Find(BankBuildingAnchor) ?? GameObject.Find(CommercialHubAnchor);
                position = anchor != null
                    ? anchor.transform.position + new Vector3(0.3f, 0f, 0.3f)
                    : new Vector3(-1.7f, 0.05f, 4.7f);
                radius = 0f;
            }

            var obj = new GameObject("Location_Bank");
            obj.transform.position = position;
            BankInteraction bank = obj.AddComponent<BankInteraction>();
            if (radius > 0f)
            {
                bank.SetInteractRadius(radius);
            }
        }

        static void EnsureWork()
        {
            if (GameObject.Find("Location_Work") != null)
            {
                return;
            }

            // The far end of the main road stands in for "town" (reachable from the west side of the
            // parked van; 1.52 from the EAST spot, DESIGN_SPEC 3.3).
            GameObject anchor = GameObject.Find(EastRoadAnchor);
            Vector3 position = anchor != null
                ? anchor.transform.position + new Vector3(0f, 0f, 0.3f)
                : new Vector3(4f, 0.05f, 0.3f);

            var obj = new GameObject("Location_Work");
            obj.transform.position = position;
            obj.AddComponent<WorkInteraction>();
        }
    }
}
