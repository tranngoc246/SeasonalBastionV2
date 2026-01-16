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
    public enum ClaimKind
    {
        StorageSource,
        StorageDest,
        TowerResupply,
        BuildSite,
        ProducerNode
    }

    public readonly struct ClaimKey
    {
        public readonly ClaimKind Kind;
        public readonly int A; // id value (building/tower/site)
        public readonly int B; // optional (resource type int)
        public ClaimKey(ClaimKind k,int a,int b){Kind=k;A=a;B=b;}
    }
}
