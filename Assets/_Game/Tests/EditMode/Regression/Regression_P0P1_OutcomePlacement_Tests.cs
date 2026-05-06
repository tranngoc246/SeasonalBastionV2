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
        public void Placement_EntryCellAlreadyRoad_AllowsPlace()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);

            var grid = new GridMap(16, 16);
            var world = new WorldState();

            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_test_2x2",
                SizeX = 2,
                SizeY = 2,
                BaseLevel = 1,
                MaxHp = 10
            });

            var placement = new PlacementService(grid, world, data, index: null, bus);

            var anchor = new CellPos(5, 5);
            var rot = Dir4.N;
            var entry = new CellPos(anchor.X + 0, anchor.Y + 2);
            grid.SetRoad(entry, true);

            var vr = placement.ValidateBuilding("bld_test_2x2", anchor, rot);

            Assert.That(vr.Ok, Is.True, $"Expected OK when entry is road, but got {vr.FailReason}");
        }

        [Test]
        public void Placement_SeaTerrain_BlocksRoadAndBuildingPlacement()
        {
            var bus = new TestEventBus();
            var grid = new GridMap(16, 16);
            var terrain = new TerrainMap(16, 16);
            var world = new WorldState();

            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_test_1x1",
                SizeX = 1,
                SizeY = 1,
                BaseLevel = 1,
                MaxHp = 10
            });

            var placement = new PlacementService(grid, world, data, index: null, bus, terrain);

            terrain.Set(new CellPos(4, 4), TerrainType.Sea);
            terrain.Set(new CellPos(4, 5), TerrainType.Shore);
            grid.SetRoad(new CellPos(4, 6), true);

            Assert.That(placement.CanPlaceRoad(new CellPos(4, 4)), Is.False, "Road should not be placeable on Sea terrain.");

            var vr = placement.ValidateBuilding("bld_test_1x1", new CellPos(4, 4), Dir4.N);
            Assert.That(vr.Ok, Is.False, "Building should not be placeable on Sea terrain.");
        }

        [Test]
        public void JobExecutorRegistry_RepairWork_IsRegistered()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            var clock = new FakeRunClock();
            var outcome = new FakeRunOutcomeService();
            var world = new WorldState();

            var s = MakeServices(bus, data, noti, clock, outcome, world: world);
            var reg = new JobExecutorRegistry(s);

            Assert.DoesNotThrow(() =>
            {
                var ex = reg.Get(JobArchetype.RepairWork);
                Assert.That(ex, Is.Not.Null);
            });
        }

        [TestCase(1, 6f, 4)]
        [TestCase(2, 6f, 6)]
        [TestCase(3, 6f, 8)]
        public void HarvestExecutor_IronHut_ParamsMatchLocked(int level, float expSec, int expYield)
        {
            var t = typeof(HarvestExecutor);
            var mi = t.GetMethod("GetHarvestParams", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(mi, Is.Not.Null, "GetHarvestParams not found (signature changed?)");

            object[] args = new object[] { "bld_ironhut_t1", level, 0f, 0 };
            mi.Invoke(null, args);

            float workSec = (float)args[2];
            int yield = (int)args[3];

            Assert.That(workSec, Is.EqualTo(expSec));
            Assert.That(yield, Is.EqualTo(expYield));
        }

        [TestCase("bld_farmhouse", "bld_farmhouse")]
        [TestCase("bld_farmhouse_t1", "bld_farmhouse")]
        [TestCase("bld_lumbercamp_t2", "bld_lumbercamp")]
        [TestCase("bld_quarry_t3", "bld_quarry")]
        [TestCase(" bld_hq_t1 ", "bld_hq")]
        public void DefIdTierUtil_BaseId_StripsTierSuffix(string raw, string expected)
        {
            var actual = DefIdTierUtil.BaseId(raw?.Trim());
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void GameLoop_StartNewRun_CallsRunOutcomeResetOutcome()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var data = new TestDataRegistry();
            var clock = new FakeRunClock();
            var outcome = new FakeRunOutcomeService();

            var s = MakeServices(bus, data, noti, clock, outcome);
            var loop = new GameLoop(s);

            loop.StartNewRun(seed: 123, startMapConfigJsonOrMarkdown: null);

            Assert.That(outcome.ResetCalled, Is.EqualTo(1), "ResetOutcome should be called exactly once on StartNewRun");
        }

        [Test]
        public void RunOutcomeService_Defeat_SetsReasonAndPublishesRunEndedEvent()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", IsHQ = true, SizeX = 1, SizeY = 1 });

            var world = new WorldState();
            world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_hq_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 0,
                MaxHP = 10
            });

            var sut = new RunOutcomeService(bus, world, data);

            RunEndedEvent? seen = null;
            bus.Subscribe<RunEndedEvent>(e => seen = e);

            sut.Tick(0.016f);

            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(sut.Reason, Is.EqualTo(RunEndReason.HqDestroyed));
            Assert.That(seen.HasValue, Is.True, "RunEndedEvent should be published on defeat.");
            Assert.That(seen.Value.Outcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(seen.Value.Reason, Is.EqualTo(RunEndReason.HqDestroyed));
        }

        [Test]
        public void RunOutcomeService_Victory_SetsReasonAndResetOutcome_ClearsIt()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            var world = new WorldState();
            var sut = new RunOutcomeService(bus, world, data);

            RunEndedEvent? seen = null;
            bus.Subscribe<RunEndedEvent>(e => seen = e);

            bus.Publish(new WaveEndedEvent("Y2_SCALED_Y1_Winter_W4", 2, Season.Winter, 4, true, true));

            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(sut.Reason, Is.EqualTo(RunEndReason.FinalWaveCleared));
            Assert.That(seen.HasValue, Is.True, "RunEndedEvent should be published on victory.");
            Assert.That(seen.Value.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(seen.Value.Reason, Is.EqualTo(RunEndReason.FinalWaveCleared));

            sut.ResetOutcome();

            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Ongoing));
            Assert.That(sut.Reason, Is.EqualTo(RunEndReason.None));
        }

        [Test]
        public void RunOutcomeService_Victory_OnlyTriggersForFinalWaveYear2()
        {
            var bus = new TestEventBus();
            var sut = new RunOutcomeService(bus, new WorldState(), new TestDataRegistry());

            bus.Publish(new WaveEndedEvent("wave_y1_final", 1, Season.Winter, 4, true, true));
            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Ongoing), "Year 1 final wave must not trigger victory.");

            bus.Publish(new WaveEndedEvent("wave_y2_not_final", 2, Season.Winter, 3, false, false));
            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Ongoing), "Non-final Year 2 wave must not trigger victory.");

            bus.Publish(new WaveEndedEvent("wave_y2_final", 2, Season.Winter, 4, true, true));
            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(sut.Reason, Is.EqualTo(RunEndReason.FinalWaveCleared));
        }

        [Test]
        public void RunOutcomeService_OnlyTriggersRunEndedEventOnce_WhenOutcomeAlreadyEnded()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", IsHQ = true, SizeX = 1, SizeY = 1 });

            var world = new WorldState();
            world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_hq_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 0,
                MaxHP = 10
            });

            var sut = new RunOutcomeService(bus, world, data);
            int eventCount = 0;
            bus.Subscribe<RunEndedEvent>(_ => eventCount++);

            sut.Tick(0.016f);
            sut.Tick(0.016f);
            bus.Publish(new WaveEndedEvent("wave_y2_final", 2, Season.Winter, 4, true, true));

            Assert.That(eventCount, Is.EqualTo(1), "RunEndedEvent should be published only once.");
            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(sut.Reason, Is.EqualTo(RunEndReason.HqDestroyed));
        }

        [Test]
        public void RunOutcomeService_LoseHasPriorityOverWin_WhenHqAlreadyDead()
        {
            var bus = new TestEventBus();
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", IsHQ = true, SizeX = 1, SizeY = 1 });

            var world = new WorldState();
            world.Buildings.Create(new BuildingState
            {
                Id = default,
                DefId = "bld_hq_t1",
                Anchor = new CellPos(1, 1),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 0,
                MaxHP = 10
            });

            var sut = new RunOutcomeService(bus, world, data);

            sut.Tick(0.016f);
            bus.Publish(new WaveEndedEvent("wave_y2_final", 2, Season.Winter, 4, true, true));

            Assert.That(sut.Outcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(sut.Reason, Is.EqualTo(RunEndReason.HqDestroyed));
        }

        [Test]
        public void TickOrder_WhenRunEnded_DoesNotTickSimulationServices()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var build = new FakeBuildOrderService();
            var combat = new FakeCombatService();
            var resource = new FakeResourceFlowService();
            var ammo = new FakeAmmoTickService();
            var jobs = new FakeJobSchedulerTickService();
            var producer = new FakeProducerLoopTickService();
            var unlock = new FakeUnlockTickService();
            var outcome = new FakeRunOutcomeService();
            outcome.Defeat();

            var services = MakeServices(bus, new TestDataRegistry(), noti, new FakeRunClock(), outcome);
            services.BuildOrderService = build;
            services.CombatService = combat;
            services.ResourceFlowService = resource;
            services.AmmoService = ammo;
            services.JobScheduler = jobs;
            services.ProducerLoopService = producer;
            services.UnlockService = unlock;

            TickOrder.TickAll(services, 0.1f);

            Assert.That(build.TickCalls, Is.EqualTo(0));
            Assert.That(combat.TickCalls, Is.EqualTo(0));
            Assert.That(resource.TickCalls, Is.EqualTo(0));
            Assert.That(ammo.TickCalls, Is.EqualTo(0));
            Assert.That(jobs.TickCalls, Is.EqualTo(0));
            Assert.That(producer.TickCalls, Is.EqualTo(0));
            Assert.That(unlock.TickCalls, Is.EqualTo(0));
        }
    }
}
