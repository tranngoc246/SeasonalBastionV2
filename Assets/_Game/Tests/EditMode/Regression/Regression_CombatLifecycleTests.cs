using NUnit.Framework;
using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_CombatLifecycleTests
    {
        private static GameServices MakeCombatServices(TestDataRegistry data, FakeRunClock clock, FakeRunOutcomeService outcome, WorldState world = null, GridMap grid = null)
        {
            var bus = new TestEventBus();
            world ??= new WorldState();
            grid ??= new GridMap(64, 64);

            var services = RegressionTestServiceFactory.MakeServices(
                bus,
                data,
                new NotificationService(bus),
                clock,
                outcome,
                world,
                grid);

            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
            services.WaveCalendarResolver = new FakeWaveCalendarResolver();
            return services;
        }

        private static void SeedBasicLane(GameServices services)
        {
            services.RunStartRuntime.SpawnGates.Add(new SeasonalBastion.RunStart.SpawnGate(0, new CellPos(32, 63), Dir4.S));
            services.RunStartRuntime.Lanes[0] = new SeasonalBastion.RunStart.LaneRuntime(0, new CellPos(32, 63), Dir4.S, new CellPos(32, 32));
        }

        private static void SeedHq(WorldState world)
        {
            var id = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(31, 31),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100,
            });
            var b = world.Buildings.Get(id);
            b.Id = id;
            world.Buildings.Set(id, b);
        }

        private static List<EnemyId> GetEnemyIds(WorldState world)
        {
            var ids = new List<EnemyId>();
            foreach (var id in world.Enemies.Ids) ids.Add(id);
            ids.Sort((a, b) => a.Value.CompareTo(b.Value));
            return ids;
        }

        [Test]
        public void CombatLoad_WithSavedEnemies_DoesNotDoubleSpawnUntilCleared()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var clock = new FakeRunClock();

            clock.ForceSeasonDay(Season.Autumn, 1);
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();
            var services = MakeCombatServices(data, clock, outcome, world);
            SeedBasicLane(services);
            SeedHq(world);
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_resume_guard",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var combat = new CombatService(services);
            services.CombatService = combat;

            var restoredId = world.Enemies.Create(new EnemyState
            {
                DefId = "enemy_saved",
                Cell = new CellPos(32, 63),
                Hp = 10,
                Lane = 0,
                MoveProgress01 = 0f,
            });
            var restored = world.Enemies.Get(restoredId);
            restored.Id = restoredId;
            world.Enemies.Set(restoredId, restored);

            combat.ResetAfterLoad(new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 });

            for (int i = 0; i < 6; i++) combat.Tick(0.5f);
            var beforeClearIds = GetEnemyIds(world);
            Assert.That(beforeClearIds.Count, Is.EqualTo(1));
            Assert.That(beforeClearIds[0].Value, Is.EqualTo(restoredId.Value), "No new enemy id should appear before restored enemies are cleared.");

            world.Enemies.ClearAll();
            for (int i = 0; i < 4; i++) combat.Tick(0.5f);

            var afterResumeIds = GetEnemyIds(world);
            Assert.That(afterResumeIds.Count, Is.EqualTo(1));

            var spawned = world.Enemies.Get(afterResumeIds[0]);
            Assert.That(spawned.DefId, Is.EqualTo("enemy_test"), "Deferred spawn should replace the restored enemy with the scheduled wave enemy.");
            Assert.That(spawned.Hp, Is.GreaterThan(0));
            Assert.That(spawned.Lane, Is.EqualTo(0));
            Assert.That(spawned.WaveId, Is.EqualTo("wave_resume_guard"));
            Assert.That(spawned.WaveYear, Is.EqualTo(1));
            Assert.That(spawned.WaveSeason, Is.EqualTo(Season.Autumn));
            Assert.That(spawned.WaveDay, Is.EqualTo(1));
        }

        [Test]
        public void CombatLoad_WithoutSavedEnemies_ResumesSpawning()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var clock = new FakeRunClock();

            clock.ForceSeasonDay(Season.Autumn, 1);
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();
            var services = MakeCombatServices(data, clock, outcome, world);
            SeedBasicLane(services);
            SeedHq(world);
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_resume_now",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var combat = new CombatService(services);
            services.CombatService = combat;
            combat.ResetAfterLoad(new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 });

            for (int i = 0; i < 4; i++) combat.Tick(0.5f);
            Assert.That(world.Enemies.Count, Is.EqualTo(1));
        }

        [Test]
        public void WaveCompletesExactlyOnce_WhenSpawnDoneAndOwnedEnemiesCleared()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var clock = new FakeRunClock();

            clock.ForceSeasonDay(Season.Autumn, 1);
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();
            var services = MakeCombatServices(data, clock, outcome, world);
            SeedBasicLane(services);
            SeedHq(world);
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_complete_once",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var combat = new CombatService(services);
            services.CombatService = combat;
            int ended = 0;
            combat.OnWaveEnded += _ => ended++;

            combat.OnDefendPhaseStarted();
            for (int i = 0; i < 4; i++) combat.Tick(0.5f);
            Assert.That(world.Enemies.Count, Is.EqualTo(1));

            world.Enemies.ClearAll();
            for (int i = 0; i < 8; i++) combat.Tick(0.5f);

            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void DefeatStopsCombatProgression_AndPreventsFurtherSpawn()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var clock = new FakeRunClock();

            clock.ForceSeasonDay(Season.Autumn, 1);
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();
            var services = MakeCombatServices(data, clock, outcome, world);
            SeedBasicLane(services);
            SeedHq(world);
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_stop_on_defeat",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[]
                    {
                        new WaveEntryDef { EnemyId = "enemy_test", Count = 1 },
                        new WaveEntryDef { EnemyId = "enemy_test", Count = 1 }
                    }
                });

            var combat = new CombatService(services);
            services.CombatService = combat;
            combat.OnDefendPhaseStarted();
            combat.Tick(0.5f);
            Assert.That(world.Enemies.Count, Is.EqualTo(1));

            outcome.Defeat();
            for (int i = 0; i < 6; i++) combat.Tick(0.5f);

            Assert.That(combat.IsActive, Is.False);
            Assert.That(world.Enemies.Count, Is.EqualTo(1));
        }

        [Test]
        public void NewRunReset_ClearsCombatEnemyAndDeferredRuntimeState()
        {
            var cfg = UnityEngine.Resources.Load<UnityEngine.TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (cfg == null)
                Assert.Ignore("RunStart config resource is not available in EditMode test runtime; skip combat reset regression.");

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
            var services = RegressionTestServiceFactory.MakeServices(bus, data, new NotificationService(bus), clock, outcome, world, grid, new PlacementService(grid, world, data, index: null, bus));
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.BuildOrderService = new FakeBuildOrderService();
            services.CombatService = new CombatService(services);

            var loop = new GameLoop(services);
            loop.StartNewRun(seed: 111, startMapConfigJsonOrMarkdown: cfg.text);

            var enemyId = world.Enemies.Create(new EnemyState
            {
                DefId = "enemy_stale",
                Cell = new CellPos(5, 5),
                Hp = 10,
                Lane = 0,
                WaveId = "wave_old",
                WaveYear = 1,
                WaveSeason = Season.Autumn,
                WaveDay = 1,
            });
            var enemy = world.Enemies.Get(enemyId);
            enemy.Id = enemyId;
            world.Enemies.Set(enemyId, enemy);

            loop.StartNewRun(seed: 222, startMapConfigJsonOrMarkdown: cfg.text);

            Assert.That(world.Enemies.Count, Is.EqualTo(0));
            Assert.That(services.CombatService.IsActive, Is.False);

            for (int i = 0; i < 6; i++) services.CombatService.Tick(0.5f);
            Assert.That(world.Enemies.Count, Is.EqualTo(0), "After a fresh New Run in build phase, stale combat/deferred runtime state must not spawn enemies.");
        }

        [Test]
        public void EnemyDestruction_UpdatesLiveWaveAccounting_ForWaveCompletion()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var clock = new FakeRunClock();

            clock.ForceSeasonDay(Season.Autumn, 1);
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();
            var services = MakeCombatServices(data, clock, outcome, world);
            SeedBasicLane(services);
            SeedHq(world);
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_destroy_accounting",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var combat = new CombatService(services);
            services.CombatService = combat;
            int ended = 0;
            combat.OnWaveEnded += _ => ended++;
            combat.OnDefendPhaseStarted();

            for (int i = 0; i < 4; i++) combat.Tick(0.5f);
            var ids = new List<EnemyId>();
            foreach (var id in world.Enemies.Ids) ids.Add(id);
            Assert.That(ids.Count, Is.EqualTo(1));

            world.Enemies.Destroy(ids[0]);
            for (int i = 0; i < 8; i++) combat.Tick(0.5f);

            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void RestoredEnemiesClearedAfterLoad_AllowsDeferredSpawningToContinue()
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            var clock = new FakeRunClock();

            clock.ForceSeasonDay(Season.Autumn, 1);
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();
            var services = MakeCombatServices(data, clock, outcome, world);
            SeedBasicLane(services);
            SeedHq(world);
            services.WaveCalendarResolver = new FakeWaveCalendarResolver(
                new WaveDef
                {
                    DefId = "wave_deferred_resume",
                    Year = 1,
                    Season = Season.Autumn,
                    Day = 1,
                    Entries = new[] { new WaveEntryDef { EnemyId = "enemy_test", Count = 1 } }
                });

            var combat = new CombatService(services);
            services.CombatService = combat;

            var restoredId = world.Enemies.Create(new EnemyState
            {
                DefId = "enemy_saved",
                Cell = new CellPos(32, 63),
                Hp = 10,
                Lane = 0,
            });
            var restored = world.Enemies.Get(restoredId);
            restored.Id = restoredId;
            world.Enemies.Set(restoredId, restored);

            combat.ResetAfterLoad(new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 });
            for (int i = 0; i < 4; i++) combat.Tick(0.5f);

            var beforeClearIds = GetEnemyIds(world);
            Assert.That(beforeClearIds.Count, Is.EqualTo(1));
            Assert.That(beforeClearIds[0].Value, Is.EqualTo(restoredId.Value), "Before restored enemies are cleared, only the restored enemy should exist.");

            world.Enemies.ClearAll();
            for (int i = 0; i < 4; i++) combat.Tick(0.5f);

            var afterResumeIds = GetEnemyIds(world);
            Assert.That(afterResumeIds.Count, Is.EqualTo(1));

            var spawned = world.Enemies.Get(afterResumeIds[0]);
            Assert.That(spawned.DefId, Is.EqualTo("enemy_test"), "After restored enemies are cleared, deferred spawning should produce the scheduled wave enemy.");
            Assert.That(spawned.Hp, Is.GreaterThan(0));
            Assert.That(spawned.Lane, Is.EqualTo(0));
            Assert.That(spawned.WaveId, Is.EqualTo("wave_deferred_resume"));
            Assert.That(spawned.WaveYear, Is.EqualTo(1));
            Assert.That(spawned.WaveSeason, Is.EqualTo(Season.Autumn));
            Assert.That(spawned.WaveDay, Is.EqualTo(1));
        }
    }
}
