using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCompletionService
    {
        private readonly IWorldState _worldState;
        private readonly IGridMap _gridMap;
        private readonly IDataRegistry _dataRegistry;
        private readonly IWorldIndex _worldIndex;
        private readonly IEventBus _eventBus;
        private readonly INotificationService _notificationService;
        private readonly ISaveService _saveService;
        private readonly IRunClock _runClock;
        private readonly Action<SiteId> _cancelTrackedJobsForSite;
        private readonly Action<int> _removeAutoRoadByOrder;

        public BuildOrderCompletionService(
            IWorldState worldState,
            IGridMap gridMap,
            IDataRegistry dataRegistry,
            IWorldIndex worldIndex,
            IEventBus eventBus,
            INotificationService notificationService,
            ISaveService saveService,
            IRunClock runClock,
            Action<SiteId> cancelTrackedJobsForSite,
            Action<int> removeAutoRoadByOrder)
        {
            _worldState = worldState;
            _gridMap = gridMap;
            _dataRegistry = dataRegistry;
            _worldIndex = worldIndex;
            _eventBus = eventBus;
            _notificationService = notificationService;
            _saveService = saveService;
            _runClock = runClock;
            _cancelTrackedJobsForSite = cancelTrackedJobsForSite;
            _removeAutoRoadByOrder = removeAutoRoadByOrder;
        }

        public void CompletePlace(ref BuildOrder o)
        {
            if (o.Completed) return;
            if (!TryGetActiveSite(o.Site, out var site))
            {
                o.Completed = true;
                _removeAutoRoadByOrder?.Invoke(o.OrderId);
                return;
            }

            if (!CanFinalize(site))
                return;

            _cancelTrackedJobsForSite?.Invoke(o.Site);

            ClearSiteFootprint(site);

            if (!FinalizePlacedBuilding(ref o, site))
                return;

            DestroyBuildSite(o.Site, publishEvent: true);
            ValidateFinalizedState(o.TargetBuilding);
            o.Completed = true;
            _removeAutoRoadByOrder?.Invoke(o.OrderId);
        }

        public void CompleteUpgrade(ref BuildOrder o)
        {
            if (o.Completed) return;
            if (!TryGetActiveSite(o.Site, out var site))
            {
                o.Completed = true;
                return;
            }

            if (!CanFinalize(site))
                return;

            _cancelTrackedJobsForSite?.Invoke(o.Site);

            ClearSiteFootprint(site);

            if (!FinalizeUpgrade(ref o, site))
                return;

            DestroyBuildSite(o.Site, publishEvent: true);
            ValidateFinalizedState(o.TargetBuilding);
            o.Completed = true;
        }

        private bool FinalizePlacedBuilding(ref BuildOrder o, in BuildSiteState site)
        {
            if (!_worldState.Buildings.Exists(o.TargetBuilding))
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Site removed but building missing during place finalize: site={site.Id.Value}, building={o.TargetBuilding.Value}.");
                return false;
            }

            var b = _worldState.Buildings.Get(o.TargetBuilding);
            if (b.IsConstructed)
            {
                EnsureConstructedBuildingIndexedAndOccupying(o.TargetBuilding, b, publishPlacedEvent: false, rebuildIndex: true);
                return true;
            }

            b.IsConstructed = true;

            if (b.MaxHP <= 0)
            {
                int mhp = 100;
                if (_dataRegistry.TryGetBuilding(b.DefId, out var placedDef) && placedDef != null)
                    mhp = Math.Max(1, placedDef.MaxHp);
                b.MaxHP = mhp;
            }

            if (b.HP <= 0)
                b.HP = b.MaxHP;

            _worldState.Buildings.Set(o.TargetBuilding, b);
            EnsureConstructedBuildingIndexedAndOccupying(o.TargetBuilding, b, publishPlacedEvent: true, rebuildIndex: true);

            _notificationService?.Push(
                key: $"BuildComplete_{o.TargetBuilding.Value}",
                title: "Hoàn thành xây dựng",
                body: "Một công trình đã hoàn tất và sẵn sàng hoạt động.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.75f,
                dedupeByKey: true
            );

            TryAutosaveOnMilestone();
            return true;
        }

        private bool FinalizeUpgrade(ref BuildOrder o, in BuildSiteState site)
        {
            if (!_worldState.Buildings.Exists(o.TargetBuilding))
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Site removed but building missing during upgrade finalize: site={site.Id.Value}, building={o.TargetBuilding.Value}.");
                return false;
            }

            var b = _worldState.Buildings.Get(o.TargetBuilding);
            string fromId = string.IsNullOrWhiteSpace(site.FromDefId) ? b.DefId : site.FromDefId;
            string toId = o.BuildingDefId;

            bool alreadyFinalized = string.Equals(b.DefId, toId, StringComparison.Ordinal)
                                   && b.IsConstructed
                                   && b.Level == Math.Max(1, site.TargetLevel);

            if (!alreadyFinalized)
            {
                b.DefId = toId;
                b.Level = Math.Max(1, site.TargetLevel);
                b.IsConstructed = true;

                int mhp = 100;
                if (_dataRegistry.TryGetBuilding(toId, out var upgradedDef) && upgradedDef != null)
                    mhp = Math.Max(1, upgradedDef.MaxHp);
                b.MaxHP = mhp;
                b.HP = mhp;

                _worldState.Buildings.Set(o.TargetBuilding, b);
            }

            SyncUpgradeTowerState(o.TargetBuilding, ref b);
            EnsureConstructedBuildingIndexedAndOccupying(o.TargetBuilding, b, publishPlacedEvent: false, rebuildIndex: true);

            if (!alreadyFinalized)
            {
                _eventBus.Publish(new BuildingUpgradedEvent(fromId, toId, o.TargetBuilding));

                _notificationService?.Push(
                    key: $"UpgradeComplete_{o.TargetBuilding.Value}",
                    title: "Nâng cấp hoàn tất",
                    body: "Công trình đã được nâng cấp thành công.",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, toId),
                    cooldownSeconds: 0.75f,
                    dedupeByKey: true
                );
            }

            TryAutosaveOnMilestone();
            return true;
        }

        private void CleanupBuildSite(SiteId siteId, in BuildSiteState site, bool publishEvent)
        {
            ClearSiteFootprint(site);
            DestroyBuildSite(siteId, publishEvent);
        }

        private void DestroyBuildSite(SiteId siteId, bool publishEvent)
        {
            if (_worldState.Sites.Exists(siteId))
                _worldState.Sites.Destroy(siteId);

            if (publishEvent)
                _eventBus?.Publish(new WorldStateChangedEvent("BuildSite", siteId.Value));
        }

        private void ClearSiteFootprint(in BuildSiteState site)
        {
            var def = SafeGetBuildingDef(site.BuildingDefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _gridMap?.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));
        }

        private void EnsureConstructedBuildingIndexedAndOccupying(BuildingId buildingId, in BuildingState building, bool publishPlacedEvent, bool rebuildIndex = false)
        {
            var def = SafeGetBuildingDef(building.DefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _gridMap?.SetBuilding(new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy), buildingId);

            SyncTowerStateForConstructedBuilding(buildingId, building, def, w, h);

            try
            {
                if (rebuildIndex)
                    _worldIndex?.OnBuildingDestroyed(buildingId);
                _worldIndex?.OnBuildingCreated(buildingId);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Failed to refresh WorldIndex for finalized building {buildingId.Value}: {ex}");
            }

            if (publishPlacedEvent)
                _eventBus?.Publish(new BuildingPlacedEvent(building.DefId, buildingId));
            _eventBus?.Publish(new WorldStateChangedEvent("Building", buildingId.Value));
            _eventBus?.Publish(new RoadsDirtyEvent());
        }

        private void ValidateFinalizedState(BuildingId buildingId)
        {
            if (buildingId.Value == 0)
                return;

            if (_worldState?.Buildings == null || !_worldState.Buildings.Exists(buildingId))
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Site removed but building missing: {buildingId.Value}.");
                return;
            }

            var building = _worldState.Buildings.Get(buildingId);
            if (!building.IsConstructed)
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Building exists but not constructed: {buildingId.Value} ({building.DefId}).");

            BuildOrderInvariantHelper.AssertBuildInvariant(_worldState, _gridMap, _dataRegistry, _worldIndex, buildingId);
        }

        private void SyncUpgradeTowerState(BuildingId buildingId, ref BuildingState building)
        {
            try
            {
                var def = SafeGetBuildingDef(building.DefId);
                if (def == null || _worldState?.Towers == null)
                    return;

                int w = Math.Max(1, def.SizeX);
                int h = Math.Max(1, def.SizeY);
                var towerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

                TowerId found = default;
                foreach (var tid in _worldState.Towers.Ids)
                {
                    if (!_worldState.Towers.Exists(tid)) continue;
                    var ts0 = _worldState.Towers.Get(tid);
                    if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y)
                    {
                        found = tid;
                        break;
                    }
                }

                if (!def.IsTower)
                {
                    if (found.Value != 0)
                    {
                        _worldState.Towers.Destroy(found);
                        _eventBus?.Publish(new WorldStateChangedEvent("Tower", found.Value));
                    }
                    building.Ammo = 0;
                    _worldState.Buildings.Set(buildingId, building);
                    return;
                }

                int hpMax = Math.Max(1, def.MaxHp);
                int ammoMax = 0;
                if (_dataRegistry.TryGetTower(building.DefId, out var tdef) && tdef != null)
                {
                    hpMax = Math.Max(1, tdef.MaxHp);
                    ammoMax = Math.Max(0, tdef.AmmoMax);
                }

                if (found.Value != 0)
                {
                    var ts = _worldState.Towers.Get(found);
                    ts.HpMax = hpMax;
                    ts.Hp = hpMax;
                    ts.AmmoCap = ammoMax;
                    if (ts.Ammo > ts.AmmoCap) ts.Ammo = ts.AmmoCap;
                    _worldState.Towers.Set(found, ts);
                    _eventBus?.Publish(new WorldStateChangedEvent("Tower", found.Value));
                    building.Ammo = ts.Ammo;
                }
                else
                {
                    var ts = new TowerState
                    {
                        Cell = towerCell,
                        Hp = hpMax,
                        HpMax = hpMax,
                        Ammo = ammoMax,
                        AmmoCap = ammoMax,
                    };

                    var tid = _worldState.Towers.Create(ts);
                    ts.Id = tid;
                    _worldState.Towers.Set(tid, ts);
                    _eventBus?.Publish(new WorldStateChangedEvent("Tower", tid.Value));
                    building.Ammo = ammoMax;
                }

                _worldState.Buildings.Set(buildingId, building);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BuildOrderCompletionService] Tower sync failed during upgrade finalize for building {buildingId.Value}: {ex.Message}");
            }
        }

        private void SyncTowerStateForConstructedBuilding(BuildingId buildingId, in BuildingState building, BuildingDef def, int w, int h)
        {
            if (def == null || !def.IsTower || _worldState?.Towers == null)
                return;

            var towerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

            foreach (var tid0 in _worldState.Towers.Ids)
            {
                if (!_worldState.Towers.Exists(tid0)) continue;
                var ts0 = _worldState.Towers.Get(tid0);
                if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y)
                    return;
            }

            int hpMax = Math.Max(1, def.MaxHp);
            int ammoMax = 0;

            if (_dataRegistry.TryGetTower(building.DefId, out var tdef) && tdef != null)
            {
                hpMax = Math.Max(1, tdef.MaxHp);
                ammoMax = Math.Max(0, tdef.AmmoMax);
            }

            var ts = new TowerState
            {
                Cell = towerCell,
                Hp = hpMax,
                HpMax = hpMax,
                Ammo = ammoMax,
                AmmoCap = ammoMax,
            };

            var tid = _worldState.Towers.Create(ts);
            ts.Id = tid;
            _worldState.Towers.Set(tid, ts);
            _eventBus?.Publish(new WorldStateChangedEvent("Tower", tid.Value));

            var updated = building;
            updated.Ammo = ammoMax;
            _worldState.Buildings.Set(buildingId, updated);
        }

        private bool TryGetActiveSite(SiteId siteId, out BuildSiteState site)
        {
            site = default;
            if (siteId.Value == 0 || _worldState?.Sites == null || !_worldState.Sites.Exists(siteId))
                return false;

            site = _worldState.Sites.Get(siteId);
            return true;
        }

        private static bool CanFinalize(in BuildSiteState site)
            => site.IsReadyToWork && site.WorkSecondsDone + 1e-4f >= site.WorkSecondsTotal;

        private void TryAutosaveOnMilestone()
        {
            if (_saveService == null || _worldState == null || _runClock == null)
                return;

            int constructed = 0;
            foreach (var id in _worldState.Buildings.Ids)
            {
                if (!_worldState.Buildings.Exists(id)) continue;
                if (_worldState.Buildings.Get(id).IsConstructed) constructed++;
            }

            if (constructed > 0 && constructed % 3 == 0)
            {
                var res = _saveService.SaveRunToSlot(_worldState, _runClock, 1, autosave: true);
                if (res.Code == SaveResultCode.Ok)
                    _notificationService?.Push("autosave.milestone", "Tự động lưu", "Đã tự động lưu tại một mốc tiến trình quan trọng.", NotificationSeverity.Info, default, 30f, true);
            }
        }

        private BuildingDef SafeGetBuildingDef(string defId)
        {
            if (_dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return _dataRegistry.GetBuilding(defId); }
            catch { return null; }
        }
    }
}
