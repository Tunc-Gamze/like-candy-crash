using System;
using UnityEngine;

namespace Birdsong
{
    public interface IPreferences
    {
        string Get(string key);
        void Set(string key, string value);
        void Delete(string key);
        void Flush();
    }

    public sealed class PlayerPreferences : IPreferences
    {
        public string Get(string key) => PlayerPrefs.GetString(key, "");
        public void Set(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Delete(string key) => PlayerPrefs.DeleteKey(key);
        public void Flush() => PlayerPrefs.Save();
    }

    [Serializable]
    public class ProgressData
    {
        public int version = 1;
        public int highestUnlocked = 1;
        public bool[] completed = new bool[6];
        public int[] bestScores = new int[6];
        public bool sound = true;
    }

    public sealed class ProgressStore
    {
        public const string Key = "birdsong.progress.v1";
        public const string BackupKey = Key + ".backup";
        public ProgressData Data { get; private set; }
        public string Warning { get; private set; }
        public bool ReadOnly { get; private set; }
        readonly IPreferences preferences;
        string lastValid;

        public ProgressStore(IPreferences preferences)
        {
            this.preferences = preferences;
            string raw = preferences.Get(Key);
            string backup = preferences.Get(BackupKey);
            if (string.IsNullOrEmpty(raw) && string.IsNullOrEmpty(backup)) { Data = new ProgressData(); return; }
            try
            {
                if (!string.IsNullOrEmpty(raw) && JsonUtility.FromJson<ProgressData>(raw)?.version > 1)
                {
                    Data = new ProgressData();
                    ReadOnly = true;
                    Warning = "This save belongs to a newer game version. It has been preserved; progress is temporary.";
                    return;
                }
            }
            catch (ArgumentException) { }
            Data = Parse(raw);
            if (Data != null) { lastValid = raw; return; }
            Data = Parse(backup);
            if (Data != null)
            {
                lastValid = backup;
                Warning = "Progress was recovered from the last valid backup.";
                try
                {
                    if (!string.IsNullOrEmpty(raw)) preferences.Set(Key + ".damaged", raw);
                    preferences.Flush();
                }
                catch (Exception exception)
                {
                    ReadOnly = true;
                    Warning = "Progress recovered, but storage is unavailable. Progress is temporary: " + exception.Message;
                }
            }
            else
            {
                Data = new ProgressData();
                ReadOnly = true;
                Warning = "Saved progress could not be read. Playing temporarily; the original save is preserved.";
            }
        }

        public static ProgressData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                foreach (string field in new[] { "version", "highestUnlocked", "completed", "bestScores", "sound" })
                    if (!json.Contains("\"" + field + "\"")) return null;
                var data = JsonUtility.FromJson<ProgressData>(json);
                if (data == null || data.version != 1 || data.completed == null || data.completed.Length != 6 ||
                    data.bestScores == null || data.bestScores.Length != 6) return null;
                int unlocked = 1;
                for (int i = 0; i < 6; i++)
                {
                    if (data.bestScores[i] < 0 || (data.completed[i] && i > 0 && !data.completed[i - 1])) return null;
                    if (data.completed[i]) unlocked = Math.Min(6, i + 2);
                }
                return data.highestUnlocked == unlocked ? data : null;
            }
            catch (ArgumentException) { return null; }
        }

        public void Record(int levelIndex, int score, bool won)
        {
            if (levelIndex < 0 || levelIndex >= 6 || levelIndex >= Data.highestUnlocked || score < 0)
                throw new ArgumentOutOfRangeException(nameof(levelIndex));
            Data.bestScores[levelIndex] = Math.Max(Data.bestScores[levelIndex], score);
            if (won)
            {
                Data.completed[levelIndex] = true;
                Data.highestUnlocked = Math.Max(Data.highestUnlocked, Math.Min(6, levelIndex + 2));
            }
            Save();
        }
        public void SetSound(bool enabled) { Data.sound = enabled; Save(); }
        public bool Save()
        {
            if (ReadOnly) return false;
            try
            {
                string json = JsonUtility.ToJson(Data);
                preferences.Set(BackupKey, lastValid ?? json);
                preferences.Set(Key, json);
                preferences.Flush();
                lastValid = json;
                return true;
            }
            catch (Exception exception)
            {
                Warning = "Progress could not be saved: " + exception.Message;
                Debug.LogWarning(Warning);
                return false;
            }
        }
        public void ResetProgress()
        {
            bool sound = Data.sound;
            preferences.Delete(Key);
            preferences.Delete(BackupKey);
            preferences.Delete(Key + ".damaged");
            Data = new ProgressData { sound = sound };
            lastValid = null;
            ReadOnly = false;
            Warning = null;
            Save();
        }
    }
}
