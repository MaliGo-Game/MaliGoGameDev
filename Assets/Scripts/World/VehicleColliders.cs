using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Gives every parked car one simple collider. The scene's cars (added by the generator's "Add Parked Vehicles")
    /// carried a non-convex MeshCollider on each part: the body shell (open underneath, its lower edge 0.05 u off the
    /// ground, below the player's 0.066 u step), four separate wheels tucked under it and the van's back door. The
    /// player's small capsule (radius 0.09) wedged under the body's lip and between the wheels, and anything put down
    /// inside a car's footprint was trapped inside the hollow shell.
    ///
    /// Here each car's part colliders are removed and replaced with one BoxCollider on the car's root, fitted to its
    /// meshes' bounds, starting at the ground (no lip to step under or onto) and padded by <see cref="Padding"/>. The
    /// car goes on the <see cref="LayerName"/> layer, which the player's ground snap ignores, so the player is never
    /// snapped up onto a car. Idempotent: a car already fitted is left alone.
    /// </summary>
    public static class VehicleColliders
    {
        /// <summary>The scene group the parked cars are under.</summary>
        public const string GroupName = "Vehicles";

        /// <summary>The physics layer for cars (ProjectSettings/TagManager, user layer 8).</summary>
        public const string LayerName = "Vehicle";

        /// <summary>Used when the layer is not named in this project's settings (an unnamed layer works the same).</summary>
        const int FallbackLayer = 8;

        /// <summary>World units added to each side of a car's box, so the capsule stops a hair before the paint.</summary>
        public const float Padding = 0.012f;

        static int cachedLayer = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cachedLayer = -1;
        }

        /// <summary>The Vehicle layer index.</summary>
        public static int Layer
        {
            get
            {
                if (cachedLayer < 0)
                {
                    int named = LayerMask.NameToLayer(LayerName);
                    cachedLayer = named >= 0 ? named : FallbackLayer;
                }

                return cachedLayer;
            }
        }

        /// <summary>A layer mask of everything except cars (and the layers raycasts skip by default).</summary>
        public static int AllButVehicles => Physics.DefaultRaycastLayers & ~(1 << Layer);

        /// <summary>Fits every car under the scene's <see cref="GroupName"/> group. Returns how many were fitted.</summary>
        public static int FitAll()
        {
            GameObject group = GameObject.Find(GroupName);
            if (group == null)
            {
                return 0;
            }

            int fitted = 0;
            foreach (Transform car in group.transform)
            {
                if (Fit(car.gameObject) != null)
                {
                    fitted++;
                }
            }

            return fitted;
        }

        /// <summary>
        /// Gives <paramref name="car"/> its single box (or returns the one it already has), removing every other
        /// collider under it. Null if the car has no meshes to measure.
        /// </summary>
        public static BoxCollider Fit(GameObject car)
        {
            if (car == null)
            {
                return null;
            }

            BoxCollider box = car.GetComponent<BoxCollider>();
            bool already = box != null && car.layer == Layer;
            if (already)
            {
                return box;
            }

            if (!TryLocalBounds(car.transform, out Bounds local))
            {
                return null;
            }

            foreach (Collider part in car.GetComponentsInChildren<Collider>(true))
            {
                if (part.gameObject != car)
                {
                    part.enabled = false;
                    Object.Destroy(part);
                }
            }

            if (box == null)
            {
                box = car.AddComponent<BoxCollider>();
            }

            // From the ground (local y 0 is the car's pivot at its tyres' contact) to the roof, padded in world units.
            float scale = Mathf.Max(0.0001f, Mathf.Abs(car.transform.lossyScale.x));
            float pad = Padding / scale;
            float bottom = Mathf.Min(0f, local.min.y);
            float top = local.max.y + pad;
            box.center = new Vector3(local.center.x, (bottom + top) * 0.5f, local.center.z);
            box.size = new Vector3(local.size.x + pad * 2f, top - bottom, local.size.z + pad * 2f);
            box.isTrigger = false;

            SetLayerRecursively(car.transform, Layer);
            return box;
        }

        /// <summary>The meshes' bounds under <paramref name="root"/> in its own space, from the shared meshes' bounds
        /// (available in a build even when the mesh is not readable). Also used for the expansion's buildings.</summary>
        public static bool TryLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 partToRoot = toRoot * filter.transform.localToWorldMatrix;
                Bounds meshBounds = mesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    Vector3 point = partToRoot.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return any && bounds.size.x > 0f && bounds.size.z > 0f;
        }

        static void SetLayerRecursively(Transform node, int layer)
        {
            node.gameObject.layer = layer;
            for (int i = 0; i < node.childCount; i++)
            {
                SetLayerRecursively(node.GetChild(i), layer);
            }
        }
    }
}
