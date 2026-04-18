using SeasonalBastion.Contracts;
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
                        _owner.RemoveArmoryMappingByJob(jid);

                    if (_s.WorldState != null && _s.WorldState.Towers.Exists(new TowerId(tid)))
                        _owner.MaybeRequeueTowerAmmoRequest(new TowerId(tid));
                }
            }

            _owner.CleanupResupplyArmoryMappings();
        }

        internal void EnsureResupplyTowerJobs()
        {
            _owner.PruneInvalidResupplyRequests();

            int guard = _owner.CountEligibleResupplyRequests() + 4;
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
                    {
                        Log.E($"[Ammo] resupply skipped tower {req.Tower.Value}: no ammo source totalTowers={_owner.Debug_TotalTowers} emptyTowers={_owner.Debug_TowersWithoutAmmo} activeResupplyJobs={_owner.Debug_ActiveResupplyJobs} armoryAmmo={_owner.Debug_ArmoryAvailableAmmo}");
                        _s.NotificationService?.Push(
                            key: $"ammo.no_source.{req.Tower.Value}",
                            title: "Không có nguồn tiếp tế ammo",
                            body: "Tower cần ammo nhưng hiện chưa có armory hoặc kho phù hợp để cấp đạn.",
                            severity: NotificationSeverity.Warning,
                            payload: default,
                            cooldownSeconds: 12f,
                            dedupeByKey: true);
                    }
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
                _s.NotificationService?.Push(
                    key: $"ammo.resupply.queued.{req.Tower.Value}",
                    title: "Đã tạo lệnh tiếp tế",
                    body: "Một tower đang chờ được tiếp tế ammo từ armory hoặc kho.",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 12f,
                    dedupeByKey: true);
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


    }
}
