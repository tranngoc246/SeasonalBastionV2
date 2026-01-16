// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class WorldOps : IWorldOps
    {
        private readonly IWorldState _w;
        private readonly IEventBus _bus;

        public WorldOps(IWorldState w, IEventBus bus) { _w = w; _bus = bus; }

        public BuildingId CreateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            // TODO: create state from def
            var st = new BuildingState { DefId = buildingDefId, Anchor = anchor, Rotation = rotation };
            var id = _w.Buildings.Create(st);

            _bus.Publish(new BuildingPlacedEvent(buildingDefId, id));
            return id;
        }

        public void DestroyBuilding(BuildingId id)
        {
            _w.Buildings.Destroy(id);
            // TODO: publish destroyed event
        }

        public NpcId CreateNpc(string npcDefId, CellPos spawn)
        {
            var st = new NpcState { DefId = npcDefId, Cell = spawn };
            return _w.Npcs.Create(st);
        }

        public void DestroyNpc(NpcId id) => _w.Npcs.Destroy(id);

        public EnemyId CreateEnemy(string enemyDefId, CellPos spawn, int lane)
        {
            var st = new EnemyState { DefId = enemyDefId, Cell = spawn, Lane = lane };
            return _w.Enemies.Create(st);
        }

        public void DestroyEnemy(EnemyId id) => _w.Enemies.Destroy(id);

        public SiteId CreateBuildSite(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            var st = new BuildSiteState { BuildingDefId = buildingDefId, Anchor = anchor, Rotation = rotation };
            return _w.Sites.Create(st);
        }

        public void DestroyBuildSite(SiteId id) => _w.Sites.Destroy(id);
    }
}
