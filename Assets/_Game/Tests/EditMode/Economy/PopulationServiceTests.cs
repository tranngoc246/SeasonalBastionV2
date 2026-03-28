using NUnit.Framework;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class PopulationServiceTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public BuildingDef GetBuilding(string id)
            {
                return id switch
                {
                    "bld_hq_t1" => new BuildingDef
                    {
                        DefId = id,
                        BaseLevel = 1,
                        SizeX = 5,
                        SizeY = 5,
                        IsHQ = true,
                        IsWarehouse = true,
                        CapFood = new StorageCapsByLevel { L1 = 120, L2 = 180, L3 = 240 }
                    },
                    "bld_house_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsHouse = true },
                    "bld_house_t2" => new BuildingDef { DefId = id, BaseLevel = 2, SizeX = 3, SizeY = 3, IsHouse = true },
                    "bld_house_t3" => new BuildingDef { DefId = id, BaseLevel = 3, SizeX = 3, SizeY = 3, IsHouse = true },
                    _ => null
                };
            }

            public bool TryGetBuilding(string id, out BuildingDef def)
            {
                def = GetBuilding(id);
                return def != null;
            }

            public EnemyDef GetEnemy(string id) => throw new System.NotSupportedException();
            public bool TryGetEnemy(string id, out EnemyDef def) { def = default; return false; }
            public WaveDef GetWave(string id) => throw new System.NotSupportedException();
            public bool TryGetWave(string id, out WaveDef def) { def = default; return false; }
            public RewardDef GetReward(string id) => throw new System.NotSupportedException();
            public bool TryGetReward(string id, out RewardDef def) { def = default; return false; }
            public RecipeDef GetRecipe(string id) => throw new System.NotSupportedException();
            public bool TryGetRecipe(string id, out RecipeDef def) { def = default; return false; }
            public NpcDef GetNpc(string id) => new NpcDef { DefId = id, BaseMoveSpeed = 1f, RoadSpeedMultiplier = 2f };
            public bool TryGetNpc(string id, out NpcDef def) { def = GetNpc(id); return true; }
            public TowerDef GetTower(string id) => throw new System.NotSupportedException();
            public bool TryGetTower(string id, out TowerDef def) { def = default; return false; }
            public bool TryGetBuildableNode(string id, out BuildableNodeDef node) { node = default; return false; }
            public System.Collections.Generic.IReadOnlyList<UpgradeEdgeDef> GetUpgradeEdgesFrom(string fromNodeId) => System.Array.Empty<UpgradeEdgeDef>();
            public bool TryGetUpgradeEdge(string edgeId, out UpgradeEdgeDef edge) { edge = default; return false; }
            public bool IsPlaceableBuildable(string nodeId) => false;
            public T GetDef<T>(string id) where T : UnityEngine.Object => throw new System.NotSupportedException();
            public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object { def = default; return false; }
        }

        private static GameServices MakeServices(IWorldState world = null, IGridMap grid = null)
        {
            var bus = new EventBus();
            var data = new TestDataRegistry();
            var services = new GameServices
            {
                EventBus = bus,
                DataRegistry = data,
                NotificationService = new NotificationService(bus),
                RunClock = new RunClockService(bus),
                RunOutcomeService = new RunOutcomeService(bus, world ?? new WorldState(), data),
                WorldState = world ?? new WorldState(),
                GridMap = grid ?? new GridMap(32, 32)
            };

            services.StorageService = new StorageService(services.WorldState, services.DataRegistry, services.EventBus);
            services.PopulationService = new PopulationService(services);
            return services;
        }

        private static BuildingId AddBuilding(GameServices s, string defId, int x, int y, int level = 1, bool constructed = true, int food = 0)
        {
            var id = s.WorldState.Buildings.Create(new BuildingState
            {
                DefId = defId,
                Anchor = new CellPos(x, y),
                Rotation = Dir4.N,
                Level = level,
                IsConstructed = constructed,
                HP = 100,
                MaxHP = 100,
                Food = food
            });

            var st = s.WorldState.Buildings.Get(id);
            st.Id = id;
            s.WorldState.Buildings.Set(id, st);
            return id;
        }

        private static NpcId AddNpc(GameServices s, int x, int y)
        {
            var id = s.WorldState.Npcs.Create(new NpcState
            {
                DefId = "npc_villager_t1",
                Cell = new CellPos(x, y),
                Workplace = default,
                CurrentJob = default,
                IsIdle = true
            });

            var st = s.WorldState.Npcs.Get(id);
            st.Id = id;
            s.WorldState.Npcs.Set(id, st);
            return id;
        }

        [Test]
        public void RebuildDerivedState_ComputesPopulationCurrentAndHousingCap()
        {
            var services = MakeServices();
            AddBuilding(services, "bld_house_t1", 10, 10, level: 1, constructed: true);
            AddBuilding(services, "bld_house_t2", 14, 10, level: 2, constructed: true);
            AddBuilding(services, "bld_house_t3", 18, 10, level: 3, constructed: false);
            AddNpc(services, 1, 1);
            AddNpc(services, 2, 1);
            AddNpc(services, 3, 1);

            services.PopulationService.RebuildDerivedState();
            var state = services.PopulationService.State;

            Assert.That(state.PopulationCurrent, Is.EqualTo(3));
            Assert.That(state.PopulationCap, Is.EqualTo(6));
            Assert.That(state.DailyFoodNeed, Is.EqualTo(15));
        }

        [Test]
        public void OnDayStarted_ConsumesFiveFoodPerNpcPerDay()
        {
            var services = MakeServices();
            var hq = AddBuilding(services, "bld_hq_t1", 10, 10, food: 40);
            AddNpc(services, 1, 1);
            AddNpc(services, 2, 1);
            AddNpc(services, 3, 1);

            services.PopulationService.OnDayStarted();

            Assert.That(services.StorageService.GetAmount(hq, ResourceType.Food), Is.EqualTo(25));
            Assert.That(services.PopulationService.State.StarvationDays, Is.EqualTo(0));
            Assert.That(services.PopulationService.State.StarvedToday, Is.False);
        }

        [Test]
        public void OnDayStarted_GrowsOneNpc_WhenHousingAndFoodReserveAllow()
        {
            var services = MakeServices();
            AddBuilding(services, "bld_hq_t1", 10, 10, food: 60);
            AddBuilding(services, "bld_house_t1", 18, 10, level: 1, constructed: true);
            AddBuilding(services, "bld_house_t1", 22, 10, level: 1, constructed: true);
            AddNpc(services, 1, 1);
            AddNpc(services, 2, 1);
            AddNpc(services, 3, 1);

            services.PopulationService.OnDayStarted();
            var state = services.PopulationService.State;

            Assert.That(state.PopulationCurrent, Is.EqualTo(4), "Expected one new villager after a valid growth day.");
            Assert.That(state.GrowthProgressDays, Is.EqualTo(0f));
            Assert.That(services.WorldState.Npcs.Get(new NpcId(4)).Workplace.Value, Is.EqualTo(0));
            Assert.That(services.WorldState.Npcs.Get(new NpcId(4)).IsIdle, Is.True);
        }

        [Test]
        public void OnDayStarted_DoesNotGrow_WhenFoodReserveIsBelowTwoDays()
        {
            var services = MakeServices();
            AddBuilding(services, "bld_hq_t1", 10, 10, food: 20);
            AddBuilding(services, "bld_house_t1", 18, 10, level: 1, constructed: true);
            AddBuilding(services, "bld_house_t1", 22, 10, level: 1, constructed: true);
            AddNpc(services, 1, 1);
            AddNpc(services, 2, 1);
            AddNpc(services, 3, 1);

            services.PopulationService.OnDayStarted();
            var state = services.PopulationService.State;

            Assert.That(state.PopulationCurrent, Is.EqualTo(3));
            Assert.That(state.GrowthProgressDays, Is.EqualTo(0f));
        }

        [Test]
        public void OnDayStarted_IncrementsStarvation_WhenFoodIsInsufficient()
        {
            var services = MakeServices();
            var hq = AddBuilding(services, "bld_hq_t1", 10, 10, food: 7);
            AddNpc(services, 1, 1);
            AddNpc(services, 2, 1);
            AddNpc(services, 3, 1);

            services.PopulationService.OnDayStarted();
            var state = services.PopulationService.State;

            Assert.That(state.StarvedToday, Is.True);
            Assert.That(state.StarvationDays, Is.EqualTo(1));
            Assert.That(state.PopulationCurrent, Is.EqualTo(3));
            Assert.That(services.StorageService.GetAmount(hq, ResourceType.Food), Is.EqualTo(0));
        }

        [Test]
        public void LoadState_RestoresProgressAndDerivedValues()
        {
            var services = MakeServices();
            AddBuilding(services, "bld_house_t2", 14, 10, level: 2, constructed: true);
            AddNpc(services, 1, 1);
            AddNpc(services, 2, 1);

            services.PopulationService.LoadState(0.75f, 2, true);
            var state = services.PopulationService.State;

            Assert.That(state.GrowthProgressDays, Is.EqualTo(0.75f));
            Assert.That(state.StarvationDays, Is.EqualTo(2));
            Assert.That(state.StarvedToday, Is.True);
            Assert.That(state.PopulationCurrent, Is.EqualTo(2));
            Assert.That(state.PopulationCap, Is.EqualTo(4));
            Assert.That(state.DailyFoodNeed, Is.EqualTo(10));
        }
    }
}
