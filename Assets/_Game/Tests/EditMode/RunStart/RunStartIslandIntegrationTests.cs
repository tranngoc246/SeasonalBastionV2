using NUnit.Framework;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class RunStartIslandIntegrationTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public BuildingDef GetBuilding(string id)
            {
                return id switch
                {
                    "bld_hq_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 5, SizeY = 5, IsHQ = true, IsWarehouse = true, MaxHp = 100 },
                    "bld_house_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsHouse = true, MaxHp = 60 },
                    "bld_farmhouse_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsProducer = true, MaxHp = 80 },
                    "bld_lumbercamp_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsProducer = true, MaxHp = 80 },
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
            public NpcDef GetNpc(string id) => new NpcDef { DefId = id, BaseMoveSpeed = 1f, RoadSpeedMultiplier = 1.3f };
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

        [Test]
        public void TryApply_IslandDraft_AppliesTerrainAndWorldSuccessfully()
        {
            var bus = new EventBus();
            var data = new TestDataRegistry();
            var world = new WorldState();
            var services = new GameServices
            {
                EventBus = bus,
                DataRegistry = data,
                NotificationService = new NotificationService(bus),
                RunClock = new RunClockService(bus),
                RunOutcomeService = new RunOutcomeService(bus, world, data),
                WorldState = world,
                GridMap = new GridMap(96, 96),
                TerrainMap = new TerrainMap(96, 96),
                RuntimeMapSize = new MapSize(96, 96),
                RunStartRuntime = new RunStartRuntime(),
                ResourcePatchService = new ResourcePatchService(),
                WorldIndex = new WorldIndexService(world, data)
            };

            string json = System.IO.File.ReadAllText(@"C:\UnityProjects\SeasonalBastionV2\Assets\_Game\Resources\RunStart\StartMapConfig_Island_96x96_v1.json");

            var ok = RunStartFacade.TryApply(services, json, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(services.TerrainMap.Get(new CellPos(0, 0)), Is.EqualTo(TerrainType.Sea));
            Assert.That(services.TerrainMap.Get(new CellPos(48, 48)), Is.EqualTo(TerrainType.Land));
            Assert.That(services.TerrainMap.Get(new CellPos(18, 48)), Is.EqualTo(TerrainType.Shore));
            Assert.That(services.WorldState.Buildings.Count, Is.GreaterThan(0));
            Assert.That(services.GridMap.IsRoad(new CellPos(48, 48)), Is.True);
        }
    }
}
