using MaliGo.Characters;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    /// <summary>
    /// Connects PlayerData to the human PlayerCharacter visual representation.
    /// </summary>
    public class PlayerIdentityBridge : MonoBehaviour
    {
        [SerializeField] PlayerCharacterVisualController visualController;
        [SerializeField] PlayerCharacterCatalog catalog;

        IAppearanceVisualProvider appearanceProvider;
        Material runtimeMaterial;
        string appliedAppearanceKey;
        PlayerCharacterVisualController appliedVisual;

        void Awake()
        {
            if (visualController == null)
            {
                visualController = GetComponentInChildren<PlayerCharacterVisualController>();
            }

            if (catalog == null)
            {
                catalog = Resources.Load<PlayerCharacterCatalog>("PlayerCharacterCatalog");
            }

            appearanceProvider = catalog != null
                ? new KenneyAppearanceVisualProvider(catalog)
                : null;
        }

        void OnEnable()
        {
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.OnPlayerDataChanged += HandlePlayerDataChanged;
                ApplyCurrentPlayerData(PlayerDataManager.Instance.CurrentPlayer);
            }
        }

        void OnDisable()
        {
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.OnPlayerDataChanged -= HandlePlayerDataChanged;
            }
        }

        void Start()
        {
            if (PlayerDataManager.Instance != null)
            {
                ApplyCurrentPlayerData(PlayerDataManager.Instance.CurrentPlayer);
            }
        }

        public void Configure(PlayerCharacterVisualController visual, PlayerCharacterCatalog characterCatalog)
        {
            visualController = visual;
            catalog = characterCatalog;
            appliedAppearanceKey = null;
            appearanceProvider = catalog != null
                ? new KenneyAppearanceVisualProvider(catalog)
                : null;
        }

        void HandlePlayerDataChanged(PlayerData data)
        {
            ApplyCurrentPlayerData(data);
        }

        public void ApplyCurrentPlayerData(PlayerData data)
        {
            if (data == null || visualController == null)
            {
                return;
            }

            // OnPlayerDataChanged fires on every money change; the look is rebuilt only when it changed, and the
            // material made for the previous look is destroyed so repeated changes don't leak materials.
            string key = AppearanceKey(data.appearance);
            if (key != appliedAppearanceKey || visualController != appliedVisual)
            {
                ApplyAppearance(data.appearance);
                appliedAppearanceKey = key;
                appliedVisual = visualController;
            }

            if (!string.IsNullOrWhiteSpace(data.characterName))
            {
                gameObject.name = $"Player_{SanitizeName(data.characterName)}";
            }
        }

        void ApplyAppearance(AppearanceData appearance)
        {
            if (appearance == null)
            {
                return;
            }

            Material material = catalog != null ? catalog.CreateRuntimeSkinMaterial(appearance) : null;
            if (material != null)
            {
                visualController.SetSkinMaterial(material);
                ReleaseRuntimeMaterial();
                runtimeMaterial = material;
                return;
            }

            appearanceProvider?.ApplyAppearance(appearance, visualController);
        }

        void ReleaseRuntimeMaterial()
        {
            // Also drops this look's reference to its shared skin texture (destroyed when nothing else shows it).
            PlayerCharacterCatalog.ReleaseRuntimeSkinMaterial(runtimeMaterial);
            runtimeMaterial = null;
        }

        void OnDestroy()
        {
            ReleaseRuntimeMaterial();
        }

        /// <summary>The resolved outfit and tone plus the other appearance fields (<see cref="CharacterLooks.Key"/>).</summary>
        static string AppearanceKey(AppearanceData a) => CharacterLooks.Key(a);

        static string SanitizeName(string name)
        {
            return name.Trim().Replace('/', '_').Replace('\\', '_');
        }
    }
}
