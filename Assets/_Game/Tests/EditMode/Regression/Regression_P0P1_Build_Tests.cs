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
        public void BuildOrderService_RebuildFromSites_CreatesActivePlaceOrder()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            var clock = new FakeRunClock();
            var outcome = new FakeRunOutcomeService();

            var world = new WorldState();

            // Create placeholder building (not constructed)
            var bId = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_test_placeholder",
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = false,
                HP = 10,
                MaxHP = 10
            });

            // Ensure state.Id is set
            var bs = world.Buildings.Get(bId);
            bs.Id = bId;
            world.Buildings.Set(bId, bs);

            // Create active site matching this placeholder
            var sId = world.Sites.Create(new BuildSiteState
            {
                Id = default,
                BuildingDefId = "bld_test_placeholder",
                TargetLevel = 1,
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 2f,
                WorkSecondsTotal = 10f,
                DeliveredSoFar = null,
                RemainingCosts = null
            });

            var site = world.Sites.Get(sId);
            site.Id = sId;
            world.Sites.Set(sId, site);

            var services = MakeServices(bus, data, noti, clock, outcome, world: world);

            var bos = new BuildOrderService(services);

            // Call your P0.2 method
            int created = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();

            Assert.That(created, Is.EqualTo(1), "Expected exactly 1 order rebuilt from 1 active site");

            Assert.That(bos.TryGet(1, out var order), Is.True, "Expected orderId=1 to exist after rebuild");
            Assert.That(order.Kind, Is.EqualTo(BuildOrderKind.PlaceNew));
            Assert.That(order.TargetBuilding.Value, Is.EqualTo(bId.Value));
            Assert.That(order.Site.Value, Is.EqualTo(sId.Value));
            Assert.That(order.WorkSecondsRequired, Is.EqualTo(10f));
            Assert.That(order.WorkSecondsDone, Is.EqualTo(2f));
        }

        [Test]
        public void BuildOrderService_CreatePlaceOrder_ReturnsZero_WhenResourcesAreInsufficient()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_costly",
                SizeX = 1,
                SizeY = 1,
                BaseLevel = 1,
                MaxHp = 10,
                BuildCostsL1 = new[] { new CostDef { Resource = ResourceType.Wood, Amount = 5 } }
            });

            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var placement = new FakePlacementService();
            var storage = new FakeStorageService();
            storage.SetAmount(new BuildingId(1), ResourceType.Wood, 3);

            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid, placement: placement);
            services.StorageService = storage;

            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int orderId = bos.CreatePlaceOrder("bld_costly", new CellPos(4, 4), Dir4.N);

            Assert.That(orderId, Is.EqualTo(0));
            Assert.That(world.Buildings.Count, Is.EqualTo(0), "Should not create placeholder building when resources are insufficient.");
            Assert.That(world.Sites.Count, Is.EqualTo(0), "Should not create build site when resources are insufficient.");

            var inbox = noti.GetInbox();
            Assert.That(inbox.Count, Is.EqualTo(1));
            Assert.That(inbox[0].Title, Is.EqualTo("Thiếu tài nguyên"));
            Assert.That(inbox[0].Body, Is.EqualTo("Cần 5 Wood, hiện chỉ có 3."));
        }

        [Test]
        public void BuildOrderService_CreatePlaceOrder_ReturnsZero_WhenPlacementIsInvalid()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_test_invalid_place",
                SizeX = 1,
                SizeY = 1,
                BaseLevel = 1,
                MaxHp = 10
            });

            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var placement = new FakePlacementService
            {
                NextResult = new PlacementResult(false, PlacementFailReason.NoRoadConnection, new CellPos(5, 6))
            };

            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid, placement: placement);
            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int orderId = bos.CreatePlaceOrder("bld_test_invalid_place", new CellPos(5, 5), Dir4.N);

            Assert.That(orderId, Is.EqualTo(0));
            Assert.That(placement.ValidateCalls, Is.EqualTo(1));
            Assert.That(world.Buildings.Count, Is.EqualTo(0));
            Assert.That(world.Sites.Count, Is.EqualTo(0));

            var inbox = noti.GetInbox();
            Assert.That(inbox.Count, Is.EqualTo(1));
            Assert.That(inbox[0].Title, Is.EqualTo("Không thể đặt công trình"));
            Assert.That(inbox[0].Body, Is.EqualTo("Công trình cần kết nối với đường."));
        }

        [Test]
        public void BuildOrderService_CreateUpgradeOrder_ReturnsZero_WhenUpgradeIsLocked()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 150, IsHQ = true });
            data.AddNode(new BuildableNodeDef { Id = "bld_hq_t2", Level = 2, Placeable = false });
            data.AddUpgradeEdge(new UpgradeEdgeDef
            {
                Id = "hq_t1_to_t2",
                From = "bld_hq_t1",
                To = "bld_hq_t2",
                WorkChunks = 2,
                RequiresUnlocked = "unlock_hq_t2"
            });

            var world = new WorldState();
            var buildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);

            var unlocks = new FakeUnlockService();
            unlocks.Unlock("bld_hq_t1");

            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(12, 12));
            services.UnlockService = unlocks;

            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int orderId = bos.CreateUpgradeOrder(buildingId);

            Assert.That(orderId, Is.EqualTo(0));
            Assert.That(world.Sites.Count, Is.EqualTo(0), "Locked upgrade should not create upgrade site.");
            Assert.That(bos.TryGet(1, out _), Is.False);

            var inbox = noti.GetInbox();
            Assert.That(inbox.Count, Is.EqualTo(1));
            Assert.That(inbox[0].Title, Is.EqualTo("Chưa mở khóa"));
            Assert.That(inbox[0].Body, Is.EqualTo("Nâng cấp này chưa khả dụng ở thời điểm hiện tại."));
        }

        [Test]
        public void BuildOrderService_CreateUpgradeOrder_ReturnsZero_WhenBuildingEntryIsUnreachable()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 150, IsHQ = true });
            data.AddNode(new BuildableNodeDef { Id = "bld_hq_t2", Level = 2, Placeable = false });
            data.AddUpgradeEdge(new UpgradeEdgeDef
            {
                Id = "hq_t1_to_t2",
                From = "bld_hq_t1",
                To = "bld_hq_t2",
                WorkChunks = 2
            });

            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var terrain = new TerrainMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var buildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var building = world.Buildings.Get(buildingId); building.Id = buildingId; world.Buildings.Set(buildingId, building);

            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.TerrainMap = terrain;
            services.Pathfinder = new NpcPathfinder(grid, terrain);
            var blockedEntry = EntryCellUtil.GetApproachCellForBuilding(services, building, building.Anchor);
            terrain.Set(blockedEntry, TerrainType.Sea);

            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int orderId = bos.CreateUpgradeOrder(buildingId);

            Assert.That(orderId, Is.EqualTo(0));
            Assert.That(world.Sites.Count, Is.EqualTo(0));
            Assert.That(bos.TryGet(1, out _), Is.False);

            var inbox = noti.GetInbox();
            Assert.That(inbox.Count, Is.EqualTo(1));
            Assert.That(inbox[0].Title, Is.EqualTo("Không thể nâng cấp"));
        }

        [Test]
        public void BuildOrderService_CreateRepairOrder_ReturnsZero_WhenBuildingEntryIsUnreachable()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });

            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var terrain = new TerrainMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var buildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 50,
                MaxHP = 100
            });
            var building = world.Buildings.Get(buildingId); building.Id = buildingId; world.Buildings.Set(buildingId, building);

            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.TerrainMap = terrain;
            services.Pathfinder = new NpcPathfinder(grid, terrain);
            var blockedEntry = EntryCellUtil.GetApproachCellForBuilding(services, building, building.Anchor);
            terrain.Set(blockedEntry, TerrainType.Sea);

            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int orderId = bos.CreateRepairOrder(buildingId);

            Assert.That(orderId, Is.EqualTo(0));
            Assert.That(bos.TryGet(1, out _), Is.False);

            var inbox = noti.GetInbox();
            Assert.That(inbox.Count, Is.EqualTo(1));
            Assert.That(inbox[0].Title, Is.EqualTo("Không thể sửa chữa"));
        }

        [Test]
        public void JobBoard_ArmoryFilteredPeek_PrioritizesResupplyTower()
        {
            var board = new JobBoard();
            var workplace = new BuildingId(10);

            board.Enqueue(new Job { Workplace = workplace, Archetype = JobArchetype.HaulToForge, Status = JobStatus.Created });
            board.Enqueue(new Job { Workplace = workplace, Archetype = JobArchetype.HaulAmmoToArmory, Status = JobStatus.Created });
            board.Enqueue(new Job { Workplace = workplace, Archetype = JobArchetype.ResupplyTower, Status = JobStatus.Created });

            bool ok = board.TryPeekForWorkplaceFiltered(workplace, WorkRoleFlags.Armory, out var peek);

            Assert.That(ok, Is.True);
            Assert.That(peek.Archetype, Is.EqualTo(JobArchetype.ResupplyTower));
        }

        [Test]
        public void JobScheduler_AnyHarvestProducerHasAmount_AcceptsTieredDefIds()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            var world = new WorldState();
            var claims = new ClaimService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            var exec = new JobExecutorRegistry(services);

            var bId = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_lumbercamp_t2",
                Anchor = new CellPos(5, 5),
                Rotation = Dir4.N,
                Level = 2,
                IsConstructed = true,
                Wood = 7,
                HP = 10,
                MaxHP = 10
            });
            var b = world.Buildings.Get(bId);
            b.Id = bId;
            world.Buildings.Set(bId, b);

            var scheduler = new JobScheduler(services, world, board, claims, exec, bus, data, noti);

            var mi = typeof(JobScheduler).GetMethod("AnyHarvestProducerHasAmount", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(mi, Is.Not.Null);

            bool hasWood = (bool)mi.Invoke(scheduler, new object[] { ResourceType.Wood });
            bool hasFood = (bool)mi.Invoke(scheduler, new object[] { ResourceType.Food });

            Assert.That(hasWood, Is.True);
            Assert.That(hasFood, Is.False);
        }

        [Test]
        public void JobEnqueueService_Haul_DoesNotDuplicateActiveJobForSameWorkplaceAndType()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_warehouse", WorkRoles = WorkRoleFlags.HaulBasic, IsWarehouse = true, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_warehouse_t1", WorkRoles = WorkRoleFlags.HaulBasic, IsWarehouse = true, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp", WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp_t1", WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });

            var world = new WorldState();
            var grid = new GridMap(20, 20);
            for (int x = 0; x <= 10; x++) grid.SetRoad(new CellPos(x, 8), true);

            var board = new JobBoard();
            var cleanup = new JobStateCleanupService(new ClaimService());
            var workplacePolicy = new JobWorkplacePolicy(data);
            var resourcePolicy = new ResourceLogisticsPolicy();
            var services = MakeServices(new TestEventBus(), data, new NotificationService(new TestEventBus()), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            var worldIndex = new WorldIndexService(world, data);
            services.WorldIndex = worldIndex;
            services.StorageService = new StorageService(world, data, services.EventBus);
            services.ResourceFlowService = new ResourceFlowService(world, services.WorldIndex, services.StorageService, services.Pathfinder);
            var enqueue = new JobEnqueueService(services, world, board, workplacePolicy, resourcePolicy, cleanup, new FakeHarvestTargetSelector(new CellPos(1, 1)));

            var srcId = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_lumbercamp_t1",
                Anchor = new CellPos(2, 8),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 7,
                HP = 10,
                MaxHP = 10
            });
            var src = world.Buildings.Get(srcId); src.Id = srcId; world.Buildings.Set(srcId, src);

            var wid = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(8, 8),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 0,
                HP = 10,
                MaxHP = 10
            });
            var w = world.Buildings.Get(wid);
            w.Id = wid;
            world.Buildings.Set(wid, w);

            worldIndex.RebuildAll();

            var buildingIds = new List<BuildingId> { wid };
            var workplacesWithNpc = new HashSet<int> { wid.Value };
            var haulMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHaulJobsIfNeeded(buildingIds, workplacesWithNpc, haulMap, rt => rt == ResourceType.Wood);
            enqueue.EnqueueHaulJobsIfNeeded(buildingIds, workplacesWithNpc, haulMap, rt => rt == ResourceType.Wood);

            Assert.That(haulMap.Count, Is.EqualTo(1));
            Assert.That(board.CountActiveJobs(JobArchetype.HaulBasic), Is.EqualTo(1));
        }

        [Test]
        public void JobEnqueueService_Haul_DoesNotCreateJob_WhenNoReachableSourceExists()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_warehouse", WorkRoles = WorkRoleFlags.HaulBasic, IsWarehouse = true, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_warehouse_t1", WorkRoles = WorkRoleFlags.HaulBasic, IsWarehouse = true, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp", WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp_t1", WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });

            var world = new WorldState();
            var grid = new GridMap(20, 20);
            for (int x = 0; x <= 4; x++) grid.SetRoad(new CellPos(x, 8), true);
            for (int x = 10; x <= 14; x++) grid.SetRoad(new CellPos(x, 8), true);
            var terrain = new TerrainMap(20, 20);
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);
            for (int y = 0; y < 20; y++)
                terrain.Set(new CellPos(7, y), TerrainType.Sea);

            var board = new JobBoard();
            var cleanup = new JobStateCleanupService(new ClaimService());
            var workplacePolicy = new JobWorkplacePolicy(data);
            var resourcePolicy = new ResourceLogisticsPolicy();
            var services = MakeServices(new TestEventBus(), data, new NotificationService(new TestEventBus()), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.TerrainMap = terrain;
            services.Pathfinder = new NpcPathfinder(grid, terrain);
            var worldIndex = new WorldIndexService(world, data);
            services.WorldIndex = worldIndex;
            services.StorageService = new StorageService(world, data, services.EventBus);
            services.ResourceFlowService = new ResourceFlowService(world, services.WorldIndex, services.StorageService, services.Pathfinder);
            var enqueue = new JobEnqueueService(services, world, board, workplacePolicy, resourcePolicy, cleanup, new FakeHarvestTargetSelector(new CellPos(1, 1)));

            var srcId = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_lumbercamp_t1",
                Anchor = new CellPos(12, 8),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 7,
                HP = 10,
                MaxHP = 10
            });
            var src = world.Buildings.Get(srcId); src.Id = srcId; world.Buildings.Set(srcId, src);

            var wid = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(2, 8),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = 0,
                HP = 10,
                MaxHP = 10
            });
            var w = world.Buildings.Get(wid); w.Id = wid; world.Buildings.Set(wid, w);

            worldIndex.RebuildAll();

            var buildingIds = new List<BuildingId> { wid };
            var workplacesWithNpc = new HashSet<int> { wid.Value };
            var haulMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHaulJobsIfNeeded(buildingIds, workplacesWithNpc, haulMap, rt => rt == ResourceType.Wood);

            Assert.That(haulMap.Count, Is.EqualTo(0));
            Assert.That(board.CountActiveJobs(JobArchetype.HaulBasic), Is.EqualTo(0));
        }

        [Test]
        public void JobAssignmentService_TryAssign_AssignsOnlyAllowedRoleFilteredJob()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_builderhut", WorkRoles = WorkRoleFlags.Build });

            var world = new WorldState();
            var board = new JobBoard();
            var workplacePolicy = new JobWorkplacePolicy(data);
            var notificationPolicy = new JobNotificationPolicy(noti);
            var assign = new JobAssignmentService(world, board, workplacePolicy, notificationPolicy);

            var workplaceId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_builderhut_t1",
                Anchor = new CellPos(6, 6),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 50,
                MaxHP = 50
            });
            var workplace = world.Buildings.Get(workplaceId); workplace.Id = workplaceId; world.Buildings.Set(workplaceId, workplace);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(6, 5),
                Workplace = workplaceId,
                IsIdle = true
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            board.Enqueue(new Job { Workplace = workplaceId, Archetype = JobArchetype.HaulBasic, Status = JobStatus.Created, ResourceType = ResourceType.Wood });
            var allowedJobId = board.Enqueue(new Job { Workplace = workplaceId, Archetype = JobArchetype.BuildWork, Status = JobStatus.Created, TargetCell = new CellPos(6, 6) });

            bool ok = assign.TryAssign(npcId, ref npc, _ => true);

            Assert.That(ok, Is.True);
            Assert.That(npc.CurrentJob.Value, Is.EqualTo(allowedJobId.Value));
            Assert.That(npc.IsIdle, Is.False);
            Assert.That(board.TryGet(allowedJobId, out var claimed), Is.True);
            Assert.That(claimed.Status, Is.EqualTo(JobStatus.InProgress));
            Assert.That(claimed.ClaimedBy.Value, Is.EqualTo(npcId.Value));
        }

        [Test]
        public void JobAssignmentService_TryAssign_ReturnsFalse_WhenWorkplaceRolesAreInvalid()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_decoration", WorkRoles = WorkRoleFlags.None });

            var world = new WorldState();
            var board = new JobBoard();
            var workplacePolicy = new JobWorkplacePolicy(data);
            var notificationPolicy = new JobNotificationPolicy(noti);
            var assign = new JobAssignmentService(world, board, workplacePolicy, notificationPolicy);

            var workplaceId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_decoration_t1",
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 10,
                MaxHP = 10
            });
            var workplace = world.Buildings.Get(workplaceId); workplace.Id = workplaceId; world.Buildings.Set(workplaceId, workplace);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(3, 2),
                Workplace = workplaceId,
                IsIdle = true
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            board.Enqueue(new Job { Workplace = workplaceId, Archetype = JobArchetype.HaulBasic, Status = JobStatus.Created, ResourceType = ResourceType.Wood });

            bool ok = assign.TryAssign(npcId, ref npc, _ => true);

            Assert.That(ok, Is.False);
            Assert.That(npc.CurrentJob.Value, Is.EqualTo(0));
            Assert.That(npc.IsIdle, Is.True);

            var inbox = noti.GetInbox();
            Assert.That(inbox.Count, Is.EqualTo(0));
        }

        [Test]
        public void JobAssignmentService_TryAssign_ReturnsFalse_ForUnassignedNpc()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_farmhouse", WorkRoles = WorkRoleFlags.Harvest });

            var world = new WorldState();
            var board = new JobBoard();
            var workplacePolicy = new JobWorkplacePolicy(data);
            var notificationPolicy = new JobNotificationPolicy(noti);
            var assign = new JobAssignmentService(world, board, workplacePolicy, notificationPolicy);

            var workplaceId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_farmhouse_t1",
                Anchor = new CellPos(4, 4),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 20,
                MaxHP = 20
            });
            var workplace = world.Buildings.Get(workplaceId); workplace.Id = workplaceId; world.Buildings.Set(workplaceId, workplace);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(4, 3),
                Workplace = default,
                IsIdle = true
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            board.Enqueue(new Job { Workplace = workplaceId, Archetype = JobArchetype.Harvest, Status = JobStatus.Created });

            bool ok = assign.TryAssign(npcId, ref npc, _ => true);

            Assert.That(ok, Is.False);
            Assert.That(npc.CurrentJob.Value, Is.EqualTo(0));
            Assert.That(npc.IsIdle, Is.True);
        }

        [Test]
        public void WorkforceAssignmentRules_CanAssignToTarget_RespectsSlotCap_AndExcludeNpc()
        {
            var world = new WorldState();
            var workplaceId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_farmhouse_t1",
                Anchor = new CellPos(10, 10),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 20,
                MaxHP = 20
            });
            var workplace = world.Buildings.Get(workplaceId); workplace.Id = workplaceId; world.Buildings.Set(workplaceId, workplace);

            var def = new BuildingDef { DefId = "bld_farmhouse", WorkRoles = WorkRoleFlags.Harvest };

            var npcA = world.Npcs.Create(new NpcState { DefId = "npc_a", Workplace = workplaceId, IsIdle = true });
            var a = world.Npcs.Get(npcA); a.Id = npcA; world.Npcs.Set(npcA, a);

            var npcB = world.Npcs.Create(new NpcState { DefId = "npc_b", Workplace = default, IsIdle = true });
            var b = world.Npcs.Get(npcB); b.Id = npcB; world.Npcs.Set(npcB, b);

            bool canAssignOther = WorkforceAssignmentRules.CanAssignToTarget(world, workplace, def, workplaceId, npcB, out var reasonOther);
            bool canKeepCurrent = WorkforceAssignmentRules.CanAssignToTarget(world, workplace, def, workplaceId, npcA, out var reasonCurrent);

            Assert.That(WorkforceAssignmentRules.GetMaxAssignedFor(def, workplace.Level), Is.EqualTo(1));
            Assert.That(canAssignOther, Is.False);
            Assert.That(reasonOther, Is.EqualTo("Đã đủ worker (1/1)."));
            Assert.That(canKeepCurrent, Is.True);
        }

        [Test]
        public void JobStateCleanupService_CleanupNpcJob_ClearsCurrentJob_SetsIdle_AndReleasesClaims()
        {
            var claims = new ClaimService();
            var cleanup = new JobStateCleanupService(claims);
            var npcId = new NpcId(7);
            var claimA = new ClaimKey(ClaimKind.StorageSource, 101, (int)ResourceType.Wood);
            var claimB = new ClaimKey(ClaimKind.BuildSite, 202, 0);

            claims.TryAcquire(claimA, npcId);
            claims.TryAcquire(claimB, npcId);

            var npc = new NpcState
            {
                CurrentJob = new JobId(55),
                IsIdle = false
            };

            cleanup.CleanupNpcJob(npcId, ref npc);

            Assert.That(npc.CurrentJob.Value, Is.EqualTo(0));
            Assert.That(npc.IsIdle, Is.True);
            Assert.That(claims.IsOwnedBy(claimA, npcId), Is.False);
            Assert.That(claims.IsOwnedBy(claimB, npcId), Is.False);
            Assert.That(claims.ActiveClaimsCount, Is.EqualTo(0));
        }

        [Test]
        public void JobExecutionService_TickCurrentJobs_CleansUpNpcState_WhenCurrentJobIsMissing()
        {
            var world = new WorldState();
            var board = new JobBoard();
            var claims = new ClaimService();
            var cleanup = new JobStateCleanupService(claims);
            var services = MakeServices(new TestEventBus(), new TestDataRegistry(), new NotificationService(new TestEventBus()), new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            var registry = new JobExecutorRegistry(services);
            var exec = new JobExecutionService(services, world, board, registry, cleanup);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(2, 2),
                CurrentJob = new JobId(999),
                IsIdle = false
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            var claim = new ClaimKey(ClaimKind.StorageSource, 77, (int)ResourceType.Wood);
            claims.TryAcquire(claim, npcId);

            exec.TickCurrentJobs(new List<NpcId> { npcId }, 0.1f);

            var after = world.Npcs.Get(npcId);
            Assert.That(after.CurrentJob.Value, Is.EqualTo(0));
            Assert.That(after.IsIdle, Is.True);
            Assert.That(claims.IsOwnedBy(claim, npcId), Is.False);
        }

        [Test]
        public void JobExecutionService_TickCurrentJobs_CleansUpNpcState_WhenExecutorLeavesTerminalJob()
        {
            var world = new WorldState();
            var board = new JobBoard();
            var claims = new ClaimService();
            var cleanup = new JobStateCleanupService(claims);
            var services = MakeServices(new TestEventBus(), new TestDataRegistry(), new NotificationService(new TestEventBus()), new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            var registry = new JobExecutorRegistry(services);
            var mapField = typeof(JobExecutorRegistry).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mapField, Is.Not.Null);
            var map = mapField.GetValue(registry) as Dictionary<JobArchetype, IJobExecutor>;
            Assert.That(map, Is.Not.Null);
            map[JobArchetype.Harvest] = new FakeJobExecutor((nid, ns, job, dt) => JobStatus.Completed);

            var exec = new JobExecutionService(services, world, board, registry, cleanup);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(2, 2),
                IsIdle = false
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            var jobId = board.Enqueue(new Job
            {
                Workplace = new BuildingId(1),
                Archetype = JobArchetype.Harvest,
                Status = JobStatus.InProgress,
                ClaimedBy = npcId,
                ResourceType = ResourceType.Wood,
                TargetCell = new CellPos(3, 3)
            });

            npc.CurrentJob = jobId;
            world.Npcs.Set(npcId, npc);

            var claim = new ClaimKey(ClaimKind.ProducerNode, 88, 0);
            claims.TryAcquire(claim, npcId);

            exec.TickCurrentJobs(new List<NpcId> { npcId }, 0.1f);

            var afterNpc = world.Npcs.Get(npcId);
            Assert.That(afterNpc.CurrentJob.Value, Is.EqualTo(0));
            Assert.That(afterNpc.IsIdle, Is.True);
            Assert.That(claims.IsOwnedBy(claim, npcId), Is.False);
            Assert.That(board.TryGet(jobId, out var afterJob), Is.True);
            Assert.That(afterJob.Status, Is.EqualTo(JobStatus.Completed));
        }

        [Test]
        public void BuildOrderEventBridge_StoresAutoRoadByOrderId()
        {
            var bus = new TestEventBus();
            var roads = new Dictionary<int, CellPos>();
            var services = MakeServices(bus, new TestDataRegistry(), new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService());
            var bridge = new BuildOrderEventBridge(services, roads);

            bridge.EnsureSubscribed();
            bus.Publish(new BuildOrderAutoRoadCreatedEvent(42, new CellPos(9, 11)));

            Assert.That(roads.TryGetValue(42, out var road), Is.True);
            Assert.That(road.X, Is.EqualTo(9));
            Assert.That(road.Y, Is.EqualTo(11));
        }

        [Test]
        public void BuildJobPlanner_EnsureBuildJobsForSite_DoesNotDuplicateActiveWorkJob()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_builderhut", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            var grid = new GridMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    grid.SetRoad(new CellPos(x, y), true);

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            var board = new JobBoard();
            services.JobBoard = board;
            services.Pathfinder = null;

            var workplace = world.Buildings.Create(new BuildingState { DefId = "bld_builderhut_t1", Anchor = new CellPos(3, 4), Rotation = Dir4.N, Level = 1, IsConstructed = true, HP = 20, MaxHP = 20 });
            var wb = world.Buildings.Get(workplace); wb.Id = workplace; world.Buildings.Set(workplace, wb);
            grid.SetBuilding(wb.Anchor, workplace);

            var deliver = new Dictionary<int, List<JobId>>();
            var work = new Dictionary<int, JobId>();
            var planner = new BuildJobPlanner(services, deliver, work);
            var siteId = new SiteId(7);
            var site = new BuildSiteState { BuildingDefId = "bld_builderhut_t1", Anchor = new CellPos(6, 4), Rotation = Dir4.N };

            planner.EnsureBuildJobsForSite(siteId, site, workplace);
            planner.EnsureBuildJobsForSite(siteId, site, workplace);

            Assert.That(work.Count, Is.EqualTo(1));
            Assert.That(board.CountActiveJobs(JobArchetype.BuildWork), Is.EqualTo(1));
        }

        [Test]
        public void BuildJobPlanner_EnsureBuildJobsForSite_DoesNotCreateJobs_WhenSiteEntryUnreachable()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_builderhut", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            var grid = new GridMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    grid.SetRoad(new CellPos(x, y), true);
            var terrain = new TerrainMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.TerrainMap = terrain;
            services.Pathfinder = new NpcPathfinder(grid, terrain);
            var board = new JobBoard();
            services.JobBoard = board;

            var workplace = world.Buildings.Create(new BuildingState { DefId = "bld_builderhut_t1", Anchor = new CellPos(1, 1), Rotation = Dir4.N, Level = 1, IsConstructed = true, HP = 20, MaxHP = 20 });
            var wb = world.Buildings.Get(workplace); wb.Id = workplace; world.Buildings.Set(workplace, wb);

            var deliver = new Dictionary<int, List<JobId>>();
            var work = new Dictionary<int, JobId>();
            var planner = new BuildJobPlanner(services, deliver, work);
            var siteId = new SiteId(8);
            var site = new BuildSiteState { BuildingDefId = "bld_builderhut_t1", Anchor = new CellPos(4, 4), Rotation = Dir4.N };
            var blockedEntry = EntryCellUtil.GetApproachCellForSite(services, site, new CellPos(1, 1));
            terrain.Set(blockedEntry, TerrainType.Sea);

            planner.EnsureBuildJobsForSite(siteId, site, workplace);

            Assert.That(work.Count, Is.EqualTo(0));
            Assert.That(board.CountActiveJobs(JobArchetype.BuildWork), Is.EqualTo(0));
        }

        [Test]
        public void BuildOrderCancellationService_PlaceCancel_RollsBackAutoRoad_WhenCellIsOtherwiseEmpty()
        {
            var bus = new TestEventBus();
            var grid = new GridMap(12, 12);
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_test", SizeX = 1, SizeY = 1, MaxHp = 10 });
            var noti = new NotificationService(bus);
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.JobBoard = new JobBoard();

            var target = world.Buildings.Create(new BuildingState { DefId = "bld_test", Anchor = new CellPos(2, 2), IsConstructed = false, HP = 10, MaxHP = 10 });
            var b = world.Buildings.Get(target); b.Id = target; world.Buildings.Set(target, b);

            var siteId = world.Sites.Create(new BuildSiteState { BuildingDefId = "bld_test", Anchor = new CellPos(2, 2), IsActive = true, WorkSecondsTotal = 1f });
            var s = world.Sites.Get(siteId); s.Id = siteId; world.Sites.Set(siteId, s);
            grid.SetSite(new CellPos(2, 2), siteId);

            var roads = new Dictionary<int, CellPos> { [99] = new CellPos(1, 1) };
            grid.SetRoad(new CellPos(1, 1), true);

            var cancellation = new BuildOrderCancellationService(services, true, roads, new Dictionary<int, JobId>(), _ => { });
            var order = new BuildOrder { OrderId = 99, Kind = BuildOrderKind.PlaceNew, BuildingDefId = "bld_test", TargetBuilding = target, Site = siteId, Completed = false };

            cancellation.Cancel(ref order);

            Assert.That(grid.IsRoad(new CellPos(1, 1)), Is.False);
            Assert.That(world.Sites.Exists(siteId), Is.False);
            Assert.That(world.Buildings.Exists(target), Is.False);
        }

        [Test]
        public void BuildOrderCancellationService_PlaceCancel_DoesNotRemovePreexistingRoad_WhenNoRecordedAutoRoadExists()
        {
            var bus = new TestEventBus();
            var grid = new GridMap(12, 12);
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_test", SizeX = 1, SizeY = 1, MaxHp = 10 });
            var noti = new NotificationService(bus);
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.JobBoard = new JobBoard();

            var target = world.Buildings.Create(new BuildingState { DefId = "bld_test", Anchor = new CellPos(2, 2), IsConstructed = false, HP = 10, MaxHP = 10 });
            var b = world.Buildings.Get(target); b.Id = target; world.Buildings.Set(target, b);

            var siteId = world.Sites.Create(new BuildSiteState { BuildingDefId = "bld_test", Anchor = new CellPos(2, 2), IsActive = true, WorkSecondsTotal = 1f, Rotation = Dir4.N });
            var s = world.Sites.Get(siteId); s.Id = siteId; world.Sites.Set(siteId, s);
            grid.SetSite(new CellPos(2, 2), siteId);

            // Preexisting road at the same driveway cell a placement would have used.
            grid.SetRoad(new CellPos(2, 3), true);

            var cancellation = new BuildOrderCancellationService(services, true, new Dictionary<int, CellPos>(), new Dictionary<int, JobId>(), _ => { });
            var order = new BuildOrder { OrderId = 99, Kind = BuildOrderKind.PlaceNew, BuildingDefId = "bld_test", TargetBuilding = target, Site = siteId, Completed = false };

            cancellation.Cancel(ref order);

            Assert.That(grid.IsRoad(new CellPos(2, 3)), Is.True, "Preexisting road must remain when no auto-road record exists for the order.");
            Assert.That(world.Sites.Exists(siteId), Is.False);
            Assert.That(world.Buildings.Exists(target), Is.False);
        }

        [Test]
        public void BuildJobPlanner_EnsureBuildJobsForSite_PrunesStaleTrackedWorkJob_AndCreatesReplacement()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_builderhut", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            var grid = new GridMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    grid.SetRoad(new CellPos(x, y), true);
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.JobBoard = new JobBoard();
            services.Pathfinder = null;

            var workplace = world.Buildings.Create(new BuildingState { DefId = "bld_builderhut_t1", Anchor = new CellPos(3, 4), Rotation = Dir4.N, Level = 1, IsConstructed = true, HP = 20, MaxHP = 20 });
            var wb = world.Buildings.Get(workplace); wb.Id = workplace; world.Buildings.Set(workplace, wb);
            grid.SetBuilding(wb.Anchor, workplace);

            var deliver = new Dictionary<int, List<JobId>>();
            var work = new Dictionary<int, JobId>();
            var planner = new BuildJobPlanner(services, deliver, work);

            var staleJob = new Job
            {
                Archetype = JobArchetype.BuildWork,
                Status = JobStatus.Completed,
                Workplace = new BuildingId(1),
                Site = new SiteId(7),
                TargetCell = new CellPos(4, 4)
            };
            var staleId = services.JobBoard.Enqueue(staleJob);
            work[7] = staleId;

            var siteId = new SiteId(7);
            var site = new BuildSiteState { BuildingDefId = "bld_builderhut_t1", Anchor = new CellPos(4, 4), Rotation = Dir4.N };

            planner.EnsureBuildJobsForSite(siteId, site, workplace);

            Assert.That(work.ContainsKey(7), Is.True);
            Assert.That(work[7], Is.Not.EqualTo(staleId), "Planner should replace stale tracked job with a new active job id.");
            Assert.That(services.JobBoard.TryGet(work[7], out var repl), Is.True);
            Assert.That(repl.Status, Is.EqualTo(JobStatus.Created));
            Assert.That(repl.Workplace.Value, Is.EqualTo(workplace.Value));
        }

        [Test]
        public void BuildJobPlanner_EnsureBuildJobsForSite_RecreatesWorkJob_AfterTerminalState()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_builderhut", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", WorkRoles = WorkRoleFlags.Build, SizeX = 1, SizeY = 1, MaxHp = 20 });
            var grid = new GridMap(12, 12);
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                    grid.SetRoad(new CellPos(x, y), true);
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.JobBoard = new JobBoard();
            services.Pathfinder = null;

            var workplace = world.Buildings.Create(new BuildingState { DefId = "bld_builderhut_t1", Anchor = new CellPos(4, 5), Rotation = Dir4.N, Level = 1, IsConstructed = true, HP = 20, MaxHP = 20 });
            var wb = world.Buildings.Get(workplace); wb.Id = workplace; world.Buildings.Set(workplace, wb);
            grid.SetBuilding(wb.Anchor, workplace);

            var deliver = new Dictionary<int, List<JobId>>();
            var work = new Dictionary<int, JobId>();
            var planner = new BuildJobPlanner(services, deliver, work);

            var firstJob = new Job
            {
                Archetype = JobArchetype.BuildWork,
                Status = JobStatus.Completed,
                Workplace = new BuildingId(2),
                Site = new SiteId(8),
                TargetCell = new CellPos(5, 5)
            };
            var firstId = services.JobBoard.Enqueue(firstJob);
            work[8] = firstId;

            var siteId = new SiteId(8);
            var site = new BuildSiteState { BuildingDefId = "bld_builderhut_t1", Anchor = new CellPos(5, 5), Rotation = Dir4.N };

            planner.EnsureBuildJobsForSite(siteId, site, workplace);
            var recreatedId = work[8];

            Assert.That(recreatedId, Is.Not.EqualTo(firstId));
            Assert.That(services.JobBoard.TryGet(recreatedId, out var recreated), Is.True);
            Assert.That(recreated.Status, Is.EqualTo(JobStatus.Created));
            Assert.That(recreated.Archetype, Is.EqualTo(JobArchetype.BuildWork));
            Assert.That(recreated.Workplace.Value, Is.EqualTo(workplace.Value));
        }

        [Test]
        public void BuildOrderCancellationService_PlaceCancel_RefundsDeliveredResources_ToNearestValidStorage()
        {
            var bus = new TestEventBus();
            var grid = new GridMap(20, 20);
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_test", SizeX = 1, SizeY = 1, MaxHp = 10 });
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 1, SizeY = 1, MaxHp = 100, IsHQ = true, IsWarehouse = true });
            data.Add(new BuildingDef { DefId = "bld_warehouse_t1", SizeX = 1, SizeY = 1, MaxHp = 100, IsWarehouse = true });
            var noti = new NotificationService(bus);
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.JobBoard = new JobBoard();
            var storage = new FakeStorageService();
            services.StorageService = storage;
            services.WorldIndex = new WorldIndexService(world, data);

            var nearId = world.Buildings.Create(new BuildingState { DefId = "bld_hq_t1", Anchor = new CellPos(6, 5), IsConstructed = true, HP = 100, MaxHP = 100 });
            var near = world.Buildings.Get(nearId); near.Id = nearId; world.Buildings.Set(nearId, near);
            grid.SetBuilding(new CellPos(6, 5), nearId);

            var farId = world.Buildings.Create(new BuildingState { DefId = "bld_warehouse_t1", Anchor = new CellPos(15, 15), IsConstructed = true, HP = 100, MaxHP = 100 });
            var far = world.Buildings.Get(farId); far.Id = farId; world.Buildings.Set(farId, far);
            grid.SetBuilding(new CellPos(15, 15), farId);

            services.WorldIndex.RebuildAll();
            storage.SetCap(nearId, ResourceType.Wood, 100);
            storage.SetCap(farId, ResourceType.Wood, 100);

            var target = world.Buildings.Create(new BuildingState { DefId = "bld_test", Anchor = new CellPos(5, 5), IsConstructed = false, HP = 10, MaxHP = 10 });
            var b = world.Buildings.Get(target); b.Id = target; world.Buildings.Set(target, b);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_test",
                Anchor = new CellPos(5, 5),
                IsActive = true,
                WorkSecondsTotal = 1f,
                DeliveredSoFar = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 7 } }
            });
            var s = world.Sites.Get(siteId); s.Id = siteId; world.Sites.Set(siteId, s);
            grid.SetSite(new CellPos(5, 5), siteId);

            var cancellation = new BuildOrderCancellationService(services, true, new Dictionary<int, CellPos>(), new Dictionary<int, JobId>(), _ => { });
            var order = new BuildOrder { OrderId = 100, Kind = BuildOrderKind.PlaceNew, BuildingDefId = "bld_test", TargetBuilding = target, Site = siteId, Completed = false };

            cancellation.Cancel(ref order);

            Assert.That(storage.GetAmount(nearId, ResourceType.Wood), Is.EqualTo(7), "Nearest valid storage should receive refunded delivered resources.");
            Assert.That(storage.GetAmount(farId, ResourceType.Wood), Is.EqualTo(0), "Farther storage should not receive refund when nearer valid storage has capacity.");
        }

        [Test]
        public void BuildOrderCancellationService_CancelRepair_CancelsTrackedRepairJob_AndRemovesTracking()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            services.JobBoard = new JobBoard();

            var trackedRepair = new Dictionary<int, JobId>();
            var repairJob = new Job
            {
                Archetype = JobArchetype.RepairWork,
                Status = JobStatus.Created,
                Workplace = new BuildingId(3),
                TargetCell = new CellPos(4, 4)
            };
            var repairJobId = services.JobBoard.Enqueue(repairJob);
            trackedRepair[77] = repairJobId;

            var cancellation = new BuildOrderCancellationService(services, true, new Dictionary<int, CellPos>(), trackedRepair, _ => { });
            cancellation.CancelRepairJob(77);

            Assert.That(trackedRepair.ContainsKey(77), Is.False, "Tracked repair job entry should be removed after cancel.");
            Assert.That(services.JobBoard.TryGet(repairJobId, out var after), Is.True);
            Assert.That(after.Status, Is.EqualTo(JobStatus.Cancelled));
        }

        [Test]
        public void BuildOrderTickProcessor_CompletesPlaceOrder_WhenSiteReadyAndWorkDone()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var services = MakeServices(bus, new TestDataRegistry(), new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world);

            var orders = new Dictionary<int, BuildOrder>();
            var active = new List<int>();
            var siteId = world.Sites.Create(new BuildSiteState
            {
                Id = default,
                BuildingDefId = "bld_test",
                Anchor = new CellPos(3, 3),
                IsActive = true,
                WorkSecondsDone = 5f,
                WorkSecondsTotal = 5f,
                RemainingCosts = new List<CostDef>()
            });
            var st = world.Sites.Get(siteId); st.Id = siteId; world.Sites.Set(siteId, st);

            orders[1] = new BuildOrder { OrderId = 1, Kind = BuildOrderKind.PlaceNew, Site = siteId, BuildingDefId = "bld_test", Completed = false };
            active.Add(1);

            int ensureCalled = 0;
            int cancelCalled = 0;
            int completeCalled = 0;
            int completedEvent = 0;

            var tick = new BuildOrderTickProcessor(
                services,
                orders,
                active,
                () => new BuildingId(5),
                (sid, site, workplace) => ensureCalled++,
                sid => cancelCalled++,
                (int id, ref BuildOrder order, BuildingId workplace) => { },
                (ref BuildOrder order) => { order.Completed = true; completeCalled++; },
                (ref BuildOrder order) => { },
                id => completedEvent++);

            tick.Tick(0.1f);

            Assert.That(ensureCalled, Is.EqualTo(1));
            Assert.That(cancelCalled, Is.EqualTo(1));
            Assert.That(completeCalled, Is.EqualTo(1));
            Assert.That(completedEvent, Is.EqualTo(1));
            Assert.That(active.Count, Is.EqualTo(0));
            Assert.That(orders[1].Completed, Is.True);
        }

        [Test]
        public void BuildOrderTickProcessor_CompletesUpgradeOrder_WhenSiteReadyAndWorkDone()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var services = MakeServices(bus, new TestDataRegistry(), new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world);

            var orders = new Dictionary<int, BuildOrder>();
            var active = new List<int>();
            var siteId = world.Sites.Create(new BuildSiteState
            {
                Id = default,
                BuildingDefId = "bld_upgrade_test",
                Anchor = new CellPos(6, 6),
                IsActive = true,
                WorkSecondsDone = 3f,
                WorkSecondsTotal = 3f,
                RemainingCosts = new List<CostDef>()
            });
            var st = world.Sites.Get(siteId); st.Id = siteId; world.Sites.Set(siteId, st);

            orders[2] = new BuildOrder { OrderId = 2, Kind = BuildOrderKind.Upgrade, Site = siteId, BuildingDefId = "bld_upgrade_test", TargetBuilding = new BuildingId(44), Completed = false };
            active.Add(2);

            int ensureCalled = 0;
            int cancelCalled = 0;
            int completeUpgradeCalled = 0;
            int completedEvent = 0;

            var tick = new BuildOrderTickProcessor(
                services,
                orders,
                active,
                () => new BuildingId(5),
                (sid, site, workplace) => ensureCalled++,
                sid => cancelCalled++,
                (int id, ref BuildOrder order, BuildingId workplace) => { },
                (ref BuildOrder order) => { },
                (ref BuildOrder order) => { order.Completed = true; completeUpgradeCalled++; },
                id => completedEvent++);

            tick.Tick(0.1f);

            Assert.That(ensureCalled, Is.EqualTo(1));
            Assert.That(cancelCalled, Is.EqualTo(1));
            Assert.That(completeUpgradeCalled, Is.EqualTo(1));
            Assert.That(completedEvent, Is.EqualTo(1));
            Assert.That(active.Count, Is.EqualTo(0));
            Assert.That(orders[2].Completed, Is.True);
        }

    }
}
