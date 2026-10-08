using System;
using System.Collections.Generic;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.World
{
    /// <summary>
    /// One walk-in room, as built by <see cref="InteriorRoomBuilder"/>. Positions are world space; the room's floor top
    /// is at <see cref="Origin"/>.y and its two walls stand on the +X and +Z sides (the far sides from the isometric
    /// camera, which looks toward +X +Z), so the camera sees into it.
    /// </summary>
    public sealed class InteriorRoom
    {
        public string Id;
        public GameObject Root;

        /// <summary>The centre of the floor, on its top face.</summary>
        public Vector3 Origin;
        public float Width;
        public float Depth;

        /// <summary>The floor (world XZ: x/width = X, y/height = Z).</summary>
        public Rect Floor;

        /// <summary>Where the player stands after coming in, and the way they face.</summary>
        public Vector3 EntrySpot;
        public Vector3 EntryFacing;

        /// <summary>The middle of the inside door on the wall face, and the normal pointing into the room.</summary>
        public Vector3 DoorPoint;
        public Vector3 DoorNormal;
        public float DoorHalfWidth;

        /// <summary>Where the "go out" prompt sits (on the door mat).</summary>
        public Vector3 ExitPromptPoint;

        /// <summary>Where the room's own location is used (Home: beside the bed; Bank: at the teller counter).</summary>
        public Vector3 InteractPoint;
        public float InteractRadius;

        /// <summary>Home only: where the player wakes up each morning, and the way they face.</summary>
        public bool HasWakeSpot;
        public Vector3 WakeSpot;
        public Vector3 WakeFacing;
    }

    /// <summary>
    /// Builds the walk-in rooms at runtime (no scene edits): a floor, two back walls, a door and furniture from the
    /// Kenney furniture kit (<see cref="InteriorPieceCatalog"/>), with simple BoxColliders on the walls, the open
    /// sides and every piece the player must walk around. The kit's models are scaled to the world (about 0.27 u per
    /// metre: a bed ~0.56 u long, the door ~0.5 u tall, walls 0.62 u) from their measured bounds, so it does not matter
    /// what unit the FBX import used. A missing model is stood in for by a plain box of the right size, so a room is
    /// never left empty or broken.
    ///
    /// Every surface colour is one shared material (made once per colour from <paramref name="template"/>, the lawn's
    /// URP Lit material, so its shader is certain to be in the build); the kit's own imported materials are shared
    /// as they are. The builder owns the materials it made: <see cref="Dispose"/> destroys them.
    /// </summary>
    public sealed class InteriorRoomBuilder : IDisposable
    {
        /// <summary>Every furniture-kit model the rooms use (the baker ships exactly these).</summary>
        public static readonly string[] PieceIds =
        {
            "bedSingle", "cabinetBedDrawerTable", "lampSquareTable", "kitchenCabinet", "kitchenCoffeeMachine",
            "kitchenFridgeSmall", "table", "chairCushion", "rugRectangle", "rugDoormat", "pottedPlant", "doorway",
            "radio", "kitchenBar", "computerScreen", "chairDesk"
        };

        /// <summary>
        /// The kit's native sizes (x, y, z) in its own units, measured from the OBJ versions of the same models
        /// (1 kit unit is about 1.8 m: the single bed is 1.125 long). Used to scale each model to
        /// <see cref="KitScale"/> whatever unit its FBX imported at, and to size a stand-in box if a model is missing.
        /// </summary>
        static readonly Dictionary<string, Vector3> NativeSizes = new Dictionary<string, Vector3>
        {
            { "bedSingle", new Vector3(0.571f, 0.375f, 1.125f) },
            { "cabinetBedDrawerTable", new Vector3(0.266f, 0.263f, 0.217f) },
            { "lampSquareTable", new Vector3(0.12f, 0.29f, 0.12f) },
            { "kitchenCabinet", new Vector3(0.43f, 0.45f, 0.45f) },
            { "kitchenCoffeeMachine", new Vector3(0.19f, 0.177f, 0.24f) },
            { "kitchenFridgeSmall", new Vector3(0.43f, 0.6f, 0.292f) },
            { "table", new Vector3(0.841f, 0.327f, 0.447f) },
            { "chairCushion", new Vector3(0.2f, 0.46f, 0.2f) },
            { "rugRectangle", new Vector3(1.57f, 0.01f, 0.92f) },
            { "rugDoormat", new Vector3(0.429f, 0.01f, 0.237f) },
            { "pottedPlant", new Vector3(0.212f, 0.654f, 0.241f) },
            { "doorway", new Vector3(0.486f, 1.01f, 0.113f) },
            { "radio", new Vector3(0.315f, 0.228f, 0.098f) },
            { "kitchenBar", new Vector3(0.43f, 0.42f, 0.21f) },
            { "computerScreen", new Vector3(0.393f, 0.294f, 0.104f) },
            { "chairDesk", new Vector3(0.335f, 0.608f, 0.314f) },
        };

        /// <summary>World units per kit unit: 0.5 puts the bed at ~0.56 u (2 m) and a chair seat at a person's knee.</summary>
        public const float KitScale = 0.5f;

        /// <summary>Wall height (u, ~2.3 m) and thickness; the floor slab's thickness.</summary>
        public const float WallHeight = 0.62f;
        const float WallThickness = 0.05f;
        const float FloorThickness = 0.04f;

        /// <summary>Furniture colliders are at least this tall, well over the player's 0.066 u step, so nothing is
        /// climbed; and no taller than this, which is enough to block a 0.47 u capsule.</summary>
        const float MinColliderHeight = 0.12f;
        const float MaxColliderHeight = 0.35f;

        /// <summary>The coloured ground around the room, big enough to fill the view at any room framing.</summary>
        const float SurroundSize = 16f;

        // Kenney's furniture faces -Z (its fronts, drawer handles and seats look that way), so a yaw of 0 faces -Z,
        // 180 faces +Z, -90 faces +X and 90 faces -X.
        const float FaceMinusZ = 0f;
        const float FacePlusZ = 180f;
        const float FacePlusX = -90f;
        const float FaceMinusX = 90f;

        static readonly Color Surround = Hex(0x0F5E2E);
        static readonly Color Wood = Hex(0xB47B41);
        static readonly Color Glass = Hex(0xBFE3F2);

        readonly InteriorPieceCatalog catalog;
        readonly Material template;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly Mesh cubeMesh;
        readonly Mesh cylinderMesh;

        public InteriorRoomBuilder(InteriorPieceCatalog catalog, Material template)
        {
            this.catalog = catalog;
            this.template = template;
            cubeMesh = BuiltinMesh(PrimitiveType.Cube);
            cylinderMesh = BuiltinMesh(PrimitiveType.Cylinder);
        }

        /// <summary>Destroys the materials this builder made (the rooms must be gone or about to go).</summary>
        public void Dispose()
        {
            foreach (Material material in materials.Values)
            {
                if (material != null)
                {
                    UnityEngine.Object.Destroy(material);
                }
            }

            materials.Clear();
        }

        // ================================================================ the rooms

        /// <summary>
        /// The player's home: a small, warm bachelor flat. Bed in the far corner with a bedside table and lamp, a
        /// kitchen counter (two cabinets, a small fridge, a kettle) along the back wall, a table with a chair and a
        /// radio, a rug, a plant and a window. The Home sheet is used beside the bed.
        /// </summary>
        public InteriorRoom BuildHome(Vector3 origin, Transform parent)
        {
            const float width = 2.0f;
            const float depth = 1.7f;
            float east = width * 0.5f;
            float north = depth * 0.5f;

            InteriorRoom room = Shell("Home", origin, width, depth, parent,
                floor: Hex(0xC08A5B), plinth: Hex(0x7A5236), wall: Hex(0xF3E6D3), trim: Hex(0xB47B41));
            Transform root = room.Root.transform;

            // Bed along the east wall, head in the far corner (its head end is at the model's +Z).
            Place(root, "bedSingle", east - 0.153f, north - 0.291f, FaceMinusZ, 0f, true);
            float nightstandTop = Place(root, "cabinetBedDrawerTable", east - 0.383f, north - 0.064f, FaceMinusZ, 0f, true);
            Place(root, "lampSquareTable", east - 0.383f, north - 0.064f, FaceMinusZ, nightstandTop, false);

            // Kitchen counter on the north wall: two cabinets, a kettle on one, and a small fridge.
            float cabinetTop = Place(root, "kitchenCabinet", -0.30f, north - 0.1175f, FaceMinusZ, 0f, true);
            Place(root, "kitchenCabinet", -0.085f, north - 0.1175f, FaceMinusZ, 0f, true);
            Place(root, "kitchenCoffeeMachine", -0.30f, north - 0.1175f, FaceMinusZ, cabinetTop, false);
            Place(root, "kitchenFridgeSmall", 0.13f, north - 0.078f, FaceMinusZ, 0f, true);

            // Table with a chair (facing the table) and a radio, on a rug.
            Place(root, "rugRectangle", 0.12f, -0.15f, FaceMinusZ, 0.002f, false);
            float tableTop = Place(root, "table", -0.50f, -0.30f, FaceMinusZ, 0f, true);
            Place(root, "chairCushion", -0.50f, -0.47f, FacePlusZ, 0f, true);
            Place(root, "radio", -0.58f, -0.27f, FaceMinusZ, tableTop, false);

            Place(root, "pottedPlant", east - 0.12f, -north + 0.13f, FaceMinusZ, 0f, true);
            Window(root, east, -0.15f);

            Door(room, -0.70f);

            room.InteractPoint = origin + new Vector3(east - 0.42f, 0f, north - 0.40f);
            room.InteractRadius = 0.25f;
            room.HasWakeSpot = true;
            room.WakeSpot = origin + new Vector3(east - 0.60f, 0f, north - 0.70f);
            room.WakeFacing = new Vector3(-1f, 0f, -1f).normalized;
            return room;
        }

        /// <summary>
        /// The Bank branch: a teller counter along the east wall closing off the teller's corner (a screen and a desk
        /// chair behind it), a queue line of posts and rope in front of it, two waiting chairs by the door, plants
        /// and a plain "Bank" plate (generic: no real bank's name or colours). The Bank sheet is used at the counter.
        /// </summary>
        public InteriorRoom BuildBank(Vector3 origin, Transform parent)
        {
            const float width = 2.2f;
            const float depth = 1.8f;
            float east = width * 0.5f;
            float north = depth * 0.5f;

            InteriorRoom room = Shell("Bank", origin, width, depth, parent,
                floor: Hex(0xE4DED3), plinth: Hex(0x8C8579), wall: Hex(0xFBF6EC), trim: Hex(0x087A18));
            Transform root = room.Root.transform;

            // A green dado band along both walls.
            Box(root, "Dado_East", new Vector3(east - 0.004f, 0.11f, 0f), new Vector3(0.008f, 0.1f, depth), Hex(0x0F5E2E), false, false);
            Box(root, "Dado_North", new Vector3(0f, 0.11f, north - 0.004f), new Vector3(width, 0.1f, 0.008f), Hex(0x0F5E2E), false, false);

            // The counter: five bar units turned to run along Z from the north wall, then two across to the east
            // wall, so the teller's corner behind it is closed off.
            const float counterX = 0.65f;
            const float unit = 0.215f;
            float counterTop = 0f;
            for (int i = 0; i < 5; i++)
            {
                float z = north - unit * 0.5f - unit * i;
                counterTop = Place(root, "kitchenBar", counterX, z, FaceMinusX, 0f, true);
            }

            float returnZ = north - unit * 5f - 0.0525f;
            Place(root, "kitchenBar", counterX + 0.11f, returnZ, FaceMinusZ, 0f, true);
            Place(root, "kitchenBar", counterX + 0.325f, returnZ, FaceMinusZ, 0f, true);

            // The teller's side: screens facing the teller, and the teller's chair.
            Place(root, "computerScreen", counterX + 0.02f, 0.45f, FacePlusX, counterTop, false);
            Place(root, "computerScreen", counterX + 0.02f, -0.02f, FacePlusX, counterTop, false);
            Place(root, "chairDesk", east - 0.17f, 0.22f, FaceMinusX, 0f, true);

            Sign(root, new Vector3(east, 0.47f, 0.22f), "Bank");

            // Queue line in front of the counter: three posts with rope between them (one collider for the line).
            const float queueX = 0.05f;
            QueuePost(root, queueX, 0.45f);
            QueuePost(root, queueX, 0f);
            QueuePost(root, queueX, -0.45f);
            Box(root, "Rope_A", new Vector3(queueX, 0.14f, 0.225f), new Vector3(0.016f, 0.016f, 0.45f), Hex(0xA8432F), false, true);
            Box(root, "Rope_B", new Vector3(queueX, 0.14f, -0.225f), new Vector3(0.016f, 0.016f, 0.45f), Hex(0xA8432F), false, true);
            Blocker(root, "Queue_Line", new Vector3(queueX, 0.15f, 0f), new Vector3(0.06f, 0.3f, 0.96f));

            // Waiting chairs by the door, plants in the open corners.
            Place(root, "chairCushion", -0.36f, north - 0.08f, FaceMinusZ, 0f, true);
            Place(root, "chairCushion", -0.13f, north - 0.08f, FaceMinusZ, 0f, true);
            Place(root, "pottedPlant", -east + 0.14f, -north + 0.14f, FaceMinusZ, 0f, true);
            Place(root, "pottedPlant", 0.45f, -north + 0.13f, FaceMinusZ, 0f, true);

            Door(room, -0.75f);

            room.InteractPoint = origin + new Vector3(0.40f, 0f, 0.30f);
            room.InteractRadius = 0.3f;
            return room;
        }

        // ================================================================ shell

        InteriorRoom Shell(string id, Vector3 origin, float width, float depth, Transform parent,
            Color floor, Color plinth, Color wall, Color trim)
        {
            var rootObject = new GameObject("Interior_" + id);
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.position = origin;
            Transform root = rootObject.transform;

            float east = width * 0.5f;
            float north = depth * 0.5f;

            // The ground around the room fills the rest of the view, so nothing of the sky or the town shows.
            Box(root, "Surround", new Vector3(0f, -0.075f, 0f), new Vector3(SurroundSize, 0.02f, SurroundSize), Surround, false, false);
            Box(root, "Plinth", new Vector3(0f, -0.045f, 0f), new Vector3(width + 0.08f, 0.05f, depth + 0.08f), plinth, false, false);
            Box(root, "Floor", new Vector3(0f, -FloorThickness * 0.5f, 0f), new Vector3(width, FloorThickness, depth), floor, true, false);

            // Walls on the two far sides only; the east wall also covers the corner.
            Box(root, "Wall_East", new Vector3(east + WallThickness * 0.5f, WallHeight * 0.5f, WallThickness * 0.5f),
                new Vector3(WallThickness, WallHeight, depth + WallThickness), wall, true, false);
            Box(root, "Wall_North", new Vector3(0f, WallHeight * 0.5f, north + WallThickness * 0.5f),
                new Vector3(width, WallHeight, WallThickness), wall, true, false);
            Color cap = Color.Lerp(wall, Color.black, 0.18f);
            Box(root, "WallCap_East", new Vector3(east + WallThickness * 0.5f, WallHeight + 0.006f, WallThickness * 0.5f),
                new Vector3(WallThickness + 0.004f, 0.012f, depth + WallThickness + 0.004f), cap, false, false);
            Box(root, "WallCap_North", new Vector3(0f, WallHeight + 0.006f, north + WallThickness * 0.5f),
                new Vector3(width, 0.012f, WallThickness + 0.004f), cap, false, false);
            Box(root, "Skirting_East", new Vector3(east - 0.005f, 0.022f, 0f), new Vector3(0.01f, 0.044f, depth), trim, false, false);
            Box(root, "Skirting_North", new Vector3(0f, 0.022f, north - 0.005f), new Vector3(width, 0.044f, 0.01f), trim, false, false);

            // The two open sides are closed by invisible walls (the camera looks in over them).
            Blocker(root, "Edge_West", new Vector3(-east - 0.05f, 0.3f, 0f), new Vector3(0.1f, 0.6f, depth + 0.2f));
            Blocker(root, "Edge_South", new Vector3(0f, 0.3f, -north - 0.05f), new Vector3(width + 0.2f, 0.6f, 0.1f));

            return new InteriorRoom
            {
                Id = id,
                Root = rootObject,
                Origin = origin,
                Width = width,
                Depth = depth,
                Floor = Rect.MinMaxRect(origin.x - east, origin.z - north, origin.x + east, origin.z + north)
            };
        }

        /// <summary>The way out: a door on the north wall at <paramref name="x"/> with a mat in front of it. Coming in,
        /// the player stands a few steps in front of it facing into the room.</summary>
        void Door(InteriorRoom room, float x)
        {
            Transform root = room.Root.transform;
            float north = room.Depth * 0.5f;

            Place(root, "doorway", x, north - 0.03f, FaceMinusZ, 0f, false);
            Box(root, "DoorFrame_L", new Vector3(x - 0.135f, 0.27f, north - 0.008f), new Vector3(0.025f, 0.54f, 0.016f), Wood, false, false);
            Box(root, "DoorFrame_R", new Vector3(x + 0.135f, 0.27f, north - 0.008f), new Vector3(0.025f, 0.54f, 0.016f), Wood, false, false);
            Box(root, "DoorFrame_Top", new Vector3(x, 0.545f, north - 0.008f), new Vector3(0.295f, 0.025f, 0.016f), Wood, false, false);
            Place(root, "rugDoormat", x, north - 0.13f, FaceMinusZ, 0.002f, false);

            room.DoorPoint = room.Origin + new Vector3(x, 0f, north);
            room.DoorNormal = Vector3.back;
            room.DoorHalfWidth = 0.13f;
            room.ExitPromptPoint = room.DoorPoint + room.DoorNormal * 0.23f;
            room.EntrySpot = room.DoorPoint + room.DoorNormal * 0.47f;
            room.EntryFacing = room.DoorNormal;
        }

        /// <summary>A window on the east wall's inside face, centred at <paramref name="z"/>.</summary>
        void Window(Transform root, float east, float z)
        {
            Box(root, "Window_Frame", new Vector3(east - 0.008f, 0.36f, z), new Vector3(0.016f, 0.24f, 0.34f), Wood, false, false);
            Box(root, "Window_Glass", new Vector3(east - 0.018f, 0.36f, z), new Vector3(0.004f, 0.19f, 0.29f), Glass, false, false);
            Box(root, "Window_Bar", new Vector3(east - 0.021f, 0.36f, z), new Vector3(0.004f, 0.19f, 0.012f), Wood, false, false);
            Box(root, "Window_Sill", new Vector3(east - 0.022f, 0.235f, z), new Vector3(0.044f, 0.014f, 0.38f), Wood, false, false);
        }

        void QueuePost(Transform root, float x, float z)
        {
            Cylinder(root, "Post_Base", new Vector3(x, 0.004f, z), new Vector3(0.07f, 0.004f, 0.07f), Hex(0x52604E));
            Cylinder(root, "Post", new Vector3(x, 0.085f, z), new Vector3(0.026f, 0.085f, 0.026f), Hex(0xDFA464));
            Cylinder(root, "Post_Top", new Vector3(x, 0.172f, z), new Vector3(0.036f, 0.008f, 0.036f), Hex(0xDFA464));
        }

        /// <summary>A dark green plate on the east wall's inside face with <paramref name="text"/> on it, drawn by a
        /// small world-space canvas in the UI font.</summary>
        void Sign(Transform root, Vector3 wallPoint, string text)
        {
            const float plateWidth = 0.44f;
            const float plateHeight = 0.13f;
            Box(root, "Sign_Plate", wallPoint + new Vector3(-0.007f, 0f, 0f), new Vector3(0.014f, plateHeight, plateWidth), Hex(0x0F5E2E), false, false);

            var canvasObject = new GameObject("Sign_Text", typeof(RectTransform));
            canvasObject.layer = root.gameObject.layer;
            var rect = (RectTransform)canvasObject.transform;
            rect.SetParent(root, false);
            rect.localPosition = wallPoint + new Vector3(-0.016f, 0f, 0f);
            // A world-space canvas faces down its own -Z: turned to look toward +X, it reads from inside the room.
            rect.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            rect.sizeDelta = new Vector2(plateWidth * 1000f, plateHeight * 1000f);
            rect.localScale = Vector3.one * 0.001f;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var label = new GameObject("Label", typeof(RectTransform));
            label.layer = canvasObject.layer;
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var textComponent = label.AddComponent<Text>();
            textComponent.font = UiFonts.Get(UiFontWeight.Black);
            textComponent.fontSize = 88;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = UiTheme.TextOnInverse;
            textComponent.raycastTarget = false;
            textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.text = text;
        }

        // ================================================================ pieces

        /// <summary>
        /// Places the kit model <paramref name="id"/> with its footprint centred on (<paramref name="x"/>,
        /// <paramref name="z"/>) in room space, standing on <paramref name="baseY"/>, turned by <paramref name="yaw"/>
        /// (see the Face constants). A <paramref name="solid"/> piece gets a BoxCollider over its footprint. Returns the
        /// room-space height of its top, so another piece can stand on it.
        /// </summary>
        float Place(Transform root, string id, float x, float z, float yaw, float baseY, bool solid)
        {
            Vector3 native = NativeSizes.TryGetValue(id, out Vector3 size) ? size : new Vector3(0.2f, 0.2f, 0.2f);
            Vector3 target = native * KitScale;

            var holder = new GameObject(id);
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = new Vector3(x, baseY, z);
            holder.transform.localRotation = Quaternion.identity;

            Vector3 footprint = target;
            GameObject prefab = LoadPiece(id);
            if (prefab != null)
            {
                footprint = FitModel(holder.transform, prefab, Mathf.Max(native.x, Mathf.Max(native.y, native.z)));
            }
            else
            {
                // A stand-in of the right size, so the room still works and reads (a missing bake or model).
                Box(holder.transform, id + "_Box", new Vector3(0f, target.y * 0.5f, 0f), target, Wood, false, true);
            }

            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (solid)
            {
                float height = Mathf.Clamp(footprint.y, MinColliderHeight, MaxColliderHeight);
                var collider = holder.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, height * 0.5f, 0f);
                collider.size = new Vector3(Mathf.Max(0.02f, footprint.x), height, Mathf.Max(0.02f, footprint.z));
            }

            return baseY + footprint.y;
        }

        /// <summary>
        /// Instantiates <paramref name="prefab"/> under <paramref name="holder"/> (still unrotated), scales it so its
        /// largest side is <paramref name="nativeLargest"/> x <see cref="KitScale"/>, and moves it so its footprint is
        /// centred on the holder and its base is on it. Returns its scaled size (holder space, before the yaw).
        /// </summary>
        static Vector3 FitModel(Transform holder, GameObject prefab, float nativeLargest)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, holder, false);
            instance.name = prefab.name;

            // Models imported with an Animator get one per copy; furniture never animates.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                UnityEngine.Object.Destroy(animator);
            }

            foreach (Collider modelCollider in instance.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.Destroy(modelCollider);
            }

            if (!TryRendererBounds(instance, out Bounds bounds))
            {
                return Vector3.one * 0.1f;
            }

            float measured = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = measured > 1e-5f ? nativeLargest * KitScale / measured : 1f;

            Vector3 pivot = holder.position;
            instance.transform.localScale = instance.transform.localScale * scale;
            instance.transform.localPosition = new Vector3(
                -(bounds.center.x - pivot.x) * scale,
                -(bounds.min.y - pivot.y) * scale,
                -(bounds.center.z - pivot.z) * scale);
            return bounds.size * scale;
        }

        static bool TryRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        GameObject LoadPiece(string id)
        {
            GameObject prefab = catalog != null ? catalog.Find(id) : null;
#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(InteriorPieceCatalog.FurnitureFolder + id + ".fbx");
            }
#endif
            return prefab;
        }

        // ================================================================ primitives

        GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Color colour, bool collider, bool castShadows)
        {
            GameObject box = MeshObject(parent, name, cubeMesh, centre, size, colour, castShadows);
            if (collider)
            {
                box.AddComponent<BoxCollider>(); // unit box, scaled with the object
            }

            return box;
        }

        void Cylinder(Transform parent, string name, Vector3 centre, Vector3 scale, Color colour)
        {
            MeshObject(parent, name, cylinderMesh, centre, scale, colour, true);
        }

        GameObject MeshObject(Transform parent, string name, Mesh mesh, Vector3 centre, Vector3 scale, Color colour, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(colour);
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static void Blocker(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = size;
        }

        Material MaterialFor(Color colour)
        {
            if (materials.TryGetValue(colour, out Material existing) && existing != null)
            {
                return existing;
            }

            Material material;
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
            }

            material.name = "Interior_" + ColorUtility.ToHtmlStringRGB(colour);
            material.mainTexture = null;
            material.color = colour;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            materials[colour] = material;
            return material;
        }

        static Mesh BuiltinMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            temp.SetActive(false);
            UnityEngine.Object.Destroy(temp);
            return mesh;
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
