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
        public void SaveLoadApplier_ContinuePath_RestoresSavedClockAndDoesNotInjectRunStartBaseline()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.AddNpc(new NpcDef { DefId = "npc_test" });
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 5, SizeY = 5, MaxHp = 300, IsHQ = true, WorkRoles = WorkRoleFlags.Build | WorkRoleFlags.HaulBasic, CapWood = new StorageCapsByLevel { L1 = 200 }, CapFood = new StorageCapsByLevel { L1 = 200 }, CapStone = new StorageCapsByLevel { L1 = 200 }, CapIron = new StorageCapsByLevel { L1 = 200 }, CapAmmo = new StorageCapsByLevel { L1 = 200 } });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsHouse = true });

            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new RunStartRuntime();
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.BuildOrderService = new FakeBuildOrderService();

            var savedHqId = new BuildingId(7);
            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Winter.ToString(),
                dayIndex = 4,
                timeScale = 2f,
                yearIndex = 3,
                dayTimer = 17.5f,
                world = new WorldDTO
                {
                    Roads = new List<CellPosI32> { new CellPosI32(12, 12), new CellPosI32(13, 12) },
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = savedHqId,
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(20, 20),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 300,
                            MaxHP = 300,
                            Wood = 9,
                            Food = 4,
                            Stone = 2,
                            Iron = 1,
                            Ammo = 0,
                        }
                    },
                    Npcs = new List<NpcState>
                    {
                        new NpcState
                        {
                            Id = new NpcId(5),
                            DefId = "npc_test",
                            Cell = new CellPos(22, 22),
                            Workplace = savedHqId,
                            CurrentJob = new JobId(123),
                            IsIdle = false
                        }
                    },
                    Towers = new List<TowerState>(),
                    Enemies = new List<EnemyState>()
                },
                build = new BuildDTO(),
                combat = new CombatDTO()
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(world.Buildings.Count, Is.EqualTo(1), "Continue/load should restore only saved buildings, not inject Wave 1 start package buildings.");
            Assert.That(world.Npcs.Count, Is.EqualTo(1), "Continue/load should restore only saved NPCs, not inject start package NPCs.");
            Assert.That(world.Towers.Count, Is.EqualTo(0), "Continue/load should not inject start package towers when save has none.");
            Assert.That(clock.CurrentSeason, Is.EqualTo(Season.Winter), "Continue/load should preserve saved season.");
            Assert.That(clock.DayIndex, Is.EqualTo(4), "Continue/load should preserve saved day.");
            Assert.That(clock.TimeScale, Is.EqualTo(2f), "Continue/load should preserve saved timescale instead of app default speed.");
            Assert.That(world.Buildings.Exists(savedHqId), Is.True, "Saved HQ should be restored by continue path.");
            Assert.That(services.StorageService.GetAmount(savedHqId, ResourceType.Wood), Is.EqualTo(9), "Continue/load should preserve saved HQ storage values instead of start-package seeding.");
            Assert.That(services.StorageService.GetAmount(savedHqId, ResourceType.Food), Is.EqualTo(4));
            Assert.That(services.StorageService.GetAmount(savedHqId, ResourceType.Stone), Is.EqualTo(2));
            Assert.That(services.StorageService.GetAmount(savedHqId, ResourceType.Iron), Is.EqualTo(1));
            Assert.That(grid.IsRoad(new CellPos(12, 12)), Is.True, "Continue/load should restore saved roads.");
            Assert.That(grid.IsRoad(new CellPos(13, 12)), Is.True, "Continue/load should restore saved roads.");

            var restoredNpc = world.Npcs.Get(new NpcId(5));
            Assert.That(restoredNpc.Workplace.Value, Is.EqualTo(savedHqId.Value));
            Assert.That(restoredNpc.CurrentJob.Value, Is.EqualTo(0), "Continue/load should clear stale runtime job references when restoring NPCs.");
            Assert.That(restoredNpc.IsIdle, Is.True, "Continue/load should reset NPCs to idle for runtime reassignment.");
        }

        [Test]
        public void SaveLoadApplier_ClearsStaleNpcCurrentJob_AndResetsIdleState_AfterLoad()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip save-load runtime-cache side effects assertion.");

            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.AddNpc(new NpcDef { DefId = "npc_test" });
            data.Add(new BuildingDef { DefId = "bld_warehouse", SizeX = 2, SizeY = 2, MaxHp = 20, IsWarehouse = true, WorkRoles = WorkRoleFlags.HaulBasic });
            data.Add(new BuildingDef { DefId = "bld_warehouse_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 20, IsWarehouse = true, WorkRoles = WorkRoleFlags.HaulBasic });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();

            var workplaceId = new BuildingId(3);
            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Spring.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = workplaceId,
                            DefId = "bld_warehouse_t1",
                            Anchor = new CellPos(4, 4),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 20,
                            MaxHP = 20
                        }
                    },
                    Npcs = new List<NpcState>
                    {
                        new NpcState
                        {
                            Id = new NpcId(7),
                            DefId = "npc_test",
                            Cell = new CellPos(5, 5),
                            Workplace = workplaceId,
                            CurrentJob = new JobId(999),
                            IsIdle = false
                        }
                    },
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO(),
                combat = new CombatDTO(),
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(world.Npcs.Exists(new NpcId(7)), Is.True);
            var npc = world.Npcs.Get(new NpcId(7));
            Assert.That(npc.CurrentJob.Value, Is.EqualTo(0), "Load-apply should clear stale CurrentJob references from save data.");
            Assert.That(npc.IsIdle, Is.True, "Load-apply should reset NPCs to idle so runtime re-assignment can rebuild consistently.");
        }

        [Test]
        public void SaveLoadApplier_RebuildsRunStartRuntimeCaches_AfterLoad()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip runtime-cache rebuild assertion.");

            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Spring.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(1),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(31, 31),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        }
                    }
                },
                build = new BuildDTO(),
                combat = new CombatDTO(),
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(services.RunStartRuntime, Is.Not.Null);
            Assert.That(services.RunStartRuntime.Lanes, Is.Not.Null);
            Assert.That(services.RunStartRuntime.SpawnGates.Count, Is.GreaterThan(0), "Spawn gates cache should be rebuilt after load.");
            Assert.That(services.RunStartRuntime.Lanes.Count, Is.GreaterThan(0), "Lane runtime cache should be rebuilt after load when an HQ exists in loaded world state.");
        }

        [Test]
        public void SaveLoadApplier_DefendWithAliveEnemies_AfterLoad_DoesNotDoubleSpawnUntilCleared()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.AddEnemy(new EnemyDef { DefId = "enemy_saved", MaxHp = 10 });
            data.AddEnemy(new EnemyDef { DefId = "enemy_test", MaxHp = 10 });

            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_test_after_load",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var combat = new SeasonalBastion.CombatService(services);
            services.CombatService = combat;

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Autumn.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 5f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(1),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(31, 31),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        }
                    },
                    Enemies = new List<EnemyState>
                    {
                        new EnemyState
                        {
                            Id = new EnemyId(9),
                            DefId = "enemy_saved",
                            Cell = new CellPos(32, 63),
                            Hp = 10,
                            Lane = 0,
                            MoveProgress01 = 0f,
                        }
                    }
                },
                build = new BuildDTO(),
                combat = new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 },
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(combat.IsActive, Is.True, "Combat should stay active after loading a defend snapshot.");
            Assert.That(world.Enemies.Count, Is.EqualTo(1), "Saved live enemies should be restored before any wave resume logic runs.");

            for (int i = 0; i < 8; i++)
                combat.Tick(0.5f);

            Assert.That(world.Enemies.Count, Is.EqualTo(1), "Combat should defer new wave spawn while restored enemies are still alive after load.");

            world.Enemies.ClearAll();

            for (int i = 0; i < 4; i++)
                combat.Tick(0.5f);

            Assert.That(world.Enemies.Count, Is.GreaterThan(0), "Once restored enemies are cleared, deferred defend wave should start spawning again.");
        }

        [Test]
        public void SaveLoadApplier_DefendWithoutAliveEnemies_AfterLoad_RestartsWaveSpawning()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.AddEnemy(new EnemyDef { DefId = "enemy_test", MaxHp = 10 });

            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_test_restart_after_load",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Autumn.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 5f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(1),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(31, 31),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        }
                    }
                },
                build = new BuildDTO(),
                combat = new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 },
            };

            var combat = new SeasonalBastion.CombatService(services);
            services.CombatService = combat;

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(combat.IsActive, Is.True, "Combat should become active after loading a defend snapshot.");
            Assert.That(world.Enemies.Count, Is.EqualTo(0), "This regression starts from a defend snapshot without restored live enemies.");

            for (int i = 0; i < 4; i++)
                combat.Tick(0.5f);

            Assert.That(world.Enemies.Count, Is.GreaterThan(0), "When no enemies are restored from save, defend load should resume wave spawning immediately.");
        }

        [Test]
        public void BuildOrderService_RebuildAfterLoad_RestoresExactlyOneActiveOrder_ForSingleActiveSite()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_test", SizeX = 1, SizeY = 1, MaxHp = 10 });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);

            var placeholderId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_test",
                Anchor = new CellPos(4, 4),
                Rotation = Dir4.N,
                IsConstructed = false,
                HP = 10,
                MaxHP = 10
            });
            var placeholder = world.Buildings.Get(placeholderId);
            placeholder.Id = placeholderId;
            world.Buildings.Set(placeholderId, placeholder);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_test",
                Anchor = new CellPos(4, 4),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 0.25f,
                WorkSecondsTotal = 2f,
                TargetBuilding = placeholderId,
                Kind = 0
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(4, 4), siteId);

            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int created1 = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();
            bool foundOrder1 = bos.TryGet(1, out var rebuilt1);
            bool foundExtraOrder1 = bos.TryGet(2, out _);

            int created2 = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();
            bool foundOrder2 = bos.TryGet(1, out var rebuilt2);
            bool foundExtraOrder2 = bos.TryGet(2, out _);

            Assert.That(created1, Is.EqualTo(1));
            Assert.That(foundOrder1, Is.True);
            Assert.That(rebuilt1.Site.Value, Is.EqualTo(siteId.Value));
            Assert.That(rebuilt1.TargetBuilding.Value, Is.EqualTo(placeholderId.Value));
            Assert.That(foundExtraOrder1, Is.False);
            Assert.That(created2, Is.EqualTo(1), "Rebuild should deterministically recreate the same single active order, not accumulate duplicates.");
            Assert.That(foundOrder2, Is.True);
            Assert.That(rebuilt2.Site.Value, Is.EqualTo(siteId.Value));
            Assert.That(rebuilt2.TargetBuilding.Value, Is.EqualTo(placeholderId.Value));
            Assert.That(foundExtraOrder2, Is.False);
        }

        [Test]
        public void BuildOrderService_RebuildAfterLoad_Smoke_RestoresPlaceOrderProgress_AndPlaceholderBinding()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_smoke_reload",
                SizeX = 1,
                SizeY = 1,
                BaseLevel = 1,
                MaxHp = 12,
                BuildCostsL1 = new[] { new CostDef { Resource = ResourceType.Wood, Amount = 4 } },
                BuildChunksL1 = 2
            });

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);

            var placeholderId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_smoke_reload",
                Anchor = new CellPos(7, 7),
                Rotation = Dir4.E,
                Level = 1,
                IsConstructed = false,
                HP = 12,
                MaxHP = 12
            });
            var placeholder = world.Buildings.Get(placeholderId); placeholder.Id = placeholderId; world.Buildings.Set(placeholderId, placeholder);
            grid.SetBuilding(new CellPos(7, 7), placeholderId);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_smoke_reload",
                TargetLevel = 1,
                Anchor = new CellPos(7, 7),
                Rotation = Dir4.E,
                IsActive = true,
                WorkSecondsDone = 1.5f,
                WorkSecondsTotal = 6f,
                TargetBuilding = placeholderId,
                Kind = 0,
                RemainingCosts = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 2 } },
                DeliveredSoFar = new List<CostDef> { new CostDef { Resource = ResourceType.Wood, Amount = 2 } }
            });
            var site = world.Sites.Get(siteId); site.Id = siteId; world.Sites.Set(siteId, site);
            grid.SetSite(new CellPos(7, 7), siteId);

            var bos = new BuildOrderService(services);
            services.BuildOrderService = bos;

            int created = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();

            Assert.That(created, Is.EqualTo(1));
            Assert.That(bos.TryGet(1, out var order), Is.True);
            Assert.That(order.Kind, Is.EqualTo(BuildOrderKind.PlaceNew));
            Assert.That(order.Site.Value, Is.EqualTo(siteId.Value));
            Assert.That(order.TargetBuilding.Value, Is.EqualTo(placeholderId.Value));
            Assert.That(order.BuildingDefId, Is.EqualTo("bld_smoke_reload"));
            Assert.That(order.WorkSecondsDone, Is.EqualTo(1.5f));
            Assert.That(order.WorkSecondsRequired, Is.EqualTo(6f));
            Assert.That(order.Completed, Is.False);
        }

        [Test]
        public void RunStartValidator_CollectRuntimeIssues_FlagsGateNotConnected()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });

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

            // Main road component connected to HQ.
            grid.SetRoad(new CellPos(3, 4), true);
            grid.SetRoad(new CellPos(3, 5), true);
            grid.SetRoad(new CellPos(3, 6), true);

            // Isolated gate road component.
            grid.SetRoad(new CellPos(10, 10), true);
            services.RunStartRuntime.SpawnGates.Add(new SpawnGate(1, new CellPos(10, 10), Dir4.W));

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "GATE_NOT_CONNECTED"), Is.True);
        }

        [Test]
        public void RunStartValidator_CollectRuntimeIssues_FlagsGateNotRoad()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(12, 12);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });

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
            grid.SetRoad(new CellPos(3, 5), true);

            // Gate exists in runtime but its cell is not a road on the map.
            services.RunStartRuntime.SpawnGates.Add(new SpawnGate(2, new CellPos(9, 9), Dir4.W));

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "GATE_NOT_ROAD"), Is.True);
        }

        [Test]
        public void RunStartFacade_TryApply_FailsFastOnInvalidHeader_WithoutCreatingPartialWorldState()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });

            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.StorageService = new FakeStorageService();
            services.RunStartRuntime = new RunStartRuntime();

            var cfgJson = @"{
              ""schemaVersion"": 1,
              ""coordSystem"": { ""origin"": ""top-left"", ""indexing"": ""0-based"", ""notes"": ""invalid header"" },
              ""map"": { ""width"": 16, ""height"": 16, ""buildableRect"": { ""xMin"": 0, ""yMin"": 0, ""xMax"": 15, ""yMax"": 15 } },
              ""lockedInvariants"": [""HQ_REQUIRED""] ,
              ""roads"": [ { ""x"": 3, ""y"": 4 } ],
              ""initialBuildings"": [
                { ""defId"": ""bld_hq_t1"", ""anchor"": { ""x"": 3, ""y"": 3 }, ""rotation"": ""N"" }
              ]
            }";

            bool ok = SeasonalBastion.RunStart.RunStartFacade.TryApply(services, cfgJson, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("coordSystem.origin"));
            Assert.That(world.Buildings.Count, Is.EqualTo(0), "Invalid header should fail before creating any building state.");
            Assert.That(world.Npcs.Count, Is.EqualTo(0), "Invalid header should fail before spawning any NPC.");
            Assert.That(grid.IsRoad(new CellPos(3, 4)), Is.False, "Invalid header should fail before applying roads/world mutations.");
            Assert.That(services.RunStartRuntime.Lanes.Count, Is.EqualTo(0));
            Assert.That(services.RunStartRuntime.SpawnGates.Count, Is.EqualTo(0));
        }

        [Test]
        public void RunStartWorldBuilder_ApplyWorld_FailsFast_WhenBuildingDefIsMissing()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.RunStartRuntime = new RunStartRuntime();
            services.WorldIndex = new WorldIndexService(world, data);

            var cfgJson = @"{
              ""schemaVersion"": 1,
              ""coordSystem"": { ""origin"": ""bottom-left"", ""indexing"": ""xy"", ""notes"": ""test"" },
              ""map"": { ""width"": 16, ""height"": 16, ""buildableRect"": { ""xMin"": 0, ""yMin"": 0, ""xMax"": 15, ""yMax"": 15 } },
              ""lockedInvariants"": [""HQ_REQUIRED""],
              ""initialBuildings"": [
                { ""defId"": ""bld_missing_def_t1"", ""anchor"": { ""x"": 4, ""y"": 4 }, ""rotation"": ""N"" }
              ]
            }";

            bool parsed = SeasonalBastion.RunStart.RunStartInputParser.TryParseConfig(cfgJson, out var cfg, out var parseError);
            Assert.That(parsed, Is.True, parseError);

            var ctx = new SeasonalBastion.RunStart.RunStartBuildContext();
            bool ok = SeasonalBastion.RunStart.RunStartWorldBuilder.ApplyWorld(services, cfg, ctx, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("BuildingDef not found"));
            Assert.That(world.Buildings.Count, Is.EqualTo(0), "World builder should fail before creating any building state for missing defs.");
        }

        [Test]
        public void RunStartValidator_CollectRuntimeIssues_FlagsMissingHq()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(8, 8);
            var data = new TestDataRegistry();
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.RunStartRuntime = new RunStartRuntime();
            grid.SetRoad(new CellPos(1, 1), true);

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "HQ_MISSING"), Is.True);
        }

        [Test]
        public void SaveLoadApplier_GridOccupancyMatchesRestoredWorld_AndClearsStaleOccupancy()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip save-load runtime-cache side effects assertion.");

            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 2, SizeY = 2, MaxHp = 50, IsHouse = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();

            grid.SetRoad(new CellPos(1, 1), true);
            grid.SetBuilding(new CellPos(2, 2), new BuildingId(999));
            grid.SetSite(new CellPos(3, 3), new SiteId(999));

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Spring.ToString(),
                dayIndex = 2,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Roads = new List<CellPosI32> { new CellPosI32(4, 4) },
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(10),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(6, 6),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        },
                        new BuildingState
                        {
                            Id = new BuildingId(11),
                            DefId = "bld_house_t1",
                            Anchor = new CellPos(10, 10),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = false,
                            HP = 25,
                            MaxHP = 50,
                        }
                    }
                },
                build = new BuildDTO
                {
                    Sites = new List<BuildSiteState>
                    {
                        new BuildSiteState
                        {
                            Id = new SiteId(20),
                            BuildingDefId = "bld_house_t1",
                            TargetLevel = 1,
                            Anchor = new CellPos(10, 10),
                            Rotation = Dir4.N,
                            IsActive = true,
                            WorkSecondsDone = 1f,
                            WorkSecondsTotal = 5f,
                            DeliveredSoFar = new List<CostDef>(),
                            RemainingCosts = new List<CostDef>(),
                            Kind = 0,
                        }
                    }
                },
                combat = new CombatDTO(),
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(grid.IsRoad(new CellPos(1, 1)), Is.False, "Old pre-load road occupancy must be cleared before applying snapshot.");
            Assert.That(grid.IsRoad(new CellPos(4, 4)), Is.True, "Saved roads must be restored exactly once.");
            var hqOcc = grid.Get(new CellPos(6, 6));
            Assert.That(hqOcc.Kind, Is.EqualTo(CellOccupancyKind.Building));
            Assert.That(hqOcc.Building.Value, Is.EqualTo(10));
            var siteOcc = grid.Get(new CellPos(10, 10));
            Assert.That(siteOcc.Kind, Is.EqualTo(CellOccupancyKind.Site));
            Assert.That(siteOcc.Site.Value, Is.EqualTo(20));
            Assert.That(grid.Get(new CellPos(2, 2)).Kind, Is.EqualTo(CellOccupancyKind.Empty));
            Assert.That(grid.Get(new CellPos(3, 3)).Kind, Is.EqualTo(CellOccupancyKind.Empty));
        }

        [Test]
        public void BuildOrderService_RebuildFromSites_AfterLoad_IsIdempotentAcrossRepeatedCalls()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            var world = new WorldState();

            var placeholderId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_test_placeholder",
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = false,
                HP = 10,
                MaxHP = 10
            });
            var placeholder = world.Buildings.Get(placeholderId);
            placeholder.Id = placeholderId;
            world.Buildings.Set(placeholderId, placeholder);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                BuildingDefId = "bld_test_placeholder",
                TargetLevel = 1,
                Anchor = new CellPos(3, 3),
                Rotation = Dir4.N,
                IsActive = true,
                WorkSecondsDone = 2f,
                WorkSecondsTotal = 10f,
                DeliveredSoFar = new List<CostDef>(),
                RemainingCosts = new List<CostDef>(),
                Kind = 0,
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);

            var services = MakeServices(bus, data, noti, new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            var bos = new BuildOrderService(services);

            int createdFirst = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();
            int createdSecond = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();

            Assert.That(createdFirst, Is.EqualTo(1));
            Assert.That(createdSecond, Is.EqualTo(1), "Reload helper may rebuild from scratch, but should not accumulate duplicates across repeated calls.");
            Assert.That(bos.TryGet(1, out var firstOrder), Is.True);
            Assert.That(firstOrder.Site.Value, Is.EqualTo(siteId.Value));
            Assert.That(bos.TryGet(2, out _), Is.False, "Repeated rebuild should reset/reconstruct logical active orders rather than append duplicates.");
        }

        [Test]
        public void SaveLoadApplier_InvalidSnapshot_FailsBeforeMutatingExistingRuntimeState()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();

            var existingId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100,
            });
            var existing = world.Buildings.Get(existingId);
            existing.Id = existingId;
            world.Buildings.Set(existingId, existing);
            grid.SetBuilding(new CellPos(1, 1), existingId);

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Spring.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(2),
                            DefId = "bld_missing_def_t1",
                            Anchor = new CellPos(5, 5),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 50,
                            MaxHP = 50,
                        }
                    }
                },
                build = new BuildDTO(),
                combat = new CombatDTO(),
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Missing BuildingDef"));
            Assert.That(world.Buildings.Exists(existingId), Is.True, "Invalid snapshot should fail before clearing/restoring runtime world state.");
            Assert.That(grid.Get(new CellPos(1, 1)).Building.Value, Is.EqualTo(existingId.Value));
        }

        [Test]
        public void SaveLoadApplier_DeepValidation_RejectsMissingEnemyTowerNpcAndBrokenSiteReference()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Spring.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(1),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(1, 1),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        }
                    },
                    Npcs = new List<NpcState>
                    {
                        new NpcState
                        {
                            Id = new NpcId(2),
                            DefId = "npc_missing",
                            Workplace = new BuildingId(999),
                            Cell = new CellPos(2, 2),
                        }
                    },
                    Towers = new List<TowerState>
                    {
                        new TowerState
                        {
                            Id = new TowerId(3),
                            Cell = new CellPos(4, 4),
                        }
                    },
                    Enemies = new List<EnemyState>
                    {
                        new EnemyState
                        {
                            Id = new EnemyId(4),
                            DefId = "enemy_missing",
                            Cell = new CellPos(5, 5),
                        }
                    }
                },
                build = new BuildDTO
                {
                    Sites = new List<BuildSiteState>
                    {
                        new BuildSiteState
                        {
                            Id = new SiteId(10),
                            BuildingDefId = "bld_hq_t1",
                            Anchor = new CellPos(1, 1),
                            Rotation = Dir4.N,
                            IsActive = true,
                            WorkSecondsTotal = 2f,
                            TargetBuilding = new BuildingId(777),
                            Kind = 0,
                        }
                    }
                },
                combat = new CombatDTO(),
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("TargetBuildingId").Or.Contain("Missing NpcDef").Or.Contain("Missing EnemyDef"));
        }

        [Test]
        public void SaveLoadApplier_DeepValidation_RejectsGridOverlapMismatch()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new RunStartRuntime();

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Spring.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(1),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(2, 2),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        },
                        new BuildingState
                        {
                            Id = new BuildingId(2),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(3, 3),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 100,
                            MaxHP = 100,
                        }
                    }
                },
                build = new BuildDTO(),
                combat = new CombatDTO(),
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("overlap").IgnoreCase);
        }

        [Test]
        public void NewRun_AfterSuccessfulLoad_DoesNotLeakLoadedWorldState()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip new-run-after-load regression.");

            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 5, SizeY = 5, MaxHp = 300, IsHQ = true, WorkRoles = WorkRoleFlags.Build | WorkRoleFlags.HaulBasic, CapWood = new StorageCapsByLevel { L1 = 200 }, CapFood = new StorageCapsByLevel { L1 = 200 }, CapStone = new StorageCapsByLevel { L1 = 200 }, CapIron = new StorageCapsByLevel { L1 = 200 }, CapAmmo = new StorageCapsByLevel { L1 = 200 } });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsHouse = true });
            data.Add(new BuildingDef { DefId = "bld_farmhouse_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapFood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_lumbercamp_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 100 } });
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 3, SizeY = 3, MaxHp = 180, IsTower = true });
            data.AddEnemy(new EnemyDef { DefId = "enemy_saved", MaxHp = 8 });
            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new RunStartRuntime();
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.BuildOrderService = new FakeBuildOrderService();
            services.CombatService = new CombatService(services);

            var dto = new RunSaveDTO
            {
                schemaVersion = 1,
                season = Season.Winter.ToString(),
                dayIndex = 3,
                timeScale = 1f,
                yearIndex = 2,
                dayTimer = 4f,
                world = new WorldDTO
                {
                    Roads = new List<CellPosI32> { new CellPosI32(8, 8) },
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(50),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(20, 20),
                            Rotation = Dir4.N,
                            Level = 1,
                            IsConstructed = true,
                            HP = 300,
                            MaxHP = 300,
                            Wood = 33,
                        }
                    },
                    Enemies = new List<EnemyState>
                    {
                        new EnemyState { Id = new EnemyId(60), DefId = "enemy_saved", Cell = new CellPos(30, 30), Hp = 8, Lane = 0 }
                    }
                },
                build = new BuildDTO(),
                combat = new CombatDTO { IsDefendActive = true },
            };

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(world.Buildings.Exists(new BuildingId(50)), Is.True);
            Assert.That(world.Enemies.Count, Is.EqualTo(1));

            var loop = new GameLoop(services);
            loop.StartNewRun(seed: 123, startMapConfigJsonOrMarkdown: cfg.text);

            Assert.That(world.Buildings.Exists(new BuildingId(50)), Is.False, "New Run should clear previously loaded world state before applying fresh baseline config.");
            Assert.That(world.Enemies.Count, Is.EqualTo(0), "New Run should not leak enemies restored by prior load/apply.");
            Assert.That(grid.IsRoad(new CellPos(8, 8)), Is.False, "New Run should clear roads restored by prior load before applying fresh run-start state.");
        }
    }
}
