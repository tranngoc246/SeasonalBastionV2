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
    public readonly struct StorageSnapshot
    {
        public readonly int Wood, Food, Stone, Iron, Ammo;
        public readonly int CapWood, CapFood, CapStone, CapIron, CapAmmo;
        public StorageSnapshot(int w,int f,int s,int i,int a,int cw,int cf,int cs,int ci,int ca)
        { Wood=w;Food=f;Stone=s;Iron=i;Ammo=a;CapWood=cw;CapFood=cf;CapStone=cs;CapIron=ci;CapAmmo=ca; }
    }

    public readonly struct StoragePick
    {
        public readonly BuildingId Building;
        public readonly int Distance; // Manhattan
        public StoragePick(BuildingId b,int d){Building=b;Distance=d;}
    }
}
