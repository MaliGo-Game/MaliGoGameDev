using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Finds room for the player before they are put down somewhere (coming out of a building, getting out of the
    /// car): a spot where their capsule overlaps no solid collider, that stands on the ground (not on a car, a roof
    /// or a prop) and that is inside the walkable town. The Bank's street exit used to land inside the delivery van
    /// parked in front of its door; nothing should ever put the player inside a car or a building again.
    /// </summary>
    public static class PlayerPlacement
    {
        /// <summary>The player's capsule (PlayerCharacterSpawner: height 0.47, radius 0.09).</summary>
        public const float PlayerRadius = 0.09f;
        public const float PlayerHeight = 0.47f;

        /// <summary>Extra room checked around the capsule (u).</summary>
        const float Clearance = 0.015f;

        /// <summary>The highest the ground under a spot may be above the spot's own height (u): a kerb, not a car.</summary>
        const float MaxGroundRise = 0.08f;

        /// <summary>Height (u) above a spot the downward look starts from: above every town roof (the tallest shop is
        /// 1.7 u), far below the walk-in rooms (60 u up).</summary>
        const float LookDownFrom = 2.5f;

        /// <summary>Distances (u) and directions searched around a blocked spot, nearest first.</summary>
        static readonly float[] SearchRings = { 0.12f, 0.24f, 0.36f, 0.5f, 0.7f };
        const int SearchDirections = 12;

        static readonly Collider[] Overlaps = new Collider[8];

        /// <summary>
        /// True when a standing player at <paramref name="spot"/> (feet height in y) would overlap nothing solid,
        /// stands on walkable ground and is inside the town. <paramref name="ignore"/> (e.g. the player's own
        /// controller) is not counted.
        /// </summary>
        public static bool IsClear(Vector3 spot, Collider ignore = null)
        {
            if (WalkableArea.TryGetTown(out Rect town) && !WalkableArea.IsInterior && !town.Contains(new Vector2(spot.x, spot.z)))
            {
                return false;
            }

            float radius = PlayerRadius + Clearance;
            Vector3 bottom = spot + Vector3.up * (radius + 0.02f);
            Vector3 top = spot + Vector3.up * Mathf.Max(radius + 0.03f, PlayerHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Overlaps, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = Overlaps[i];
                if (other != null && other != ignore && !(other is CharacterController))
                {
                    return false;
                }
            }

            // Nothing over the spot and standing on the ground: looking straight down from above the rooftops, the
            // first thing hit must be the ground (not a car, a roof or a tree). This also catches a spot inside a
            // building's hollow mesh shell, where the capsule overlaps no triangle at all.
            if (Physics.Raycast(spot + Vector3.up * LookDownFrom, Vector3.down, out RaycastHit below, LookDownFrom + 0.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && below.collider != ignore && !(below.collider is CharacterController))
            {
                if (below.collider.gameObject.layer == VehicleColliders.Layer || below.point.y > spot.y + MaxGroundRise)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <paramref name="desired"/> if it is clear, otherwise the nearest clear spot around it (searching outward
        /// up to 0.7 u). False (and <paramref name="spot"/> = <paramref name="desired"/>) when nothing nearby is clear.
        /// </summary>
        public static bool TryFindClearSpot(Vector3 desired, out Vector3 spot, Collider ignore = null)
        {
            Physics.SyncTransforms();
            if (IsClear(desired, ignore))
            {
                spot = desired;
                return true;
            }

            for (int ring = 0; ring < SearchRings.Length; ring++)
            {
                for (int i = 0; i < SearchDirections; i++)
                {
                    float angle = i * (Mathf.PI * 2f / SearchDirections);
                    Vector3 candidate = desired + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * SearchRings[ring];
                    if (IsClear(candidate, ignore))
                    {
                        spot = candidate;
                        return true;
                    }
                }
            }

            spot = desired;
            return false;
        }
    }
}
