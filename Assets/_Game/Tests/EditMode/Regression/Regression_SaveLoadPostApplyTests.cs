using NUnit.Framework;
using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.TestTools;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_SaveLoadPostApplyTests
    {
        private static GameServices MakeServices(TestEventBus bus, TestDataRegistry data, WorldState world, GridMap grid)
        {
            var services = RegressionTestServiceFactory.MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world, grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.BuildOrderService = new FakeBuildOrderService();
            services.CombatService = new FakeCombatService();
            services.PopulationService = new FakePopulationService();
            services.RunStartRuntime = new RunStartRuntime();
            return services;
        }

        [Test]
        public void TryApply_FailsAndRollsBack_WhenPostLoadFindsUnrelatedTowerBacking()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });
            data.AddTower(new TowerDef { DefId = "bld_tower_arrow_t1", MaxHp = 100, AmmoMax = 12 });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);

            var originalId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_tower_arrow_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100,
            });
            var original = world.Buildings.Get(originalId);
            original.Id = originalId;
            world.Buildings.Set(originalId, original);
            grid.SetBuilding(new CellPos(1, 1), originalId);
            services.WorldIndex.RebuildAll();

            var dto = new RunSaveDTO
            {
                season = Season.Spring.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        MakeBuilding(10, "bld_tower_arrow_t1", 3, 3, constructed: true),
                        MakeBuilding(11, "bld_tower_arrow_t1", 9, 9, constructed: true),
                    },
                    Towers = new List<TowerState>
                    {
                        MakeTower(21, 9, 9, 12, 12, 100, 100)
                    },
                    Npcs = new List<NpcState>(),
                    Enemies = new List<EnemyState>(),
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO { Sites = new List<BuildSiteState>() },
                combat = new CombatDTO(),
                population = new PopulationDTO(),
            };

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SaveLoad\] Tower backing missing for building 10 \(bld_tower_arrow_t1\): no tower found at expected cell \(3,3\)\..*"));
            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[SaveLoad\] Post-apply validation failed for constructed building 10 \(bld_tower_arrow_t1\): Tower backing missing for building 10 \(bld_tower_arrow_t1\): no tower found at expected cell \(3,3\)\..*"));
            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Post-apply validation failed"));
            Assert.That(world.Buildings.Exists(originalId), Is.True, "Failed apply must roll back to previous runtime snapshot.");
            Assert.That(world.Buildings.Count, Is.EqualTo(1));
            Assert.That(grid.Get(new CellPos(1, 1)).Building.Value, Is.EqualTo(originalId.Value));
        }

        [Test]
        public void TowerBackingValidator_Passes_WhenTowerBelongsToExactBuilding()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var buildingId = world.Buildings.Create(MakeBuildingState("bld_tower_arrow_t1", 4, 4, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(4, 4), buildingId);

            var towerId = world.Towers.Create(MakeTower(0, 4, 4, 12, 12, 100, 100));
            var tower = world.Towers.Get(towerId);
            tower.Id = towerId;
            world.Towers.Set(towerId, tower);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool ok = TowerBackingValidator.ValidateBuildingHasCorrectTower(world, data, index, buildingId, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null.Or.Empty);
        }

        [Test]
        public void TowerBackingValidator_Fails_WhenTowerAtExpectedCellBelongsToDifferentBuilding()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);

            var wrongBuildingId = world.Buildings.Create(MakeBuildingState("bld_tower_arrow_t1", 7, 7, true));
            var wrongBuilding = world.Buildings.Get(wrongBuildingId);
            wrongBuilding.Id = wrongBuildingId;
            world.Buildings.Set(wrongBuildingId, wrongBuilding);
            grid.SetBuilding(new CellPos(7, 7), wrongBuildingId);

            var victimBuildingId = world.Buildings.Create(MakeBuildingState("bld_tower_arrow_t1", 1, 1, true));
            var victimBuilding = world.Buildings.Get(victimBuildingId);
            victimBuilding.Id = victimBuildingId;
            world.Buildings.Set(victimBuildingId, victimBuilding);
            grid.SetBuilding(new CellPos(1, 1), victimBuildingId);

            var towerId = world.Towers.Create(MakeTower(0, 7, 7, 12, 12, 100, 100));
            var tower = world.Towers.Get(towerId);
            tower.Id = towerId;
            world.Towers.Set(towerId, tower);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool okWrong = TowerBackingValidator.ValidateBuildingHasCorrectTower(world, data, index, wrongBuildingId, out var wrongError);
            bool okVictim = TowerBackingValidator.ValidateBuildingHasCorrectTower(world, data, index, victimBuildingId, out var victimError);

            Assert.That(okWrong, Is.True, wrongError);
            Assert.That(okVictim, Is.False);
            Assert.That(victimError, Does.Contain("no tower found at expected cell (1,1)"));
        }

        [Test]
        public void TowerBackingValidator_Fails_WhenIndexedTowerMissingFromExpectedAnchorCell()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_tower_large_t1", SizeX = 3, SizeY = 3, BaseLevel = 1, MaxHp = 100, IsTower = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);

            var buildingAId = world.Buildings.Create(MakeBuildingState("bld_tower_large_t1", 0, 0, true));
            var buildingA = world.Buildings.Get(buildingAId);
            buildingA.Id = buildingAId;
            world.Buildings.Set(buildingAId, buildingA);

            var buildingBId = world.Buildings.Create(MakeBuildingState("bld_tower_large_t1", 1, 1, true));
            var buildingB = world.Buildings.Get(buildingBId);
            buildingB.Id = buildingBId;
            world.Buildings.Set(buildingBId, buildingB);

            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                grid.SetBuilding(new CellPos(buildingA.Anchor.X + x, buildingA.Anchor.Y + y), buildingAId);
                grid.SetBuilding(new CellPos(buildingB.Anchor.X + x, buildingB.Anchor.Y + y), buildingBId);
            }

            var towerId = world.Towers.Create(MakeTower(0, 2, 2, 12, 12, 100, 100));
            var tower = world.Towers.Get(towerId);
            tower.Id = towerId;
            world.Towers.Set(towerId, tower);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool ok = TowerBackingValidator.ValidateBuildingHasCorrectTower(world, data, index, buildingAId, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("no tower found at expected cell (1,1)"));
        }

        [Test]
        public void ConstructedBuildingInvariantValidator_Passes_ForValidConstructedBuilding()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHouse = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var buildingId = world.Buildings.Create(MakeBuildingState("bld_house_t1", 3, 4, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);

            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                grid.SetBuilding(new CellPos(3 + x, 4 + y), buildingId);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool ok = ConstructedBuildingInvariantValidator.Validate(world, grid, data, index, buildingId, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null.Or.Empty);
        }

        [Test]
        public void ConstructedBuildingInvariantValidator_Fails_WhenWorldIndexMissingBuilding()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_arrowtower_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });
            data.AddTower(new TowerDef { DefId = "bld_arrowtower_t1", MaxHp = 100, AmmoMax = 12 });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var buildingId = world.Buildings.Create(MakeBuildingState("bld_arrowtower_t1", 5, 5, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(5, 5), buildingId);

            bool ok = ConstructedBuildingInvariantValidator.Validate(world, grid, data, worldIndex: null, buildingId, out var error);

            Assert.That(ok, Is.True, "Null WorldIndex should skip index containment checks.");

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            ok = ConstructedBuildingInvariantValidator.Validate(world, grid, data, index, buildingId, out error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Tower backing missing"));
            Assert.That(error, Does.Contain($"building {buildingId.Value}"));
        }

        [Test]
        public void ConstructedBuildingInvariantValidator_Fails_WhenGridFootprintIsWrong()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHouse = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var buildingId = world.Buildings.Create(MakeBuildingState("bld_house_t1", 2, 2, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(2, 2), buildingId);
            grid.SetBuilding(new CellPos(3, 2), buildingId);
            grid.SetBuilding(new CellPos(2, 3), buildingId);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool ok = ConstructedBuildingInvariantValidator.Validate(world, grid, data, index, buildingId, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Grid mismatch"));
            Assert.That(error, Does.Contain($"building {buildingId.Value}"));
        }

        [Test]
        public void ConstructedBuildingInvariantValidator_Fails_WhenSiteStillReferencesConstructedBuilding()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsHouse = true });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var buildingId = world.Buildings.Create(MakeBuildingState("bld_house_t1", 6, 6, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(6, 6), buildingId);

            var siteId = world.Sites.Create(new BuildSiteState
            {
                Anchor = new CellPos(6, 6),
                TargetBuilding = buildingId,
                IsActive = true,
            });
            var site = world.Sites.Get(siteId);
            site.Id = siteId;
            world.Sites.Set(siteId, site);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool ok = ConstructedBuildingInvariantValidator.Validate(world, grid, data, index, buildingId, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain($"Site {siteId.Value} still references constructed building {buildingId.Value}"));
        }

        [Test]
        public void ConstructedBuildingInvariantValidator_Fails_WhenTowerBackingIsInvalid()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_tower_arrow_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsTower = true });
            data.AddTower(new TowerDef { DefId = "bld_tower_arrow_t1", MaxHp = 100, AmmoMax = 12 });

            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var buildingId = world.Buildings.Create(MakeBuildingState("bld_tower_arrow_t1", 8, 8, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(8, 8), buildingId);

            var index = new WorldIndexService(world, data);
            index.RebuildAll();

            bool ok = ConstructedBuildingInvariantValidator.Validate(world, grid, data, index, buildingId, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Tower backing missing"));
            Assert.That(error, Does.Contain($"building {buildingId.Value}"));
        }

        [Test]
        public void TryApply_Succeeds_ForIntermediateConstructionState_WithPlaceholderAndSite()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 80, BuildChunksL1 = 2 });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);
            var buildOrders = new BuildOrderService(services);
            services.BuildOrderService = buildOrders;

            var dto = new RunSaveDTO
            {
                season = Season.Spring.ToString(),
                dayIndex = 2,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        MakeBuilding(100, "bld_house_t1", 4, 4, constructed: false)
                    },
                    Towers = new List<TowerState>(),
                    Npcs = new List<NpcState>(),
                    Enemies = new List<EnemyState>(),
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO
                {
                    Sites = new List<BuildSiteState>
                    {
                        new BuildSiteState
                        {
                            Id = new SiteId(200),
                            BuildingDefId = "bld_house_t1",
                            TargetLevel = 1,
                            Anchor = new CellPos(4, 4),
                            Rotation = Dir4.N,
                            IsActive = true,
                            WorkSecondsDone = 2f,
                            WorkSecondsTotal = 10f,
                            DeliveredSoFar = new List<CostDef>(),
                            RemainingCosts = new List<CostDef>(),
                            Kind = 0,
                            TargetBuilding = new BuildingId(100),
                        }
                    }
                },
                combat = new CombatDTO(),
                population = new PopulationDTO(),
            };

            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.True, error);
            Assert.That(world.Buildings.Exists(new BuildingId(100)), Is.True);
            Assert.That(world.Sites.Exists(new SiteId(200)), Is.True);
            Assert.That(world.Buildings.Get(new BuildingId(100)).IsConstructed, Is.False);
            AssertIntermediateSiteState(services, new BuildingId(100), new SiteId(200), new CellPos(4, 4), 2, 2);
            Assert.That(buildOrders.TryGet(1, out var order), Is.True, "Active construction site should rebuild exactly one active build order after load.");
            Assert.That(order.Site.Value, Is.EqualTo(200));
            Assert.That(order.TargetBuilding.Value, Is.EqualTo(100));
            Assert.That(order.WorkSecondsDone, Is.EqualTo(2f));
            AssertNoDuplicateQueuedJobs(services.JobBoard);
        }

        [Test]
        public void TryApply_Succeeds_ForUpgradeIntermediateState_WithSiteAndExistingBuilding()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 150, IsHQ = true });
            data.AddUpgradeEdge(new UpgradeEdgeDef { Id = "hq_t1_to_t2", From = "bld_hq_t1", To = "bld_hq_t2" });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);
            var buildOrders = new BuildOrderService(services);
            services.BuildOrderService = buildOrders;

            var dto = new RunSaveDTO
            {
                season = Season.Spring.ToString(),
                dayIndex = 3,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        MakeBuilding(300, "bld_hq_t1", 6, 6, constructed: false)
                    },
                    Towers = new List<TowerState>(),
                    Npcs = new List<NpcState>(),
                    Enemies = new List<EnemyState>(),
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO
                {
                    Sites = new List<BuildSiteState>
                    {
                        new BuildSiteState
                        {
                            Id = new SiteId(301),
                            BuildingDefId = "bld_hq_t2",
                            TargetLevel = 2,
                            Anchor = new CellPos(6, 6),
                            Rotation = Dir4.N,
                            IsActive = true,
                            WorkSecondsDone = 4f,
                            WorkSecondsTotal = 12f,
                            DeliveredSoFar = new List<CostDef>(),
                            RemainingCosts = new List<CostDef>(),
                            Kind = 1,
                            TargetBuilding = new BuildingId(300),
                            FromDefId = "bld_hq_t1",
                            EdgeId = "hq_t1_to_t2"
                        }
                    }
                },
                combat = new CombatDTO(),
                population = new PopulationDTO(),
            };

            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.True, error);
            Assert.That(world.Buildings.Exists(new BuildingId(300)), Is.True);
            Assert.That(world.Sites.Exists(new SiteId(301)), Is.True);
            AssertUpgradeIntermediateState(services, new BuildingId(300), new SiteId(301), new CellPos(6, 6), 2, 2);
            Assert.That(buildOrders.TryGet(1, out var order), Is.True, "Upgrade site should rebuild one active order after load.");
            Assert.That(order.Site.Value, Is.EqualTo(301));
            Assert.That(order.TargetBuilding.Value, Is.EqualTo(300));
            AssertNoDuplicateQueuedJobs(services.JobBoard);
        }

        [Test]
        public void TryApply_Succeeds_WhenEnemiesAreAlive_AfterLoad()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.AddEnemy(new EnemyDef { DefId = "enemy_test", MaxHp = 10 });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);

            var dto = new RunSaveDTO
            {
                season = Season.Autumn.ToString(),
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        MakeBuilding(400, "bld_hq_t1", 2, 2, constructed: true)
                    },
                    Towers = new List<TowerState>(),
                    Npcs = new List<NpcState>(),
                    Enemies = new List<EnemyState>
                    {
                        new EnemyState { Id = new EnemyId(401), DefId = "enemy_test", Cell = new CellPos(10, 10), Hp = 10, Lane = 0 }
                    },
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO { Sites = new List<BuildSiteState>() },
                combat = new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 },
                population = new PopulationDTO(),
            };

            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.True, error);
            Assert.That(world.Enemies.Count, Is.EqualTo(1));
            AssertConstructedBuildingOccupancy(services, new BuildingId(400), 2, 2);
            AssertNoDuplicateQueuedJobs(services.JobBoard);
        }

        [Test]
        public void TryApply_FailsAndRollsBack_WhenRuntimeJobReferencesMissingTower()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsHouse = true });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);

            var buildingId = world.Buildings.Create(MakeBuildingState("bld_house_t1", 5, 5, true));
            var building = world.Buildings.Get(buildingId);
            building.Id = buildingId;
            world.Buildings.Set(buildingId, building);
            grid.SetBuilding(new CellPos(5, 5), buildingId);
            services.WorldIndex.RebuildAll();

            var board = (JobBoard)services.JobBoard;
            var jobId = board.Enqueue(new Job
            {
                Archetype = JobArchetype.ResupplyTower,
                Workplace = buildingId,
                Tower = new TowerId(999),
                Status = JobStatus.Created,
            });

            var validate = typeof(SaveLoadApplier).GetMethod("ValidatePostApplyRuntime", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validate, Is.Not.Null);
            LogAssert.Expect(UnityEngine.LogType.Warning, "[SaveLoad] Post-apply validation failed: job 1 references missing tower 999.");
            object[] args = { services, new RunSaveDTO { world = new WorldDTO(), build = new BuildDTO(), combat = new CombatDTO(), population = new PopulationDTO() }, null };
            validate.Invoke(null, args);
            var validationError = args[2] as string;

            Assert.That(validationError, Does.Contain("missing tower 999"));
            Assert.That(board.TryGet(jobId, out var liveJob), Is.True);
            Assert.That(liveJob.Tower.Value, Is.EqualTo(999));
        }

        private sealed class FakeBuildOrderService : IBuildOrderService
        {
            public event Action<int> OnOrderCompleted { add { } remove { } }
            public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation) => 0;
            public int CreateUpgradeOrder(BuildingId building) => 0;
            public int CreateRepairOrder(BuildingId building) => 0;
            public bool TryGet(int orderId, out BuildOrder order) { order = default; return false; }
            public void Cancel(int orderId) { }
            public bool CancelBySite(SiteId siteId) => false;
            public bool CancelByBuilding(BuildingId buildingId) => false;
            public void Tick(float dt) { }
            public void ClearAll() { }
        }

        private sealed class FakeCombatService : ICombatService
        {
            public bool IsActive { get; private set; }
            public event Action<string> OnWaveStarted { add { } remove { } }
            public event Action<string> OnWaveEnded { add { } remove { } }
            public void OnDefendPhaseStarted() => IsActive = true;
            public void OnDefendPhaseEnded() => IsActive = false;
            public void Tick(float dt) { }
            public void SpawnWave(string waveDefId) { }
            public void KillAllEnemies() { }
            public void ForceResolveWave() { }
            public void ResetAfterLoad(CombatDTO combat) => IsActive = combat != null && combat.IsDefendActive;
        }

        private sealed class FakePopulationService : IPopulationService
        {
            public PopulationState State => new PopulationState();
            public void Reset() { }
            public void RebuildDerivedState() { }
            public void OnDayStarted() { }
            public void LoadState(float growthProgressDays, int starvationDays, bool starvedToday) { }
        }

        private static BuildingState MakeBuilding(int id, string defId, int x, int y, bool constructed)
        {
            var st = MakeBuildingState(defId, x, y, constructed);
            st.Id = new BuildingId(id);
            return st;
        }

        private static BuildingState MakeBuildingState(string defId, int x, int y, bool constructed)
        {
            return new BuildingState
            {
                DefId = defId,
                Anchor = new CellPos(x, y),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = constructed,
                HP = 100,
                MaxHP = 100,
            };
        }

        [Test]
        public void TryApply_Succeeds_ForZeroAmmoTower_WithPendingResupply_AndResupplyCanContinue()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_armory_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsArmory = true, WorkRoles = WorkRoleFlags.Armory, CapAmmo = new StorageCapsByLevel { L1 = 200, L2 = 200, L3 = 200 } });
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.AddNpc(new NpcDef { DefId = "npc_test", BaseMoveSpeed = 1f, RoadSpeedMultiplier = 1.3f });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);
            var storage = new FakeStorageService();
            services.StorageService = storage;
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.AmmoService = new AmmoService(services);

            var dto = new RunSaveDTO
            {
                season = Season.Autumn.ToString(),
                dayIndex = 2,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        MakeBuilding(500, "bld_armory_t1", 2, 2, constructed: true),
                        MakeBuilding(501, "bld_hq_t1", 8, 8, constructed: true),
                    },
                    Towers = new List<TowerState>
                    {
                        MakeTower(510, 12, 12, 0, 20, 100, 100)
                    },
                    Npcs = new List<NpcState>
                    {
                        MakeNpc(520, "npc_test", new CellPos(2, 3), new BuildingId(500))
                    },
                    Enemies = new List<EnemyState>(),
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO { Sites = new List<BuildSiteState>() },
                combat = new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 },
                population = new PopulationDTO(),
            };

            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.True, error);
            storage.SetCap(new BuildingId(500), ResourceType.Ammo, 200);
            storage.SetAmount(new BuildingId(500), ResourceType.Ammo, 40);

            var ammo = (AmmoService)services.AmmoService;
            ammo.Tick(0.1f);

            AssertConstructedBuildingOccupancy(services, new BuildingId(500), 1, 1);
            AssertConstructedBuildingOccupancy(services, new BuildingId(501), 2, 2);
            Assert.That(world.Towers.Exists(new TowerId(510)), Is.True);
            Assert.That(world.Towers.Get(new TowerId(510)).Ammo, Is.EqualTo(0));
            Assert.That(services.JobBoard.TryPeekForWorkplace(new BuildingId(500), out var job), Is.True, "Tower resupply should resume after load for a zero-ammo tower.");
            Assert.That(job.Archetype, Is.EqualTo(JobArchetype.ResupplyTower));
            Assert.That(job.Tower.Value, Is.EqualTo(510));
            Assert.That(job.SourceBuilding.Value, Is.EqualTo(500));
            AssertNoDuplicateQueuedJobs(services.JobBoard);
        }

        [Test]
        public void TryApply_Succeeds_ForZeroAmmoTower_WithCreatedResupplyJob_DoesNotCreateDuplicateJobs()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_armory_t1", SizeX = 1, SizeY = 1, BaseLevel = 1, MaxHp = 100, IsArmory = true, WorkRoles = WorkRoleFlags.Armory, CapAmmo = new StorageCapsByLevel { L1 = 200, L2 = 200, L3 = 200 } });
            data.AddNpc(new NpcDef { DefId = "npc_test", BaseMoveSpeed = 1f, RoadSpeedMultiplier = 1.3f });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);
            var storage = new FakeStorageService();
            var board = new JobBoard();
            services.StorageService = storage;
            services.JobBoard = board;
            services.ClaimService = new ClaimService();
            services.AmmoService = new AmmoService(services);

            var dto = new RunSaveDTO
            {
                season = Season.Autumn.ToString(),
                dayIndex = 2,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        MakeBuilding(600, "bld_armory_t1", 2, 2, constructed: true)
                    },
                    Towers = new List<TowerState>
                    {
                        MakeTower(610, 10, 10, 0, 20, 100, 100)
                    },
                    Npcs = new List<NpcState>
                    {
                        MakeNpc(620, "npc_test", new CellPos(2, 3), new BuildingId(600))
                    },
                    Enemies = new List<EnemyState>(),
                    Roads = new List<CellPosI32>()
                },
                build = new BuildDTO { Sites = new List<BuildSiteState>() },
                combat = new CombatDTO(),
                population = new PopulationDTO(),
            };

            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.True, error);
            storage.SetCap(new BuildingId(600), ResourceType.Ammo, 200);
            storage.SetAmount(new BuildingId(600), ResourceType.Ammo, 40);

            var createdJobId = board.Enqueue(new Job
            {
                Archetype = JobArchetype.ResupplyTower,
                Status = JobStatus.Created,
                Workplace = new BuildingId(600),
                SourceBuilding = new BuildingId(600),
                Tower = new TowerId(610),
                Amount = 10,
                TargetCell = new CellPos(10, 10),
            });

            var ammo = (AmmoService)services.AmmoService;
            ammo.Tick(0.1f);

            Assert.That(board.TryGet(createdJobId, out var activeJob), Is.True);
            Assert.That(activeJob.Status, Is.EqualTo(JobStatus.Created));
            Assert.That(activeJob.Tower.Value, Is.EqualTo(610));
            Assert.That(board.CountActiveJobs(JobArchetype.ResupplyTower), Is.EqualTo(1), "Existing created resupply work should prevent duplicate recreation.");
            AssertNoDuplicateQueuedJobs(board);
        }

        private static void AssertConstructedBuildingOccupancy(GameServices services, BuildingId buildingId, int width, int height)
        {
            Assert.That(services.WorldState.Buildings.Exists(buildingId), Is.True);
            var building = services.WorldState.Buildings.Get(buildingId);
            Assert.That(building.IsConstructed, Is.True);
            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                var occ = services.GridMap.Get(new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy));
                Assert.That(occ.Kind, Is.EqualTo(CellOccupancyKind.Building));
                Assert.That(occ.Building.Value, Is.EqualTo(buildingId.Value));
            }
        }

        private static void AssertIntermediateSiteState(GameServices services, BuildingId buildingId, SiteId siteId, CellPos anchor, int width, int height)
        {
            Assert.That(services.WorldState.Buildings.Exists(buildingId), Is.True);
            Assert.That(services.WorldState.Sites.Exists(siteId), Is.True);

            var building = services.WorldState.Buildings.Get(buildingId);
            var site = services.WorldState.Sites.Get(siteId);

            Assert.That(building.IsConstructed, Is.False);
            Assert.That(site.TargetBuilding.Value, Is.EqualTo(buildingId.Value));
            Assert.That(site.Anchor.X, Is.EqualTo(anchor.X));
            Assert.That(site.Anchor.Y, Is.EqualTo(anchor.Y));
            Assert.That(site.Kind, Is.EqualTo(0));

            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                var occ = services.GridMap.Get(new CellPos(anchor.X + dx, anchor.Y + dy));
                Assert.That(occ.Kind, Is.EqualTo(CellOccupancyKind.Site));
                Assert.That(occ.Site.Value, Is.EqualTo(siteId.Value));
            }
        }

        private static void AssertUpgradeIntermediateState(GameServices services, BuildingId buildingId, SiteId siteId, CellPos anchor, int width, int height)
        {
            Assert.That(services.WorldState.Buildings.Exists(buildingId), Is.True);
            Assert.That(services.WorldState.Sites.Exists(siteId), Is.True);

            var building = services.WorldState.Buildings.Get(buildingId);
            var site = services.WorldState.Sites.Get(siteId);

            Assert.That(site.Kind, Is.EqualTo(1));
            Assert.That(site.TargetBuilding.Value, Is.EqualTo(buildingId.Value));
            Assert.That(building.Anchor.X, Is.EqualTo(anchor.X));
            Assert.That(building.Anchor.Y, Is.EqualTo(anchor.Y));

            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
            {
                var occ = services.GridMap.Get(new CellPos(anchor.X + dx, anchor.Y + dy));
                Assert.That(occ.Kind, Is.EqualTo(CellOccupancyKind.Building));
                Assert.That(occ.Building.Value, Is.EqualTo(buildingId.Value));
            }
        }

        private static void AssertNoDuplicateQueuedJobs(IJobBoard board)
        {
            if (board is not JobBoard concrete)
                return;

            var jobsField = typeof(JobBoard).GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic);
            var queuesField = typeof(JobBoard).GetField("_queues", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(jobsField?.GetValue(concrete), Is.TypeOf<Dictionary<int, Job>>());
            Assert.That(queuesField?.GetValue(concrete), Is.TypeOf<Dictionary<int, Queue<int>>>());

            var jobs = (Dictionary<int, Job>)jobsField.GetValue(concrete);
            var queues = (Dictionary<int, Queue<int>>)queuesField.GetValue(concrete);
            var seenJobs = new HashSet<int>();

            foreach (var pair in queues)
            {
                var seenPerQueue = new HashSet<int>();
                foreach (var jobId in pair.Value)
                {
                    Assert.That(seenPerQueue.Add(jobId), Is.True, $"Duplicate queued job {jobId} under workplace {pair.Key}.");
                    Assert.That(jobs.ContainsKey(jobId), Is.True, $"Queue references missing job {jobId}.");
                    Assert.That(jobs[jobId].Workplace.Value, Is.EqualTo(pair.Key));
                    seenJobs.Add(jobId);
                }
            }

            foreach (var pair in jobs)
            {
                if (pair.Value.Status == JobStatus.Created)
                    Assert.That(seenJobs.Contains(pair.Key), Is.True, $"Created job {pair.Key} should appear in a workplace queue.");
            }
        }

        private static NpcState MakeNpc(int id, string defId, CellPos cell, BuildingId workplace, JobId currentJob = default)
        {
            return new NpcState
            {
                Id = new NpcId(id),
                DefId = defId,
                Cell = cell,
                Workplace = workplace,
                CurrentJob = currentJob,
                IsIdle = currentJob.Value == 0,
            };
        }

        private static TowerState MakeTower(int id, int x, int y, int ammo, int ammoCap, int hp, int hpMax)
        {
            return new TowerState
            {
                Id = new TowerId(id),
                Cell = new CellPos(x, y),
                Ammo = ammo,
                AmmoCap = ammoCap,
                Hp = hp,
                HpMax = hpMax,
            };
        }
    }
}
