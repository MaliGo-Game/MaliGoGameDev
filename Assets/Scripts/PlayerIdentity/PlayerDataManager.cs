using System;
using System.IO;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    public class PlayerDataManager : MonoBehaviour
    {
        public const string SaveFileName = "player_data.json";

        public static PlayerDataManager Instance { get; private set; }

        [SerializeField] private PlayerData currentPlayer = PlayerData.CreateNew();

        public event Action<PlayerData> OnPlayerDataChanged;

        public PlayerData CurrentPlayer => currentPlayer;

        public bool HasSavedData => File.Exists(GetSavePath());

        public bool IsCharacterCreated => currentPlayer != null && currentPlayer.isCharacterCreated;

        public static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryLoad();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetPlayerData(PlayerData data, bool saveImmediately = true)
        {
            currentPlayer = data ?? PlayerData.CreateNew();
            NotifyChanged();

            if (saveImmediately)
            {
                Save();
            }
        }

        public void UpdatePlayerData(Action<PlayerData> mutator, bool saveImmediately = false)
        {
            if (mutator == null || currentPlayer == null)
            {
                return;
            }

            mutator(currentPlayer);
            NotifyChanged();

            if (saveImmediately)
            {
                Save();
            }
        }

        public bool TryLoad()
        {
            string path = GetSavePath();
            if (!File.Exists(path))
            {
                currentPlayer = PlayerData.CreateNew();
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonUtility.FromJson<PlayerData>(json);
                if (loaded == null)
                {
                    currentPlayer = PlayerData.CreateNew();
                    return false;
                }

                loaded.appearance ??= new AppearanceData();
                loaded.financialProfile ??= new FinancialProfile();
                loaded.financialStats ??= new FinancialStats();
                loaded.progression ??= new ProgressionData();
                loaded.goals ??= Array.Empty<FinancialGoal>();
                loaded.completedScenarioIds ??= Array.Empty<string>();
                loaded.obligations ??= Array.Empty<Obligation>();
                if (loaded.currentDay < 1)
                {
                    loaded.currentDay = 1;
                }

                // Saves made before bills existed get the default ones, once.
                ObligationDefaults.AddBaseObligations(loaded);

                currentPlayer = loaded;
                NotifyChanged();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerDataManager] Failed to load save: {ex.Message}");
                currentPlayer = PlayerData.CreateNew();
                return false;
            }
        }

        public void Save()
        {
            if (currentPlayer == null)
            {
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(currentPlayer, true);
                File.WriteAllText(GetSavePath(), json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerDataManager] Failed to save player data: {ex.Message}");
            }
        }

        public void DeleteSave()
        {
            string path = GetSavePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            currentPlayer = PlayerData.CreateNew();
            NotifyChanged();
        }

        void NotifyChanged()
        {
            OnPlayerDataChanged?.Invoke(currentPlayer);
        }
    }
}
