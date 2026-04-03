using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    internal sealed class TowerResupplyPlanner
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;

        internal TowerResupplyPlanner(AmmoService owner)
        {
            _owner = owner;
            _s = owner.Services;
        }

        internal void CleanupResupplyTowerInFlight()
        {
            if (_owner.ResupplyJobByTower.Count == 0 && _owner.ResupplyJobByArmory.Count == 0) return;

            _owner.TempTowerKeys.Clear();
            foreach (var kv in _owner.ResupplyJobByTower)
                _owner.TempTowerKeys.Add(kv.Key);

            for (int i = 0; i < _owner.TempTowerKeys.Count; i++)
            {
                int tid = _owner.TempTowerKeys[i];
                var jid = _owner.ResupplyJobByTower[tid];

                if (!_s.JobBoard.TryGet(jid, out var j) || AmmoService.IsTerminal(j.Status))
                {
                    _owner.ResupplyJobByTower.Remove(tid);

                    if (j.Workplace.Value != 0)
                        _owner.ResupplyJobByArmory.Remove(j.Workplace.Value);
                    else
                        RemoveArmoryMappingByJob(jid);

                    if (_s.WorldState != null && _s.WorldState.Towers.Exists(new TowerId(tid)))
                        _owner.MaybeRequeueTowerAmmoRequest(new TowerId(tid));
                }
            }

            CleanupResupplyArmoryMappings();
        }

        internal void EnsureResupplyTowerJobs()
        {
            PruneInvalidRequests(_owner.UrgentRequests);
            PruneInvalidRequests(_owner.NormalRequests);

            int guard = CountEligibleRequests() + 4;
            while (guard-- > 0)
            {
                if (!TryCreateNextResupplyTowerJob())
                    break;
            }
        }

        internal bool TryCreateNextResupplyTowerJob()
        {
            if (!_owner.TryPickBestRequest(out var list, out var idx, out var req, out var towerState))
                return false;

            int scanned = 0;
            int maxScan = _owner.UrgentRequests.Count + _owner.NormalRequests.Count + 1;
            while (scanned < maxScan)
            {
                if (!TryPickBestResupplySource(towerState, out var source, out var sourceState, out var availableAmmo))
                {
                    if (_owner.TowerNoSourceLogged.Add(req.Tower.Value))
                        Log.E($"[Ammo] resupply skipped tower {req.Tower.Value}: no ammo source totalTowers={_owner.Debug_TotalTowers} emptyTowers={_owner.Debug_TowersWithoutAmmo} activeResupplyJobs={_owner.Debug_ActiveResupplyJobs} armoryAmmo={_owner.Debug_ArmoryAvailableAmmo}");
                    return false;
                }

                if (_owner.ResupplyJobByTower.TryGetValue(req.Tower.Value, out var existingTowerJob))
                {
                    if (_s.JobBoard.TryGet(existingTowerJob, out var existing) && !AmmoService.IsTerminal(existing.Status))
                        return false;

                    _owner.ResupplyJobByTower.Remove(req.Tower.Value);
                }

                if (_owner.ResupplyJobByArmory.TryGetValue(source.Value, out var oldId))
                {
                    if (_s.JobBoard.TryGet(oldId, out var old) && !AmmoService.IsTerminal(old.Status))
                    {
                        if (old.Status == JobStatus.Created && req.Priority == AmmoRequestPriority.Urgent)
                        {
                            int currentTid = old.Tower.Value;
                            int urgentTid = req.Tower.Value;
                            if (urgentTid != 0 && urgentTid != currentTid)
                            {
                                int urgentNeed = towerState.AmmoCap - towerState.Ammo;
                                int urgentAmount = _owner.GetArmoryResupplyTripByLevel_Value(sourceState.Level);
                                if (urgentAmount > urgentNeed) urgentAmount = urgentNeed;
                                if (urgentAmount > availableAmmo) urgentAmount = availableAmmo;

                                if (urgentAmount > 0)
                                {
                                    if (currentTid != 0)
                                        _owner.ResupplyJobByTower.Remove(currentTid);

                                    _owner.ConsumeRequestAt(list, idx);
                                    old.Tower = req.Tower;
                                    old.Amount = urgentAmount;
                                    _s.JobBoard.Update(old);
                                    _owner.ResupplyJobByArmory[source.Value] = old.Id;
                                    _owner.ResupplyJobByTower[urgentTid] = old.Id;
                                    _owner.TowerNoSourceLogged.Remove(req.Tower.Value);
                                    _owner.TowerNoJobLogged.Remove(req.Tower.Value);
                                    if (_owner.DebugAmmoLogsValue)
                                        Log.E($"[Ammo] resupply reprioritized source={source.Value} tower={urgentTid} amount={urgentAmount}");
                                    return true;
                                }
                            }
                        }

                        scanned++;
                        _owner.RotateRequestToBack(list, idx, req);
                        if (!_owner.TryPickBestRequest(out list, out idx, out req, out towerState))
                            return false;
                        continue;
                    }

                    _owner.ResupplyJobByArmory.Remove(source.Value);
                }

                int need = towerState.AmmoCap - towerState.Ammo;
                int amount = _owner.GetArmoryResupplyTripByLevel_Value(sourceState.Level);
                if (amount > need) amount = need;
                if (amount > availableAmmo) amount = availableAmmo;
                if (amount <= 0)
                    return false;

                _owner.ConsumeRequestAt(list, idx);

                var j = new Job
                {
                    Archetype = JobArchetype.ResupplyTower,
                    Status = JobStatus.Created,
                    Workplace = source,
                    SourceBuilding = source,
                    Tower = req.Tower,
                    ResourceType = ResourceType.Ammo,
                    Amount = amount,
                    TargetCell = default,
                    CreatedAt = 0
                };

                var id = _s.JobBoard.Enqueue(j);
                _owner.ResupplyJobByArmory[source.Value] = id;
                _owner.ResupplyJobByTower[req.Tower.Value] = id;
                _owner.TowerNoSourceLogged.Remove(req.Tower.Value);
                _owner.TowerNoJobLogged.Remove(req.Tower.Value);
                _owner.TowerDeadlockLogged.Remove(req.Tower.Value);
                if (_owner.DebugAmmoLogsValue)
                    Log.E($"[Ammo] resupply created source={source.Value} tower={req.Tower.Value} amount={amount} priority={req.Priority}");
                return true;
            }

            if (_owner.TowerNoJobLogged.Add(req.Tower.Value))
                Log.E($"[Ammo] Armory has ammo but no job created. tower={req.Tower.Value} totalTowers={_owner.Debug_TotalTowers} emptyTowers={_owner.Debug_TowersWithoutAmmo} activeResupplyJobs={_owner.Debug_ActiveResupplyJobs} armoryAmmo={_owner.Debug_ArmoryAvailableAmmo} pending={_owner.PendingRequests}");
            return false;
        }

        internal bool TryPickBestResupplySource(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo)
        {
            source = default;
            sourceState = default;
            availableAmmo = 0;

            int bestRank = int.MaxValue;
            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            EvaluateResupplySources(_s.WorldIndex.Armories, towerState.Cell, 0, ref source, ref sourceState, ref availableAmmo, ref bestRank, ref bestDist, ref bestId);
            EvaluateResupplySources(_s.WorldIndex.Warehouses, towerState.Cell, 1, ref source, ref sourceState, ref availableAmmo, ref bestRank, ref bestDist, ref bestId);

            return source.Value != 0;
        }

        internal void EvaluateResupplySources(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId)
        {
            if (candidates == null) return;

            for (int i = 0; i < candidates.Count; i++)
            {
                var bid = candidates[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var st = _s.WorldState.Buildings.Get(bid);
                if (!st.IsConstructed) continue;
                if (!_owner.WorkplacesWithNpc.Contains(bid.Value)) continue;
                if (!_s.StorageService.CanStore(bid, ResourceType.Ammo)) continue;

                int ammo = _s.StorageService.GetAmount(bid, ResourceType.Ammo);
                if (ammo <= 0) continue;

                int dist = AmmoService.Manhattan(st.Anchor, targetCell);
                int idv = bid.Value;

                if (rank < bestRank || (rank == bestRank && (dist < bestDist || (dist == bestDist && idv < bestId))))
                {
                    bestRank = rank;
                    bestDist = dist;
                    bestId = idv;
                    bestSource = bid;
                    bestState = st;
                    bestAmmo = ammo;
                }
            }
        }

        internal void LogPotentialResupplyDeadlock()
        {
            if (_owner.Debug_TowersWithoutAmmo <= 0)
            {
                _owner.TowerDeadlockLogged.Clear();
                return;
            }

            if (_owner.Debug_ArmoryAvailableAmmo <= 0)
                return;

            if (_owner.Debug_ActiveResupplyJobs > 0)
            {
                _owner.TowerDeadlockLogged.Clear();
                return;
            }

            int eligibleRequests = CountEligibleRequests();
            if (eligibleRequests <= 0)
                return;

            LogDeadlockForRequests(_owner.UrgentRequests);
            LogDeadlockForRequests(_owner.NormalRequests);
        }

        internal void UpdateDebugMetrics()
        {
            _owner.Debug_TotalTowers = 0;
            _owner.Debug_TowersWithoutAmmo = 0;
            _owner.Debug_ArmoryAvailableAmmo = 0;
            _owner.Debug_ActiveResupplyJobs = CountTrackedActiveResupplyJobs();

            var towers = _s.WorldIndex.Towers;
            if (towers != null)
            {
                for (int i = 0; i < towers.Count; i++)
                {
                    var tid = towers[i];
                    if (!_s.WorldState.Towers.Exists(tid)) continue;
                    _owner.Debug_TotalTowers++;
                    var tower = _s.WorldState.Towers.Get(tid);
                    if (tower.Ammo <= 0)
                        _owner.Debug_TowersWithoutAmmo++;
                }
            }

            var armories = _s.WorldIndex.Armories;
            if (armories != null)
            {
                for (int i = 0; i < armories.Count; i++)
                {
                    var armory = armories[i];
                    if (!_s.WorldState.Buildings.Exists(armory)) continue;
                    var st = _s.WorldState.Buildings.Get(armory);
                    if (!st.IsConstructed) continue;
                    _owner.Debug_ArmoryAvailableAmmo += Math.Max(0, _s.StorageService.GetAmount(armory, ResourceType.Ammo));
                }
            }
        }

        private void CleanupResupplyArmoryMappings()
        {
            if (_owner.ResupplyJobByArmory.Count == 0) return;

            _owner.TempTowerKeys.Clear();
            foreach (var kv in _owner.ResupplyJobByArmory)
                _owner.TempTowerKeys.Add(kv.Key);

            for (int i = 0; i < _owner.TempTowerKeys.Count; i++)
            {
                int armoryId = _owner.TempTowerKeys[i];
                var jid = _owner.ResupplyJobByArmory[armoryId];
                if (!_s.JobBoard.TryGet(jid, out var j) || AmmoService.IsTerminal(j.Status))
                    _owner.ResupplyJobByArmory.Remove(armoryId);
            }
        }

        private void RemoveArmoryMappingByJob(JobId jobId)
        {
            if (_owner.ResupplyJobByArmory.Count == 0) return;

            _owner.TempTowerKeys.Clear();
            foreach (var kv in _owner.ResupplyJobByArmory)
            {
                if (kv.Value.Value == jobId.Value)
                    _owner.TempTowerKeys.Add(kv.Key);
            }

            for (int i = 0; i < _owner.TempTowerKeys.Count; i++)
                _owner.ResupplyJobByArmory.Remove(_owner.TempTowerKeys[i]);
        }

        private int CountEligibleRequests()
        {
            int count = 0;
            count += CountEligibleRequests(_owner.UrgentRequests);
            count += CountEligibleRequests(_owner.NormalRequests);
            return count;
        }

        private int CountTrackedActiveResupplyJobs()
        {
            int count = 0;
            foreach (var kv in _owner.ResupplyJobByTower)
            {
                if (!_s.JobBoard.TryGet(kv.Value, out var job))
                    continue;
                if (!AmmoService.IsTerminal(job.Status))
                    count++;
            }
            return count;
        }

        private int CountEligibleRequests(List<AmmoRequest> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var req = list[i];
                if (req.Tower.Value == 0) continue;
                if (!_s.WorldState.Towers.Exists(req.Tower)) continue;
                var tower = _s.WorldState.Towers.Get(req.Tower);
                if (tower.AmmoCap <= 0) continue;
                if (tower.Ammo >= tower.AmmoCap) continue;
                count++;
            }
            return count;
        }

        private void LogDeadlockForRequests(List<AmmoRequest> list)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                int tid = list[i].Tower.Value;
                if (tid == 0) continue;
                if (_owner.TowerDeadlockLogged.Add(tid))
                    Log.E($"[Ammo] Armory has ammo but no job created. tower={tid} totalTowers={_owner.Debug_TotalTowers} emptyTowers={_owner.Debug_TowersWithoutAmmo} activeResupplyJobs={_owner.Debug_ActiveResupplyJobs} armoryAmmo={_owner.Debug_ArmoryAvailableAmmo} pending={_owner.PendingRequests}");
            }
        }

        private void PruneInvalidRequests(List<AmmoRequest> list)
        {
            if (list == null || list.Count == 0) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var r = list[i];
                int tid = r.Tower.Value;
                bool remove = false;

                if (tid == 0) remove = true;
                else if (!_s.WorldState.Towers.Exists(r.Tower)) remove = true;
                else
                {
                    var ts = _s.WorldState.Towers.Get(r.Tower);
                    if (ts.AmmoCap <= 0) remove = true;
                    else
                    {
                        int need = ts.AmmoCap - ts.Ammo;
                        if (need <= 0) remove = true;
                    }
                }

                if (!remove) continue;

                list.RemoveAt(i);
                if (tid != 0)
                {
                    _owner.PendingReqTower.Remove(tid);
                    _owner.PendingPriorityByTower.Remove(tid);
                    _owner.TowerNoJobLogged.Remove(tid);
                    _owner.TowerDeadlockLogged.Remove(tid);
                }
            }
        }
    }
}
