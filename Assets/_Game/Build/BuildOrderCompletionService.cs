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

            _cancelTrackedJobsForSite?.Invoke(o.Site);

            var site = _s.WorldState.Sites.Get(o.Site);
            var def = _s.DataRegistry.GetBuilding(o.BuildingDefId);

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));

            _s.WorldState.Sites.Destroy(o.Site);

            var b = _s.WorldState.Buildings.Get(o.TargetBuilding);
            b.IsConstructed = true;

            if (b.MaxHP <= 0)
            {
                int mhp = 100;
                if (_s.DataRegistry.TryGetBuilding(b.DefId, out var placedDef) && placedDef != null)
                    mhp = Math.Max(1, placedDef.MaxHp);
                b.MaxHP = mhp;
            }
            if (b.HP <= 0) b.HP = b.MaxHP;

            _s.WorldState.Buildings.Set(o.TargetBuilding, b);

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.SetBuilding(new CellPos(b.Anchor.X + dx, b.Anchor.Y + dy), o.TargetBuilding);

            if (def != null && def.IsTower && _s.WorldState?.Towers != null)
            {
                var towerCell = new CellPos(b.Anchor.X + (w / 2), b.Anchor.Y + (h / 2));

                bool exists = false;
                foreach (var tid0 in _s.WorldState.Towers.Ids)
                {
                    var ts0 = _s.WorldState.Towers.Get(tid0);
                    if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y) { exists = true; break; }
                }

                if (!exists)
                {
                    int hpMax = Math.Max(1, def.MaxHp);
                    int ammoMax = 0;

                    if (_s.DataRegistry.TryGetTower(b.DefId, out var tdef) && tdef != null)
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

                    b.Ammo = ammoMax;
                    _s.WorldState.Buildings.Set(o.TargetBuilding, b);
                }
            }

            try { _s.WorldIndex?.OnBuildingCreated(o.TargetBuilding); } catch { }

            _s.EventBus.Publish(new BuildingPlacedEvent(o.BuildingDefId, o.TargetBuilding));

            _s.NotificationService?.Push(
                key: $"BuildComplete_{o.TargetBuilding.Value}",
                title: "Construction",
                body: $"Completed: {o.BuildingDefId} (Lv {b.Level}) @ ({b.Anchor.X},{b.Anchor.Y})",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            o.Completed = true;
            _removeAutoRoadByOrder?.Invoke(o.OrderId);
        }

        public void CompleteUpgrade(ref BuildOrder o)
        {
            if (o.Completed) return;

            _cancelTrackedJobsForSite?.Invoke(o.Site);

            var site = _s.WorldState.Sites.Get(o.Site);
            _s.WorldState.Sites.Destroy(o.Site);

            var b = _s.WorldState.Buildings.Get(o.TargetBuilding);

            string fromId = site.FromDefId;
            string toId = o.BuildingDefId;

            b.DefId = toId;
            b.Level = Math.Max(1, site.TargetLevel);
            b.IsConstructed = true;

            int mhp = 100;
            if (_s.DataRegistry.TryGetBuilding(toId, out var upgradedDef) && upgradedDef != null)
                mhp = Math.Max(1, upgradedDef.MaxHp);
            b.MaxHP = mhp;
            b.HP = mhp;

            _s.WorldState.Buildings.Set(o.TargetBuilding, b);

            try
            {
                var def = _s.DataRegistry.GetBuilding(toId);
                if (def != null && def.IsTower && _s.WorldState?.Towers != null)
                {
                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);
                    var towerCell = new CellPos(b.Anchor.X + (w / 2), b.Anchor.Y + (h / 2));

                    TowerId found = default;
                    foreach (var tid in _s.WorldState.Towers.Ids)
                    {
                        var ts0 = _s.WorldState.Towers.Get(tid);
                        if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y) { found = tid; break; }
                    }

                    int hpMax = Math.Max(1, def.MaxHp);
                    int ammoMax = 0;
                    if (_s.DataRegistry.TryGetTower(toId, out var tdef) && tdef != null)
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

                        b.Ammo = ts.Ammo;
                        _s.WorldState.Buildings.Set(o.TargetBuilding, b);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BuildOrderCompletionService] Tower sync failed during upgrade '{fromId}' -> '{toId}' for building {o.TargetBuilding.Value}: {ex.Message}");
            }

            try { _s.WorldIndex?.OnBuildingDestroyed(o.TargetBuilding); } catch { }
            try { _s.WorldIndex?.OnBuildingCreated(o.TargetBuilding); } catch { }

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

            o.Completed = true;
        }
    }
}
