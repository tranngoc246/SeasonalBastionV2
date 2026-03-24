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
        // Test Doubles (minimal)
        // -------------------------

        private sealed class TestEventBus : IEventBus
        {
            private readonly Dictionary<Type, List<Delegate>> _subs = new();

            public void Publish<T>(T evt) where T : struct
            {
                if (_subs.TryGetValue(typeof(T), out var list))
                {
                    for (int i = 0; i < list.Count; i++)
                        ((Action<T>)list[i]).Invoke(evt);
                }
            }

            public void Subscribe<T>(Action<T> handler) where T : struct
            {
                if (!_subs.TryGetValue(typeof(T), out var list))
                {
                    list = new List<Delegate>();
                    _subs.Add(typeof(T), list);
                }
                list.Add(handler);
            }

            public void Unsubscribe<T>(Action<T> handler) where T : struct
            {
                if (_subs.TryGetValue(typeof(T), out var list))
                    list.Remove(handler);
            }
        }

        private sealed class TestDataRegistry : IDataRegistry
        {
            public bool TryGetBuildableNode(string id, out BuildableNodeDef node) { node = null; return false; }
            public IReadOnlyList<UpgradeEdgeDef> GetUpgradeEdgesFrom(string fromNodeId) => Array.Empty<UpgradeEdgeDef>();
            public bool TryGetUpgradeEdge(string edgeId, out UpgradeEdgeDef edge) { edge = null; return false; }
            public bool IsPlaceableBuildable(string nodeId) => true;

            private readonly Dictionary<string, BuildingDef> _b = new(StringComparer.Ordinal);

            public void Add(BuildingDef def) => _b[def.DefId] = def;

            public BuildingDef GetBuilding(string id)
            {
                if (_b.TryGetValue(id, out var def)) return def;
                throw new KeyNotFoundException($"BuildingDef not found: {id}");
            }

            public bool TryGetBuilding(string id, out BuildingDef def) => _b.TryGetValue(id, out def);

            // Not used in these tests
            public EnemyDef GetEnemy(string id) => throw new NotSupportedException();
            public bool TryGetEnemy(string id, out EnemyDef def) { def = default; return false; }
            public WaveDef GetWave(string id) => throw new NotSupportedException();
            public bool TryGetWave(string id, out WaveDef def) { def = default; return false; }
            public RewardDef GetReward(string id) => throw new NotSupportedException();
            public bool TryGetReward(string id, out RewardDef def) { def = default; return false; }
            public RecipeDef GetRecipe(string id) => throw new NotSupportedException();
            public bool TryGetRecipe(string id, out RecipeDef def) { def = default; return false; }
            public NpcDef GetNpc(string id) => throw new NotSupportedException();
            public bool TryGetNpc(string id, out NpcDef def) { def = default; return false; }
            public TowerDef GetTower(string id) => throw new NotSupportedException();
            public bool TryGetTower(string id, out TowerDef def) { def = default; return false; }

            public T GetDef<T>(string id) where T : UnityEngine.Object => throw new NotSupportedException();
            public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object { def = default; return false; }
        }

        private sealed class FakeRunClock : IRunClock
        {
            public Season CurrentSeason { get; private set; } = Season.Spring;
            public int DayIndex { get; private set; } = 1;
            public Phase CurrentPhase { get; private set; } = Phase.Build;

            public float TimeScale { get; private set; } = 1f;
            public bool DefendSpeedUnlocked { get; set; } = false;

            public event Action<Season, int> OnSeasonDayChanged;
            public event Action<Phase> OnPhaseChanged;
            public event Action OnDayEnded;

            public void SetTimeScale(float scale) => TimeScale = scale;

            public void ForceSeasonDay(Season s, int dayIndex)
            {
                CurrentSeason = s;
                DayIndex = dayIndex;
                OnSeasonDayChanged?.Invoke(s, dayIndex);
            }

            // not used
            public void RaiseDayEnded() => OnDayEnded?.Invoke();
            public void RaisePhaseChanged(Phase p) { CurrentPhase = p; OnPhaseChanged?.Invoke(p); }
        }

        private sealed class FakeRunOutcomeService : IRunOutcomeService
        {
            public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;

            public int ResetCalled { get; private set; } = 0;

            // P0.3: you added this to interface + GameLoop calls it
            public void ResetOutcome()
            {
                ResetCalled++;
                Outcome = RunOutcome.Ongoing;
            }

            public void Defeat()
            {
                Outcome = RunOutcome.Defeat;
                OnRunEnded?.Invoke(Outcome);
            }

            public void Victory()
            {
                Outcome = RunOutcome.Victory;
                OnRunEnded?.Invoke(Outcome);
            }

            public void Abort()
            {
                Outcome = RunOutcome.Abort;
                OnRunEnded?.Invoke(Outcome);
            }

            public event Action<RunOutcome> OnRunEnded;
        }

        private sealed class FakeStorageService : IStorageService
        {
            public StorageSnapshot GetStorage(BuildingId building) => default;
            public bool CanStore(BuildingId building, ResourceType type) => true;
            public int GetAmount(BuildingId building, ResourceType type) => 0;
            public int GetCap(BuildingId building, ResourceType type) => 999;
            public int Add(BuildingId building, ResourceType type, int amount) => amount < 0 ? 0 : amount;
            public int Remove(BuildingId building, ResourceType type, int amount) => amount < 0 ? 0 : amount;
            public int GetTotal(ResourceType type) => 0;
        }

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

            var scheduler = new JobScheduler(world, board, claims, exec, bus, data, noti);

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
            var enqueue = new JobEnqueueService(world, board, workplacePolicy, resourcePolicy, cleanup);

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
        public void SaveLoadApplier_RebuildsRunStartRuntimeCaches_AfterLoad()
        {
            var bus = new TestEventBus();
            var world = new WorldState();
            var grid = new GridMap(64, 64);
            var data = new TestDataRegistry();
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
                world = new WorldDTO(),
                build = new BuildDTO(),
                combat = new CombatDTO(),
            };

            bool ok = SaveLoadApplier.TryApply(services, dto, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(services.RunStartRuntime, Is.Not.Null);
            Assert.That(services.RunStartRuntime.Lanes, Is.Not.Null);
            Assert.That(services.RunStartRuntime.Lanes.Count, Is.GreaterThan(0), "Lane runtime cache should be rebuilt after load.");
            Assert.That(services.RunStartRuntime.SpawnGates.Count, Is.GreaterThan(0), "Spawn gates cache should be rebuilt after load.");
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
            int activeCount1 = GetPrivateListCount<int>(bos, "_active");
            int orderCount1 = GetPrivateDictionaryCount<int, BuildOrder>(bos, "_orders");

            int created2 = bos.RebuildActivePlaceOrdersFromSitesAfterLoad();
            int activeCount2 = GetPrivateListCount<int>(bos, "_active");
            int orderCount2 = GetPrivateDictionaryCount<int, BuildOrder>(bos, "_orders");

            Assert.That(created1, Is.EqualTo(1));
            Assert.That(activeCount1, Is.EqualTo(1));
            Assert.That(orderCount1, Is.EqualTo(1));
            Assert.That(created2, Is.EqualTo(1), "Rebuild should deterministically recreate the same single active order, not accumulate duplicates.");
            Assert.That(activeCount2, Is.EqualTo(1));
            Assert.That(orderCount2, Is.EqualTo(1));
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

        private static int GetPrivateListCount<T>(object instance, string fieldName)
        {
            var f = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            var list = f?.GetValue(instance) as List<T>;
            return list?.Count ?? -1;
        }

        private static int GetPrivateDictionaryCount<TKey, TValue>(object instance, string fieldName)
        {
            var f = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = f?.GetValue(instance) as Dictionary<TKey, TValue>;
            return dict?.Count ?? -1;
        }
    }
}