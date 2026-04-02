using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class WorldOps : IWorldOps
    {
        private readonly IWorldState _w;
        private readonly IEventBus _bus;
        private readonly IDataRegistry _data;
        private readonly IWorldIndex _index;
        private readonly IJobBoard _jobs;

        public WorldOps(IWorldState w, IEventBus bus, IDataRegistry data = null, IWorldIndex index = null, IJobBoard jobs = null)
        {
            _w = w;
            _bus = bus;
            _data = data;
            _index = index;
            _jobs = jobs;
        }

        public BuildingId CreateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            int level = 1;
            int maxHp = 1;
            bool isConstructed = true;
            int ammo = 0;

            if (_data != null && _data.TryGetBuilding(buildingDefId, out var def) && def != null)
            {
                level = Math.Max(1, def.BaseLevel);
                maxHp = Math.Max(1, def.MaxHp);

                if (def.IsTower && _data.TryGetTower(buildingDefId, out var towerDef) && towerDef != null)
                    ammo = Math.Max(0, towerDef.AmmoMax);
            }

            var st = new BuildingState
            {
                DefId = buildingDefId,
                Anchor = anchor,
                Rotation = rotation,
                Level = level,
                IsConstructed = isConstructed,
                HP = maxHp,
                MaxHP = maxHp,
                Ammo = ammo,
            };

            var id = _w.Buildings.Create(st);
            st.Id = id;
            _w.Buildings.Set(id, st);

            TryCreateTowerState(st);
            NotifyBuildingCreated(buildingDefId, id);
            return id;
        }

        public void DestroyBuilding(BuildingId id)
        {
            if (!_w.Buildings.Exists(id))
                return;

            var st = _w.Buildings.Get(id);
            string defId = st.DefId;

            ClearNpcWorkplaceReferences(id);
            CancelQueuedJobsForWorkplace(id);
            DestroyTowerStateForBuilding(st);

            _w.Buildings.Destroy(id);

            try { _index?.OnBuildingDestroyed(id); } catch { }
            _bus?.Publish(new BuildingDestroyedEvent(defId, id));
            _bus?.Publish(new WorldStateChangedEvent("Building", id.Value));
            _bus?.Publish(new RoadsDirtyEvent());
        }

        public NpcId CreateNpc(string npcDefId, CellPos spawn)
        {
            var st = new NpcState { DefId = npcDefId, Cell = spawn, Workplace = default, CurrentJob = default, IsIdle = true };
            var id = _w.Npcs.Create(st);
            st.Id = id;
            _w.Npcs.Set(id, st);
            _bus?.Publish(new WorldStateChangedEvent("Npc", id.Value));
            return id;
        }

        public void DestroyNpc(NpcId id)
        {
            _w.Npcs.Destroy(id);
            _bus?.Publish(new WorldStateChangedEvent("Npc", id.Value));
        }

        public EnemyId CreateEnemy(string enemyDefId, CellPos spawn, int lane)
        {
            int hp = 1;
            if (_data != null && _data.TryGetEnemy(enemyDefId, out var def) && def != null)
                hp = Math.Max(1, def.MaxHp);

            var st = new EnemyState { DefId = enemyDefId, Cell = spawn, Lane = lane, Hp = hp };
            var id = _w.Enemies.Create(st);
            st.Id = id;
            _w.Enemies.Set(id, st);
            _bus?.Publish(new WorldStateChangedEvent("Enemy", id.Value));
            return id;
        }

        public void DestroyEnemy(EnemyId id)
        {
            _w.Enemies.Destroy(id);
            _bus?.Publish(new WorldStateChangedEvent("Enemy", id.Value));
        }

        public SiteId CreateBuildSite(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            var st = new BuildSiteState { BuildingDefId = buildingDefId, Anchor = anchor, Rotation = rotation };
            var id = _w.Sites.Create(st);
            st.Id = id;
            _w.Sites.Set(id, st);
            _bus?.Publish(new WorldStateChangedEvent("BuildSite", id.Value));
            return id;
        }

        public void DestroyBuildSite(SiteId id)
        {
            _w.Sites.Destroy(id);
            _bus?.Publish(new WorldStateChangedEvent("BuildSite", id.Value));
        }

        private void NotifyBuildingCreated(string buildingDefId, BuildingId id)
        {
            try { _index?.OnBuildingCreated(id); } catch { }
            _bus?.Publish(new BuildingPlacedEvent(buildingDefId, id));
            _bus?.Publish(new WorldStateChangedEvent("Building", id.Value));
            _bus?.Publish(new RoadsDirtyEvent());
        }

        private void TryCreateTowerState(in BuildingState building)
        {
            if (_data == null) return;
            if (!_data.TryGetBuilding(building.DefId, out var def) || def == null || !def.IsTower) return;

            int hpMax = Math.Max(1, def.MaxHp);
            int ammoMax = 0;
            if (_data.TryGetTower(building.DefId, out var towerDef) && towerDef != null)
            {
                hpMax = Math.Max(1, towerDef.MaxHp);
                ammoMax = Math.Max(0, towerDef.AmmoMax);
            }

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);
            var towerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

            foreach (var tid in _w.Towers.Ids)
            {
                if (!_w.Towers.Exists(tid)) continue;
                var existing = _w.Towers.Get(tid);
                if (existing.Cell.X == towerCell.X && existing.Cell.Y == towerCell.Y)
                    return;
            }

            var tower = new TowerState
            {
                Cell = towerCell,
                Hp = hpMax,
                HpMax = hpMax,
                Ammo = ammoMax,
                AmmoCap = ammoMax,
            };

            var towerId = _w.Towers.Create(tower);
            tower.Id = towerId;
            _w.Towers.Set(towerId, tower);
        }

        private void DestroyTowerStateForBuilding(in BuildingState building)
        {
            if (_data == null) return;
            if (!_data.TryGetBuilding(building.DefId, out var def) || def == null || !def.IsTower) return;

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);
            var towerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

            foreach (var tid in _w.Towers.Ids)
            {
                if (!_w.Towers.Exists(tid)) continue;
                var existing = _w.Towers.Get(tid);
                if (existing.Cell.X == towerCell.X && existing.Cell.Y == towerCell.Y)
                {
                    _w.Towers.Destroy(tid);
                    return;
                }
            }
        }

        private void ClearNpcWorkplaceReferences(BuildingId buildingId)
        {
            foreach (var npcId in _w.Npcs.Ids)
            {
                if (!_w.Npcs.Exists(npcId)) continue;

                var npc = _w.Npcs.Get(npcId);
                if (npc.Workplace.Value != buildingId.Value)
                    continue;

                if (npc.CurrentJob.Value != 0)
                    _jobs?.Cancel(npc.CurrentJob);

                npc.Workplace = default;
                npc.CurrentJob = default;
                npc.IsIdle = true;
                _w.Npcs.Set(npcId, npc);
            }
        }

        private void CancelQueuedJobsForWorkplace(BuildingId workplace)
        {
            if (_jobs == null) return;

            while (_jobs.TryPeekForWorkplace(workplace, out var job))
                _jobs.Cancel(job.Id);
        }
    }
}
