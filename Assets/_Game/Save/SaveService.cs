// _Game/Save/SaveService.cs
using System;
using System.IO;
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

        public bool HasRunSave()
            => File.Exists(RunPath) || GetLatestValidSlot() != 0;

        public bool HasAnyRunSave()
            => HasRunSave();

        public void DeleteRunSave()
        {
            DeleteIfExists(RunPath, RunTempPath, RunBackupPath);

            for (int slot = 1; slot <= 3; slot++)
                DeleteIfExists(GetSlotPath(slot), GetSlotTempPath(slot), GetSlotBackupPath(slot));

            DeleteIfExists(GetAutosavePath(), GetAutosaveTempPath(), GetAutosaveBackupPath());
        }

        public SaveResult SaveRun(IWorldState world, IRunClock clock)
        {
            try
            {
                if (!CanSave(world, clock, out var invalidResult))
                    return invalidResult;

                var file = CreateImmutableRunSnapshot(world, clock);
                file.timestampUtc = DateTime.UtcNow.ToString("o");
                var json = JsonUtility.ToJson(file, true);
                AtomicWriteRunSave(json, RunPath, RunTempPath, RunBackupPath);

                if (GetLatestValidSlot() == 0)
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
                if (!CanSave(world, clock, out var invalidResult))
                    return invalidResult;

                int safeSlot = Mathf.Max(1, slot);
                var file = CreateImmutableRunSnapshot(world, clock);
                file.timestampUtc = DateTime.UtcNow.ToString("o");
                var json = JsonUtility.ToJson(file, true);

                ResolveSaveTarget(safeSlot, autosave, out var path, out var tempPath, out var backupPath);
                AtomicWriteRunSave(json, path, tempPath, backupPath);

                return new SaveResult(SaveResultCode.Ok, autosave ? "Autosaved run" : $"Saved slot {safeSlot}");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] SaveRunToSlot failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        private bool CanSave(IWorldState world, IRunClock clock, out SaveResult result)
        {
            if (world == null || clock == null)
            {
                result = new SaveResult(SaveResultCode.Failed, "world/clock null");
                return false;
            }

            result = default;
            return true;
        }

        private void ResolveSaveTarget(int slot, bool autosave, out string path, out string tempPath, out string backupPath)
        {
            if (autosave)
            {
                path = GetAutosavePath();
                tempPath = GetAutosaveTempPath();
                backupPath = GetAutosaveBackupPath();
                return;
            }

            path = GetSlotPath(slot);
            tempPath = GetSlotTempPath(slot);
            backupPath = GetSlotBackupPath(slot);
        }

        private void DeleteIfExists(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
        }

        private int ResolveRunSeed(IRunClock clock)
        {
            if (LastLoadedOrSavedSeed != 0)
                return LastLoadedOrSavedSeed;
            return 0;
        }
    }
}
