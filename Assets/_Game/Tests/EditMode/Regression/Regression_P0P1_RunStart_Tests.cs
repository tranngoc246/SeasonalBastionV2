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
        public void RunStartStorageInitializer_Fails_WhenNoConstructedHqExists()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var data = new TestDataRegistry();
            var noti = new NotificationService(bus);
            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            services.StorageService = new FakeStorageService();

            bool ok = SeasonalBastion.RunStart.RunStartStorageInitializer.ApplyStartingStorage(services, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("constructed HQ"));
        }

        [Test]
        public void RunStartStorageInitializer_AppliesExpectedStartingStorage_ToConstructedHqOnly()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(20, 20);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", SizeX = 2, SizeY = 2, MaxHp = 80, WorkRoles = WorkRoleFlags.Build });

            var storage = new FakeStorageService();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.StorageService = storage;

            var hqId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var hq = world.Buildings.Get(hqId); hq.Id = hqId; world.Buildings.Set(hqId, hq);

            var otherId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_builderhut_t1",
                Anchor = new CellPos(8, 8),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 80,
                MaxHP = 80
            });
            var other = world.Buildings.Get(otherId); other.Id = otherId; world.Buildings.Set(otherId, other);

            bool ok = SeasonalBastion.RunStart.RunStartStorageInitializer.ApplyStartingStorage(services, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(storage.GetAmount(hqId, ResourceType.Wood), Is.EqualTo(30));
            Assert.That(storage.GetAmount(hqId, ResourceType.Stone), Is.EqualTo(20));
            Assert.That(storage.GetAmount(hqId, ResourceType.Food), Is.EqualTo(10));
            Assert.That(storage.GetAmount(hqId, ResourceType.Iron), Is.EqualTo(0));
            Assert.That(storage.GetAmount(hqId, ResourceType.Ammo), Is.EqualTo(0));
            Assert.That(storage.GetAmount(otherId, ResourceType.Wood), Is.EqualTo(0), "Starting storage should seed only HQ.");
            Assert.That(storage.GetAmount(otherId, ResourceType.Stone), Is.EqualTo(0));
            Assert.That(storage.GetAmount(otherId, ResourceType.Food), Is.EqualTo(0));
        }

        [Test]
        public void RunStartHqResolver_TryResolveHQTargetCell_PicksDeterministicLowestId_WhenMultipleHqCandidatesExist()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(24, 24);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, MaxHp = 120, IsHQ = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);

            var firstHqId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t2",
                Anchor = new CellPos(4, 6),
                Rotation = Dir4.N,
                Level = 2,
                IsConstructed = true,
                HP = 120,
                MaxHP = 120
            });
            var firstHq = world.Buildings.Get(firstHqId); firstHq.Id = firstHqId; world.Buildings.Set(firstHqId, firstHq);

            var secondHqId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(14, 10),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var secondHq = world.Buildings.Get(secondHqId); secondHq.Id = secondHqId; world.Buildings.Set(secondHqId, secondHq);

            bool ok = SeasonalBastion.RunStart.RunStartHqResolver.TryResolveHQTargetCell(services, out var target);

            Assert.That(ok, Is.True);
            Assert.That(firstHqId.Value, Is.LessThan(secondHqId.Value));
            Assert.That(target, Is.EqualTo(new CellPos(4, 6)), "Resolver should deterministically use the constructed HQ with the lowest BuildingId.");
        }

        [Test]
        public void RunStartHqResolver_TryResolveHQTargetCell_AcceptsCanonicalTieredFallback()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(20, 20);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, MaxHp = 100 });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);

            var hqId = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_hq_t2",
                Anchor = new CellPos(4, 6),
                Rotation = Dir4.N,
                Level = 2,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var hq = world.Buildings.Get(hqId); hq.Id = hqId; world.Buildings.Set(hqId, hq);

            bool ok = SeasonalBastion.RunStart.RunStartHqResolver.TryResolveHQTargetCell(services, out var target);

            Assert.That(ok, Is.True);
            Assert.That(target.X, Is.EqualTo(4));
            Assert.That(target.Y, Is.EqualTo(6));
        }

        [Test]
        public void RunStartPlacementHelper_TryPickValidAnchor_RelocatesToNearestValidCandidate()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            var blocked = new HashSet<(int x, int y)>
            {
                (8, 8),
                (8, 7),
                (7, 8)
            };
            IPlacementService placement = new DelegatingPlacementService((buildingDefId, anchor, rotation) =>
            {
                return blocked.Contains((anchor.X, anchor.Y))
                    ? new PlacementResult(false, PlacementFailReason.Overlap, anchor)
                    : new PlacementResult(true, PlacementFailReason.None, anchor);
            });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid, placement: placement);
            services.RunStartRuntime = new RunStartRuntime();

            bool ok = SeasonalBastion.RunStart.RunStartPlacementHelper.TryPickValidAnchor(
                services,
                "bld_test",
                new CellPos(8, 8),
                1,
                1,
                Dir4.N,
                out var finalAnchor);

            Assert.That(ok, Is.True);
            Assert.That(finalAnchor, Is.EqualTo(new CellPos(9, 8)), "Relocation should pick the nearest valid diamond-ring candidate.");
        }

        [Test]
        public void RunStartPlacementHelper_TryPickValidAnchor_DoesNotRelocateOutsideBuildableRect()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            var placement = new DelegatingPlacementService((buildingDefId, anchor, rotation) =>
            {
                return anchor.X == 5 && anchor.Y == 4
                    ? new PlacementResult(false, PlacementFailReason.Overlap, anchor)
                    : new PlacementResult(true, PlacementFailReason.None, anchor);
            });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid, placement: placement);
            services.RunStartRuntime = new RunStartRuntime
            {
                BuildableRect = new IntRect(4, 4, 5, 5)
            };

            bool ok = SeasonalBastion.RunStart.RunStartPlacementHelper.TryPickValidAnchor(
                services,
                "bld_test",
                new CellPos(5, 4),
                1,
                1,
                Dir4.N,
                out var finalAnchor);

            Assert.That(ok, Is.True);
            Assert.That(finalAnchor, Is.EqualTo(new CellPos(4, 4)), "Helper should skip out-of-rect candidates and pick the first valid in-rect candidate.");
        }

        [Test]
        public void RunStartValidator_CollectRuntimeIssues_FlagsUnbuiltNpcWorkplace()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_builderhut_t1", SizeX = 2, SizeY = 2, MaxHp = 80, WorkRoles = WorkRoleFlags.Build });

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.RunStartRuntime = new RunStartRuntime();

            var hqId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var hq = world.Buildings.Get(hqId); hq.Id = hqId; world.Buildings.Set(hqId, hq);
            grid.SetBuilding(new CellPos(2, 2), hqId);
            grid.SetBuilding(new CellPos(3, 2), hqId);
            grid.SetBuilding(new CellPos(2, 3), hqId);
            grid.SetBuilding(new CellPos(3, 3), hqId);
            grid.SetRoad(new CellPos(3, 4), true);

            var hutId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_builderhut_t1",
                Anchor = new CellPos(6, 6),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = false,
                HP = 80,
                MaxHP = 80
            });
            var hut = world.Buildings.Get(hutId); hut.Id = hutId; world.Buildings.Set(hutId, hut);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(4, 4),
                Workplace = hutId,
                IsIdle = true
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "NPC_WORKPLACE_UNBUILT"), Is.True);
            Assert.That(issues.Exists(x => x.Code == "NPC_WORKPLACE_MISSING"), Is.False);
        }

        [Test]
        public void RunStartValidator_CollectRuntimeIssues_FlagsBlockedNpcSpawn_AndMissingWorkplace()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.RunStartRuntime = new RunStartRuntime();

            var hqId = world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var hq = world.Buildings.Get(hqId); hq.Id = hqId; world.Buildings.Set(hqId, hq);
            grid.SetBuilding(new CellPos(2, 2), hqId);
            grid.SetBuilding(new CellPos(3, 2), hqId);
            grid.SetBuilding(new CellPos(2, 3), hqId);
            grid.SetBuilding(new CellPos(3, 3), hqId);
            grid.SetRoad(new CellPos(3, 4), true);
            grid.SetRoad(new CellPos(3, 1), true);
            grid.SetRoad(new CellPos(1, 3), true);
            grid.SetRoad(new CellPos(4, 3), true);

            var npcId = world.Npcs.Create(new NpcState
            {
                Id = default,
                DefId = "npc_test",
                Cell = new CellPos(2, 2),
                Workplace = new BuildingId(999),
                IsIdle = true
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "NPC_SPAWN_BLOCKED"), Is.True);
            Assert.That(issues.Exists(x => x.Code == "NPC_WORKPLACE_MISSING"), Is.True);
        }

        [Test]
        public void RunStartValidator_CollectRuntimeIssues_FlagsNpcSpawnOutOfBounds()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(8, 8);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.RunStartRuntime = new RunStartRuntime();
            grid.SetRoad(new CellPos(3, 4), true);

            var hqId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(2, 2),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });
            var hq = world.Buildings.Get(hqId); hq.Id = hqId; world.Buildings.Set(hqId, hq);
            grid.SetBuilding(new CellPos(2, 2), hqId);
            grid.SetBuilding(new CellPos(3, 2), hqId);
            grid.SetBuilding(new CellPos(2, 3), hqId);
            grid.SetBuilding(new CellPos(3, 3), hqId);

            var npcId = world.Npcs.Create(new NpcState
            {
                DefId = "npc_test",
                Cell = new CellPos(99, 99),
                Workplace = default,
                IsIdle = true
            });
            var npc = world.Npcs.Get(npcId); npc.Id = npcId; world.Npcs.Set(npcId, npc);

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "NPC_SPAWN_OOB"), Is.True);
        }

        [Test]
        public void RunStartFacade_TryApply_BuildsExpectedWave1StartPackageBaseline()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip Wave 1 start package baseline assertion.");

            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 5, SizeY = 5, MaxHp = 300, IsHQ = true, WorkRoles = WorkRoleFlags.Build | WorkRoleFlags.HaulBasic, CapWood = new StorageCapsByLevel { L1 = 200 }, CapFood = new StorageCapsByLevel { L1 = 200 }, CapStone = new StorageCapsByLevel { L1 = 200 }, CapIron = new StorageCapsByLevel { L1 = 200 }, CapAmmo = new StorageCapsByLevel { L1 = 200 } });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsHouse = true });
            data.Add(new BuildingDef { DefId = "bld_farmhouse_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapFood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 3, SizeY = 3, MaxHp = 180, IsTower = true });

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new RunStartRuntime();

            bool ok = SeasonalBastion.RunStart.RunStartFacade.TryApply(services, cfg.text, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(services.RunStartRuntime.SpawnGates.Count, Is.EqualTo(3), "Wave 1 baseline should expose 3 spawn gates.");
            Assert.That(services.RunStartRuntime.Lanes.Count, Is.EqualTo(3), "Wave 1 baseline should resolve 3 lane runtime rows.");
            Assert.That(world.Npcs.Count, Is.EqualTo(3), "Wave 1 start package should spawn exactly 3 initial NPCs.");
            Assert.That(world.Towers.Count, Is.EqualTo(1), "Wave 1 baseline should create exactly 1 initial arrow tower.");

            BuildingId hqId = default;
            BuildingId farmhouseId = default;
            BuildingId lumbercampId = default;
            int hqCount = 0;
            int houseCount = 0;

            foreach (var bid in world.Buildings.Ids)
            {
                if (!world.Buildings.Exists(bid)) continue;
                var bs = world.Buildings.Get(bid);
                if (bs.DefId == "bld_hq_t1") { hqId = bid; hqCount++; }
                if (bs.DefId == "bld_house_t1") houseCount++;
                if (bs.DefId == "bld_farmhouse_t1") farmhouseId = bid;
                if (bs.DefId == "bld_lumbercamp_t1") lumbercampId = bid;
            }

            Assert.That(hqCount, Is.EqualTo(1), "Wave 1 baseline should have exactly one HQ.");
            Assert.That(houseCount, Is.EqualTo(2), "Wave 1 baseline should have exactly two houses.");
            Assert.That(hqId.Value, Is.Not.EqualTo(0));
            Assert.That(farmhouseId.Value, Is.Not.EqualTo(0));
            Assert.That(lumbercampId.Value, Is.Not.EqualTo(0));

            Assert.That(services.StorageService.GetAmount(hqId, ResourceType.Wood), Is.EqualTo(30));
            Assert.That(services.StorageService.GetAmount(hqId, ResourceType.Stone), Is.EqualTo(20));
            Assert.That(services.StorageService.GetAmount(hqId, ResourceType.Food), Is.EqualTo(10));
            Assert.That(services.StorageService.GetAmount(hqId, ResourceType.Iron), Is.EqualTo(0));
            Assert.That(services.StorageService.GetAmount(hqId, ResourceType.Ammo), Is.EqualTo(0));

            var towerId = default(TowerId);
            foreach (var tid in world.Towers.Ids)
            {
                towerId = tid;
                break;
            }
            Assert.That(towerId.Value, Is.Not.EqualTo(0));
            var tower = world.Towers.Get(towerId);
            Assert.That(tower.Ammo, Is.EqualTo(tower.AmmoCap), "Initial arrow tower should start with full ammo as declared by config override.");

            int npcAtHq = 0, npcAtFarm = 0, npcAtLumber = 0;
            foreach (var nid in world.Npcs.Ids)
            {
                var npc = world.Npcs.Get(nid);
                Assert.That(grid.IsInside(npc.Cell), Is.True, $"NPC {nid.Value} should spawn inside map bounds.");
                var occ = grid.Get(npc.Cell).Kind;
                Assert.That(occ, Is.Not.EqualTo(CellOccupancyKind.Building), $"NPC {nid.Value} should not spawn into a building footprint.");
                Assert.That(occ, Is.Not.EqualTo(CellOccupancyKind.Site), $"NPC {nid.Value} should not spawn into a site footprint.");

                if (npc.Workplace.Value == hqId.Value) npcAtHq++;
                if (npc.Workplace.Value == farmhouseId.Value) npcAtFarm++;
                if (npc.Workplace.Value == lumbercampId.Value) npcAtLumber++;
            }

            Assert.That(npcAtHq, Is.EqualTo(1), "Wave 1 baseline should seed one NPC to HQ.");
            Assert.That(npcAtFarm, Is.EqualTo(1), "Wave 1 baseline should seed one NPC to farmhouse.");
            Assert.That(npcAtLumber, Is.EqualTo(1), "Wave 1 baseline should seed one NPC to lumbercamp.");
        }

        [Test]
        public void GameLoop_StartNewRun_Twice_ResetsWorldGridAndRunStartRuntimeWithoutLeakingState()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip New Run reset regression.");

            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 5, SizeY = 5, MaxHp = 300, IsHQ = true, WorkRoles = WorkRoleFlags.Build | WorkRoleFlags.HaulBasic, CapWood = new StorageCapsByLevel { L1 = 200 }, CapFood = new StorageCapsByLevel { L1 = 200 }, CapStone = new StorageCapsByLevel { L1 = 200 }, CapIron = new StorageCapsByLevel { L1 = 200 }, CapAmmo = new StorageCapsByLevel { L1 = 200 } });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsHouse = true });
            data.Add(new BuildingDef { DefId = "bld_farmhouse_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapFood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 3, SizeY = 3, MaxHp = 180, IsTower = true });

            var clock = new FakeRunClock();
            var outcome = new FakeRunOutcomeService();
            var notification = new NotificationService(bus);
            var services = MakeServices(bus, data, notification, clock, outcome, world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new RunStartRuntime();
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.BuildOrderService = new FakeBuildOrderService();

            if (services.TerrainMap != null)
            {
                for (int y = 0; y < services.TerrainMap.Height; y++)
                    for (int x = 0; x < services.TerrainMap.Width; x++)
                        services.TerrainMap.Set(new CellPos(x, y), TerrainType.Land);
            }

            var loop = new GameLoop(services);

            bool firstOk = SeasonalBastion.RunStart.RunStartFacade.TryApply(services, cfg.text, out var firstError);
            Assert.That(firstOk, Is.True, firstError);
            Assert.That(world.Buildings.Count, Is.EqualTo(6), "Baseline run should create 6 initial buildings including the arrow tower building.");
            Assert.That(world.Npcs.Count, Is.EqualTo(3), "Baseline run should create 3 NPCs.");
            Assert.That(world.Towers.Count, Is.EqualTo(1), "Baseline run should create 1 tower.");
            Assert.That(services.RunStartRuntime.SpawnGates.Count, Is.EqualTo(3));
            Assert.That(services.RunStartRuntime.Lanes.Count, Is.EqualTo(3));

            var rogueBuildingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_house_t1",
                Anchor = new CellPos(5, 5),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 50,
                MaxHP = 50
            });
            var rogueBuilding = world.Buildings.Get(rogueBuildingId); rogueBuilding.Id = rogueBuildingId; world.Buildings.Set(rogueBuildingId, rogueBuilding);
            grid.SetBuilding(new CellPos(5, 5), rogueBuildingId);

            var rogueNpcId = world.Npcs.Create(new NpcState { DefId = "npc_test", Cell = new CellPos(6, 6), Workplace = default, IsIdle = false });
            var rogueNpc = world.Npcs.Get(rogueNpcId); rogueNpc.Id = rogueNpcId; world.Npcs.Set(rogueNpcId, rogueNpc);

            var rogueEnemyId = world.Enemies.Create(new EnemyState { DefId = "enemy_test", Cell = new CellPos(7, 7), Hp = 10, Lane = 9 });
            var rogueEnemy = world.Enemies.Get(rogueEnemyId); rogueEnemy.Id = rogueEnemyId; world.Enemies.Set(rogueEnemyId, rogueEnemy);

            var rogueTowerId = world.Towers.Create(new TowerState { Cell = new CellPos(8, 8), Hp = 10, HpMax = 10, Ammo = 0, AmmoCap = 10 });
            var rogueTower = world.Towers.Get(rogueTowerId); rogueTower.Id = rogueTowerId; world.Towers.Set(rogueTowerId, rogueTower);

            var rogueSiteId = world.Sites.Create(new BuildSiteState { BuildingDefId = "bld_house_t1", Anchor = new CellPos(9, 9), Rotation = Dir4.N, IsActive = true, Kind = 0 });
            var rogueSite = world.Sites.Get(rogueSiteId); rogueSite.Id = rogueSiteId; world.Sites.Set(rogueSiteId, rogueSite);
            grid.SetSite(new CellPos(9, 9), rogueSiteId);
            grid.SetRoad(new CellPos(10, 10), true);

            services.RunStartRuntime.SpawnGates.Add(new SpawnGate(99, new CellPos(10, 10), Dir4.N));
            services.RunStartRuntime.Lanes[99] = new LaneRuntime(99, new CellPos(10, 10), Dir4.N, new CellPos(30, 30));
            services.RunStartRuntime.Zones["rogue_zone"] = new ZoneRect("rogue_zone", "Test", "", new IntRect(1, 1, 2, 2), 4);
            services.RunStartRuntime.LockedInvariants.Add("rogue invariant");

            clock.ForceSeasonDay(Season.Winter, 4);
            clock.SetTimeScale(3f);
            outcome.Defeat();

            loop.StartNewRun(seed: 222, startMapConfigJsonOrMarkdown: cfg.text);

            Assert.That(world.Buildings.Count, Is.EqualTo(6), "Second New Run should rebuild baseline buildings without leaking rogue building state.");
            Assert.That(world.Npcs.Count, Is.EqualTo(3), "Second New Run should rebuild baseline NPCs without duplicates.");
            Assert.That(world.Towers.Count, Is.EqualTo(1), "Second New Run should rebuild baseline tower state without duplicates.");
            Assert.That(world.Sites.Count, Is.EqualTo(0), "Second New Run should clear stale sites.");
            Assert.That(world.Enemies.Count, Is.EqualTo(0), "Second New Run should clear stale enemies.");
            Assert.That(world.Buildings.Exists(rogueBuildingId), Is.False, "Rogue building injected after first run should not survive second New Run.");
            Assert.That(world.Npcs.Exists(rogueNpcId), Is.False, "Rogue NPC injected after first run should not survive second New Run.");
            Assert.That(world.Enemies.Exists(rogueEnemyId), Is.False, "Rogue enemy injected after first run should not survive second New Run.");
            Assert.That(world.Towers.Exists(rogueTowerId), Is.False, "Rogue tower injected after first run should not survive second New Run.");
            Assert.That(world.Sites.Exists(rogueSiteId), Is.False, "Rogue site injected after first run should not survive second New Run.");
            Assert.That(grid.Get(new CellPos(10, 10)).Kind, Is.EqualTo(CellOccupancyKind.Empty), "Roads not in baseline config should be cleared on second New Run.");
            Assert.That(services.RunStartRuntime.SpawnGates.Count, Is.EqualTo(3), "RunStartRuntime spawn gates should be rebuilt from baseline, not accumulated.");
            Assert.That(services.RunStartRuntime.Lanes.Count, Is.EqualTo(3), "RunStartRuntime lanes should be rebuilt from baseline, not accumulated.");
            Assert.That(services.RunStartRuntime.Zones.ContainsKey("rogue_zone"), Is.False, "Transient zones injected between runs should be cleared before rebuild.");
            Assert.That(services.RunStartRuntime.LockedInvariants.Contains("rogue invariant"), Is.False, "Transient locked invariants injected between runs should be cleared before rebuild.");
            Assert.That(clock.CurrentSeason, Is.EqualTo(Season.Spring), "Second New Run should reset season to Spring.");
            Assert.That(clock.DayIndex, Is.EqualTo(1), "Second New Run should reset day index to 1.");
            Assert.That(clock.TimeScale, Is.EqualTo(1f), "Second New Run should reset clock speed to default build speed.");
            Assert.That(outcome.Outcome, Is.EqualTo(RunOutcome.Ongoing), "Second New Run should reset run outcome.");
            Assert.That(outcome.ResetCalled, Is.EqualTo(1), "Run outcome should be reset once for the single StartNewRun call in this regression setup.");
        }

    }
}
