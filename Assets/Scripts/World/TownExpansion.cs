using System.Collections.Generic;
using MaliGo.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaliGo.World
{
    /// <summary>
    /// Builds the streets around the original town at runtime (<see cref="TownLayout"/>: a ring road, the main road and
    /// the connecting road carried on to it, houses and shops facing it, trees) and makes the scene safe to walk and
    /// drive in. No scene edits: the original town, its scenario spots, Home, Bank, Work, the interiors and their door
    /// triggers stay exactly where they are.
    ///
    /// Every world load (<see cref="Ensure"/>, a bootstrap step before the interiors):
    /// 1. The parked cars get one fitted box each on the Vehicle layer (<see cref="VehicleColliders"/>), and the
    ///    delivery van that was parked across the Bank's door is moved up the kerb, clear of it.
    /// 2. With <see cref="MaliGoFeatures.TownExpansion"/> on: the road tiles, buildings and trees are instantiated
    ///    from the baked <see cref="TownPieceCatalog"/> under one "Town_Expansion" group (not under the generator's
    ///    measured groups, so the walkable town is the road rectangle, not the outer houses' back gardens). The
    ///    cul-de-sac tile, now a through road, is hidden (its object stays: a scenario spot is anchored to it).
    /// 3. The invisible boundary walls are moved out around the new streets, the lawn is made big enough to fill
    ///    the view at the new edges, and the walkable town and the car's drive area are grown to the road rectangle
    ///    (<see cref="WalkableArea.SetTownExtension"/>, <see cref="WalkableArea.SetDriveArea"/>), which the camera
    ///    bounds follow.
    ///
    /// Cost on a 3.6 GB phone: 21 small models shared with the scene's kits (one atlas material per kit, so the SRP
    /// Batcher draws them cheaply), ~70 road tiles that cast no shadows, ~25 buildings and ~27 trees with box
    /// colliders (no mesh colliders), no lights. The orthographic view shows only a few tiles at a time, so frustum
    /// culling skips most of it every frame. Nothing runs per frame.
    /// </summary>
    public static class TownExpansion
    {
        public const string RootName = "Town_Expansion";
        const string EnvironmentRootName = "--- ENVIRONMENT ---";
        const string RoadsGroupName = "Roads_Network";
        const string LawnName = "Ground_Lawn";

        /// <summary>The van the generator parked across the Bank's west-facing door (its nose 0.33 u from the door,
        /// its body covering the whole doorway and the spot the Bank's exit put the player on), and where it goes.</summary>
        const string DeliveryVanName = "Vehicle_DeliveryVan";
        static readonly Vector3 DeliveryVanScenePosition = new Vector3(-3.3f, 0f, 4.4f);
        static readonly Vector3 DeliveryVanClearPosition = new Vector3(-3.3f, 0f, 5.25f);

        /// <summary>How far the boundary walls stand outside the road rectangle (u).</summary>
        const float WallMargin = 0.6f;

        /// <summary>Lawn kept beyond the road rectangle on every side (u), so the view never runs past the ground.</summary>
        const float LawnBeyondRoads = 9f;

        /// <summary>Unity's built-in plane is 10 x 10 units at scale 1.</summary>
        const float PlaneSize = 10f;

        /// <summary>A tree's trunk collider (u): the crowns overhang the pavement, only the trunk stops you.</summary>
        static readonly Vector3 TrunkSize = new Vector3(0.08f, 0.4f, 0.08f);

        static int builtSceneHandle = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            builtSceneHandle = -1;
        }

        /// <summary>Applies the vehicle fixes and builds the expansion once per world scene.</summary>
        public static void Ensure()
        {
            int handle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (builtSceneHandle == handle || GameObject.Find(RootName) != null)
            {
                return;
            }

            builtSceneHandle = handle;

            int cars = VehicleColliders.FitAll();
            MoveVanOffBankDoor();

            if (!MaliGoFeatures.TownExpansion)
            {
                Debug.Log($"[TownExpansion] {cars} parked cars fitted; streets off by feature switch.");
                return;
            }

            var root = new GameObject(RootName);
            GameObject environment = GameObject.Find(EnvironmentRootName);
            if (environment != null)
            {
                root.transform.SetParent(environment.transform, false);
            }

            var catalog = Resources.Load<TownPieceCatalog>(TownPieceCatalog.ResourceName);
            var loaded = new Dictionary<string, GameObject>();
            int roads = BuildRoads(root.transform, catalog, loaded);
            int buildings = BuildPlacements(root.transform, catalog, loaded, TownLayout.Buildings, "Building_");
            int trees = BuildPlacements(root.transform, catalog, loaded, TownLayout.Trees, "Tree_");

            if (roads == 0)
            {
                Debug.LogWarning("[TownExpansion] No road models found (TownPieceCatalog not baked?); the town keeps its old edges.");
                return;
            }

            HideReplacedTiles();
            TownLayout.RoadBounds(out float minX, out float maxX, out float minZ, out float maxZ);
            Rect roadArea = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            MoveBoundaryWalls(roadArea);
            EnsureLawnCovers(roadArea);
            WalkableArea.SetDriveArea(roadArea);
            WalkableArea.SetTownExtension(roadArea);

            Debug.Log($"[TownExpansion] {cars} parked cars fitted; built {roads} road tiles, {buildings} buildings, {trees} trees; " +
                      $"roads x {minX}..{maxX}, z {minZ}..{maxZ}.");
        }

        // ================================================================ vehicles

        static void MoveVanOffBankDoor()
        {
            GameObject van = GameObject.Find(DeliveryVanName);
            if (van == null)
            {
                return;
            }

            Vector3 position = van.transform.position;
            if (new Vector2(position.x - DeliveryVanScenePosition.x, position.z - DeliveryVanScenePosition.z).sqrMagnitude > 0.01f)
            {
                return; // moved in the scene since; leave it
            }

            van.transform.position = new Vector3(DeliveryVanClearPosition.x, position.y, DeliveryVanClearPosition.z);
        }

        // ================================================================ building

        static int BuildRoads(Transform root, TownPieceCatalog catalog, Dictionary<string, GameObject> loaded)
        {
            var group = new GameObject("Roads");
            group.transform.SetParent(root, false);
            int built = 0;
            foreach (RoadTile tile in TownLayout.TilesToBuild())
            {
                GameObject prefab = Load(TownLayout.ModelOf(tile.Piece), catalog, loaded);
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = Object.Instantiate(prefab, new Vector3(tile.X, 0f, tile.Z),
                    Quaternion.Euler(0f, tile.Yaw, 0f), group.transform);
                instance.name = $"Road_{tile.Piece}_{tile.X}_{tile.Z}";
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                RemoveColliders(instance);
                built++;
            }

            return built;
        }

        static int BuildPlacements(Transform root, TownPieceCatalog catalog, Dictionary<string, GameObject> loaded,
            TownPlacement[] placements, string prefix)
        {
            var group = new GameObject(prefix.TrimEnd('_') + "s");
            group.transform.SetParent(root, false);
            int built = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                TownPlacement placement = placements[i];
                GameObject prefab = Load(placement.Model, catalog, loaded);
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = Object.Instantiate(prefab, new Vector3(placement.X, 0f, placement.Z),
                    Quaternion.Euler(0f, placement.Yaw, 0f), group.transform);
                instance.name = prefix + i;
                RemoveColliders(instance);

                BoxCollider box = instance.AddComponent<BoxCollider>();
                if (placement.IsTree)
                {
                    box.center = new Vector3(0f, TrunkSize.y * 0.5f, 0f);
                    box.size = TrunkSize;
                }
                else
                {
                    FitBox(instance.transform, box);
                }

                built++;
            }

            return built;
        }

        /// <summary>A building's box: its meshes' bounds in its own space, from the ground up.</summary>
        static void FitBox(Transform building, BoxCollider box)
        {
            if (!VehicleColliders.TryLocalBounds(building, out Bounds local))
            {
                box.size = new Vector3(1f, 1f, 1f);
                box.center = new Vector3(0f, 0.5f, 0f);
                return;
            }

            float bottom = Mathf.Min(0f, local.min.y);
            box.center = new Vector3(local.center.x, (bottom + local.max.y) * 0.5f, local.center.z);
            box.size = new Vector3(local.size.x, local.max.y - bottom, local.size.z);
        }

        static void RemoveColliders(GameObject instance)
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.Destroy(collider);
            }
        }

        static GameObject Load(string key, TownPieceCatalog catalog, Dictionary<string, GameObject> loaded)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (loaded.TryGetValue(key, out GameObject cached))
            {
                return cached;
            }

            GameObject prefab = catalog != null ? catalog.Find(key) : null;
