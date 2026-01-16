// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildOrderService : IBuildOrderService
    {
        private readonly GameServices _s;
        private int _nextOrderId = 1;

        public event System.Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices s){ _s = s; }

        public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            // TODO:
            // - validate placement
            // - create build site
            // - create order referencing site
            return _nextOrderId++;
        }

        public int CreateUpgradeOrder(BuildingId building) { throw new System.NotImplementedException(); }
        public int CreateRepairOrder(BuildingId building) { throw new System.NotImplementedException(); }

        public bool TryGet(int orderId, out BuildOrder order) { order = default; return false; }

        public void Cancel(int orderId) { /* TODO */ }

        public void Tick(float dt)
        {
            // TODO:
            // - for each active order: generate delivery/work jobs
            // - detect completion -> commit building/upgrade/repair, fire event
        }
    }
}
