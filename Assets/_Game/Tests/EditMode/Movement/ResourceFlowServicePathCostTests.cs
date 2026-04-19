using NUnit.Framework;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class ResourceFlowServicePathCostTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public BuildingDef GetBuilding(string id)
            {
                return MakeWarehouseDef(id);
            }

            public bool TryGetBuilding(string id, out BuildingDef def)
            {
                def = MakeWarehouseDef(id);
                return true;
            }

            public EnemyDef GetEnemy(string id) => throw new System.NotSupportedException();
            public bool TryGetEnemy(string id, out EnemyDef def) { def = default; return false; }
            public WaveDef GetWave(string id) => throw new System.NotSupportedException();
            public bool TryGetWave(string id, out WaveDef def) { def = default; return false; }
            public RewardDef GetReward(string id) => throw new System.NotSupportedException();
            public bool TryGetReward(string id, out RewardDef def) { def = default; return false; }
            public RecipeDef GetRecipe(string id) => throw new System.NotSupportedException();
            public bool TryGetRecipe(string id, out RecipeDef def) { def = default; return false; }
            public NpcDef GetNpc(string id) => throw new System.NotSupportedException();
            public bool TryGetNpc(string id, out NpcDef def) { def = default; return false; }
            public TowerDef GetTower(string id) => throw new System.NotSupportedException();
            public bool TryGetTower(string id, out TowerDef def) { def = default; return false; }
            public BuildableNodeDef GetBuildableNode(string id) => throw new System.NotSupportedException();
            public bool TryGetBuildableNode(string id, out BuildableNodeDef node) { node = default; return false; }
            public UpgradeEdgeDef GetUpgradeEdge(string id) => throw new System.NotSupportedException();
            public bool TryGetUpgradeEdge(string id, out UpgradeEdgeDef edge) { edge = default; return false; }
            public System.Collections.Generic.IReadOnlyList<UpgradeEdgeDef> GetUpgradeEdgesFrom(string fromNodeId) => System.Array.Empty<UpgradeEdgeDef>();
            public bool IsPlaceableBuildable(string nodeId) => false;
            public T GetDef<T>(string id) where T : UnityEngine.Object => throw new System.NotSupportedException();
            public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object { def = default; return false; }
        }

        private static BuildingDef MakeWarehouseDef(string defId)
        {
            return new BuildingDef
            {
                DefId = defId,
                IsWarehouse = true,
                SizeX = 1,
                SizeY = 1,
                CapWood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapFood = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapStone = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapIron = new StorageCapsByLevel { L1 = 300, L2 = 600, L3 = 1000 },
                CapAmmo = new StorageCapsByLevel { L1 = 0, L2 = 0, L3 = 0 },
            };
        }

        private static BuildingState MakeConstructedStorage(BuildingId id, CellPos anchor, string defId)
        {
            return new BuildingState
            {
                Id = id,
                DefId = defId,
                Anchor = anchor,
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
            };
        }

        [Test]
        public void TryPickDest_PrefersLowerPathCost_OverLowerManhattanDistance()
        {
            var world = new WorldState();
            var data = new TestDataRegistry();
            var index = new WorldIndexService(world, data);
            var storage = new StorageService(world, data, null);
            var grid = new GridMap(10, 10);

            var start = new CellPos(0, 0);
            var destNear = world.Buildings.Create(MakeConstructedStorage(default, new CellPos(0, 3), "bld_warehouse_t1"));
            var destRoad = world.Buildings.Create(MakeConstructedStorage(default, new CellPos(9, 1), "bld_warehouse_t1"));
            index.RebuildAll();

            // Force the near candidate into a long all-ground detour:
            // block the direct vertical path except a single opening near the bottom.
            grid.SetBuilding(new CellPos(0, 1), new BuildingId(101));
            grid.SetBuilding(new CellPos(0, 2), new BuildingId(102));
            grid.SetBuilding(new CellPos(1, 1), new BuildingId(103));
            grid.SetBuilding(new CellPos(1, 2), new BuildingId(104));
            grid.SetBuilding(new CellPos(1, 3), new BuildingId(105));
            grid.SetBuilding(new CellPos(1, 4), new BuildingId(106));
            grid.SetBuilding(new CellPos(1, 5), new BuildingId(107));
            grid.SetBuilding(new CellPos(1, 6), new BuildingId(108));
            grid.SetBuilding(new CellPos(1, 7), new BuildingId(109));
            // opening remains at (1,8)

            // Far candidate gets a mostly-road route: one ground step down, then road across row 1.
            for (int x = 0; x <= 9; x++)
                grid.SetRoad(new CellPos(x, 1), true);

            var pathfinder = new NpcPathfinder(grid);
            Assert.That(pathfinder.TryEstimateCost(start, new CellPos(0, 3), out var nearCost), Is.True);
            Assert.That(pathfinder.TryEstimateCost(start, new CellPos(9, 1), out var roadCost), Is.True);
            Assert.That(roadCost, Is.LessThan(nearCost), "Fixture must guarantee far road destination is truly cheaper by path cost.");

            var sut = new ResourceFlowService(world, index, storage, pathfinder);

            bool ok = sut.TryPickDest(start, ResourceType.Wood, 1, out var pick);

            Assert.That(ok, Is.True);
            Assert.That(pick.Building.Value, Is.EqualTo(destRoad.Value), "Expected lower path-cost destination to beat lower Manhattan destination.");
        }

        [Test]
        public void TryPickSource_FallsBackToManhattan_WhenPathEstimateUnavailable()
        {
            var world = new WorldState();
            var data = new TestDataRegistry();
            var index = new WorldIndexService(world, data);
            var storage = new StorageService(world, data, null);

            var nearState = MakeConstructedStorage(default, new CellPos(2, 2), "bld_warehouse_t1");
            nearState.Wood = 20;
            var farState = MakeConstructedStorage(default, new CellPos(6, 2), "bld_warehouse_t1");
            farState.Wood = 20;

            var srcNear = world.Buildings.Create(nearState);
            var srcFar = world.Buildings.Create(farState);
            index.RebuildAll();

            var sut = new ResourceFlowService(world, index, storage, null);

            bool ok = sut.TryPickSource(new CellPos(0, 2), ResourceType.Wood, 1, out var pick);

            Assert.That(ok, Is.True);
            Assert.That(pick.Building.Value, Is.EqualTo(srcNear.Value));
        }

        [Test]
        public void TryPickDest_SkipsUnreachableIslandCandidate_WhenPathfinderExists()
        {
            var world = new WorldState();
            var data = new TestDataRegistry();
            var index = new WorldIndexService(world, data);
            var storage = new StorageService(world, data, null);
            var grid = new GridMap(8, 5);
            var terrain = new TerrainMap(8, 5);

            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 8; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var start = new CellPos(0, 2);
            var unreachable = world.Buildings.Create(MakeConstructedStorage(default, new CellPos(2, 2), "bld_warehouse_t1"));
            var reachable = world.Buildings.Create(MakeConstructedStorage(default, new CellPos(6, 2), "bld_warehouse_t1"));
            index.RebuildAll();

            terrain.Set(new CellPos(1, 1), TerrainType.Sea);
            terrain.Set(new CellPos(1, 2), TerrainType.Sea);
            terrain.Set(new CellPos(1, 3), TerrainType.Sea);
            terrain.Set(new CellPos(2, 1), TerrainType.Sea);
            terrain.Set(new CellPos(2, 3), TerrainType.Sea);
            terrain.Set(new CellPos(3, 1), TerrainType.Sea);
            terrain.Set(new CellPos(3, 2), TerrainType.Sea);
            terrain.Set(new CellPos(3, 3), TerrainType.Sea);

            var pathfinder = new NpcPathfinder(grid, terrain);
            var sut = new ResourceFlowService(world, index, storage, pathfinder);

            bool ok = sut.TryPickDest(start, ResourceType.Wood, 1, out var pick);

            Assert.That(ok, Is.True);
            Assert.That(pick.Building.Value, Is.EqualTo(reachable.Value));
            Assert.That(pick.Building.Value, Is.Not.EqualTo(unreachable.Value));
        }

        [Test]
        public void TryPickSource_ReturnsFalse_WhenAllCandidatesAreSeaIsolated()
        {
            var world = new WorldState();
            var data = new TestDataRegistry();
            var index = new WorldIndexService(world, data);
            var storage = new StorageService(world, data, null);
            var grid = new GridMap(6, 5);
            var terrain = new TerrainMap(6, 5);

            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 6; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var isolated = MakeConstructedStorage(default, new CellPos(4, 2), "bld_warehouse_t1");
            isolated.Wood = 20;
            world.Buildings.Create(isolated);
            index.RebuildAll();

            terrain.Set(new CellPos(3, 1), TerrainType.Sea);
            terrain.Set(new CellPos(3, 2), TerrainType.Sea);
            terrain.Set(new CellPos(3, 3), TerrainType.Sea);
            terrain.Set(new CellPos(4, 1), TerrainType.Sea);
            terrain.Set(new CellPos(4, 3), TerrainType.Sea);
            terrain.Set(new CellPos(5, 1), TerrainType.Sea);
            terrain.Set(new CellPos(5, 2), TerrainType.Sea);
            terrain.Set(new CellPos(5, 3), TerrainType.Sea);

            var pathfinder = new NpcPathfinder(grid, terrain);
            var sut = new ResourceFlowService(world, index, storage, pathfinder);

            bool ok = sut.TryPickSource(new CellPos(0, 2), ResourceType.Wood, 1, out var pick);

            Assert.That(ok, Is.False);
            Assert.That(pick.Building.Value, Is.EqualTo(0));
        }
    }
}
