// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System.IO;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveService : ISaveService
    {
        private readonly SaveMigrator _migrator;
        private readonly IDataRegistry _data;

        public int CurrentSchemaVersion => _migrator.CurrentSchemaVersion;

        private string RunPath => Path.Combine(Application.persistentDataPath, "run_save.json");
        private string MetaPath => Path.Combine(Application.persistentDataPath, "meta_save.json");

        public SaveService(SaveMigrator migrator, IDataRegistry data)
        { _migrator = migrator; _data = data; }

        public bool HasRunSave() => File.Exists(RunPath);

        public void DeleteRunSave()
        {
            if (File.Exists(RunPath)) File.Delete(RunPath);
        }

        public SaveResult SaveRun(IWorldState world, IRunClock clock)
        {
            // TODO: build DTO from world+clock (keep minimal)
            return new SaveResult(SaveResultCode.Ok, "TODO");
        }

        public SaveResult LoadRun(out RunSaveDTO dto)
        {
            dto = null;
            if (!File.Exists(RunPath)) return new SaveResult(SaveResultCode.NotFound, "No run save");

            // TODO: read json, migrate, return
            return new SaveResult(SaveResultCode.Ok, "TODO");
        }

        public SaveResult SaveMeta(MetaSaveDTO dto)
        {
            // TODO: write
            return new SaveResult(SaveResultCode.Ok, "TODO");
        }

        public SaveResult LoadMeta(out MetaSaveDTO dto)
        {
            dto = null;
            // TODO: read+ migrate
            return new SaveResult(SaveResultCode.Ok, "TODO");
        }
    }
}
