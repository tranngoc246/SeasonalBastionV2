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
        public void JobDefIdUtil_NormalizeBuildingDefId_StripsTierSuffix(string raw, string expected)
        {
            var actual = JobDefIdUtil.NormalizeBuildingDefId(raw);
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
    }
}