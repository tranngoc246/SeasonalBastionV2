using NUnit.Framework;
using SeasonalBastion.Contracts;
using System;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_AmmoResupplyTests
    {
        private static GameServices MakeServices(
            IEventBus bus,
            IDataRegistry data,
            INotificationService noti,
            IRunClock clock,
            IRunOutcomeService outcome,
            IWorldState world = null,
            IGridMap grid = null,
            IPlacementService placement = null
        )
        {
            return new GameServices
            {
                EventBus = bus,
                DataRegistry = data,
                NotificationService = noti,
                RunClock = clock,
                RunOutcomeService = outcome,
                WorldState = world,
                GridMap = grid,
                PlacementService = placement
            };
        }

        [Test]
        public void AmmoResupply_ZeroAmmo_AlwaysTriggersJob_WhenStorageHasAmmo()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(16, 16));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            var tower = CreateTower(world, new CellPos(8, 8), ammo: 0, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 50);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);

            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(1));
            Assert.That(board.TryPeekForWorkplace(armory, out var job), Is.True);
            Assert.That(job.Archetype, Is.EqualTo(JobArchetype.ResupplyTower));
            Assert.That(job.Tower.Value, Is.EqualTo(tower.Value));
        }

        [Test]
        public void AmmoResupply_LowAmmo_TriggersJob_WhenStorageHasAmmo()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(16, 16));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            CreateTower(world, new CellPos(8, 8), ammo: 5, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 50);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);

            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(1));
            Assert.That(board.TryPeekForWorkplace(armory, out var job), Is.True);
            Assert.That(job.Archetype, Is.EqualTo(JobArchetype.ResupplyTower));
        }

        [Test]
        public void AmmoResupply_NoStorageAmmo_DoesNotCreateJob()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(16, 16));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            CreateTower(world, new CellPos(8, 8), ammo: 0, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 0);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);

            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(0));
        }

        [Test]
        public void AmmoResupply_DoesNotCreateDuplicateJobs_ForSameTower()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(16, 16));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            var tower = CreateTower(world, new CellPos(8, 8), ammo: 0, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 50);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);
            sut.NotifyTowerAmmoChanged(tower, 0, 20);
            sut.Tick(0.1f);

            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(1));
        }

        [Test]
        public void AmmoResupply_MultipleTowers_PrioritizesLowestAmmoThenTowerId()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(32, 32));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            CreateNpc(world, armory, new CellPos(1, 2));
            CreateTower(world, new CellPos(10, 10), ammo: 3, ammoCap: 20);
            var towerB = CreateTower(world, new CellPos(12, 10), ammo: 0, ammoCap: 20);
            var towerC = CreateTower(world, new CellPos(14, 10), ammo: 0, ammoCap: 20);
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 100);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);

            Assert.That(board.TryPeekForWorkplace(armory, out var job), Is.True);
            Assert.That(job.Tower.Value, Is.EqualTo(Math.Min(towerB.Value, towerC.Value)), "Empty towers must outrank low towers; ties break by TowerId.");
        }

        [Test]
        public void AmmoResupply_SecondDepletion_CreatesNewJobAgain_AfterPreviousCompleted()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(16, 16));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            var tower = CreateTower(world, new CellPos(8, 8), ammo: 5, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 100);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);
            Assert.That(board.TryPeekForWorkplace(armory, out var firstJob), Is.True);
            firstJob.Status = JobStatus.Completed;
            board.Update(firstJob);

            var state = world.Towers.Get(tower);
            state.Ammo = 20;
            world.Towers.Set(tower, state);
            sut.NotifyTowerAmmoChanged(tower, 20, 20);

            state = world.Towers.Get(tower);
            state.Ammo = 0;
            world.Towers.Set(tower, state);
            sut.NotifyTowerAmmoChanged(tower, 0, 20);
            sut.Tick(0.1f);

            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(1));
            Assert.That(board.TryPeekForWorkplace(armory, out var secondJob), Is.True);
            Assert.That(secondJob.Id.Value, Is.Not.EqualTo(firstJob.Id.Value));
            Assert.That(secondJob.Tower.Value, Is.EqualTo(tower.Value));
        }

        [Test]
        public void AmmoResupply_RecreatesJob_AfterPreviousJobBecomesTerminal_AndTowerStillNeedsAmmo()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(16, 16));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            var tower = CreateTower(world, new CellPos(8, 8), ammo: 0, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 50);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);
            Assert.That(board.TryPeekForWorkplace(armory, out var firstJob), Is.True);
            firstJob.Status = JobStatus.Cancelled;
            board.Update(firstJob);

            sut.Tick(0.1f);

            Assert.That(board.TryPeekForWorkplace(armory, out var recreated), Is.True);
            Assert.That(recreated.Id.Value, Is.Not.EqualTo(firstJob.Id.Value));
            Assert.That(recreated.Tower.Value, Is.EqualTo(tower.Value));
        }

        [Test]
        public void AmmoResupply_UrgentTower_ReprioritizesCreatedLowPriorityJob()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(32, 32));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            var towerA = CreateTower(world, new CellPos(10, 10), ammo: 5, ammoCap: 20);
            var towerB = CreateTower(world, new CellPos(12, 10), ammo: 20, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 100);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);
            Assert.That(board.TryPeekForWorkplace(armory, out var createdJob), Is.True);
            Assert.That(createdJob.Status, Is.EqualTo(JobStatus.Created));
            Assert.That(createdJob.Tower.Value, Is.EqualTo(towerA.Value));

            var towerBState = world.Towers.Get(towerB);
            towerBState.Ammo = 0;
            world.Towers.Set(towerB, towerBState);
            sut.NotifyTowerAmmoChanged(towerB, 0, 20);
            sut.Tick(0.1f);

            Assert.That(board.TryPeekForWorkplace(armory, out var reprioritized), Is.True);
            Assert.That(reprioritized.Id.Value, Is.EqualTo(createdJob.Id.Value));
            Assert.That(reprioritized.Tower.Value, Is.EqualTo(towerB.Value));
            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(1));
        }

        [Test]
        public void AmmoResupply_FallsBackToWarehouse_WhenArmoryHasNoAmmo()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var storage = new FakeStorageService();
            var board = new JobBoard();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: new GridMap(32, 32));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = storage;
            services.JobBoard = board;
            services.CombatService = new FakeCombatService();

            var armory = CreateConstructedBuilding(world, data, "bld_armory_t1", new CellPos(1, 1), level: 1, isArmory: true, isWarehouse: false, workRoles: WorkRoleFlags.Armory);
            var warehouse = CreateConstructedBuilding(world, data, "bld_warehouse_t1", new CellPos(2, 2), level: 1, isArmory: false, isWarehouse: true, workRoles: WorkRoleFlags.HaulBasic);
            var tower = CreateTower(world, new CellPos(12, 12), ammo: 0, ammoCap: 20);
            CreateNpc(world, armory, new CellPos(1, 2));
            CreateNpc(world, warehouse, new CellPos(2, 3));
            services.WorldIndex.RebuildAll();
            storage.SetCap(armory, ResourceType.Ammo, 200);
            storage.SetAmount(armory, ResourceType.Ammo, 0);
            storage.SetCap(warehouse, ResourceType.Ammo, 200);
            storage.SetAmount(warehouse, ResourceType.Ammo, 60);

            var sut = new AmmoService(services);
            sut.Tick(0.1f);

            Assert.That(board.TryPeekForWorkplace(warehouse, out var job), Is.True);
            Assert.That(job.Archetype, Is.EqualTo(JobArchetype.ResupplyTower));
            Assert.That(job.Workplace.Value, Is.EqualTo(warehouse.Value));
            Assert.That(job.SourceBuilding.Value, Is.EqualTo(warehouse.Value));
            Assert.That(job.Tower.Value, Is.EqualTo(tower.Value));
            Assert.That(board.CountForWorkplace(armory), Is.EqualTo(0));
        }

        private static BuildingId CreateConstructedBuilding(WorldState world, TestDataRegistry data, string defId, CellPos anchor, int level, bool isArmory, bool isWarehouse, WorkRoleFlags workRoles)
        {
            data.Add(new BuildingDef
            {
                DefId = defId,
                SizeX = 1,
                SizeY = 1,
                MaxHp = 100,
                BaseLevel = level,
                IsArmory = isArmory,
                IsWarehouse = isWarehouse,
                WorkRoles = workRoles,
                CapAmmo = new StorageCapsByLevel { L1 = 200, L2 = 200, L3 = 200 }
            });

            var id = world.Buildings.Create(new BuildingState
            {
                DefId = defId,
                Anchor = anchor,
                Rotation = Dir4.N,
                Level = level,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var st = world.Buildings.Get(id);
            st.Id = id;
            world.Buildings.Set(id, st);
            return id;
        }

        private static TowerId CreateTower(WorldState world, CellPos cell, int ammo, int ammoCap)
        {
            var id = world.Towers.Create(new TowerState
            {
                Cell = cell,
                Ammo = ammo,
                AmmoCap = ammoCap,
                Hp = 100,
                HpMax = 100
            });
            var st = world.Towers.Get(id);
            st.Id = id;
            world.Towers.Set(id, st);
            return id;
        }

        private static NpcId CreateNpc(WorldState world, BuildingId workplace, CellPos cell)
        {
            var id = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = cell,
                Workplace = workplace,
                IsIdle = true
            });
            var st = world.Npcs.Get(id);
            st.Id = id;
            world.Npcs.Set(id, st);
            return id;
        }
    }
}
