using NUnit.Framework;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_WorldOpsTests
    {
        [Test]
        public void WorldOps_CreateBuilding_InitializesFromDef_AndPublishesSyncEvents()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var index = new WorldIndexService(world, data);
            var board = new JobBoard();

            data.Add(new BuildingDef
            {
                DefId = "bld_arrowtower_t1",
                SizeX = 2,
                SizeY = 2,
                BaseLevel = 3,
                MaxHp = 150,
                IsTower = true,
            });
            data.AddTower(new TowerDef { DefId = "bld_arrowtower_t1", MaxHp = 175, AmmoMax = 24 });

            BuildingPlacedEvent? placed = null;
            WorldStateChangedEvent? changed = null;
            int roadsDirtyCount = 0;
            bus.Subscribe<BuildingPlacedEvent>(e => placed = e);
            bus.Subscribe<WorldStateChangedEvent>(e => changed = e);
            bus.Subscribe<RoadsDirtyEvent>(_ => roadsDirtyCount++);

            var ops = new WorldOps(world, bus, data, index, board);

            var id = ops.CreateBuilding("bld_arrowtower_t1", new CellPos(8, 10), Dir4.E);

            Assert.That(world.Buildings.Exists(id), Is.True);
            var building = world.Buildings.Get(id);
            Assert.That(building.Id.Value, Is.EqualTo(id.Value));
            Assert.That(building.DefId, Is.EqualTo("bld_arrowtower_t1"));
            Assert.That(building.Level, Is.EqualTo(3));
            Assert.That(building.IsConstructed, Is.True);
            Assert.That(building.HP, Is.EqualTo(150));
            Assert.That(building.MaxHP, Is.EqualTo(150));
            Assert.That(building.Ammo, Is.EqualTo(24));
            Assert.That(index.Towers.Count, Is.EqualTo(0), "Tower buildings are stored in WorldState.Towers; WorldIndex tower list is rebuilt from the tower store separately.");

            TowerState createdTower = default;
            bool foundTower = false;
            foreach (var tid in world.Towers.Ids)
            {
                if (!world.Towers.Exists(tid)) continue;
                createdTower = world.Towers.Get(tid);
                foundTower = true;
                break;
            }

            Assert.That(foundTower, Is.True, "Tower buildings should create synced TowerState.");
            Assert.That(createdTower.Hp, Is.EqualTo(175));
            Assert.That(createdTower.HpMax, Is.EqualTo(175));
            Assert.That(createdTower.Ammo, Is.EqualTo(24));
            Assert.That(createdTower.AmmoCap, Is.EqualTo(24));
            Assert.That(placed.HasValue, Is.True);
            Assert.That(placed.Value.Building.Value, Is.EqualTo(id.Value));
            Assert.That(placed.Value.DefId, Is.EqualTo("bld_arrowtower_t1"));
            Assert.That(changed.HasValue, Is.True);
            Assert.That(changed.Value.EntityKind, Is.EqualTo("Building"));
            Assert.That(changed.Value.EntityId, Is.EqualTo(id.Value));
            Assert.That(roadsDirtyCount, Is.EqualTo(1));
        }

        [Test]
        public void WorldOps_DestroyBuilding_CleansNpcAndJobReferences_DestroysTower_AndPublishesEvents()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var index = new WorldIndexService(world, data);
            var board = new JobBoard();

            data.Add(new BuildingDef
            {
                DefId = "bld_lumbercamp_t1",
                SizeX = 2,
                SizeY = 2,
                BaseLevel = 1,
                MaxHp = 90,
                IsProducer = true,
                WorkRoles = WorkRoleFlags.Harvest,
            });
            data.Add(new BuildingDef
            {
                DefId = "bld_arrowtower_t1",
                SizeX = 2,
                SizeY = 2,
                BaseLevel = 1,
                MaxHp = 120,
                IsTower = true,
            });
            data.AddTower(new TowerDef { DefId = "bld_arrowtower_t1", MaxHp = 120, AmmoMax = 12 });

            var ops = new WorldOps(world, bus, data, index, board);
            var workplaceId = ops.CreateBuilding("bld_lumbercamp_t1", new CellPos(2, 2), Dir4.N);
            var towerBuildingId = ops.CreateBuilding("bld_arrowtower_t1", new CellPos(6, 6), Dir4.N);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_worker",
                Cell = new CellPos(1, 1),
                Workplace = workplaceId,
                CurrentJob = default,
                IsIdle = false,
            });

            var jobId = board.Enqueue(new Job
            {
                Workplace = workplaceId,
                Archetype = JobArchetype.Harvest,
                Status = JobStatus.Created,
                TargetCell = new CellPos(9, 9),
            });

            var npc = world.Npcs.Get(npcId);
            npc.CurrentJob = jobId;
            world.Npcs.Set(npcId, npc);

            BuildingDestroyedEvent? destroyed = null;
            WorldStateChangedEvent? changed = null;
            int roadsDirtyCount = 0;
            bus.Subscribe<BuildingDestroyedEvent>(e => destroyed = e);
            bus.Subscribe<WorldStateChangedEvent>(e => changed = e);
            bus.Subscribe<RoadsDirtyEvent>(_ => roadsDirtyCount++);

            ops.DestroyBuilding(workplaceId);

            Assert.That(world.Buildings.Exists(workplaceId), Is.False);
            var npcAfter = world.Npcs.Get(npcId);
            Assert.That(npcAfter.Workplace.Value, Is.EqualTo(0));
            Assert.That(npcAfter.CurrentJob.Value, Is.EqualTo(0));
            Assert.That(npcAfter.IsIdle, Is.True);
            Assert.That(board.TryGet(jobId, out var cancelledJob), Is.True);
            Assert.That(cancelledJob.Status, Is.EqualTo(JobStatus.Cancelled));
            Assert.That(destroyed.HasValue, Is.True);
            Assert.That(destroyed.Value.Building.Value, Is.EqualTo(workplaceId.Value));
            Assert.That(destroyed.Value.DefId, Is.EqualTo("bld_lumbercamp_t1"));
            Assert.That(changed.HasValue, Is.True);
            Assert.That(changed.Value.EntityKind, Is.EqualTo("Building"));
            Assert.That(changed.Value.EntityId, Is.EqualTo(workplaceId.Value));
            Assert.That(roadsDirtyCount, Is.EqualTo(1));

            int towerCountBeforeDestroyTower = 0;
            foreach (var tid in world.Towers.Ids)
                if (world.Towers.Exists(tid)) towerCountBeforeDestroyTower++;
            Assert.That(towerCountBeforeDestroyTower, Is.EqualTo(1));

            ops.DestroyBuilding(towerBuildingId);

            int towerCountAfterDestroyTower = 0;
            foreach (var tid in world.Towers.Ids)
                if (world.Towers.Exists(tid)) towerCountAfterDestroyTower++;
            Assert.That(towerCountAfterDestroyTower, Is.EqualTo(0), "Destroying a tower building must remove paired TowerState so systems do not hold stale references.");
        }
    }
}
