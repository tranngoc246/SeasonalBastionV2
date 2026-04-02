using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobEnqueueService
    {
        private readonly GameServices _s;
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly IJobWorkplacePolicy _workplacePolicy;
        private readonly ResourceLogisticsPolicy _resourcePolicy;
        private readonly JobStateCleanupService _cleanupService;
        private readonly IHarvestTargetSelector _harvestTargetSelector;

        internal JobEnqueueService(
            GameServices s,
            IWorldState w,
            IJobBoard board,
            IJobWorkplacePolicy workplacePolicy,
            ResourceLogisticsPolicy resourcePolicy,
            JobStateCleanupService cleanupService,
            IHarvestTargetSelector harvestTargetSelector)
        {
            _s = s;
            _w = w;
            _board = board;
            _workplacePolicy = workplacePolicy;
            _resourcePolicy = resourcePolicy;
            _cleanupService = cleanupService;
            _harvestTargetSelector = harvestTargetSelector ?? throw new ArgumentNullException(nameof(harvestTargetSelector));
        }

        internal void EnqueueHarvestJobsIfNeeded(
            IReadOnlyList<BuildingId> buildingIds,
            HashSet<int> workplacesWithNpc,
            Dictionary<int, int> workplaceNpcCount,
            Dictionary<int, JobId> harvestJobByWorkplaceSlot)
        {
            for (int i = 0; i < buildingIds.Count; i++)
            {
                var bid = buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Harvest)) continue;
                if (!workplacesWithNpc.Contains(bid.Value)) continue;

                int assigned = 1;
                workplaceNpcCount.TryGetValue(bid.Value, out assigned);
                if (assigned < 1) assigned = 1;

                int slots = assigned;
                if (slots > 2) slots = 2;

                var rt = _resourcePolicy.HarvestResourceType(bs.DefId);
                int cap = _resourcePolicy.HarvestLocalCap(bs.DefId, _resourcePolicy.NormalizeLevel(bs.Level));
                int cur = _resourcePolicy.GetAmountFromBuilding(bs, rt);
                if (cap > 0 && cur >= cap) continue;

                for (int slot = 0; slot < slots; slot++)
                {
                    if (!_harvestTargetSelector.TryPickBestHarvestTarget(_s, _w, rt, bs.Anchor, bid.Value, slot, out var zoneCell))
                        continue;

                    if (zoneCell.X == 0 && zoneCell.Y == 0)
                        continue;

                    int key = bid.Value * 4 + slot;

                    if (harvestJobByWorkplaceSlot.TryGetValue(key, out var oldId))
                    {
                        if (_board.TryGet(oldId, out var old) && !_cleanupService.IsTerminal(old.Status))
                            continue;
                    }

                    var j = new Job
                    {
                        Archetype = JobArchetype.Harvest,
                        Status = JobStatus.Created,
                        Workplace = bid,
                        SourceBuilding = bid,
                        DestBuilding = default,
                        Site = default,
                        Tower = default,
                        ResourceType = rt,
                        Amount = 0,
                        TargetCell = zoneCell,
                        CreatedAt = 0
                    };

                    var newId = _board.Enqueue(j);
                    harvestJobByWorkplaceSlot[key] = newId;
                }
            }
        }

        internal void EnqueueHaulJobsIfNeeded(
            IReadOnlyList<BuildingId> buildingIds,
            HashSet<int> workplacesWithNpc,
            Dictionary<int, JobId> haulJobByWorkplaceAndType,
            System.Func<ResourceType, bool> anyHarvestProducerHasAmount)
        {
            for (int i = 0; i < buildingIds.Count; i++)
            {
                var wid = buildingIds[i];
                if (!_w.Buildings.Exists(wid)) continue;

                var bs = _w.Buildings.Get(wid);
                if (!bs.IsConstructed) continue;
                if (!_workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.HaulBasic)) continue;
                if (!workplacesWithNpc.Contains(wid.Value)) continue;

                TryEnsureHaulJob(wid, bs, ResourceType.Wood, haulJobByWorkplaceAndType, anyHarvestProducerHasAmount);
                TryEnsureHaulJob(wid, bs, ResourceType.Food, haulJobByWorkplaceAndType, anyHarvestProducerHasAmount);
                TryEnsureHaulJob(wid, bs, ResourceType.Stone, haulJobByWorkplaceAndType, anyHarvestProducerHasAmount);
                TryEnsureHaulJob(wid, bs, ResourceType.Iron, haulJobByWorkplaceAndType, anyHarvestProducerHasAmount);
            }
        }

        private void TryEnsureHaulJob(
            BuildingId workplace,
            in BuildingState destState,
            ResourceType rt,
            Dictionary<int, JobId> haulJobByWorkplaceAndType,
            System.Func<ResourceType, bool> anyHarvestProducerHasAmount)
        {
            if (anyHarvestProducerHasAmount == null || !anyHarvestProducerHasAmount(rt)) return;
            if (destState.Anchor.X == 0 && destState.Anchor.Y == 0) return;

            int cap = _resourcePolicy.DestCap(destState.DefId, _resourcePolicy.NormalizeLevel(destState.Level), rt);
            if (cap <= 0) return;

            int cur = _resourcePolicy.GetAmountFromBuilding(destState, rt);
            if (cur >= cap) return;

            int key = workplace.Value * 16 + (int)rt;

            if (haulJobByWorkplaceAndType.TryGetValue(key, out var oldId))
            {
                if (_board.TryGet(oldId, out var old) && !_cleanupService.IsTerminal(old.Status))
                    return;
            }

            var j = new Job
            {
                Archetype = JobArchetype.HaulBasic,
                Status = JobStatus.Created,
                Workplace = workplace,
                SourceBuilding = default,
                DestBuilding = workplace,
                Site = default,
                Tower = default,
                ResourceType = rt,
                Amount = 0,
                TargetCell = default,
                CreatedAt = 0
            };

            var newId = _board.Enqueue(j);
            haulJobByWorkplaceAndType[key] = newId;
        }
    }
}
