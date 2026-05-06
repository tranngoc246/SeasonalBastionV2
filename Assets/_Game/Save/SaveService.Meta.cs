using System;
using System.Collections.Generic;
using System.IO;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        public SaveResult SaveMeta(MetaSaveDTO dto)
        {
            try
            {
                if (dto == null)
                    return new SaveResult(SaveResultCode.Failed, "meta null");

                var file = new MetaSaveFile
                {
                    schemaVersion = CurrentSchemaVersion,
                    currency = dto.currency,
                    unlockIds = dto.unlockIds ?? new List<string>(),
                    perkLevels = new List<PerkKV>()
                };

                if (dto.perkLevels != null)
                {
                    foreach (var kv in dto.perkLevels)
                        file.perkLevels.Add(new PerkKV { key = kv.Key, value = kv.Value });
                }

                File.WriteAllText(MetaPath, JsonUtility.ToJson(file, true));
                return new SaveResult(SaveResultCode.Ok, "Saved meta");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] SaveMeta failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult LoadMeta(out MetaSaveDTO dto)
        {
            dto = null;
            try
            {
                if (!File.Exists(MetaPath))
                    return new SaveResult(SaveResultCode.NotFound, "No meta save");

                var json = File.ReadAllText(MetaPath);
                var file = JsonUtility.FromJson<MetaSaveFile>(json);
                if (file == null)
                    return new SaveResult(SaveResultCode.Failed, "Invalid meta json");

                dto = new MetaSaveDTO
                {
                    schemaVersion = file.schemaVersion,
                    currency = file.currency,
                    unlockIds = file.unlockIds ?? new List<string>(),
                    perkLevels = new Dictionary<string, int>()
                };

                if (file.perkLevels != null)
                {
                    for (int i = 0; i < file.perkLevels.Count; i++)
                    {
                        var p = file.perkLevels[i];
                        if (!string.IsNullOrEmpty(p.key))
                            dto.perkLevels[p.key] = p.value;
                    }
                }

                if (!_migrator.TryMigrate(dto, out var migrated))
                    return new SaveResult(SaveResultCode.IncompatibleSchema, "Meta migrate failed");

                dto = migrated;
                return new SaveResult(SaveResultCode.Ok, "Loaded meta");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] LoadMeta failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }
    }
}
