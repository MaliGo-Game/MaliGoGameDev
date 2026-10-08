using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;
using MaliGo.Characters;

public class MaliGoWorldGenerator
{
    // MaliGo Brand Color Palette
    public static readonly Color MossGreen   = new Color(0.031f, 0.478f, 0.094f); // #087A18
    public static readonly Color DeepForest  = new Color(0.059f, 0.369f, 0.180f); // #0F5E2E
    public static readonly Color Sage        = new Color(0.788f, 0.937f, 0.706f); // #C9EFB4
    public static readonly Color Cream       = new Color(0.976f, 1.000f, 0.965f); // #F9FFF6
    public static readonly Color WarmSand    = new Color(1.000f, 0.957f, 0.914f); // #FFF4E9
    public static readonly Color GoldenAmber = new Color(0.875f, 0.643f, 0.392f); // #DFA464
    public static readonly Color EmberCoral  = new Color(0.910f, 0.373f, 0.282f); // #E85F48

    private const string RoadPath = "Assets/kenney_city-kit-roads/Models/FBX format/";
    private const string SuburbanPath = "Assets/kenney_city-kit-suburban_20/Models/FBX format/";
    private const string CommercialPath = "Assets/kenney_city-kit-commercial_2.1/Models/FBX format/";
    private const string CarPath = "Assets/kenney_car-kit/Models/FBX format/";

