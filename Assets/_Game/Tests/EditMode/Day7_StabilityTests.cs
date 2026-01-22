using NUnit.Framework;
using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Day7_StabilityTests
    {
        // =========================
        // Test Doubles
        // =========================

        private sealed class TestEventBus : IEventBus
        {
            private readonly List<object> _published = new();
            private readonly Dictionary<Type, List<Delegate>> _subs = new();

            public IReadOnlyList<object> Published => _published;

            void IEventBus.Publish<T>(T evt)
            {
                _published.Add(evt!);

                if (_subs.TryGetValue(typeof(T), out var list))
                {
                    for (int i = 0; i < list.Count; i++)
                        ((Action<T>)list[i]).Invoke(evt);
                }
            }

            void IEventBus.Subscribe<T>(Action<T> handler)
            {
                if (!_subs.TryGetValue(typeof(T), out var list))
                {
                    list = new List<Delegate>();
                    _subs.Add(typeof(T), list);
                }
                list.Add(handler);
            }

            void IEventBus.Unsubscribe<T>(Action<T> handler)
            {
                if (_subs.TryGetValue(typeof(T), out var list))
                    list.Remove(handler);
            }
        }

        private sealed class TestDataRegistry : IDataRegistry
        {
            private readonly Dictionary<string, BuildingDef> _buildings = new(StringComparer.Ordinal);

            public void Add(BuildingDef def)
            {
                if (def == null) throw new ArgumentNullException(nameof(def));
                _buildings[def.DefId] = def;
            }

            public T GetDef<T>(string id) where T : UnityEngine.Object
                => throw new NotSupportedException("TestDataRegistry.GetDef<T> not used in these tests.");

            public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object
            {
                def = default;
                return false;
            }

            public BuildingDef GetBuilding(string id)
            {
                if (_buildings.TryGetValue(id, out var def)) return def;
                throw new KeyNotFoundException($"BuildingDef not found: {id}");
            }

            public EnemyDef GetEnemy(string id) => throw new NotSupportedException();
            public WaveDef GetWave(string id) => throw new NotSupportedException();
            public RewardDef GetReward(string id) => throw new NotSupportedException();
            public RecipeDef GetRecipe(string id) => throw new NotSupportedException();

            public NpcDef GetNpc(string id)
            {
                throw new NotImplementedException();
            }

            public TowerDef GetTower(string id)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Minimal BuildOrderService fake for testing PlacementService.CommitBuilding
        /// (CommitBuilding requires CreatePlaceOrder + TryGet).
        /// </summary>
        private sealed class FakeBuildOrders : IBuildOrderService
        {
            private int _next = 1;
            private readonly Dictionary<int, BuildOrder> _orders = new();

            public event Action<int> OnOrderCompleted;

            public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
            {
                int id = _next++;
                var order = new BuildOrder
                {
                    OrderId = id,
                    Kind = BuildOrderKind.PlaceNew,
                    BuildingDefId = buildingDefId,
                    TargetBuilding = new BuildingId(123), // deterministic id for test
                    Site = new SiteId(456),
                    RequiredCost = null,
                    Delivered = default,
                    WorkSecondsRequired = 1f,
                    WorkSecondsDone = 0f,
                    Completed = false
                };
                _orders[id] = order;
                return id;
            }

            public bool TryGet(int orderId, out BuildOrder order) => _orders.TryGetValue(orderId, out order);
            public IReadOnlyList<int> GetActiveOrderIds() => Array.Empty<int>();
            public void Cancel(int orderId) { }

            public int CreateUpgradeOrder(BuildingId building)
            {
                throw new NotImplementedException();
            }

            public int CreateRepairOrder(BuildingId building)
            {
                throw new NotImplementedException();
            }

            public void Tick(float dt)
            {
                throw new NotImplementedException();
            }

            public void ClearAll()
            {
                throw new NotImplementedException();
            }
        }

        // =========================
        // Helpers
        // =========================

        private static GameServices MakeServices(
            GridMap grid,
            WorldState world,
            IDataRegistry data,
            IEventBus bus,
            INotificationService noti,
            IPlacementService placement,
            IBuildOrderService buildOrders = null
        )
        {
            var s = new GameServices
            {
                GridMap = grid,
                WorldState = world,
                DataRegistry = data,
                EventBus = bus,
                NotificationService = noti,
                PlacementService = placement,
                BuildOrderService = buildOrders,
                WorldIndex = null
            };
            return s;
        }

        private static BuildingDef Def(string id, int w, int h, int baseLevel = 1)
        {
            return new BuildingDef
            {
                DefId = id,
                SizeX = w,
                SizeY = h,
                BaseLevel = baseLevel
            };
        }

        // =========================
        // Day 7 Tests (PART27 Buffer Day)
        // =========================

        [Test]
        public void Notification_Max3_NewestFirst()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);

            noti.Push("k1", "t1", "b1", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);
            noti.Push("k2", "t2", "b2", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);
            noti.Push("k3", "t3", "b3", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);
            noti.Push("k4", "t4", "b4", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);

            var visible = noti.GetVisible();

            Assert.That(visible.Count, Is.EqualTo(3), "Should cap visible notifications to 3");
            Assert.That(visible[0].Key, Is.EqualTo("k4"), "Newest-first (k4 should be first)");
            Assert.That(visible[1].Key, Is.EqualTo("k3"));
            Assert.That(visible[2].Key, Is.EqualTo("k2"));
        }

        [Test]
        public void Placement_NoRoadConnection_WhenNoRoadInEntryCross()
        {
            var grid = new GridMap(8, 8);
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(Def("b.house", 1, 1));

            var bus = new TestEventBus();
            var placement = new PlacementService(grid, world, data, index: null, bus);
            var noti = new NotificationService(bus);

            // anchor (2,2), rot N => entry/driveway = (2,3)
            var res = placement.ValidateBuilding("b.house", new CellPos(2, 2), Dir4.N);

            Assert.That(res.Ok, Is.False);
            Assert.That(res.Reason, Is.EqualTo(PlacementFailReason.NoRoadConnection));
            Assert.That(res.SuggestedRoadCell.X, Is.EqualTo(2));
            Assert.That(res.SuggestedRoadCell.Y, Is.EqualTo(3));
        }

        [Test]
        public void Placement_Commit_ConvertsDrivewayToRoad_AndPublishesRoadPlaced()
        {
            var grid = new GridMap(8, 8);
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(Def("b.house", 1, 1));

            var bus = new TestEventBus();
            var placement = new PlacementService(grid, world, data, index: null, bus);

            // Need BuildOrders bound, otherwise CommitBuilding returns default.
            var fakeOrders = new FakeBuildOrders();
            placement.BindBuildOrders(fakeOrders);

            // anchor (2,2), rot N => driveway (2,3)
            // Satisfy rule: road exists in cross of driveway (N/E/S/W or driveway itself).
            // Put road at (2,4) => in cross (north of driveway).
            grid.SetRoad(new CellPos(2, 4), true);

            // Commit should: convert driveway (2,3) to road, then create order, return TargetBuilding (123)
            var built = placement.CommitBuilding("b.house", new CellPos(2, 2), Dir4.N);

            Assert.That(built.Value, Is.EqualTo(123));
            Assert.That(grid.IsRoad(new CellPos(2, 3)), Is.True, "Driveway cell should be converted to road");

            bool publishedRoadPlacedOnDriveway = false;
            foreach (var e in bus.Published)
            {
                if (e is RoadPlacedEvent r && r.Cell.X == 2 && r.Cell.Y == 3)
                {
                    publishedRoadPlacedOnDriveway = true;
                    break;
                }
            }
            Assert.That(publishedRoadPlacedOnDriveway, Is.True, "Should publish RoadPlacedEvent for driveway conversion");
        }

        [Test]
        public void BuildOrder_Completes_ClearsSite_SetsBuildingOccupancy_PublishesBuildingPlaced()
        {
            var grid = new GridMap(10, 10);
            var world = new WorldState();
            var data = new TestDataRegistry();
            data.Add(Def("b.warehouse", 2, 2, baseLevel: 1));

            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var placement = new PlacementService(grid, world, data, index: null, bus);

            // Need road in entry cross:
            // anchor (3,3), w=2,h=2, rot N => cx=(2-1)/2=0 => entry = (3, 3+2)=(3,5)
            // Put road at (3,6) => north of entry, OK.
            grid.SetRoad(new CellPos(3, 6), true);

            var services = MakeServices(grid, world, data, bus, noti, placement);
            var buildOrders = new BuildOrderService(services);

            // Compose: optional, but keep services consistent
            services.BuildOrderService = buildOrders;

            // Create order => creates placeholder building + site + marks footprint as Site
            int orderId = buildOrders.CreatePlaceOrder("b.warehouse", new CellPos(3, 3), Dir4.N);
            Assert.That(orderId, Is.Not.EqualTo(0), "CreatePlaceOrder should succeed for valid placement");

            Assert.That(buildOrders.TryGet(orderId, out var order), Is.True);
            Assert.That(order.Kind, Is.EqualTo(BuildOrderKind.PlaceNew));
            Assert.That(order.Completed, Is.False);

            // Footprint occupied as Site right after create
            Assert.That(grid.Get(new CellPos(3, 3)).Kind, Is.EqualTo(CellOccupancyKind.Site));
            Assert.That(grid.Get(new CellPos(4, 4)).Kind, Is.EqualTo(CellOccupancyKind.Site));

            // Tick enough to complete
            buildOrders.Tick(order.WorkSecondsRequired + 10f);

            Assert.That(buildOrders.TryGet(orderId, out var after), Is.True);
            Assert.That(after.Completed, Is.True, "Order should be marked Completed");

            // Site cleared, building occupancy set
            Assert.That(grid.Get(new CellPos(3, 3)).Kind, Is.EqualTo(CellOccupancyKind.Building));
            Assert.That(grid.Get(new CellPos(4, 4)).Kind, Is.EqualTo(CellOccupancyKind.Building));

            // Building state should be IsConstructed = true
            var b = world.Buildings.Get(after.TargetBuilding);
            Assert.That(b.IsConstructed, Is.True);

            // Publish BuildingPlacedEvent on completion
            bool published = false;
            foreach (var e in bus.Published)
            {
                if (e is BuildingPlacedEvent bp && bp.Building.Value == after.TargetBuilding.Value)
                {
                    published = true;
                    break;
                }
            }
            Assert.That(published, Is.True, "Should publish BuildingPlacedEvent on completion");
        }

        [Test]
        public void Placement_NoOverlap_BuildingOnBuilding()
        {
            var grid = new GridMap(8, 8);
            var world = new WorldState();

            var data = new TestDataRegistry();
            data.Add(Def("b.house", 2, 2));

            var bus = new TestEventBus();
            var noti = new NotificationService(bus);
            var placement = new PlacementService(grid, world, data, index: null, bus);

            var existing = new BuildingId(1);
            grid.SetBuilding(new CellPos(2, 2), existing);
            grid.SetBuilding(new CellPos(3, 2), existing);
            grid.SetBuilding(new CellPos(2, 3), existing);
            grid.SetBuilding(new CellPos(3, 3), existing);

            var res = placement.ValidateBuilding("b.house", new CellPos(2, 2), Dir4.N);

            Assert.That(res.Ok, Is.False, "Should fail when overlapping existing Building occupancy.");

            Assert.That(res.Reason, Is.EqualTo(PlacementFailReason.Overlap));
        }

        [Test]
        public void Notification_Dedupe_MoveToTop_StillCap3()
        {
            var bus = new TestEventBus();
            var noti = new NotificationService(bus);

            noti.Push("k1", "t1", "b1", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);
            noti.Push("k2", "t2", "b2", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);
            noti.Push("k3", "t3", "b3", NotificationSeverity.Info, default, cooldownSeconds: 0f, dedupeByKey: false);

            noti.Push("k2", "t2b", "b2b", NotificationSeverity.Warning, default, cooldownSeconds: 0f, dedupeByKey: true);

            var visible = noti.GetVisible();

            Assert.That(visible.Count, Is.EqualTo(3), "Dedupe should not increase visible count beyond cap=3.");
            Assert.That(visible[0].Key, Is.EqualTo("k2"), "Dedupe should move updated key to top (newest-first).");
            Assert.That(visible[1].Key, Is.EqualTo("k3"));
            Assert.That(visible[2].Key, Is.EqualTo("k1"));

            Assert.That(visible[0].Title, Is.EqualTo("t2b"));
            Assert.That(visible[0].Body, Is.EqualTo("b2b"));
        }
    }
}
