// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class AmmoService : IAmmoService
    {
        private readonly GameServices _s;
        private readonly System.Collections.Generic.List<AmmoRequest> _urgent = new();
        private readonly System.Collections.Generic.List<AmmoRequest> _normal = new();

        public AmmoService(GameServices s){ _s = s; }

        public int PendingRequests => _urgent.Count + _normal.Count;

        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max)
        {
            // TODO:
            // - if <=25% enqueue request (urgent if 0)
        }

        public void EnqueueRequest(AmmoRequest req)
        {
            if (req.Priority == AmmoRequestPriority.Urgent) _urgent.Add(req);
            else _normal.Add(req);
        }

        public bool TryDequeueNext(out AmmoRequest req)
        {
            // TODO: deterministic, urgent first, then oldest
            req = default;
            return false;
        }

        public bool TryStartCraft(BuildingId forge)
        {
            // TODO: verify inputs exist, create craft job
            return false;
        }

        public void Tick(float dt)
        {
            // TODO: could be empty if you rely on job providers in BuildOrder/Job system
        }
    }
}
