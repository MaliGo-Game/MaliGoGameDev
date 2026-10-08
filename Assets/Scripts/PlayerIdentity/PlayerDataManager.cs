using System;
using System.IO;
using MaliGo.Core;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.PlayerIdentity
{
    public class PlayerDataManager : MonoBehaviour
    {
        public const string SaveFileName = "player_data.json";
        public const string BackupSuffix = ".bak";
        public const string TempSuffix = ".tmp";
        public const string CorruptSuffix = ".corrupt";

        public static PlayerDataManager Instance { get; private set; }

        /// <summary>
        /// True after TryLoad discarded a save from an older version, or found a save it could not read at all
        /// (main file and .bak both unreadable). Character creation shows the "starts fresh" notice once and
        /// clears it, so a lost save is never a silent fresh start.
        /// </summary>
        public static bool WasResetForUpdate { get; set; }

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

        void OnApplicationPause(bool paused)
        {
            if (paused && Instance == this)
            {
                Save();
            }
        }

        void OnApplicationQuit()
        {
            if (Instance == this)
            {
                Save();
            }
        }

        public void SetPlayerData(PlayerData data, bool saveImmediately = true)
        {
            try
            {
                currentPlayer = data ?? PlayerData.CreateNew();
                NotifyChanged();

                if (saveImmediately)
                {
                    Save();
                }
            }
            finally
            {
                GameEvents.FlushMoneyChanged();
            }
        }

        /// <summary>
        /// Runs the mutator, raises OnPlayerDataChanged, saves (if asked) and only then raises the queued
        /// GameEvents.MoneyChanged events, so their subscribers always see the finished state. The queue is
        /// emptied even if the mutator throws.
        /// </summary>
        public void UpdatePlayerData(Action<PlayerData> mutator, bool saveImmediately = false)
        {
            if (mutator == null || currentPlayer == null)
            {
                return;
            }

            try
            {
                mutator(currentPlayer);
                NotifyChanged();

                if (saveImmediately)
                {
                    Save();
                }
            }
            finally
            {
                GameEvents.FlushMoneyChanged();
            }
        }

        /// <summary>
        /// Loads the save (falling back to the .bak copy if the main file is missing or does not parse). After a
        /// recovery from .bak the bad main file is moved aside to .corrupt and the recovered data is saved at
        /// once, so the next Save's File.Replace can never push the bad file over the only good copy. If neither
        /// file can be read, WasResetForUpdate is set (the player sees the notice). A save from an older version
        /// is deleted with its backup and replaced by a new one, and WasResetForUpdate is set: nothing is kept.
        /// Returns true only when a current save was loaded.
        /// </summary>
        public bool TryLoad()
        {
            string path = GetSavePath();
            string backup = path + BackupSuffix;
            if (!File.Exists(path) && !File.Exists(backup))
            {
                currentPlayer = PlayerData.CreateNew();
                return false;
            }

            PlayerData loaded = ReadSave(path);
            bool fromBackup = false;
            if (loaded == null)
            {
                loaded = ReadSave(backup);
                fromBackup = loaded != null;
            }

            if (loaded == null)
            {
                Debug.LogWarning("[PlayerDataManager] Neither the save nor its backup could be read; starting fresh.");
                MoveAside(path);
                MoveAside(backup);
                currentPlayer = PlayerData.CreateNew();
                WasResetForUpdate = true;
                return false;
            }

            if (PlayerData.ShouldReset(loaded.saveVersion))
            {
                TryDelete(path);
                TryDelete(path + BackupSuffix);
                currentPlayer = PlayerData.CreateNew();
                WasResetForUpdate = true;
                return false;
            }

            Repair(loaded);
            currentPlayer = loaded;
            if (fromBackup)
            {
                Debug.LogWarning("[PlayerDataManager] The save could not be read; recovered it from the backup.");
                MoveAside(path);
                Save(); // no main file now: the temp file is moved into place and .bak is left untouched
            }

            NotifyChanged();
            return true;
        }

        /// <summary>Renames an unreadable save file to &lt;name&gt;.corrupt (replacing an older one), so it is kept
        /// for inspection but never rotated into .bak.</summary>
        static void MoveAside(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                string target = path + CorruptSuffix;
                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                File.Move(path, target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerDataManager] Could not move {Path.GetFileName(path)} aside: {ex.Message}");
                TryDelete(path);
            }
        }

        static PlayerData ReadSave(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<PlayerData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerDataManager] Failed to read {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Never leaves a field null, and normalises hand-edited profile values.</summary>
        static void Repair(PlayerData loaded)
        {
            loaded.appearance ??= new AppearanceData();
            loaded.financialProfile ??= new FinancialProfile();
            loaded.financialStats ??= new FinancialStats();
            loaded.progression ??= new ProgressionData();
            loaded.goals ??= Array.Empty<FinancialGoal>();
            loaded.completedScenarioIds ??= Array.Empty<string>();
            loaded.obligations ??= Array.Empty<Obligation>();
            loaded.today ??= new DayRecord();
            loaded.today.events ??= Array.Empty<MoneyEvent>();
            loaded.chapter ??= new ChapterRecord();
            loaded.chapter.days ??= Array.Empty<DayRecord>();
            loaded.chapter.choices ??= Array.Empty<ChoiceRecord>();
            loaded.followUps ??= Array.Empty<string>();
            loaded.paydayPlanId ??= "";
            loaded.paydayPlanText ??= "";
            loaded.spendingProfile ??= new SpendingProfile();
            loaded.spendingProfile.focus = SpendingFocus.Normalize(loaded.spendingProfile.focus);
            loaded.spendingProfile.travel = TravelMode.Normalize(loaded.spendingProfile.travel);
            loaded.bankHabits ??= new BankHabitSummary();
            loaded.bankHabits.categories ??= Array.Empty<CategoryHabit>();
            if (loaded.currentDay < 1)
            {
                loaded.currentDay = 1;
            }

            // Saves made before bills existed get the default ones, once.
            ObligationDefaults.AddBaseObligations(loaded);
        }

        /// <summary>
        /// Atomic write: serialize to player_data.json.tmp, then replace the main file (keeping the old one
        /// as .bak), or move the temp file into place when there is no main file yet.
        /// </summary>
        public void Save()
        {
            if (currentPlayer == null)
            {
                return;
            }

            string path = GetSavePath();
            string temp = path + TempSuffix;
            try
            {
                string json = JsonUtility.ToJson(currentPlayer, true);
                File.WriteAllText(temp, json);

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, path + BackupSuffix);
                    }
                    catch (Exception replaceError)
                    {
                        Debug.LogWarning($"[PlayerDataManager] File.Replace failed ({replaceError.Message}); copying instead.");
                        File.Copy(temp, path, true);
                        TryDelete(temp);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerDataManager] Failed to save player data: {ex.Message}");
            }
        }

        /// <summary>Deletes the save, its .bak, .tmp and .corrupt copies, and starts a new player in memory. PlayerPrefs are not touched.</summary>
        public void DeleteSave()
        {
            string path = GetSavePath();
            TryDelete(path);
            TryDelete(path + BackupSuffix);
            TryDelete(path + TempSuffix);
            TryDelete(path + CorruptSuffix);
            TryDelete(path + BackupSuffix + CorruptSuffix);

            currentPlayer = PlayerData.CreateNew();
            GameEvents.ClearPendingMoneyChanged();
            NotifyChanged();
        }

        /// <summary>
        /// "Forget my bank data": empties the bank habit summary and saves twice, so the .bak copy (the previous
        /// save, kept by File.Replace) no longer holds it either. Any .corrupt copies are deleted for the same reason.
        /// The spending profile and the week are kept.
        /// </summary>
        public void ForgetBankHabits()
        {
            if (currentPlayer == null)
            {
                return;
            }

            BankHabits.Forget(currentPlayer);
            NotifyChanged();
            Save();
            Save();
            string path = GetSavePath();
            TryDelete(path + CorruptSuffix);
            TryDelete(path + BackupSuffix + CorruptSuffix);
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerDataManager] Could not delete {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        void NotifyChanged()
        {
            OnPlayerDataChanged?.Invoke(currentPlayer);
        }
    }
}
