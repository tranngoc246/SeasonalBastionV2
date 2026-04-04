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

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(@"\[SaveLoad\] Post-apply validation failed for constructed building 10 \(bld_tower_arrow_t1\): WorldIndex is missing building 10 \(bld_tower_arrow_t1\)\..*"));
            bool ok = SaveLoadApplier.TryApply(services, dto, out var error, logErrors: false);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Post-apply validation failed"));
            Assert.That(world.Buildings.Exists(originalId), Is.True, "Failed apply must roll back to previous runtime snapshot.");
            Assert.That(world.Buildings.Count, Is.EqualTo(1));
            Assert.That(grid.Get(new CellPos(1, 1)).Building.Value, Is.EqualTo(originalId.Value));
        }

        [Test]
        public void TryApply_Succeeds_ForIntermediateConstructionState_WithPlaceholderAndSite()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 80 });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);

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
        }

        [Test]
        public void TryApply_Succeeds_ForUpgradeIntermediateState_WithSiteAndExistingBuilding()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_hq_t2", SizeX = 2, SizeY = 2, BaseLevel = 2, MaxHp = 150, IsHQ = true });

            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var services = MakeServices(bus, data, world, grid);

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
            LogAssert.Expect(UnityEngine.LogType.Error, "[SaveLoad] Post-apply validation failed: job 1 references missing tower 999.");
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
