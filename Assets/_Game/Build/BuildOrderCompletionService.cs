using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCompletionService
    {
        private readonly GameServices _s;
        private readonly Action<SiteId> _cancelTrackedJobsForSite;
        private readonly Action<int> _removeAutoRoadByOrder;

        public BuildOrderCompletionService(
            GameServices s,
            Action<SiteId> cancelTrackedJobsForSite,
            Action<int> removeAutoRoadByOrder)
        {
            _s = s;
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
            if (!_s.WorldState.Buildings.Exists(o.TargetBuilding))
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Site removed but building missing during place finalize: site={site.Id.Value}, building={o.TargetBuilding.Value}.");
                return false;
            }

            var b = _s.WorldState.Buildings.Get(o.TargetBuilding);
            if (b.IsConstructed)
            {
                EnsureConstructedBuildingIndexedAndOccupying(o.TargetBuilding, b, publishPlacedEvent: false, rebuildIndex: true);
                return true;
            }

            b.IsConstructed = true;

            if (b.MaxHP <= 0)
            {
                int mhp = 100;
                if (_s.DataRegistry.TryGetBuilding(b.DefId, out var placedDef) && placedDef != null)
                    mhp = Math.Max(1, placedDef.MaxHp);
                b.MaxHP = mhp;
            }

            if (b.HP <= 0)
                b.HP = b.MaxHP;

            _s.WorldState.Buildings.Set(o.TargetBuilding, b);
            EnsureConstructedBuildingIndexedAndOccupying(o.TargetBuilding, b, publishPlacedEvent: true, rebuildIndex: true);

            _s.NotificationService?.Push(
                key: $"BuildComplete_{o.TargetBuilding.Value}",
                title: "Construction",
                body: $"Completed: {o.BuildingDefId} (Lv {b.Level}) @ ({b.Anchor.X},{b.Anchor.Y})",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            TryAutosaveOnMilestone();
            return true;
        }

        private bool FinalizeUpgrade(ref BuildOrder o, in BuildSiteState site)
        {
            if (!_s.WorldState.Buildings.Exists(o.TargetBuilding))
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Site removed but building missing during upgrade finalize: site={site.Id.Value}, building={o.TargetBuilding.Value}.");
                return false;
            }

            var b = _s.WorldState.Buildings.Get(o.TargetBuilding);
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
                if (_s.DataRegistry.TryGetBuilding(toId, out var upgradedDef) && upgradedDef != null)
                    mhp = Math.Max(1, upgradedDef.MaxHp);
                b.MaxHP = mhp;
                b.HP = mhp;

                _s.WorldState.Buildings.Set(o.TargetBuilding, b);
            }

            SyncUpgradeTowerState(o.TargetBuilding, ref b);
            EnsureConstructedBuildingIndexedAndOccupying(o.TargetBuilding, b, publishPlacedEvent: false, rebuildIndex: true);

            if (!alreadyFinalized)
            {
                _s.EventBus.Publish(new BuildingUpgradedEvent(fromId, toId, o.TargetBuilding));

                _s.NotificationService?.Push(
                    key: $"UpgradeComplete_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Upgraded: {fromId} -> {toId} (Lv {b.Level})",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, toId),
                    cooldownSeconds: 0.25f,
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
            if (_s.WorldState.Sites.Exists(siteId))
                _s.WorldState.Sites.Destroy(siteId);

            if (publishEvent)
                _s.EventBus?.Publish(new WorldStateChangedEvent("BuildSite", siteId.Value));
        }

        private void ClearSiteFootprint(in BuildSiteState site)
        {
            var def = SafeGetBuildingDef(site.BuildingDefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _s.GridMap?.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));
        }

        private void EnsureConstructedBuildingIndexedAndOccupying(BuildingId buildingId, in BuildingState building, bool publishPlacedEvent, bool rebuildIndex = false)
        {
            var def = SafeGetBuildingDef(building.DefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _s.GridMap?.SetBuilding(new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy), buildingId);

            SyncTowerStateForConstructedBuilding(buildingId, building, def, w, h);

            try
            {
                if (rebuildIndex)
                    _s.WorldIndex?.OnBuildingDestroyed(buildingId);
                _s.WorldIndex?.OnBuildingCreated(buildingId);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Failed to refresh WorldIndex for finalized building {buildingId.Value}: {ex}");
            }

            if (publishPlacedEvent)
                _s.EventBus?.Publish(new BuildingPlacedEvent(building.DefId, buildingId));
            _s.EventBus?.Publish(new WorldStateChangedEvent("Building", buildingId.Value));
            _s.EventBus?.Publish(new RoadsDirtyEvent());
        }

        private void ValidateFinalizedState(BuildingId buildingId)
        {
            if (buildingId.Value == 0)
                return;

            if (_s.WorldState?.Buildings == null || !_s.WorldState.Buildings.Exists(buildingId))
            {
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Site removed but building missing: {buildingId.Value}.");
                return;
            }

            var building = _s.WorldState.Buildings.Get(buildingId);
            if (!building.IsConstructed)
                UnityEngine.Debug.LogError($"[BuildOrderCompletionService] Building exists but not constructed: {buildingId.Value} ({building.DefId}).");

            BuildOrderInvariantHelper.AssertBuildInvariant(_s.WorldState, _s.GridMap, _s.DataRegistry, _s.WorldIndex, buildingId);
        }

        private void SyncUpgradeTowerState(BuildingId buildingId, ref BuildingState building)
        {
            try
            {
                var def = SafeGetBuildingDef(building.DefId);
                if (def == null || _s.WorldState?.Towers == null)
                    return;

                int w = Math.Max(1, def.SizeX);
                int h = Math.Max(1, def.SizeY);
                var towerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

                TowerId found = default;
                foreach (var tid in _s.WorldState.Towers.Ids)
                {
                    if (!_s.WorldState.Towers.Exists(tid)) continue;
                    var ts0 = _s.WorldState.Towers.Get(tid);
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
                        _s.WorldState.Towers.Destroy(found);
                        _s.EventBus?.Publish(new WorldStateChangedEvent("Tower", found.Value));
                    }
                    building.Ammo = 0;
                    _s.WorldState.Buildings.Set(buildingId, building);
                    return;
                }

                int hpMax = Math.Max(1, def.MaxHp);
                int ammoMax = 0;
                if (_s.DataRegistry.TryGetTower(building.DefId, out var tdef) && tdef != null)
                {
                    hpMax = Math.Max(1, tdef.MaxHp);
                    ammoMax = Math.Max(0, tdef.AmmoMax);
                }

                if (found.Value != 0)
                {
                    var ts = _s.WorldState.Towers.Get(found);
                    ts.HpMax = hpMax;
                    ts.Hp = hpMax;
                    ts.AmmoCap = ammoMax;
                    if (ts.Ammo > ts.AmmoCap) ts.Ammo = ts.AmmoCap;
                    _s.WorldState.Towers.Set(found, ts);
                    _s.EventBus?.Publish(new WorldStateChangedEvent("Tower", found.Value));
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

                    var tid = _s.WorldState.Towers.Create(ts);
                    ts.Id = tid;
                    _s.WorldState.Towers.Set(tid, ts);
                    _s.EventBus?.Publish(new WorldStateChangedEvent("Tower", tid.Value));
                    building.Ammo = ammoMax;
                }

                _s.WorldState.Buildings.Set(buildingId, building);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BuildOrderCompletionService] Tower sync failed during upgrade finalize for building {buildingId.Value}: {ex.Message}");
            }
        }

        private void SyncTowerStateForConstructedBuilding(BuildingId buildingId, in BuildingState building, BuildingDef def, int w, int h)
        {
            if (def == null || !def.IsTower || _s.WorldState?.Towers == null)
                return;

            var towerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

            foreach (var tid0 in _s.WorldState.Towers.Ids)
            {
                if (!_s.WorldState.Towers.Exists(tid0)) continue;
                var ts0 = _s.WorldState.Towers.Get(tid0);
                if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y)
                    return;
            }

            int hpMax = Math.Max(1, def.MaxHp);
            int ammoMax = 0;

            if (_s.DataRegistry.TryGetTower(building.DefId, out var tdef) && tdef != null)
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

            var tid = _s.WorldState.Towers.Create(ts);
            ts.Id = tid;
            _s.WorldState.Towers.Set(tid, ts);
            _s.EventBus?.Publish(new WorldStateChangedEvent("Tower", tid.Value));

            var updated = building;
            updated.Ammo = ammoMax;
            _s.WorldState.Buildings.Set(buildingId, updated);
        }

        private bool TryGetActiveSite(SiteId siteId, out BuildSiteState site)
        {
            site = default;
            if (siteId.Value == 0 || _s.WorldState?.Sites == null || !_s.WorldState.Sites.Exists(siteId))
                return false;

            site = _s.WorldState.Sites.Get(siteId);
            return true;
        }

        private static bool CanFinalize(in BuildSiteState site)
            => site.IsReadyToWork && site.WorkSecondsDone + 1e-4f >= site.WorkSecondsTotal;

        private void TryAutosaveOnMilestone()
        {
            if (_s?.SaveService == null || _s?.WorldState == null || _s?.RunClock == null)
                return;

            int constructed = 0;
            foreach (var id in _s.WorldState.Buildings.Ids)
            {
                if (!_s.WorldState.Buildings.Exists(id)) continue;
                if (_s.WorldState.Buildings.Get(id).IsConstructed) constructed++;
            }

            if (constructed > 0 && constructed % 3 == 0)
            {
                var res = _s.SaveService.SaveRunToSlot(_s.WorldState, _s.RunClock, 1, autosave: true);
                if (res.Code == SaveResultCode.Ok)
                    _s.NotificationService?.Push("autosave.milestone", "Autosave", "Milestone autosave complete.", NotificationSeverity.Info, default, 3f, true);
            }
        }

        private BuildingDef SafeGetBuildingDef(string defId)
        {
            if (_s?.DataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return _s.DataRegistry.GetBuilding(defId); }
            catch { return null; }
        }
    }
}
