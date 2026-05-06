using NUnit.Framework;
using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed partial class Regression_P0P1_Tests : RegressionTestBase
    {
        [Test]
        public void HaulBasicExecutor_PicksDestinationByPathCost_NotJustManhattan()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_warehouse_t1",
                IsWarehouse = true,
                SizeX = 1,
                SizeY = 1,
                CapWood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapFood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapStone = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapIron = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
            });
            data.Add(new BuildingDef
            {
                DefId = "bld_farmhouse_t1",
                IsProducer = true,
                SizeX = 1,
                SizeY = 1,
                CapFood = new StorageCapsByLevel { L1 = 100, L2 = 100, L3 = 100 },
            });

            var world = new WorldState();
            var grid = new GridMap(10, 10);
            var noti = new NotificationService(bus);
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.StorageService = new StorageService(world, data, bus);
            services.WorldIndex = new WorldIndexService(world, data);
            services.AgentMover = new GridAgentMoverLite(grid, data, null);
            services.Balance = new BalanceService(services, null);

            var producerId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_farmhouse_t1",
                Anchor = new CellPos(0, 0),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Food = 50,
            });

            var nearDest = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(0, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
            });

            var farRoadDest = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(9, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
            });

            services.WorldIndex.RebuildAll();

            grid.SetBuilding(new CellPos(0, 1), new BuildingId(101));
            grid.SetBuilding(new CellPos(0, 2), new BuildingId(102));
            grid.SetBuilding(new CellPos(1, 1), new BuildingId(103));
            grid.SetBuilding(new CellPos(1, 2), new BuildingId(104));
            grid.SetBuilding(new CellPos(1, 3), new BuildingId(105));
            grid.SetBuilding(new CellPos(1, 4), new BuildingId(106));
            grid.SetBuilding(new CellPos(1, 5), new BuildingId(107));
            grid.SetBuilding(new CellPos(1, 6), new BuildingId(108));
            grid.SetBuilding(new CellPos(1, 7), new BuildingId(109));
            for (int x = 0; x <= 9; x++) grid.SetRoad(new CellPos(x, 1), true);

            var exec = new HaulBasicExecutor(services);
            var npcId = new NpcId(1);
            var npc = new NpcState { Id = npcId, DefId = "npc_test", Cell = new CellPos(0, 0), Workplace = default, IsIdle = false };
            var job = new Job
            {
                Id = new JobId(1),
                Archetype = JobArchetype.HaulBasic,
                ResourceType = ResourceType.Food,
                Status = JobStatus.InProgress,
            };

            exec.Tick(npcId, ref npc, ref job, 0.1f);

            Assert.That(job.DestBuilding.Value, Is.EqualTo(farRoadDest.Value));
            Assert.That(job.SourceBuilding.Value, Is.EqualTo(producerId.Value));
        }

        [Test]
        public void BuildWorkExecutor_PicksStorageSourceByPathCost_NotJustManhattan()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_warehouse_t1",
                IsWarehouse = true,
                SizeX = 1,
                SizeY = 1,
                CapWood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapFood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapStone = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapIron = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
            });
            data.Add(new BuildingDef
            {
                DefId = "bld_builderhut_t1",
                SizeX = 1,
                SizeY = 1,
                WorkRoles = WorkRoleFlags.Build,
            });
            data.Add(new BuildingDef
            {
                DefId = "bld_house_t1",
                SizeX = 1,
                SizeY = 1,
            });

            var world = new WorldState();
            var grid = new GridMap(10, 10);
            var noti = new NotificationService(bus);
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.StorageService = new StorageService(world, data, bus);
            services.WorldIndex = new WorldIndexService(world, data);
            services.AgentMover = new GridAgentMoverLite(grid, data, null);
            services.Pathfinder = new NpcPathfinder(grid);
            services.Balance = null;

            var workplace = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_builderhut_t1",
                Anchor = new CellPos(5, 5),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
            });

            var nearSource = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(0, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 50,
            });

            var farRoadSource = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(9, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 50,
            });

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_house_t1",
                Anchor = new CellPos(9, 9),
                Rotation = Dir4.N,
                IsActive = true,
                RemainingCosts = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 10 } },
                WorkSecondsTotal = 1f,
            });

            services.WorldIndex.RebuildAll();

            // Make near source still reachable but expensive: block straight descent and force a long detour.
            grid.SetBuilding(new CellPos(0, 1), new BuildingId(201));
            grid.SetBuilding(new CellPos(0, 2), new BuildingId(202));
            grid.SetBuilding(new CellPos(1, 1), new BuildingId(203));
            grid.SetBuilding(new CellPos(1, 2), new BuildingId(204));
            grid.SetBuilding(new CellPos(1, 3), new BuildingId(205));
            grid.SetBuilding(new CellPos(1, 4), new BuildingId(206));
            grid.SetBuilding(new CellPos(1, 5), new BuildingId(207));
            // keep opening lower down so near source is not unreachable

            for (int x = 0; x <= 9; x++) grid.SetRoad(new CellPos(x, 1), true);

            var exec = new BuildWorkExecutor(services);
            var npcId = new NpcId(2);
            var npc = new NpcState { Id = npcId, DefId = "npc_test", Cell = new CellPos(0, 0), Workplace = workplace, IsIdle = false };
            var job = new Job
            {
                Id = new JobId(2),
                Archetype = JobArchetype.BuildWork,
                Site = siteId,
                Workplace = workplace,
                Status = JobStatus.InProgress,
            };

            exec.Tick(npcId, ref npc, ref job, 0.1f);

            Assert.That(job.SourceBuilding.Value, Is.EqualTo(farRoadSource.Value));
            Assert.That(job.ResourceType, Is.EqualTo(ResourceType.Wood));
        }

        [Test]
        public void BuildWorkExecutor_Cancels_WhenSiteEntryBecomesUnreachable()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_warehouse_t1",
                IsWarehouse = true,
                SizeX = 1,
                SizeY = 1,
                CapWood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
            });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", SizeX = 1, SizeY = 1, WorkRoles = WorkRoleFlags.Build });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 1, SizeY = 1 });

            var world = new WorldState();
            var grid = new GridMap(8, 8);
            var terrain = new TerrainMap(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.StorageService = new StorageService(world, data, bus);
            services.WorldIndex = new WorldIndexService(world, data);
            services.AgentMover = new GridAgentMoverLite(grid, data, null, terrain);
            services.Pathfinder = new NpcPathfinder(grid, terrain);

            var workplace = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_builderhut_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
            });

            var source = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(1, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 20,
            });

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_house_t1",
                Anchor = new CellPos(6, 6),
                Rotation = Dir4.N,
                IsActive = true,
                RemainingCosts = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 5 } },
                WorkSecondsTotal = 1f,
            });

            services.WorldIndex.RebuildAll();

            var blockedEntry = EntryCellUtil.GetApproachCellForSite(services, world.Sites.Get(siteId), new CellPos(1, 1));
            terrain.Set(blockedEntry, TerrainType.Sea);

            var exec = new BuildWorkExecutor(services);
            var npcId = new NpcId(7);
            var npc = new NpcState { Id = npcId, DefId = "npc_test", Cell = new CellPos(1, 1), Workplace = workplace, IsIdle = false };
            var job = new Job
            {
                Id = new JobId(7),
                Archetype = JobArchetype.BuildWork,
                Site = siteId,
                Workplace = workplace,
                SourceBuilding = source,
                ResourceType = ResourceType.Wood,
                Amount = 5,
                Status = JobStatus.InProgress,
            };

            var phaseField = typeof(BuildWorkExecutor).GetField("_phase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (phaseField?.GetValue(exec) is not Dictionary<int, byte> phases1)
                throw new Exception("phase field missing");
            phases1[job.Id.Value] = 1;

            var carryField = typeof(BuildWorkExecutor).GetField("_carry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (carryField?.GetValue(exec) is not Dictionary<int, int> carry1)
                throw new Exception("carry field missing");
            carry1[job.Id.Value] = 5;

            exec.Tick(npcId, ref npc, ref job, 0.1f);

            Assert.That(job.Status, Is.EqualTo(JobStatus.Cancelled));
        }

        // -------------------------
        // P0.2 BuildOrder rebuild from Sites after Load
        // (requires your added method: RebuildActivePlaceOrdersFromSitesAfterLoad)
        // -------------------------

    }
}
