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

        // Costs remaining to deliver
        public List<CostDef> RemainingCosts;
    }
}
