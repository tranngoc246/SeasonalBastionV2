// AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1)
// Source: PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md
// Notes:
// - Contracts only: interfaces/enums/structs/DTO/events.
// - Do not put runtime logic here.
// - Namespace kept unified to minimize cross-namespace friction.

using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface ISaveService
    {
        int CurrentSchemaVersion { get; }

        SaveResult SaveRun(IWorldState world, IRunClock clock);
        SaveResult LoadRun(out RunSaveDTO dto);

        SaveResult SaveMeta(MetaSaveDTO dto);
        SaveResult LoadMeta(out MetaSaveDTO dto);

        bool HasRunSave();
        void DeleteRunSave();
    }
}