    [MenuItem("MaliGo/Generate MaliGo World Scene")]
    public static void GenerateScene()
    {
        // 1. Create a fresh scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MaliGoWorld";

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        // --- ROOT CONTAINERS ---
        GameObject envRoot = new GameObject("--- ENVIRONMENT ---");
        GameObject roadsRoot = new GameObject("Roads_Network");
        roadsRoot.transform.SetParent(envRoot.transform);
        GameObject buildingsRoot = new GameObject("Residential_Area");
        buildingsRoot.transform.SetParent(envRoot.transform);
        GameObject propsRoot = new GameObject("Street_Furniture_Props");
        propsRoot.transform.SetParent(envRoot.transform);
        GameObject vegetationRoot = new GameObject("Vegetation");
        vegetationRoot.transform.SetParent(envRoot.transform);

        // --- 2. GROUND PLANE (Sage Grass) ---
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground_Lawn";
        ground.transform.SetParent(envRoot.transform);
        ground.transform.position = new Vector3(0, -0.01f, 0);
        ground.transform.localScale = new Vector3(3.5f, 1f, 3.5f); // 35m x 35m
        Material lawnMat = new Material(litShader)
        {
            name = "MaliGo_Lawn_Mat",
            color = Sage
        };
        ground.GetComponent<Renderer>().material = lawnMat;

        // --- 3. ROADS NETWORK ---
        // Main Road running along X (-5 to +5 at Z = 0)
        for (int x = -5; x <= 5; x++)
        {
            if (x == 0)
            {
                // T-Intersection branching North (Z > 0)
                SpawnModel(RoadPath + "road-intersection.fbx", new Vector3(x, 0, 0), Quaternion.Euler(0, 0, 0), roadsRoot.transform, "Road_T_Intersection");
            }
            else if (x == -2)
            {
                // Pedestrian Crossing
                SpawnModel(RoadPath + "road-crossing.fbx", new Vector3(x, 0, 0), Quaternion.Euler(0, 0, 0), roadsRoot.transform, "Road_Crossing");
            }
            else if (x == 2)
            {
                // Driveway connection to Player's Home
                SpawnModel(RoadPath + "road-driveway-single.fbx", new Vector3(x, 0, 0), Quaternion.Euler(0, 0, 0), roadsRoot.transform, "Road_Player_Driveway");
            }
            else if (x == -4)
            {
                // Driveway connection to Neighbor A
                SpawnModel(RoadPath + "road-driveway-single.fbx", new Vector3(x, 0, 0), Quaternion.Euler(0, 0, 0), roadsRoot.transform, "Road_Neighbor_Driveway");
            }
            else
            {
                SpawnModel(RoadPath + "road-straight.fbx", new Vector3(x, 0, 0), Quaternion.Euler(0, 0, 0), roadsRoot.transform, $"Road_Main_{x}");
            }
        }

        // Connecting Road running North along Z (1 to 4 at X = 0)
        for (int z = 1; z <= 4; z++)
        {
            if (z == 4)
            {
                // Cul-de-sac / Road End
                SpawnModel(RoadPath + "road-end.fbx", new Vector3(0, 0, z), Quaternion.Euler(0, 270, 0), roadsRoot.transform, "Road_Connecting_End");
            }
            else
            {
                SpawnModel(RoadPath + "road-straight.fbx", new Vector3(0, 0, z), Quaternion.Euler(0, 90, 0), roadsRoot.transform, $"Road_Connecting_{z}");
            }
        }

        // --- 4. PLAYER'S HOME & FRONT OPEN AREA ---
        GameObject playerHomeGroup = new GameObject("Player_Home_Estate");
        playerHomeGroup.transform.SetParent(buildingsRoot.transform);
        playerHomeGroup.transform.position = Vector3.zero;

        // Player's cozy house (building-type-a or b)
        GameObject playerHouse = SpawnModel(SuburbanPath + "building-type-a.fbx", new Vector3(2.0f, 0, -2.2f), Quaternion.Euler(0, 0, 0), playerHomeGroup.transform, "Player_House");
        
        // Open front area / courtyard with garden path stones, fence, planter, and trees
        SpawnModel(SuburbanPath + "path-stones-long.fbx", new Vector3(2.0f, 0.01f, -1.2f), Quaternion.Euler(0, 0, 0), playerHomeGroup.transform, "Path_FrontDoor");
        SpawnModel(SuburbanPath + "planter.fbx", new Vector3(1.1f, 0, -1.6f), Quaternion.Euler(0, 0, 0), playerHomeGroup.transform, "Planter_Left");
        SpawnModel(SuburbanPath + "planter.fbx", new Vector3(2.9f, 0, -1.6f), Quaternion.Euler(0, 0, 0), playerHomeGroup.transform, "Planter_Right");

        // Fences around player's front yard
        SpawnModel(SuburbanPath + "fence-1x2.fbx", new Vector3(0.9f, 0, -1.0f), Quaternion.Euler(0, 0, 0), playerHomeGroup.transform, "Fence_Front_Left");
        SpawnModel(SuburbanPath + "fence-1x2.fbx", new Vector3(3.1f, 0, -1.0f), Quaternion.Euler(0, 0, 0), playerHomeGroup.transform, "Fence_Front_Right");
        SpawnModel(SuburbanPath + "fence-1x3.fbx", new Vector3(3.6f, 0, -2.0f), Quaternion.Euler(0, 90, 0), playerHomeGroup.transform, "Fence_Side_Right");

        // Front yard trees
        SpawnModel(SuburbanPath + "tree-small.fbx", new Vector3(0.9f, 0, -1.5f), Quaternion.identity, playerHomeGroup.transform, "Player_Yard_Tree_1");
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(3.3f, 0, -2.7f), Quaternion.identity, playerHomeGroup.transform, "Player_Yard_Tree_2");

