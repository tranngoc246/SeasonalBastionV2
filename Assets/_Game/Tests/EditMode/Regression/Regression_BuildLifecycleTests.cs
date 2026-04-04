using NUnit.Framework;
using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_BuildLifecycleTests
    {
        private static GameServices MakeServices(
            IEventBus bus,
            IDataRegistry data,
            INotificationService noti,
            IRunClock clock,
            IRunOutcomeService outcome,
            IWorldState world = null,
            IGridMap grid = null,
            IPlacementService placement = null)
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
        public void PlaceFlowCompletion_FinalizesExactlyOneConstructedBuilding_AndRemovesSite()
        {
            var bus = new TestEventBus();
            int placedEvents = 0;
            bus.Subscribe<BuildingPlacedEvent>(_ => placedEvents++);

            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_house_t1",
                SizeX = 1,
                SizeY = 1,
                BaseLevel = 1,
                MaxHp = 50,
                BuildChunksL1 = 1,
                BuildCostsL1 = new[] { new CostDef { Resource = ResourceType.Wood, Amount = 5 } },
                IsHouse = true
            });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world, grid, new FakePlacementService());
            services.JobBoard = new JobBoard();
            services.WorldIndex = new WorldIndexService(world, data);
            services.BuildWorkplaceResolver = new FakeBuildWorkplaceResolver(new BuildingId(88));

            var placeholderId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_house_t1",
                Anchor = new CellPos(4, 4),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = false,
                HP = 0,
                MaxHP = 50
            });
            var placeholder = world.Buildings.Get(placeholderId);
            placeholder.Id = placeholderId;
            world.Buildings.Set(placeholderId, placeholder);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_house_t1",
                TargetLevel = 1,
                Anchor = new CellPos(4, 4),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 5f,
                WorkSecondsTotal = 5f,
                DeliveredSoFar = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 5 } },
                RemainingCosts = new List<CostDef>(),
                Kind = 0,
                TargetBuilding = placeholderId,
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(4, 4), siteId);

            var completion = new BuildOrderCompletionService(services, _ => { }, _ => { });
            var order = new BuildOrder
            {
                OrderId = 1,
                Kind = BuildOrderKind.PlaceNew,
                BuildingDefId = "bld_house_t1",
                TargetBuilding = placeholderId,
                Site = siteId,
                Completed = false
            };

            completion.CompletePlace(ref order);
            completion.CompletePlace(ref order);

            Assert.That(order.Completed, Is.True);
            Assert.That(world.Sites.Exists(siteId), Is.False, "Completed place flow must remove build site exactly once.");
            Assert.That(world.Buildings.Exists(placeholderId), Is.True);

            var building = world.Buildings.Get(placeholderId);
            Assert.That(building.IsConstructed, Is.True);
            Assert.That(building.DefId, Is.EqualTo("bld_house_t1"));
            Assert.That(building.HP, Is.EqualTo(50));
            Assert.That(grid.Get(new CellPos(4, 4)).Kind, Is.EqualTo(CellOccupancyKind.Building));
            Assert.That(grid.Get(new CellPos(4, 4)).Building.Value, Is.EqualTo(placeholderId.Value));
            Assert.That(world.Buildings.Count, Is.EqualTo(1), "Re-running finalize must not spawn duplicate constructed buildings.");
            Assert.That(placedEvents, Is.EqualTo(1), "Place finalize should emit BuildingPlacedEvent exactly once.");

            BuildOrderInvariantHelper.AssertBuildInvariant(world, grid, data, services.WorldIndex, placeholderId);
        }

        [Test]
        public void UpgradeFlowCompletion_FinalizesUpgrade_AndRemovesTemporarySite()
        {
            var bus = new TestEventBus();
            int placedEvents = 0;
            int upgradedEvents = 0;
            bus.Subscribe<BuildingPlacedEvent>(_ => placedEvents++);
            bus.Subscribe<BuildingUpgradedEvent>(_ => upgradedEvents++);

            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 160, IsHQ = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world, grid, new FakePlacementService());
            services.JobBoard = new JobBoard();
            services.WorldIndex = new WorldIndexService(world, data);

            var buildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(3, 3), buildingId);
            grid.SetBuilding(new CellPos(4, 3), buildingId);
            grid.SetBuilding(new CellPos(3, 4), buildingId);
            grid.SetBuilding(new CellPos(4, 4), buildingId);
            services.WorldIndex.RebuildAll();

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_hq_t2",
                TargetLevel = 2,
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 6f,
                WorkSecondsTotal = 6f,
                DeliveredSoFar = new List<CostDef>(),
                RemainingCosts = new List<CostDef>(),
                Kind = 1,
                TargetBuilding = buildingId,
                FromDefId = "bld_hq_t1",
                EdgeId = "hq_t1_to_t2"
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(3, 3), siteId);
            grid.SetSite(new CellPos(4, 3), siteId);
            grid.SetSite(new CellPos(3, 4), siteId);
            grid.SetSite(new CellPos(4, 4), siteId);

            var completion = new BuildOrderCompletionService(services, _ => { }, _ => { });
            var order = new BuildOrder
            {
                OrderId = 2,
                Kind = BuildOrderKind.Upgrade,
                BuildingDefId = "bld_hq_t2",
                TargetBuilding = buildingId,
                Site = siteId,
                Completed = false
            };

            completion.CompleteUpgrade(ref order);
            completion.CompleteUpgrade(ref order);

            Assert.That(order.Completed, Is.True);
            Assert.That(world.Sites.Exists(siteId), Is.False, "Upgrade finalize must remove temp site.");
            Assert.That(world.Buildings.Count, Is.EqualTo(1), "Upgrade finalize must reuse existing building state, not duplicate it.");

            var upgraded = world.Buildings.Get(buildingId);
            Assert.That(upgraded.DefId, Is.EqualTo("bld_hq_t2"));
            Assert.That(upgraded.Level, Is.EqualTo(2));
            Assert.That(upgraded.IsConstructed, Is.True);
            Assert.That(upgraded.HP, Is.EqualTo(160));
            Assert.That(grid.Get(new CellPos(3, 3)).Kind, Is.EqualTo(CellOccupancyKind.Building));
            Assert.That(grid.Get(new CellPos(4, 4)).Building.Value, Is.EqualTo(buildingId.Value));
            Assert.That(placedEvents, Is.EqualTo(0), "Upgrade finalize must not emit BuildingPlacedEvent.");
            Assert.That(upgradedEvents, Is.EqualTo(1), "Upgrade finalize should emit BuildingUpgradedEvent exactly once.");

            BuildOrderInvariantHelper.AssertBuildInvariant(world, grid, data, services.WorldIndex, buildingId);
        }


        [Test]
        public void BuildInvariant_TowerBuilding_FailsWhenOnlyUnrelatedTowerExists()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_arrowtower_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });
            data.AddTower(new TowerDef { DefId = "bld_arrowtower_t1", MaxHp = 100, AmmoMax = 12 });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var worldIndex = new WorldIndexService(world, data);

            var targetBuildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_arrowtower_t1",
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var targetBuilding = world.Buildings.Get(targetBuildingId);
            targetBuilding.Id = targetBuildingId;
            world.Buildings.Set(targetBuildingId, targetBuilding);
            grid.SetBuilding(new CellPos(3, 3), targetBuildingId);

            var otherBuildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_arrowtower_t1",
                Anchor = new CellPos(10, 10),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var otherBuilding = world.Buildings.Get(otherBuildingId);
            otherBuilding.Id = otherBuildingId;
            world.Buildings.Set(otherBuildingId, otherBuilding);
            grid.SetBuilding(new CellPos(10, 10), otherBuildingId);

            var unrelatedTowerId = world.Towers.Create(new TowerState
            {
                Cell = new CellPos(10, 10),
                Hp = 100,
                HpMax = 100,
                Ammo = 12,
                AmmoCap = 12,
            });
            var unrelatedTower = world.Towers.Get(unrelatedTowerId);
            unrelatedTower.Id = unrelatedTowerId;
            world.Towers.Set(unrelatedTowerId, unrelatedTower);

            worldIndex.RebuildAll();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                BuildOrderInvariantHelper.AssertBuildInvariant(world, grid, data, worldIndex, targetBuildingId));

            Assert.That(ex.Message, Does.Contain("WorldIndex is missing building"));
        }

        [Test]
        public void BuildInvariant_TowerBuilding_PassesOnlyWhenExactTowerBackingExists()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_arrowtower_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });
            data.AddTower(new TowerDef { DefId = "bld_arrowtower_t1", MaxHp = 100, AmmoMax = 12 });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var worldIndex = new WorldIndexService(world, data);

            var buildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_arrowtower_t1",
                Anchor = new CellPos(4, 5),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(4, 5), buildingId);

            var towerId = world.Towers.Create(new TowerState
            {
                Cell = new CellPos(4, 5),
                Hp = 100,
                HpMax = 100,
                Ammo = 12,
                AmmoCap = 12,
            });
            var tower = world.Towers.Get(towerId);
            tower.Id = towerId;
            world.Towers.Set(towerId, tower);

            worldIndex.RebuildAll();

            Assert.DoesNotThrow(() =>
                BuildOrderInvariantHelper.AssertBuildInvariant(world, grid, data, worldIndex, buildingId));
        }

        [Test]
        public void UpgradeFlowCompletion_PublishesOnlyUpgradeEvent_WithoutDuplicateBuildingReaction()
        {
            var bus = new TestEventBus();
            int buildingReactionCount = 0;
            bus.Subscribe<BuildingPlacedEvent>(_ => buildingReactionCount++);
            bus.Subscribe<BuildingUpgradedEvent>(_ => buildingReactionCount++);

            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 160, IsHQ = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world, grid, new FakePlacementService());
            services.JobBoard = new JobBoard();
            services.WorldIndex = new WorldIndexService(world, data);

            var buildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(3, 3), buildingId);
            grid.SetBuilding(new CellPos(4, 3), buildingId);
            grid.SetBuilding(new CellPos(3, 4), buildingId);
            grid.SetBuilding(new CellPos(4, 4), buildingId);
            services.WorldIndex.RebuildAll();

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_hq_t2",
                TargetLevel = 2,
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 6f,
                WorkSecondsTotal = 6f,
                DeliveredSoFar = new List<CostDef>(),
                RemainingCosts = new List<CostDef>(),
                Kind = 1,
                TargetBuilding = buildingId,
                FromDefId = "bld_hq_t1",
                EdgeId = "hq_t1_to_t2"
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(3, 3), siteId);
            grid.SetSite(new CellPos(4, 3), siteId);
            grid.SetSite(new CellPos(3, 4), siteId);
            grid.SetSite(new CellPos(4, 4), siteId);

            var completion = new BuildOrderCompletionService(services, _ => { }, _ => { });
            var order = new BuildOrder
            {
                OrderId = 3,
                Kind = BuildOrderKind.Upgrade,
                BuildingDefId = "bld_hq_t2",
                TargetBuilding = buildingId,
                Site = siteId,
                Completed = false
            };

            completion.CompleteUpgrade(ref order);

            int placedPublished = 0;
            int upgradedPublished = 0;
            for (int i = 0; i < bus.Published.Count; i++)
            {
                if (bus.Published[i] is BuildingPlacedEvent) placedPublished++;
                if (bus.Published[i] is BuildingUpgradedEvent) upgradedPublished++;
            }

            Assert.That(placedPublished, Is.EqualTo(0), "Upgrade finalize must not publish BuildingPlacedEvent.");
            Assert.That(upgradedPublished, Is.EqualTo(1), "Upgrade finalize must publish BuildingUpgradedEvent exactly once.");
            Assert.That(buildingReactionCount, Is.EqualTo(1), "A listener subscribed to both placement and upgrade events should react exactly once for an upgrade.");
        }

        [Test]
        public void PlaceCancellationRollback_RemovesPlaceholderSiteAndTrackedRoad_RefundsResources_AndIsIdempotent()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 50 });
            data.Add(new BuildingDef { DefId = "bld_warehouse_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsWarehouse = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var storage = new FakeStorageService();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world, grid, new FakePlacementService());
            services.JobBoard = new JobBoard();
            services.StorageService = storage;
            services.WorldIndex = new WorldIndexService(world, data);

            var storageId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_warehouse_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var wh = world.Buildings.Get(storageId);
            wh.Id = storageId;
            world.Buildings.Set(storageId, wh);
            services.WorldIndex.RebuildAll();
            storage.SetCap(storageId, ResourceType.Wood, 100);

            var placeholderId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_house_t1",
                Anchor = new CellPos(6, 6),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = false,
                HP = 10,
                MaxHP = 50
            });
            var placeholder = world.Buildings.Get(placeholderId);
            placeholder.Id = placeholderId;
            world.Buildings.Set(placeholderId, placeholder);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_house_t1",
                TargetLevel = 1,
                Anchor = new CellPos(6, 6),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 1f,
                WorkSecondsTotal = 5f,
                DeliveredSoFar = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 4 } },
                RemainingCosts = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 1 } },
                Kind = 0,
                TargetBuilding = placeholderId,
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(6, 6), siteId);
            grid.SetRoad(new CellPos(6, 7), true);

            var roads = new Dictionary<int, CellPos> { [77] = new CellPos(6, 7) };
            int cancelTrackedCalls = 0;
            var cancellation = new BuildOrderCancellationService(services, true, roads, new Dictionary<int, JobId>(), _ => cancelTrackedCalls++);
            var order = new BuildOrder
            {
                OrderId = 77,
                Kind = BuildOrderKind.PlaceNew,
                BuildingDefId = "bld_house_t1",
                TargetBuilding = placeholderId,
                Site = siteId,
                Completed = false
            };

            cancellation.Cancel(ref order);
            cancellation.Cancel(ref order);

            Assert.That(order.Completed, Is.True);
            Assert.That(cancelTrackedCalls, Is.EqualTo(1), "Cancel should become idempotent once cleanup has happened.");
            Assert.That(world.Sites.Exists(siteId), Is.False);
            Assert.That(world.Buildings.Exists(placeholderId), Is.False);
            Assert.That(grid.IsRoad(new CellPos(6, 7)), Is.False, "Tracked auto-road should roll back when safe.");
            Assert.That(storage.GetAmount(storageId, ResourceType.Wood), Is.EqualTo(4));
        }

        [Test]
        public void UpgradeCancellationRollback_RemovesTemporarySite_AndKeepsOriginalBuildingValid()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true, IsWarehouse = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 160, IsHQ = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var storage = new FakeStorageService();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world, grid, new FakePlacementService());
            services.JobBoard = new JobBoard();
            services.StorageService = storage;
            services.WorldIndex = new WorldIndexService(world, data);

            var originalId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(5, 5),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var original = world.Buildings.Get(originalId);
            original.Id = originalId;
            world.Buildings.Set(originalId, original);
            services.WorldIndex.RebuildAll();
            storage.SetCap(originalId, ResourceType.Stone, 100);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_hq_t2",
                TargetLevel = 2,
                Anchor = new CellPos(5, 5),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 2f,
                WorkSecondsTotal = 6f,
                DeliveredSoFar = new List<CostDef> { new CostDef { Resource = ResourceType.Stone, Amount = 6 } },
                RemainingCosts = new List<CostDef> { new CostDef { Resource = ResourceType.Stone, Amount = 2 } },
                Kind = 1,
                TargetBuilding = originalId,
                FromDefId = "bld_hq_t1"
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(5, 5), siteId);
            grid.SetSite(new CellPos(6, 5), siteId);
            grid.SetSite(new CellPos(5, 6), siteId);
            grid.SetSite(new CellPos(6, 6), siteId);

            var cancellation = new BuildOrderCancellationService(services, true, new Dictionary<int, CellPos>(), new Dictionary<int, JobId>(), _ => { });
            var order = new BuildOrder
            {
                OrderId = 88,
                Kind = BuildOrderKind.Upgrade,
                BuildingDefId = "bld_hq_t2",
                TargetBuilding = originalId,
                Site = siteId,
                Completed = false
            };

            cancellation.Cancel(ref order);

            Assert.That(order.Completed, Is.True);
            Assert.That(world.Sites.Exists(siteId), Is.False);
            Assert.That(world.Buildings.Exists(originalId), Is.True);
            Assert.That(world.Buildings.Count, Is.EqualTo(1));
            var after = world.Buildings.Get(originalId);
            Assert.That(after.DefId, Is.EqualTo("bld_hq_t1"));
            Assert.That(after.IsConstructed, Is.True);
            Assert.That(storage.GetAmount(originalId, ResourceType.Stone), Is.EqualTo(6), "Delivered upgrade costs should refund according to current storage rules.");
        }

        [Test]
        public void TickProcessor_DoesNotDoubleRaiseCompletion_WhenFinalizeAlreadyCompletedOrder()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var services = MakeServices(bus, new TestDataRegistry(), new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world);

            var orders = new Dictionary<int, BuildOrder>();
            var active = new List<int>();
            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_test",
                Anchor = new CellPos(2, 2),
                IsActive = true,
                WorkSecondsDone = 1f,
                WorkSecondsTotal = 1f,
                RemainingCosts = new List<CostDef>()
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);

            orders[9] = new BuildOrder { OrderId = 9, Kind = BuildOrderKind.PlaceNew, Site = siteId, Completed = false };
            active.Add(9);

            int completeCalls = 0;
            int completedEvents = 0;
            var tick = new BuildOrderTickProcessor(
                services,
                orders,
                active,
                () => new BuildingId(3),
                (sid, st, wp) => { },
                sid => { },
                (int id, ref BuildOrder order, BuildingId workplace) => { },
                (ref BuildOrder order) => { order.Completed = true; completeCalls++; },
                (ref BuildOrder order) => { },
                _ => completedEvents++);

            tick.Tick(0.1f);
            tick.Tick(0.1f);

            Assert.That(completeCalls, Is.EqualTo(1));
            Assert.That(completedEvents, Is.EqualTo(1));
            Assert.That(active.Count, Is.EqualTo(0));
        }
    }

    internal sealed class FakeBuildWorkplaceResolver : IBuildWorkplaceResolver
    {
        private readonly BuildingId _workplace;

        public FakeBuildWorkplaceResolver(BuildingId workplace)
        {
            _workplace = workplace;
        }

        public BuildingId ResolveBuildWorkplace() => _workplace;
    }
}
