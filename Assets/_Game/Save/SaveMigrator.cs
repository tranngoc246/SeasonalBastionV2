// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveMigrator
    {
        public int CurrentSchemaVersion => 1;

        public bool TryMigrate(RunSaveDTO dto, out RunSaveDTO migrated)
        {
            // TODO: while dto.schemaVersion < Current: apply steps
            migrated = dto;
            return true;
        }

        public bool TryMigrate(MetaSaveDTO dto, out MetaSaveDTO migrated)
        {
            migrated = dto;
            return true;
        }
    }
}
