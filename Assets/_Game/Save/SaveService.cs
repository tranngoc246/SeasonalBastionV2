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

        private int ResolveRunSeed(IRunClock clock)
        {
            if (LastLoadedOrSavedSeed != 0)
                return LastLoadedOrSavedSeed;
            return 0;
        }

    }
}
