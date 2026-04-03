using NUnit.Framework;
using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_P0P1_Tests
    {
        // -------------------------
        // Helpers
        // -------------------------

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

        // -------------------------
        // P1.1 Placement: entry cell is already road => OK
        // -------------------------

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

            // For 2x2: cx=(2-1)/2=0 => entry = (anchor.x + 0, anchor.y + h) = (5, 7)
            var entry = new CellPos(anchor.X + 0, anchor.Y + 2);

            // Entry cell already road
            grid.SetRoad(entry, true);

            // With P1.1 fix, this should be OK
            var vr = placement.ValidateBuilding("bld_test_2x2", anchor, rot);

            Assert.That(vr.Ok, Is.True, $"Expected OK when entry is road, but got {vr.FailReason}");
        }

        // -------------------------
        // P0.1 RepairWork registry: must be registered
        // -------------------------

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

        // -------------------------
        // P0.4 Iron Hut harvest params: 6s + 4/6/8
        // (private static GetHarvestParams => use reflection)
        // -------------------------

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

        // -------------------------
        // P0.3 RunOutcome reset must be called on StartNewRun
        // (EditMode unit: we verify GameLoop.StartNewRun triggers ResetOutcome)
        // -------------------------

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

            // Act
            loop.StartNewRun(seed: 123, startMapConfigJsonOrMarkdown: null);

            // Assert
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

        // -------------------------
        // P0.2 BuildOrder rebuild from Sites after Load
        // (requires your added method: RebuildActivePlaceOrdersFromSitesAfterLoad)
        // -------------------------

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
            Assert.That(inbox[0].Title, Is.EqualTo("Not enough resources"));
            Assert.That(inbox[0].Body, Does.Contain("Need 5 Wood"));
            Assert.That(inbox[0].Body, Does.Contain("have 3"));
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
            Assert.That(inbox[0].Title, Is.EqualTo("Can't place"));
            Assert.That(inbox[0].Body, Is.EqualTo("No road connection."));
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
            Assert.That(inbox[0].Title, Is.EqualTo("Locked"));
            Assert.That(inbox[0].Body, Does.Contain("unlock_hq_t2"));
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
            data.Add(new BuildingDef { DefId = "bld_warehouse", WorkRoles = WorkRoleFlags.HaulBasic });

            var world = new WorldState();
            var board = new JobBoard();
            var cleanup = new JobStateCleanupService(new ClaimService());
            var workplacePolicy = new JobWorkplacePolicy(data);
            var resourcePolicy = new ResourceLogisticsPolicy();
            var services = MakeServices(new TestEventBus(), data, new NotificationService(new TestEventBus()), new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            var enqueue = new JobEnqueueService(services, world, board, workplacePolicy, resourcePolicy, cleanup, new FakeHarvestTargetSelector(new CellPos(1, 1)));

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

            var buildingIds = new List<BuildingId> { wid };
            var workplacesWithNpc = new HashSet<int> { wid.Value };
            var haulMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHaulJobsIfNeeded(buildingIds, workplacesWithNpc, haulMap, rt => rt == ResourceType.Wood);
            enqueue.EnqueueHaulJobsIfNeeded(buildingIds, workplacesWithNpc, haulMap, rt => rt == ResourceType.Wood);

            Assert.That(haulMap.Count, Is.EqualTo(1));
            Assert.That(board.CountActiveJobs(JobArchetype.HaulBasic), Is.EqualTo(1));
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
            Assert.That(inbox.Count, Is.EqualTo(1));
            Assert.That(inbox[0].Title, Is.EqualTo("NPC không có việc để làm"));
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
            var services = MakeServices(bus, new TestDataRegistry(), new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: new WorldState());
            var board = new JobBoard();
            services.JobBoard = board;

            var deliver = new Dictionary<int, List<JobId>>();
            var work = new Dictionary<int, JobId>();
            var planner = new BuildJobPlanner(services, deliver, work);
            var siteId = new SiteId(7);
            var site = new BuildSiteState { Anchor = new CellPos(4, 4) };
            var workplace = new BuildingId(3);

            planner.EnsureBuildJobsForSite(siteId, site, workplace);
            planner.EnsureBuildJobsForSite(siteId, site, workplace);

            Assert.That(work.Count, Is.EqualTo(1));
            Assert.That(board.CountActiveJobs(JobArchetype.BuildWork), Is.EqualTo(1));
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
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            services.JobBoard = new JobBoard();

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
            var site = new BuildSiteState { Anchor = new CellPos(4, 4) };
            var workplace = new BuildingId(9);

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
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world);
            services.JobBoard = new JobBoard();

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
            var site = new BuildSiteState { Anchor = new CellPos(5, 5) };
            var workplace = new BuildingId(11);

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime
            {
                BuildableRect = new SeasonalBastion.RunStart.IntRect(4, 4, 5, 5)
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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
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
            services.RunStartRuntime = new RunStart.RunStartRuntime();

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
            var placement = new PlacementService(grid, world, data, index: null, bus);
            var services = MakeServices(bus, data, notification, clock, outcome, world: world, grid: grid, placement: placement);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
            services.JobBoard = new JobBoard();
            services.ClaimService = new ClaimService();
            services.BuildOrderService = new FakeBuildOrderService();

            var loop = new GameLoop(services);
            loop.StartNewRun(seed: 111, startMapConfigJsonOrMarkdown: cfg.text);

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

            services.RunStartRuntime.SpawnGates.Add(new SeasonalBastion.RunStart.SpawnGate(99, new CellPos(10, 10), Dir4.N));
            services.RunStartRuntime.Lanes[99] = new SeasonalBastion.RunStart.LaneRuntime(99, new CellPos(10, 10), Dir4.N, new CellPos(30, 30));
            services.RunStartRuntime.Zones["rogue_zone"] = new SeasonalBastion.RunStart.ZoneRect("rogue_zone", "Test", "", new SeasonalBastion.RunStart.IntRect(1, 1, 2, 2), 4);
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
            Assert.That(outcome.ResetCalled, Is.EqualTo(2), "Run outcome should be reset once per New Run call.");
        }

        [Test]
        public void SaveLoadApplier_ContinuePath_RestoresSavedClockAndDoesNotInjectRunStartBaseline()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 5, SizeY = 5, MaxHp = 300, IsHQ = true, WorkRoles = WorkRoleFlags.Build | WorkRoleFlags.HaulBasic, CapWood = new StorageCapsByLevel { L1 = 200 }, CapFood = new StorageCapsByLevel { L1 = 200 }, CapStone = new StorageCapsByLevel { L1 = 200 }, CapIron = new StorageCapsByLevel { L1 = 200 }, CapAmmo = new StorageCapsByLevel { L1 = 200 } });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 3, SizeY = 3, MaxHp = 120, IsHouse = true });

            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
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
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_warehouse", SizeX = 2, SizeY = 2, MaxHp = 20, IsWarehouse = true, WorkRoles = WorkRoleFlags.HaulBasic });
            data.Add(new BuildingDef { DefId = "bld_warehouse_t1", SizeX = 2, SizeY = 2, BaseLevel = 1, MaxHp = 20, IsWarehouse = true, WorkRoles = WorkRoleFlags.HaulBasic });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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

            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
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

            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime.SpawnGates.Add(new SeasonalBastion.RunStart.SpawnGate(1, new CellPos(10, 10), Dir4.W));

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime.SpawnGates.Add(new SeasonalBastion.RunStart.SpawnGate(2, new CellPos(9, 9), Dir4.W));

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
            grid.SetRoad(new CellPos(1, 1), true);

            var issues = new List<SeasonalBastion.RunStart.RunStartValidationIssue>();
            SeasonalBastion.RunStart.RunStartValidator.CollectRuntimeIssues(services, issues);

            Assert.That(issues.Exists(x => x.Code == "HQ_MISSING"), Is.True);
        }


    }
}

        [Test]
        public void SaveLoadApplier_GridOccupancyMatchesRestoredWorld_AndClearsStaleOccupancy()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(16, 16);
            var data = new TestDataRegistry();
            data.Add(new BuildingDef { DefId = "bld_hq_t1", SizeX = 2, SizeY = 2, MaxHp = 100, IsHQ = true });
            data.Add(new BuildingDef { DefId = "bld_house_t1", SizeX = 2, SizeY = 2, MaxHp = 50, IsHouse = true });
            var services = MakeServices(bus, data, new NotificationService(bus), new FakeRunClock(), new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();

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

            bool ok = SeasonalBastion.SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Missing BuildingDef"));
            Assert.That(world.Buildings.Exists(existingId), Is.True, "Invalid snapshot should fail before clearing/restoring runtime world state.");
            Assert.That(grid.Get(new CellPos(1, 1)).Building.Value, Is.EqualTo(existingId.Value));
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
            var clock = new FakeRunClock();
            var services = MakeServices(bus, data, new NotificationService(bus), clock, new FakeRunOutcomeService(), world: world, grid: grid);
            services.WorldIndex = new WorldIndexService(world, data);
            services.StorageService = new StorageService(world, data, bus);
            services.RunStartRuntime = new SeasonalBastion.RunStart.RunStartRuntime();
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
