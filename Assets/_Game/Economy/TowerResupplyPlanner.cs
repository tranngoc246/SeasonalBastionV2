using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class TowerResupplyPlanner
    {
        private readonly IWorldState _worldState;
        private readonly IWorldIndex _worldIndex;
        private readonly IStorageService _storageService;
        private readonly IJobBoard _jobBoard;
        private readonly INotificationService _notificationService;
        private readonly Dictionary<int, JobId> _resupplyJobByTower;
        private readonly Dictionary<int, JobId> _resupplyJobByArmory;
        private readonly HashSet<int> _towerNoSourceLogged;
        private readonly HashSet<int> _towerNoJobLogged;
        private readonly HashSet<int> _towerDeadlockLogged;
        private readonly List<int> _tempTowerKeys;
        private readonly Func<List<AmmoRequest>> _getUrgentRequests;
        private readonly Func<List<AmmoRequest>> _getNormalRequests;
        private readonly Func<int> _getPendingRequests;
        private readonly Func<int> _getDebugTotalTowers;
        private readonly Func<int> _getDebugTowersWithoutAmmo;
        private readonly Func<int> _getDebugActiveResupplyJobs;
        private readonly Func<int> _getDebugArmoryAvailableAmmo;
        private readonly Func<bool> _debugAmmoLogs;
        private readonly Func<HashSet<int>> _getWorkplacesWithNpc;
        private readonly Func<int, int> _getArmoryResupplyTripByLevel;
        private readonly Func<int> _countEligibleResupplyRequests;
        private readonly Action _pruneInvalidResupplyRequests;
        private readonly Func<Dictionary<int, JobId>, (bool found, List<AmmoRequest> list, int index, AmmoRequest req, TowerState towerState)> _pickBestRequest;
        private readonly Action<List<AmmoRequest>, int> _consumeRequestAt;
        private readonly Action<List<AmmoRequest>, int, AmmoRequest> _rotateRequestToBack;
        private readonly Action<TowerId> _maybeRequeueTowerAmmoRequest;
        private readonly Action _cleanupResupplyArmoryMappings;
        private readonly Action<JobId> _removeArmoryMappingByJob;

        internal TowerResupplyPlanner(
            IWorldState worldState,
            IWorldIndex worldIndex,
            IStorageService storageService,
            IJobBoard jobBoard,
            INotificationService notificationService,
            Dictionary<int, JobId> resupplyJobByTower,
            Dictionary<int, JobId> resupplyJobByArmory,
            HashSet<int> towerNoSourceLogged,
            HashSet<int> towerNoJobLogged,
            HashSet<int> towerDeadlockLogged,
            List<int> tempTowerKeys,
            Func<List<AmmoRequest>> getUrgentRequests,
            Func<List<AmmoRequest>> getNormalRequests,
            Func<int> getPendingRequests,
            Func<int> getDebugTotalTowers,
            Func<int> getDebugTowersWithoutAmmo,
            Func<int> getDebugActiveResupplyJobs,
            Func<int> getDebugArmoryAvailableAmmo,
            Func<bool> debugAmmoLogs,
            Func<HashSet<int>> getWorkplacesWithNpc,
            Func<int, int> getArmoryResupplyTripByLevel,
            Func<int> countEligibleResupplyRequests,
            Action pruneInvalidResupplyRequests,
            Func<Dictionary<int, JobId>, (bool found, List<AmmoRequest> list, int index, AmmoRequest req, TowerState towerState)> pickBestRequest,
            Action<List<AmmoRequest>, int> consumeRequestAt,
            Action<List<AmmoRequest>, int, AmmoRequest> rotateRequestToBack,
            Action<TowerId> maybeRequeueTowerAmmoRequest,
            Action cleanupResupplyArmoryMappings,
            Action<JobId> removeArmoryMappingByJob)
        {
            _worldState = worldState;
            _worldIndex = worldIndex;
            _storageService = storageService;
            _jobBoard = jobBoard;
            _notificationService = notificationService;
            _resupplyJobByTower = resupplyJobByTower;
            _resupplyJobByArmory = resupplyJobByArmory;
            _towerNoSourceLogged = towerNoSourceLogged;
            _towerNoJobLogged = towerNoJobLogged;
            _towerDeadlockLogged = towerDeadlockLogged;
            _tempTowerKeys = tempTowerKeys;
            _getUrgentRequests = getUrgentRequests;
            _getNormalRequests = getNormalRequests;
            _getPendingRequests = getPendingRequests;
            _getDebugTotalTowers = getDebugTotalTowers;
            _getDebugTowersWithoutAmmo = getDebugTowersWithoutAmmo;
            _getDebugActiveResupplyJobs = getDebugActiveResupplyJobs;
            _getDebugArmoryAvailableAmmo = getDebugArmoryAvailableAmmo;
            _debugAmmoLogs = debugAmmoLogs;
            _getWorkplacesWithNpc = getWorkplacesWithNpc;
            _getArmoryResupplyTripByLevel = getArmoryResupplyTripByLevel;
            _countEligibleResupplyRequests = countEligibleResupplyRequests;
            _pruneInvalidResupplyRequests = pruneInvalidResupplyRequests;
            _pickBestRequest = pickBestRequest;
            _consumeRequestAt = consumeRequestAt;
            _rotateRequestToBack = rotateRequestToBack;
            _maybeRequeueTowerAmmoRequest = maybeRequeueTowerAmmoRequest;
            _cleanupResupplyArmoryMappings = cleanupResupplyArmoryMappings;
            _removeArmoryMappingByJob = removeArmoryMappingByJob;
        }

        internal void CleanupResupplyTowerInFlight()
        {
            if (_resupplyJobByTower.Count == 0 && _resupplyJobByArmory.Count == 0)
                return;

            _tempTowerKeys.Clear();
            foreach (var kv in _resupplyJobByTower)
                _tempTowerKeys.Add(kv.Key);

            for (int i = 0; i < _tempTowerKeys.Count; i++)
            {
                int towerId = _tempTowerKeys[i];
                var jobId = _resupplyJobByTower[towerId];

                if (!_jobBoard.TryGet(jobId, out var job) || AmmoService.IsTerminal(job.Status))
                {
                    _resupplyJobByTower.Remove(towerId);

                    if (job.Workplace.Value != 0)
                        _resupplyJobByArmory.Remove(job.Workplace.Value);
                    else
                        _removeArmoryMappingByJob(jobId);

                    if (_worldState != null && _worldState.Towers.Exists(new TowerId(towerId)))
                        _maybeRequeueTowerAmmoRequest(new TowerId(towerId));
                }
            }

            _cleanupResupplyArmoryMappings();
        }

        internal void EnsureResupplyTowerJobs()
        {
            _pruneInvalidResupplyRequests();

            int guard = _countEligibleResupplyRequests() + 4;
            while (guard-- > 0)
            {
                if (!TryCreateNextResupplyTowerJob())
                    break;
            }
        }

        internal bool TryCreateNextResupplyTowerJob()
        {
            var pick = _pickBestRequest(_resupplyJobByTower);
            if (!pick.found)
                return false;

            var list = pick.list;
            int index = pick.index;
            var request = pick.req;
            var towerState = pick.towerState;

            int scanned = 0;
            int maxScan = _getUrgentRequests().Count + _getNormalRequests().Count + 1;
            while (scanned < maxScan)
            {
                if (!TryPickBestResupplySource(towerState, out var source, out var sourceState, out var availableAmmo))
                {
                    if (_towerNoSourceLogged.Add(request.Tower.Value))
                    {
                        Log.E($"[Ammo] resupply skipped tower {request.Tower.Value}: no ammo source totalTowers={_getDebugTotalTowers()} emptyTowers={_getDebugTowersWithoutAmmo()} activeResupplyJobs={_getDebugActiveResupplyJobs()} armoryAmmo={_getDebugArmoryAvailableAmmo()}");
                        _notificationService?.Push(
                            key: $"ammo.no_source.{request.Tower.Value}",
                            title: "Khong co nguon tiep te ammo",
                            body: "Tower can ammo nhung hien chua co armory hoac kho phu hop de cap dan.",
                            severity: NotificationSeverity.Warning,
                            payload: default,
                            cooldownSeconds: 12f,
                            dedupeByKey: true);
                    }
                    return false;
                }

                if (_resupplyJobByTower.TryGetValue(request.Tower.Value, out var existingTowerJob))
                {
                    if (_jobBoard.TryGet(existingTowerJob, out var existing) && !AmmoService.IsTerminal(existing.Status))
                        return false;

                    _resupplyJobByTower.Remove(request.Tower.Value);
                }

                if (_resupplyJobByArmory.TryGetValue(source.Value, out var oldId))
                {
                    if (_jobBoard.TryGet(oldId, out var old) && !AmmoService.IsTerminal(old.Status))
                    {
                        if (old.Status == JobStatus.Created && request.Priority == AmmoRequestPriority.Urgent)
                        {
                            int currentTowerId = old.Tower.Value;
                            int urgentTowerId = request.Tower.Value;
                            if (urgentTowerId != 0 && urgentTowerId != currentTowerId)
                            {
                                int urgentNeed = towerState.AmmoCap - towerState.Ammo;
                                int urgentAmount = _getArmoryResupplyTripByLevel(sourceState.Level);
                                if (urgentAmount > urgentNeed) urgentAmount = urgentNeed;
                                if (urgentAmount > availableAmmo) urgentAmount = availableAmmo;

                                if (urgentAmount > 0)
                                {
                                    if (currentTowerId != 0)
                                        _resupplyJobByTower.Remove(currentTowerId);

                                    _consumeRequestAt(list, index);
                                    old.Tower = request.Tower;
                                    old.Amount = urgentAmount;
                                    _jobBoard.Update(old);
                                    _resupplyJobByArmory[source.Value] = old.Id;
                                    _resupplyJobByTower[urgentTowerId] = old.Id;
                                    _towerNoSourceLogged.Remove(request.Tower.Value);
                                    _towerNoJobLogged.Remove(request.Tower.Value);
                                    if (_debugAmmoLogs())
                                        Log.E($"[Ammo] resupply reprioritized source={source.Value} tower={urgentTowerId} amount={urgentAmount}");
                                    return true;
                                }
                            }
                        }

                        scanned++;
                        _rotateRequestToBack(list, index, request);
                        pick = _pickBestRequest(_resupplyJobByTower);
                        if (!pick.found)
                            return false;

                        list = pick.list;
                        index = pick.index;
                        request = pick.req;
                        towerState = pick.towerState;
                        continue;
                    }

                    _resupplyJobByArmory.Remove(source.Value);
                }

                int need = towerState.AmmoCap - towerState.Ammo;
                int amount = _getArmoryResupplyTripByLevel(sourceState.Level);
                if (amount > need) amount = need;
                if (amount > availableAmmo) amount = availableAmmo;
                if (amount <= 0)
                    return false;

                _consumeRequestAt(list, index);

                var job = new Job
                {
                    Archetype = JobArchetype.ResupplyTower,
                    Status = JobStatus.Created,
                    Workplace = source,
                    SourceBuilding = source,
                    Tower = request.Tower,
                    ResourceType = ResourceType.Ammo,
                    Amount = amount,
                    TargetCell = default,
                    CreatedAt = 0
                };

                var id = _jobBoard.Enqueue(job);
                _resupplyJobByArmory[source.Value] = id;
                _resupplyJobByTower[request.Tower.Value] = id;
                _towerNoSourceLogged.Remove(request.Tower.Value);
                _towerNoJobLogged.Remove(request.Tower.Value);
                _towerDeadlockLogged.Remove(request.Tower.Value);
                _notificationService?.Push(
                    key: $"ammo.resupply.queued.{request.Tower.Value}",
                    title: "Da tao lenh tiep te",
                    body: "Mot tower dang cho duoc tiep te ammo tu armory hoac kho.",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 12f,
                    dedupeByKey: true);
                if (_debugAmmoLogs())
                    Log.E($"[Ammo] resupply created source={source.Value} tower={request.Tower.Value} amount={amount} priority={request.Priority}");
                return true;
            }

            if (_towerNoJobLogged.Add(request.Tower.Value))
                Log.E($"[Ammo] Armory has ammo but no job created. tower={request.Tower.Value} totalTowers={_getDebugTotalTowers()} emptyTowers={_getDebugTowersWithoutAmmo()} activeResupplyJobs={_getDebugActiveResupplyJobs()} armoryAmmo={_getDebugArmoryAvailableAmmo()} pending={_getPendingRequests()}");
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

            EvaluateResupplySources(_worldIndex.Armories, towerState.Cell, 0, ref source, ref sourceState, ref availableAmmo, ref bestRank, ref bestDist, ref bestId);
            EvaluateResupplySources(_worldIndex.Warehouses, towerState.Cell, 1, ref source, ref sourceState, ref availableAmmo, ref bestRank, ref bestDist, ref bestId);

            return source.Value != 0;
        }

        internal void EvaluateResupplySources(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId)
        {
            if (candidates == null)
                return;

            var workplacesWithNpc = _getWorkplacesWithNpc();
            for (int i = 0; i < candidates.Count; i++)
            {
                var buildingId = candidates[i];
                if (!_worldState.Buildings.Exists(buildingId))
                    continue;

                var state = _worldState.Buildings.Get(buildingId);
                if (!state.IsConstructed)
                    continue;
                if (workplacesWithNpc == null || !workplacesWithNpc.Contains(buildingId.Value))
                    continue;
                if (!_storageService.CanStore(buildingId, ResourceType.Ammo))
                    continue;

                int ammo = _storageService.GetAmount(buildingId, ResourceType.Ammo);
                if (ammo <= 0)
                    continue;

                int dist = AmmoService.Manhattan(state.Anchor, targetCell);
                int idValue = buildingId.Value;
                if (rank < bestRank || (rank == bestRank && (dist < bestDist || (dist == bestDist && idValue < bestId))))
                {
                    bestRank = rank;
                    bestDist = dist;
                    bestId = idValue;
                    bestSource = buildingId;
                    bestState = state;
                    bestAmmo = ammo;
                }
            }
        }
    }
}
