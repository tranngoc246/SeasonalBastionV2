// PATCH v0.1.4 — BuildSiteState fields aligned with WorldOps usage
using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public struct BuildSiteState
    {
        public SiteId Id;

        // What is being built
        public string BuildingDefId;
        public int TargetLevel;

        // Placement
        public CellPos Anchor;
        public Dir4 Rotation;

        // Progress
        public bool IsActive;
        public float WorkSecondsDone;
        public float WorkSecondsTotal;

        // Costs delivered so far (mirror of total cost for stable UI)
        public List<CostDef> DeliveredSoFar;

        // Costs remaining to deliver
        public List<CostDef> RemainingCosts;

        public bool IsReadyToWork => RemainingCosts == null || RemainingCosts.Count == 0;

        // 0 = PlaceNew, 1 = Upgrade
        public byte Kind;

        // Upgrade: building đang được nâng cấp (PlaceNew: placeholder building)
        public BuildingId TargetBuilding;

        // Upgrade metadata (optional, giúp debug/save/load)
        public string FromDefId;
        public string EdgeId;
        public bool IsUpgrade => Kind == 1;
    }
}
