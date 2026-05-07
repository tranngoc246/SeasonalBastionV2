using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion
{
    internal sealed class AmmoTopologyCache
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;

        internal AmmoTopologyCache(AmmoService owner)
        {
            _owner = owner;
            _s = owner.Services;
        }

        internal void RebuildWorkplaceHasNpcSet()
        {
            int npcVersion = _s.WorldState?.Npcs != null ? _s.WorldState.Npcs.Version : 0;
            if (_owner.LastNpcVersionForWorkplaces == npcVersion)
                return;

            _owner.NpcIds.Clear();
            foreach (var id in _s.WorldState.Npcs.Ids) _owner.NpcIds.Add(id);
            _owner.NpcIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            _owner.WorkplacesWithNpc.Clear();
            for (int i = 0; i < _owner.NpcIds.Count; i++)
            {
                var nid = _owner.NpcIds[i];
                if (!_s.WorldState.Npcs.Exists(nid)) continue;
                var ns = _s.WorldState.Npcs.Get(nid);
                if (ns.Workplace.Value != 0)
                    _owner.WorkplacesWithNpc.Add(ns.Workplace.Value);
            }

            _owner.LastNpcVersionForWorkplaces = npcVersion;
        }

        internal void CleanupDestroyedTowerCaches()
        {
            if (_s.WorldState == null) return;

            _owner.TempTowerKeys.Clear();

            foreach (var kv in _owner.LastAmmoByTower)
            {
                int tid = kv.Key;
                if (!_s.WorldState.Towers.Exists(new TowerId(tid)) && !_owner.TempTowerKeys.Contains(tid))
                    _owner.TempTowerKeys.Add(tid);
            }

            foreach (var tid in _owner.PendingReqTower)
            {
                if (!_s.WorldState.Towers.Exists(new TowerId(tid)) && !_owner.TempTowerKeys.Contains(tid))
                    _owner.TempTowerKeys.Add(tid);
            }

            foreach (var tid in _owner.TempTowerKeys)
                _owner.RemoveTowerCacheState(tid);
        }

        internal void ScanTowersAndNotify()
        {
            var towers = _s.WorldIndex.Towers;
            if (towers == null) return;

            for (int i = 0; i < towers.Count; i++)
            {
                var tid = towers[i];
                if (!_s.WorldState.Towers.Exists(tid)) continue;

                var ts = _s.WorldState.Towers.Get(tid);

                int cur = ts.Ammo;
                int cap = ts.AmmoCap;

                if (_owner.MatchesTowerSnapshot(tid, cur, cap))
                    continue;

                _owner.RecordTowerSnapshot(tid, cur, cap);

                _owner.NotifyTowerAmmoChanged(tid, cur, cap);
            }
        }

        internal bool TryPickPreferredHaulerWorkplace(CellPos forgeAnchor, out BuildingId workplace)
        {
            workplace = default;
            if (TryPickNearestWorkplaceFromIndex(_s.WorldIndex.Armories, forgeAnchor, requireNpc: true, out workplace))
                return true;
            if (TryPickNearestWorkplaceFromIndex(_s.WorldIndex.Warehouses, forgeAnchor, requireNpc: true, out workplace))
                return true;
            return false;
        }

        internal bool TryPickNearestWorkplaceFromIndex(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best)
        {
            best = default;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                var bid = list[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (requireNpc && !_owner.WorkplacesWithNpc.Contains(bid.Value)) continue;

                int d = AmmoService.Manhattan(from, bs.Anchor);
                int idv = bid.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            return best.Value != 0;
        }

        internal bool TryPickForgeAmmoSource(CellPos refPos, out BuildingId bestForge, out int bestTakeable)
        {
            bestForge = default;
            bestTakeable = 0;

            var forges = _s.WorldIndex.Forges;
            if (forges == null || forges.Count == 0) return false;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < forges.Count; i++)
            {
                var forge = forges[i];
                if (!_s.WorldState.Buildings.Exists(forge)) continue;

                var state = _s.WorldState.Buildings.Get(forge);
                if (!state.IsConstructed) continue;
                if (!_s.StorageService.CanStore(forge, ResourceType.Ammo)) continue;

                int cap = _s.StorageService.GetCap(forge, ResourceType.Ammo);
                if (cap <= 0) continue;

                int current = _s.StorageService.GetAmount(forge, ResourceType.Ammo);
                if (current <= 0) continue;

                int keep = (cap * 20 + 99) / 100;
                if (keep < 1) keep = 1;
                if (current < keep) continue;

                int takeable = current - keep;
                if (takeable <= 0) continue;

                int dist = AmmoService.Manhattan(refPos, state.Anchor);
                int idValue = forge.Value;
                if (dist < bestDist || (dist == bestDist && idValue < bestId))
                {
                    bestDist = dist;
                    bestId = idValue;
                    bestForge = forge;
                    bestTakeable = takeable;
                }
            }

            return bestForge.Value != 0;
        }

        internal bool ContainsRequestForTower(List<AmmoRequest> list, TowerId tower)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Tower.Value == tower.Value)
                    return true;
            }
            return false;
        }

        internal void ReconcileOutstandingTowerNeeds()
        {
            var towers = _s.WorldIndex.Towers;
            if (towers == null) return;

            for (int i = 0; i < towers.Count; i++)
            {
                var tid = towers[i];
                if (!_s.WorldState.Towers.Exists(tid)) continue;

                var tower = _s.WorldState.Towers.Get(tid);
                if (tower.AmmoCap <= 0) continue;

                int need = tower.AmmoCap - tower.Ammo;
                if (need <= 0) continue;

                if (_owner.PendingReqTower.Contains(tid.Value))
                    continue;

                if (ContainsRequestForTower(_owner.UrgentRequests, tid) || ContainsRequestForTower(_owner.NormalRequests, tid))
                    continue;

                if (_owner.ResupplyJobByTower.TryGetValue(tid.Value, out var existingJob))
                {
                    if (_s.JobBoard.TryGet(existingJob, out var job) && !AmmoService.IsTerminal(job.Status))
                        continue;
                }

                AmmoRequestPriority pri = tower.Ammo <= 0 ? AmmoRequestPriority.Urgent : AmmoRequestPriority.Normal;
                _owner.EnqueueRequest(new AmmoRequest
                {
                    Tower = tid,
                    AmountNeeded = need,
                    Priority = pri,
                    CreatedAt = _owner.SimTime
                });
            }
        }
    }
}
