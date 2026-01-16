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
    public interface IEntityStore<TId,TState>
    {
        bool Exists(TId id);
        TState Get(TId id);
        void Set(TId id, TState state);      // overwrite
        TId Create(TState state);
        void Destroy(TId id);

        int Count { get; }
        System.Collections.Generic.IEnumerable<TId> Ids { get; }
    }
}