#if UNITY_EDITOR
            if (prefab == null)
            {
                string path = TownLayout.AssetPathOf(key);
                if (path != null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
#endif
            if (prefab == null)
            {
                Debug.LogWarning($"[TownExpansion] Model '{key}' is not in the TownPieceCatalog; skipped.");
            }

            loaded[key] = prefab;
            return prefab;
        }

        // ================================================================ the old town's edges

        /// <summary>Hides the scene tiles whose cells become a different piece (the cul-de-sac): their renderers are
        /// switched off, the objects stay where they are.</summary>
        static void HideReplacedTiles()
        {
            GameObject roads = GameObject.Find(RoadsGroupName);
            if (roads == null)
            {
                return;
            }

            List<RoadTile> replaced = TownLayout.ReplacedExistingCells();
            foreach (Transform tile in roads.transform)
            {
                Vector3 p = tile.position;
                for (int i = 0; i < replaced.Count; i++)
                {
                    if (Mathf.Abs(p.x - replaced[i].X) < 0.25f && Mathf.Abs(p.z - replaced[i].Z) < 0.25f)
                    {
                        foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>())
                        {
                            renderer.enabled = false;
                        }
                    }
                }
            }
        }

        /// <summary>Moves the generator's four invisible walls out around <paramref name="roads"/>.</summary>
        static void MoveBoundaryWalls(Rect roads)
        {
            float minX = roads.xMin - WallMargin;
            float maxX = roads.xMax + WallMargin;
            float minZ = roads.yMin - WallMargin;
            float maxZ = roads.yMax + WallMargin;
            float width = maxX - minX + 2f;
            float depth = maxZ - minZ + 2f;
            float midX = (minX + maxX) * 0.5f;
            float midZ = (minZ + maxZ) * 0.5f;

            PlaceWall("Boundary_North", new Vector3(midX, 2.5f, maxZ + 0.5f), new Vector3(width, 5f, 1f));
            PlaceWall("Boundary_South", new Vector3(midX, 2.5f, minZ - 0.5f), new Vector3(width, 5f, 1f));
            PlaceWall("Boundary_West", new Vector3(minX - 0.5f, 2.5f, midZ), new Vector3(1f, 5f, depth));
            PlaceWall("Boundary_East", new Vector3(maxX + 0.5f, 2.5f, midZ), new Vector3(1f, 5f, depth));
        }

        static void PlaceWall(string wallName, Vector3 position, Vector3 size)
        {
            GameObject wall = GameObject.Find(wallName);
            BoxCollider box = wall != null ? wall.GetComponent<BoxCollider>() : null;
            if (box == null)
            {
                return;
            }

            wall.transform.position = position;
            wall.transform.rotation = Quaternion.identity;
            Vector3 scale = wall.transform.lossyScale;
            box.center = Vector3.zero;
            box.size = new Vector3(size.x / Mathf.Max(0.0001f, scale.x), size.y / Mathf.Max(0.0001f, scale.y),
                size.z / Mathf.Max(0.0001f, scale.z));
        }

        /// <summary>Scales the lawn plane up (never down) so it reaches <see cref="LawnBeyondRoads"/> past the roads.</summary>
        static void EnsureLawnCovers(Rect roads)
        {
            GameObject lawn = GameObject.Find(LawnName);
            if (lawn == null)
            {
                return;
            }

            Vector3 centre = lawn.transform.position;
            float half = Mathf.Max(Mathf.Max(Mathf.Abs(roads.xMin - centre.x), Mathf.Abs(roads.xMax - centre.x)),
                Mathf.Max(Mathf.Abs(roads.yMin - centre.z), Mathf.Abs(roads.yMax - centre.z))) + LawnBeyondRoads;
            float scale = half * 2f / PlaneSize;
            Vector3 current = lawn.transform.localScale;
            if (current.x >= scale && current.z >= scale)
            {
                return;
            }

            lawn.transform.localScale = new Vector3(Mathf.Max(current.x, scale), current.y, Mathf.Max(current.z, scale));
        }
    }
}
