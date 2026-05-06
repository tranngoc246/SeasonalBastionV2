// _Game/Save/SaveService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed partial class SaveService : ISaveService
    {
        private readonly SaveMigrator _migrator;
        private readonly IDataRegistry _data;
        private readonly IGridMap _grid;
        private readonly IPopulationService _population;
        private readonly GameServices _services;

        public int LastLoadedOrSavedSeed { get; private set; }

        public int CurrentSchemaVersion => _migrator.CurrentSchemaVersion;

        public SaveService(SaveMigrator migrator, IDataRegistry data, IGridMap grid, IPopulationService population = null, GameServices services = null)
        {
            _migrator = migrator;
            _data = data;
            _grid = grid;
            _population = population;
            _services = services;
        }

        public bool HasRunSave() => File.Exists(RunPath) || GetLatestValidSlot() != 0;
        public bool HasAnyRunSave() => HasRunSave();

        public void DeleteRunSave()
        {
            if (File.Exists(RunPath)) File.Delete(RunPath);
            if (File.Exists(RunTempPath)) File.Delete(RunTempPath);
            if (File.Exists(RunBackupPath)) File.Delete(RunBackupPath);

            for (int i = 1; i <= 3; i++)
            {
                string p = GetSlotPath(i);
                string t = GetSlotTempPath(i);
                string b = GetSlotBackupPath(i);
                if (File.Exists(p)) File.Delete(p);
                if (File.Exists(t)) File.Delete(t);
                if (File.Exists(b)) File.Delete(b);
            }

            string ap = GetAutosavePath();
            string at = GetAutosaveTempPath();
            string ab = GetAutosaveBackupPath();
            if (File.Exists(ap)) File.Delete(ap);
            if (File.Exists(at)) File.Delete(at);
            if (File.Exists(ab)) File.Delete(ab);
        }

        public SaveResult SaveRun(IWorldState world, IRunClock clock)
        {
            try
            {
                if (world == null || clock == null)
                    return new SaveResult(SaveResultCode.Failed, "world/clock null");

                var file = CreateImmutableRunSnapshot(world, clock);
                file.timestampUtc = DateTime.UtcNow.ToString("o");
                var json = JsonUtility.ToJson(file, true);
                AtomicWriteRunSave(json, RunPath, RunTempPath, RunBackupPath);

                int latestSlot = GetLatestValidSlot();
                if (latestSlot == 0)
                    SaveRunToSlot(world, clock, 1, autosave: false);

                return new SaveResult(SaveResultCode.Ok, "Saved run");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] SaveRun failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult SaveRunToSlot(IWorldState world, IRunClock clock, int slot, bool autosave = false)
        {
            try
            {
                if (world == null || clock == null)
                    return new SaveResult(SaveResultCode.Failed, "world/clock null");

                int safeSlot = Mathf.Max(1, slot);
                var file = CreateImmutableRunSnapshot(world, clock);
                file.timestampUtc = DateTime.UtcNow.ToString("o");
                var json = JsonUtility.ToJson(file, true);

                string path = autosave ? GetAutosavePath() : GetSlotPath(safeSlot);
                string temp = autosave ? GetAutosaveTempPath() : GetSlotTempPath(safeSlot);
                string backup = autosave ? GetAutosaveBackupPath() : GetSlotBackupPath(safeSlot);
                AtomicWriteRunSave(json, path, temp, backup);

                return new SaveResult(SaveResultCode.Ok, autosave ? "Autosaved run" : $"Saved slot {safeSlot}");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] SaveRunToSlot failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public int GetLatestValidSlot()
        {
            var saves = ListRunSaves();
            int bestSlot = 0;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < saves.Count; i++)
            {
                var s = saves[i];
                if (s == null || !s.IsValid || s.IsAutosave || s.IsLegacy || s.Slot <= 0) continue;
                if (!DateTime.TryParse(s.TimestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) t = DateTime.MinValue;
                if (t >= bestTime)
                {
                    bestTime = t;
                    bestSlot = s.Slot;
                }
            }
            return bestSlot;
        }

        public IReadOnlyList<SaveSlotInfo> ListRunSaves()
        {
            var list = new List<SaveSlotInfo>();
            for (int i = 1; i <= 3; i++)
                list.Add(ReadSlotInfo(GetSlotPath(i), i, isAutosave: false, isLegacy: false, isBackup: false));
            list.Add(ReadSlotInfo(GetAutosavePath(), 0, isAutosave: true, isLegacy: false, isBackup: false));
            if (File.Exists(RunPath))
                list.Add(ReadSlotInfo(RunPath, 0, isAutosave: false, isLegacy: true, isBackup: false));
            return list;
        }

        public SaveResult SaveMeta(MetaSaveDTO dto)
        {
            try
            {
                if (dto == null) return new SaveResult(SaveResultCode.Failed, "meta null");

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
                if (file == null) return new SaveResult(SaveResultCode.Failed, "Invalid meta json");

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

        private int ResolveRunSeed(IRunClock clock)
        {
            if (LastLoadedOrSavedSeed != 0)
                return LastLoadedOrSavedSeed;
            return 0;
        }

        private SaveSlotInfo ReadSlotInfo(string path, int slot, bool isAutosave, bool isLegacy, bool isBackup)
        {
            var info = new SaveSlotInfo
            {
                Slot = slot,
                FileName = Path.GetFileName(path),
                IsAutosave = isAutosave,
                IsLegacy = isLegacy,
                IsBackup = isBackup,
                DisplayName = isAutosave ? "Autosave" : (isLegacy ? "Legacy Continue" : $"Save Slot {slot}"),
                IsValid = false,
                Season = "-",
                DayIndex = 0,
                YearIndex = 0,
                WaveIndex = 0,
                TimestampUtc = string.Empty,
                Error = string.Empty
            };

            if (!File.Exists(path))
            {
                info.Error = "Empty";
                return info;
            }

            try
            {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<RunSaveFile>(json);
                if (file == null)
                {
                    info.Error = "Invalid json";
                    return info;
                }

                info.IsValid = true;
                info.Season = file.season;
                info.DayIndex = file.dayIndex;
                info.YearIndex = file.yearIndex;
                info.WaveIndex = file.combat != null ? file.combat.currentWaveIndex : 0;
                info.TimestampUtc = file.timestampUtc ?? string.Empty;
                return info;
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
                return info;
            }
        }
    }
}
