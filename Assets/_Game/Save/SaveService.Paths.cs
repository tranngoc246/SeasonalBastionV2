using System.IO;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        private string RunPath => Path.Combine(Application.persistentDataPath, "run_save.json");
        private string RunTempPath => Path.Combine(Application.persistentDataPath, "run_save.tmp");
        private string RunBackupPath => Path.Combine(Application.persistentDataPath, "run_save.bak");
        private string MetaPath => Path.Combine(Application.persistentDataPath, "meta_save.json");

        private string GetSlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{Mathf.Max(1, slot)}.json");
        private string GetSlotTempPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{Mathf.Max(1, slot)}.tmp");
        private string GetSlotBackupPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{Mathf.Max(1, slot)}.bak");
        private string GetAutosavePath() => Path.Combine(Application.persistentDataPath, "save_autosave.json");
        private string GetAutosaveTempPath() => Path.Combine(Application.persistentDataPath, "save_autosave.tmp");
        private string GetAutosaveBackupPath() => Path.Combine(Application.persistentDataPath, "save_autosave.bak");
    }
}