        // --- 5. RESIDENTIAL NEIGHBOURHOOD (Neighboring Houses) ---
        // Neighbor 1: West of Player (X = -4, Z = -2.2)
        GameObject n1Group = new GameObject("Neighbor_SouthWest");
        n1Group.transform.SetParent(buildingsRoot.transform);
        SpawnModel(SuburbanPath + "building-type-b.fbx", new Vector3(-4.0f, 0, -2.2f), Quaternion.Euler(0, 0, 0), n1Group.transform, "House_Neighbor_1");
        SpawnModel(SuburbanPath + "path-stones-short.fbx", new Vector3(-4.0f, 0.01f, -1.2f), Quaternion.Euler(0, 0, 0), n1Group.transform, "Path_N1");
        SpawnModel(SuburbanPath + "fence-1x2.fbx", new Vector3(-5.0f, 0, -1.0f), Quaternion.identity, n1Group.transform, "Fence_N1");
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(-5.2f, 0, -2.2f), Quaternion.identity, n1Group.transform, "Tree_N1");

        // Neighbor 2: North-West (X = -2.8, Z = 2.4)
        GameObject n2Group = new GameObject("Neighbor_NorthWest");
        n2Group.transform.SetParent(buildingsRoot.transform);
        SpawnModel(SuburbanPath + "building-type-c.fbx", new Vector3(-2.8f, 0, 2.4f), Quaternion.Euler(0, 180, 0), n2Group.transform, "House_Neighbor_2");
        SpawnModel(SuburbanPath + "path-stones-short.fbx", new Vector3(-2.8f, 0.01f, 1.4f), Quaternion.Euler(0, 180, 0), n2Group.transform, "Path_N2");
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(-4.2f, 0, 2.6f), Quaternion.identity, n2Group.transform, "Tree_N2_A");
        SpawnModel(SuburbanPath + "tree-small.fbx", new Vector3(-1.6f, 0, 2.2f), Quaternion.identity, n2Group.transform, "Tree_N2_B");

        // Neighbor 3: North-East (X = 2.8, Z = 2.4)
        GameObject n3Group = new GameObject("Neighbor_NorthEast");
        n3Group.transform.SetParent(buildingsRoot.transform);
        SpawnModel(SuburbanPath + "building-type-d.fbx", new Vector3(2.8f, 0, 2.4f), Quaternion.Euler(0, 180, 0), n3Group.transform, "House_Neighbor_3");
        SpawnModel(SuburbanPath + "path-stones-short.fbx", new Vector3(2.8f, 0.01f, 1.4f), Quaternion.Euler(0, 180, 0), n3Group.transform, "Path_N3");
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(4.2f, 0, 2.6f), Quaternion.identity, n3Group.transform, "Tree_N3");
        SpawnModel(SuburbanPath + "fence-1x2.fbx", new Vector3(1.8f, 0, 1.0f), Quaternion.Euler(0, 180, 0), n3Group.transform, "Fence_N3");

        // Cozy Local Financial / Community Hub (Commercial Corner Cafe/Bank) at North End (X = -1.8, Z = 4.2)
        GameObject hubGroup = new GameObject("Local_Commercial_Hub");
        hubGroup.transform.SetParent(buildingsRoot.transform);
        SpawnModel(CommercialPath + "building-a.fbx", new Vector3(-2.0f, 0, 4.4f), Quaternion.Euler(0, 90, 0), hubGroup.transform, "Local_Bank_Building");
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(-2.0f, 0, 5.8f), Quaternion.identity, hubGroup.transform, "Tree_Commercial_Back");

        // Additional neighborhood trees for lushness
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(2.0f, 0, 4.4f), Quaternion.identity, vegetationRoot.transform, "Tree_NorthEast_Grove");
        SpawnModel(SuburbanPath + "tree-small.fbx", new Vector3(0.8f, 0, 2.8f), Quaternion.identity, vegetationRoot.transform, "Tree_Connecting_East");
        SpawnModel(SuburbanPath + "tree-small.fbx", new Vector3(-0.8f, 0, 2.8f), Quaternion.identity, vegetationRoot.transform, "Tree_Connecting_West");
        SpawnModel(SuburbanPath + "tree-large.fbx", new Vector3(-0.8f, 0, -2.5f), Quaternion.identity, vegetationRoot.transform, "Tree_South_Grove");

        // --- 6. STREET FURNITURE & LIGHTING ---
        // Street lamps with warm point lights
        CreateStreetLamp(new Vector3(-0.6f, 0, 0.6f), Quaternion.Euler(0, 45, 0), propsRoot.transform, "Lamp_Intersection_NW");
        CreateStreetLamp(new Vector3(0.6f, 0, 0.6f), Quaternion.Euler(0, 315, 0), propsRoot.transform, "Lamp_Intersection_NE");
        CreateStreetLamp(new Vector3(1.2f, 0, -0.6f), Quaternion.Euler(0, 180, 0), propsRoot.transform, "Lamp_PlayerHome_Front");
        CreateStreetLamp(new Vector3(-2.8f, 0, -0.6f), Quaternion.Euler(0, 180, 0), propsRoot.transform, "Lamp_MainRoad_West");
        CreateStreetLamp(new Vector3(-0.6f, 0, 3.5f), Quaternion.Euler(0, 90, 0), propsRoot.transform, "Lamp_Connecting_North");

        // Road Signs
        SpawnModel(RoadPath + "road-sign-stop.fbx", new Vector3(0.6f, 0, 0.7f), Quaternion.Euler(0, 180, 0), propsRoot.transform, "Sign_Stop_Intersection");
        SpawnModel(RoadPath + "road-sign-street.fbx", new Vector3(-0.6f, 0, -0.6f), Quaternion.Euler(0, 0, 0), propsRoot.transform, "Sign_StreetName");
        SpawnModel(RoadPath + "road-sign-warning.fbx", new Vector3(-1.2f, 0, 0.6f), Quaternion.Euler(0, 0, 0), propsRoot.transform, "Sign_Warning_Crossing");

        // --- 7. PLAYER SPAWN POINT & MALI NPC ---
        Vector3 spawnPos = new Vector3(2.0f, 0.05f, -1.3f);

        GameObject spawnPointObj = new GameObject("PlayerSpawnPoint");
        spawnPointObj.transform.position = spawnPos;
        spawnPointObj.transform.rotation = Quaternion.Euler(0, 0, 0);

        // Mali the meerkat guide (formerly Player_Sam placeholder)
        GameObject maliObj = new GameObject("Mali");
        maliObj.transform.position = spawnPos + new Vector3(1.2f, 0f, -0.8f);

        CharacterController maliController = maliObj.AddComponent<CharacterController>();
        maliController.height = 1.2f;
        maliController.radius = 0.3f;
        maliController.center = new Vector3(0, 0.6f, 0);

        GameObject spriteVisual = new GameObject("SpriteVisual");
        spriteVisual.transform.SetParent(maliObj.transform);
        spriteVisual.transform.localPosition = new Vector3(0, 0.7f, 0);
        spriteVisual.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

        SpriteRenderer sr = spriteVisual.AddComponent<SpriteRenderer>();
        Sprite samSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sam_Idle.png");
        if (samSprite != null)
        {
            sr.sprite = samSprite;
        }

        CharacterSpriteController spriteCtrl = spriteVisual.AddComponent<CharacterSpriteController>();
        if (samSprite != null) spriteCtrl.idleSprite = samSprite;

        maliObj.AddComponent<MaliGo.Characters.MaliNpcController>();
        maliObj.AddComponent<MaliGo.Characters.MaliDialogueController>();
        maliObj.AddComponent<MaliGo.Characters.MaliCompanionInteraction>();

        // Human PlayerCharacter prefab is added via MaliGo/Setup Player Character System

        // --- 8. ISOMETRIC / THREE-QUARTER CAMERA ---
        GameObject camObj = new GameObject("MaliGo_MainCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 4.8f; // Beautiful framing of player, home, and road
        cam.backgroundColor = DeepForest;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;

        // Isometric 3/4 angle framing player (2.0, 0, -1.3)
        Vector3 cameraTarget = new Vector3(1.6f, 0.4f, -0.8f);
        Vector3 camOffset = new Vector3(-7.5f, 7.5f, -7.5f);
        camObj.transform.position = cameraTarget + camOffset;
        camObj.transform.rotation = Quaternion.Euler(30.0f, 45.0f, 0.0f);

        MaliGoCameraController camCtrl = camObj.AddComponent<MaliGoCameraController>();
        camCtrl.minZoom = 3.0f;
        camCtrl.maxZoom = 10.0f;
        GameObject playerForCamera = GameObject.FindWithTag("Player");
        if (playerForCamera != null)
        {
            camCtrl.target = playerForCamera.transform;
        }

        // --- 9. DIRECTIONAL & AMBIENT LIGHTING ---
        GameObject dirLightObj = new GameObject("Sun_DirectionalLight");
        Light dirLight = dirLightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = Cream;
        dirLight.intensity = 1.25f;
        dirLight.shadows = LightShadows.Soft;
        dirLightObj.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.85f, 0.88f, 0.82f);

        // --- 10. CLEAN MALIGO UI HUD ---
        CreateHUD(MossGreen, Sage, Cream, GoldenAmber);

        // --- 11. WORLD BOUNDARIES & COLLIDERS ---
        GameObject boundaries = new GameObject("World_Boundaries");
        boundaries.transform.SetParent(envRoot.transform);

        GameObject northWall = new GameObject("Boundary_North");
        northWall.transform.SetParent(boundaries.transform);
        northWall.transform.position = new Vector3(0, 2.5f, 7.5f);
        BoxCollider bcN = northWall.AddComponent<BoxCollider>();
        bcN.size = new Vector3(25f, 5f, 1f);

        GameObject southWall = new GameObject("Boundary_South");
        southWall.transform.SetParent(boundaries.transform);
        southWall.transform.position = new Vector3(0, 2.5f, -5.5f);
        BoxCollider bcS = southWall.AddComponent<BoxCollider>();
        bcS.size = new Vector3(25f, 5f, 1f);

        GameObject westWall = new GameObject("Boundary_West");
        westWall.transform.SetParent(boundaries.transform);
        westWall.transform.position = new Vector3(-8.5f, 2.5f, 1.0f);
        BoxCollider bcW = westWall.AddComponent<BoxCollider>();
        bcW.size = new Vector3(1f, 5f, 20f);

        GameObject eastWall = new GameObject("Boundary_East");
        eastWall.transform.SetParent(boundaries.transform);
        eastWall.transform.position = new Vector3(8.5f, 2.5f, 1.0f);
        BoxCollider bcE = eastWall.AddComponent<BoxCollider>();
        bcE.size = new Vector3(1f, 5f, 20f);

        // Add colliders to all obstacles (excluding ground, roads, paths)
        var meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (var mf in meshFilters)
        {
            GameObject go = mf.gameObject;
            if (go.name.StartsWith("Road_") || go.name.StartsWith("Path_") || go.name == "Ground_Lawn") continue;

            if (go.GetComponent<Collider>() == null)
            {
                MeshCollider mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }

        // --- 12. SAVE SCENE & UPDATE BUILD SETTINGS ---
        string scenePath = "Assets/Scenes/MaliGoWorld.unity";
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);

        bool inBuild = false;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.path == scenePath) { inBuild = true; break; }
        }
        if (!inBuild)
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        Debug.Log("🎉 MaliGoWorld scene successfully generated and saved to " + scenePath);
    }

    /// <summary>
    /// Measured via MaliGoScaleAudit: Car Kit's sedan.fbx imports at (1.5 x 1.3 x 2.55) -
    /// nearly twice as long as Player_House is wide (1.3). Car Kit does NOT share City Kit's
    /// scale. This factor brings a car to roughly car-height-vs-house-height proportions
    /// (~0.4 units tall, about half the house's 0.834-unit height).
    /// </summary>
    private const float VehicleScale = 0.31f;

    /// <summary>
    /// Adds parked/roadside vehicles to the already-built MaliGoWorld scene. Purely additive -
    /// unlike GenerateScene() this does not clear or rebuild anything, so it's safe to run
    /// against a scene that already has manual tweaks. Re-running is safe: if a "Vehicles"
    /// group already exists, it re-applies VehicleScale to the existing cars instead of
    /// duplicating them - use this to pick up a corrected VehicleScale value.
    /// </summary>
    [MenuItem("MaliGo/World/Add Parked Vehicles")]
    public static void AddParkedVehicles()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.name != "MaliGoWorld")
        {
            Debug.LogWarning("[MaliGoWorldGenerator] Open MaliGoWorld.unity before running Add Parked Vehicles.");
            return;
        }

        GameObject vehiclesRoot = GameObject.Find("Vehicles");
        if (vehiclesRoot != null)
        {
            ApplyVehicleScale(vehiclesRoot);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[MaliGoWorldGenerator] 'Vehicles' already existed - rescaled existing cars to x{VehicleScale} and saved.");
            return;
        }

        GameObject envRoot = GameObject.Find("--- ENVIRONMENT ---");
        if (envRoot == null)
        {
            Debug.LogWarning("[MaliGoWorldGenerator] '--- ENVIRONMENT ---' not found - run 'Generate MaliGo World Scene' first.");
            return;
        }

        vehiclesRoot = new GameObject("Vehicles");
        vehiclesRoot.transform.SetParent(envRoot.transform);

        // Player's own car, in their driveway (Road_Player_Driveway is at x=2, house at z=-2.2)
        SpawnModel(CarPath + "sedan.fbx", new Vector3(2.0f, 0f, -0.6f), Quaternion.Euler(0, 0, 0), vehiclesRoot.transform, "Vehicle_PlayerCar");

        // Neighbor's car, in their driveway (Road_Neighbor_Driveway is at x=-4)
        SpawnModel(CarPath + "suv.fbx", new Vector3(-4.0f, 0f, -0.6f), Quaternion.Euler(0, 0, 0), vehiclesRoot.transform, "Vehicle_NeighborSUV");

        // A couple of cars along the main east-west road (avoiding the intersection/crossing/driveway tiles)
        SpawnModel(CarPath + "hatchback-sports.fbx", new Vector3(-3.0f, 0f, 0.15f), Quaternion.Euler(0, 90, 0), vehiclesRoot.transform, "Vehicle_MainRoad_1");
        SpawnModel(CarPath + "taxi.fbx", new Vector3(4.0f, 0f, 0.15f), Quaternion.Euler(0, 270, 0), vehiclesRoot.transform, "Vehicle_MainRoad_2");

        // Delivery van near the commercial hub, giving it some life - north of the Bank's west-facing door, not across
        // it (at z = 4.4 it covered the whole doorway and the spot the Bank's exit puts the player on).
        SpawnModel(CarPath + "delivery.fbx", new Vector3(-3.3f, 0f, 5.25f), Quaternion.Euler(0, 90, 0), vehiclesRoot.transform, "Vehicle_DeliveryVan");

        ApplyVehicleScale(vehiclesRoot);

        // No colliders here: at runtime MaliGo.World.VehicleColliders gives each car one fitted box on the Vehicle
        // layer. Per-part MeshColliders (body shell + four wheels) trapped and snagged the player's small capsule.

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[MaliGoWorldGenerator] Added 5 parked vehicles (scale x{VehicleScale}) to MaliGoWorld and saved the scene. Positions are a first pass - nudge in the Editor if anything clips.");
    }

    private static void ApplyVehicleScale(GameObject vehiclesRoot)
    {
        foreach (Transform child in vehiclesRoot.transform)
        {
            child.localScale = Vector3.one * VehicleScale;
        }
    }

    private static GameObject SpawnModel(string path, Vector3 pos, Quaternion rot, Transform parent, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"Model not found at: {path}");
            return null;
        }

        GameObject inst = Object.Instantiate(prefab, pos, rot, parent);
        inst.name = name;
        return inst;
    }

    private static void CreateStreetLamp(Vector3 pos, Quaternion rot, Transform parent, string name)
    {
        GameObject lamp = SpawnModel(RoadPath + "light-curved.fbx", pos, rot, parent, name);
        if (lamp != null)
        {
            GameObject lightChild = new GameObject("LampGlow");
            lightChild.transform.SetParent(lamp.transform);
            lightChild.transform.localPosition = new Vector3(0, 0.6f, -0.15f);

            Light pt = lightChild.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = GoldenAmber;
            pt.intensity = 2.0f;
            pt.range = 3.5f;
            pt.shadows = LightShadows.None;
        }
    }

    private static void CreateHUD(Color mossGreen, Color sage, Color cream, Color goldenAmber)
    {
        GameObject canvasObj = new GameObject("MaliGo_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Color statusText = cream;

        // Top Left Status Panel
        GameObject hudPanel = new GameObject("HUD_StatusPanel");
        hudPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRect = hudPanel.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0, 1);
        hudRect.anchorMax = new Vector2(0, 1);
        hudRect.pivot = new Vector2(0, 1);
        hudRect.anchoredPosition = new Vector2(24, -24);
        hudRect.sizeDelta = new Vector2(300, 170);

        // The scene HUD is hidden at runtime and replaced by HudView (DESIGN_SPEC §7.4); a flat panel is enough.
        Image hudBg = hudPanel.AddComponent<Image>();
        hudBg.color = new Color(mossGreen.r, mossGreen.g, mossGreen.b, 0.92f);

        CreateText(hudPanel, "Player: You | Level 1", new Vector2(16, -14), 20, statusText, FontStyle.Bold, defaultFont);

        // XP Bar
        GameObject xpBg = new GameObject("XP_Bar_Bg");
        xpBg.transform.SetParent(hudPanel.transform, false);
        RectTransform xpBgRect = xpBg.AddComponent<RectTransform>();
        xpBgRect.anchorMin = new Vector2(0, 1);
        xpBgRect.anchorMax = new Vector2(0, 1);
        xpBgRect.pivot = new Vector2(0, 1);
        xpBgRect.anchoredPosition = new Vector2(16, -42);
        xpBgRect.sizeDelta = new Vector2(268, 12);
        xpBg.AddComponent<Image>().color = new Color(0, 0, 0, 0.4f);

        GameObject xpFill = new GameObject("XP_Bar_Fill");
        xpFill.transform.SetParent(xpBg.transform, false);
        RectTransform xpFillRect = xpFill.AddComponent<RectTransform>();
        xpFillRect.anchorMin = new Vector2(0, 0);
        xpFillRect.anchorMax = new Vector2(0.35f, 1);
        xpFillRect.offsetMin = Vector2.zero;
        xpFillRect.offsetMax = Vector2.zero;
        xpFill.AddComponent<Image>().color = goldenAmber;

        CreateText(hudPanel, "💰 Cash: R0", new Vector2(16, -64), 18, statusText, FontStyle.Normal, defaultFont);
        CreateText(hudPanel, "🏦 Savings: R0", new Vector2(16, -96), 18, statusText, FontStyle.Normal, defaultFont);
        CreateText(hudPanel, "🌱 Financial Stress: 0%", new Vector2(16, -128), 18, statusText, FontStyle.Normal, defaultFont);

        // Top Right Goal Panel
        GameObject goalBox = new GameObject("Goal_Panel");
        goalBox.transform.SetParent(canvasObj.transform, false);
        RectTransform goalRect = goalBox.AddComponent<RectTransform>();
        goalRect.anchorMin = new Vector2(1, 1);
        goalRect.anchorMax = new Vector2(1, 1);
        goalRect.pivot = new Vector2(1, 1);
        goalRect.anchoredPosition = new Vector2(-24, -24);
        goalRect.sizeDelta = new Vector2(280, 110);

        Image goalBg = goalBox.AddComponent<Image>();
        goalBg.color = new Color(mossGreen.r, mossGreen.g, mossGreen.b, 0.92f);

        CreateText(goalBox, "Today's Financial Goal:", new Vector2(16, -16), 18, statusText, FontStyle.Bold, defaultFont);
        CreateText(goalBox, "Goal: Set your daily goal", new Vector2(16, -50), 22, new Color(0.62f, 0.38f, 0.06f), FontStyle.Bold, defaultFont);
    }

    private static void CreateText(GameObject parent, string content, Vector2 pos, int fontSize, Color color, FontStyle style, Font font)
    {
        GameObject textObj = new GameObject("Text_" + content.Substring(0, Mathf.Min(8, content.Length)));
        textObj.transform.SetParent(parent.transform, false);
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(parent.GetComponent<RectTransform>().sizeDelta.x - 32, 28);

        Text textComp = textObj.AddComponent<Text>();
        textComp.text = content;
        textComp.fontSize = fontSize;
        textComp.fontStyle = style;
        textComp.color = color;
        textComp.font = font;
        textComp.alignment = TextAnchor.MiddleLeft;
    }
}
