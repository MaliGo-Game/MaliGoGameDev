using MaliGo.Characters;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    /// <summary>
    /// Separates Mali from the human player (keeping her only as a hidden dialogue host: she is docked in the HUD,
    /// not in the world) and spawns the PlayerCharacter from PlayerData.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class PlayerCharacterSpawner : MonoBehaviour
    {
        const string MaliLegacyObjectName = "Player_Sam";
        const string MaliObjectName = "Mali";
        const string SpawnPointName = "PlayerSpawnPoint";

        /// <summary>
        /// characterMedium.fbx imports at ~3.96 world-unit height, but MaliGoWorld's Kenney
        /// City Kit environment uses a compact scale (measured: Player_House ~0.83 units,
        /// a garden fence ~0.27 units - both consistent with ~0.27 units per real metre).
        /// At that ratio a ~1.75m person should measure ~0.47 units, giving this factor
        /// (0.47 / 3.96). See Assets/Editor/MaliGoScaleAudit.cs for the measurement tool.
        /// </summary>
        const float CharacterModelScale = 0.12f;
        const float ControllerHeight = 0.47f;
        const float ControllerRadius = 0.09f;

        [SerializeField] PlayerCharacterCatalog catalog;
        [SerializeField] GameObject playerCharacterPrefab;

        public PlayerCharacterCatalog Catalog => catalog;

        void Awake()
        {
            if (catalog == null)
            {
                catalog = KenneyRuntimeCatalogFactory.LoadCatalog();
            }

            if (playerCharacterPrefab == null)
            {
                playerCharacterPrefab = Resources.Load<GameObject>("PlayerCharacter");
            }
        }

        void Start()
        {
            InitializeWorldCharacters();
        }

        public void InitializeWorldCharacters()
        {
            SeparateMaliFromPlayer();
            GameObject player = EnsurePlayerCharacter();
            RetargetCamera(player != null ? player.transform : null);
        }

        /// <summary>
        /// Turns the scene's "Mali" object (or the legacy "Player_Sam") into Mali's hidden host: it keeps the name
        /// "Mali" and her dialogue components, so everything that looks her up (<c>GameObject.Find("Mali")</c>,
        /// <c>FindFirstObjectByType&lt;MaliDialogueController&gt;()</c>) still works, but she no longer stands or
        /// walks in the world. She is docked in the HUD above the Talk button instead (<c>MobileControlsUI</c>).
        /// With no Mali in the scene an empty host is created, so her lines are never lost.
        /// </summary>
        public static GameObject SeparateMaliFromPlayer()
        {
            GameObject maliObject = GameObject.Find(MaliObjectName) ?? GameObject.Find(MaliLegacyObjectName);
            if (maliObject == null)
            {
                maliObject = new GameObject(MaliObjectName);
            }

            if (maliObject.name != MaliObjectName)
            {
                maliObject.name = MaliObjectName;
            }

            maliObject.tag = "Untagged";

            MaliGoPlayerController playerController = maliObject.GetComponent<MaliGoPlayerController>();
            if (playerController != null)
            {
                Destroy(playerController);
            }

            PlayerIdentityBridge identityBridge = maliObject.GetComponent<PlayerIdentityBridge>();
            if (identityBridge != null)
            {
                Destroy(identityBridge);
            }

            EnsureMaliCompanionComponents(maliObject);
            HideMaliInWorld(maliObject);
            return maliObject;
        }

        GameObject EnsurePlayerCharacter()
        {
            GameObject existingPlayer = GameObject.FindWithTag("Player");
            if (existingPlayer != null)
            {
                WireExistingPlayer(existingPlayer);
                return existingPlayer;
            }

            Vector3 spawnPosition = ResolveSpawnPosition();
            GameObject playerRoot = CreatePlayerCharacter(spawnPosition);
            if (playerRoot == null)
            {
                Debug.LogWarning("[PlayerCharacterSpawner] Could not create PlayerCharacter. Run MaliGo/Setup Player Character System in the Editor.");
                return null;
            }

            playerRoot.tag = "Player";
            WireExistingPlayer(playerRoot);
            return playerRoot;
        }

        Vector3 ResolveSpawnPosition()
        {
            GameObject spawnPoint = GameObject.Find(SpawnPointName);
            if (spawnPoint != null)
            {
                return spawnPoint.transform.position;
            }

            GameObject legacyMali = GameObject.Find(MaliObjectName) ?? GameObject.Find(MaliLegacyObjectName);
            // Only a Mali that came with the scene marks a spot; an empty host made by SeparateMaliFromPlayer sits
            // at the origin and has no renderers.
            if (legacyMali != null && legacyMali.GetComponentInChildren<Renderer>(true) != null)
            {
                return legacyMali.transform.position;
            }

            return new Vector3(2f, 0.05f, -1.3f);
        }

        GameObject CreatePlayerCharacter(Vector3 spawnPosition)
        {
            GameObject playerRoot;

            if (playerCharacterPrefab != null)
            {
                playerRoot = Instantiate(playerCharacterPrefab, spawnPosition, Quaternion.identity);
                playerRoot.name = "PlayerCharacter";
            }
            else if (catalog != null && catalog.characterModelPrefab != null)
            {
                playerRoot = BuildPlayerFromCatalog(spawnPosition);
            }
            else
            {
                catalog = KenneyRuntimeCatalogFactory.LoadCatalog();
                if (catalog != null && catalog.characterModelPrefab != null)
                {
                    playerRoot = BuildPlayerFromCatalog(spawnPosition);
                }
                else
                {
                    return null;
                }
            }

            return playerRoot;
        }

        GameObject BuildPlayerFromCatalog(Vector3 spawnPosition)
        {
            GameObject playerRoot = new GameObject("PlayerCharacter");
            playerRoot.transform.position = spawnPosition;

            CharacterController controller = playerRoot.AddComponent<CharacterController>();
            controller.height = ControllerHeight;
            controller.radius = ControllerRadius;
            controller.center = new Vector3(0f, ControllerHeight * 0.5f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = ControllerHeight * 0.14f;
            // Unity's default skin (0.08) is almost the whole 0.09 radius at this scale: the capsule then rests
            // 0.08 u (~30 cm) above the ground and stops that far short of walls. Unity's guidance is ~10% of the
            // radius. No minimum move either, so a light joystick tilt at a high frame rate still moves.
            controller.skinWidth = ControllerRadius * 0.1f;
            controller.minMoveDistance = 0f;

            Rigidbody rigidbody = playerRoot.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            GameObject visualRoot = new GameObject("HumanVisual");
            visualRoot.transform.SetParent(playerRoot.transform, false);

            GameObject modelInstance = Instantiate(catalog.characterModelPrefab, visualRoot.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * CharacterModelScale;

            // On the armature, not the model root: the clips' root curves would otherwise replace the 0.12
            // scale above with 100 (see CharacterRig).
            CharacterRig.AttachAnimator(modelInstance, catalog.animatorController);

            PlayerCharacterVisualController visualController = visualRoot.GetComponent<PlayerCharacterVisualController>();
            if (visualController == null)
            {
                visualController = visualRoot.AddComponent<PlayerCharacterVisualController>();
            }

            MaliGoPlayerController movement = playerRoot.AddComponent<MaliGoPlayerController>();
            movement.visualController = visualController;

            PlayerIdentityBridge bridge = playerRoot.AddComponent<PlayerIdentityBridge>();
            bridge.Configure(visualController, catalog);

            return playerRoot;
        }

        static void WireExistingPlayer(GameObject playerRoot)
        {
            PlayerCharacterVisualController visualController = playerRoot.GetComponentInChildren<PlayerCharacterVisualController>();
            MaliGoPlayerController movement = playerRoot.GetComponent<MaliGoPlayerController>();
            if (movement == null)
            {
                movement = playerRoot.AddComponent<MaliGoPlayerController>();
            }

            movement.visualController = visualController;

            PlayerIdentityBridge bridge = playerRoot.GetComponent<PlayerIdentityBridge>();
            if (bridge == null)
            {
                bridge = playerRoot.AddComponent<PlayerIdentityBridge>();
            }

            PlayerCharacterCatalog activeCatalog = Object.FindFirstObjectByType<PlayerCharacterSpawner>()?.Catalog;
            if (activeCatalog == null)
            {
                activeCatalog = KenneyRuntimeCatalogFactory.LoadCatalog();
            }

            if (visualController != null)
            {
                bridge.Configure(visualController, activeCatalog);
            }
        }

        static void RetargetCamera(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                return;
            }

            MaliGoCameraController cameraController = Object.FindFirstObjectByType<MaliGoCameraController>();
            if (cameraController != null)
            {
                cameraController.target = playerTransform;
            }
        }

        /// <summary>
        /// Removes Mali's world presence while keeping the object (and its components) alive: every renderer is
        /// switched off, colliders and her CharacterController stop blocking the player, and her NPC controller
        /// stops following (it stays attached, disabled, because the dialogue controller still sets its TALK state).
        /// </summary>
        static void HideMaliInWorld(GameObject maliObject)
        {
            foreach (Renderer renderer in maliObject.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            foreach (Collider collider in maliObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false; // CharacterController is a Collider
            }

            MaliNpcController npc = maliObject.GetComponent<MaliNpcController>();
            if (npc != null)
            {
                npc.SetFollowPlayer(false);
                npc.enabled = false;
            }
        }

        static void EnsureMaliCompanionComponents(GameObject maliObject)
        {
            if (maliObject.GetComponent<MaliDialogueController>() == null)
            {
                maliObject.AddComponent<MaliDialogueController>();
            }

            if (maliObject.GetComponent<MaliCompanionInteraction>() == null)
            {
                maliObject.AddComponent<MaliCompanionInteraction>();
            }
        }
    }
}
