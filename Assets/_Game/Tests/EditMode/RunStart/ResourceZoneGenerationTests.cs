using NUnit.Framework;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class ResourceZoneGenerationTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public BuildingDef GetBuilding(string id)
            {
                return id switch
                {
                    "bld_hq_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 5, SizeY = 5, IsHQ = true, IsWarehouse = true },
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

        private static GameServices MakeServices(int width = 64, int height = 64)
        {
            var bus = new EventBus();
            var world = new WorldState();
            var grid = new GridMap(width, height);
            var services = new GameServices
            {
                EventBus = bus,
                DataRegistry = new TestDataRegistry(),
                NotificationService = new NotificationService(bus),
                RunClock = new RunClockService(bus),
                RunOutcomeService = new RunOutcomeService(bus, world, new TestDataRegistry()),
                WorldState = world,
                GridMap = grid,
                TerrainMap = new TerrainMap(width, height),
                RuntimeMapSize = new MapSize(width, height),
                RunStartRuntime = new RunStartRuntime()
            };
            return services;
        }

        private static BuildingId AddHq(GameServices s, int x, int y)
        {
            var id = s.WorldState.Buildings.Create(new BuildingState
            {
                DefId = "bld_hq_t1",
                Anchor = new CellPos(x, y),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 100,
                MaxHP = 100
            });

            var st = s.WorldState.Buildings.Get(id);
            st.Id = id;
            s.WorldState.Buildings.Set(id, st);
            return id;
        }

        private static StartMapConfigDto MakeGeneratedConfig()
        {
            return new StartMapConfigDto
            {
                roads = System.Array.Empty<RoadCellDto>(),
                spawnGates = new[] { new SpawnGateDto { lane = 0, cell = new CellDto { x = 30, y = 0 }, dirToHQ = "S" } },
                initialBuildings = new[] { new InitialBuildingDto { defId = "bld_hq_t1", anchor = new CellDto { x = 30, y = 30 }, rotation = "N" } },
                initialNpcs = System.Array.Empty<InitialNpcDto>(),
                startHints = new[] { new StartHintDto { hintId = "hq_hint", trigger = "run_start", title = "HQ", body = "HQ start" } },
                lockedInvariants = new[] { "hq-present" },
                resourceGeneration = new ResourceGenerationDto
                {
                    mode = "GeneratedOnly",
                    seedOffset = 0,
                    starterRules = new[]
                    {
                        new ResourceSpawnRuleDto { resourceType = "Wood", countMin = 1, countMax = 1, minDistanceFromHQ = 4, maxDistanceFromHQ = 10, rectWidthMin = 3, rectWidthMax = 4, rectHeightMin = 3, rectHeightMax = 4 },
                        new ResourceSpawnRuleDto { resourceType = "Food", countMin = 1, countMax = 1, minDistanceFromHQ = 4, maxDistanceFromHQ = 10, rectWidthMin = 3, rectWidthMax = 4, rectHeightMin = 3, rectHeightMax = 4 },
                        new ResourceSpawnRuleDto { resourceType = "Stone", countMin = 1, countMax = 1, minDistanceFromHQ = 8, maxDistanceFromHQ = 16, rectWidthMin = 2, rectWidthMax = 3, rectHeightMin = 2, rectHeightMax = 3 }
                    },
                    bonusRules = new[]
                    {
                        new ResourceSpawnRuleDto { resourceType = "Stone", countMin = 1, countMax = 2, minDistanceFromHQ = 10, maxDistanceFromHQ = 20, rectWidthMin = 2, rectWidthMax = 4, rectHeightMin = 2, rectHeightMax = 4 }
                    }
                }
            };
        }

        private static string Snapshot(List<ZoneState> zones)
        {
            var parts = new List<string>();
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
                for (int c = 0; c < z.Cells.Count; c++)
                {
                    var cell = z.Cells[c];
                    if (cell.X < xMin) xMin = cell.X;
                    if (cell.Y < yMin) yMin = cell.Y;
                    if (cell.X > xMax) xMax = cell.X;
                    if (cell.Y > yMax) yMax = cell.Y;
                }
                parts.Add($"{z.Resource}:{xMin},{yMin},{xMax},{yMax}:{z.Cells.Count}");
            }
            return string.Join("|", parts);
        }

        [Test]
        public void ResourceZoneGenerator_SameSeed_ProducesSameZones()
        {
            var services = MakeServices();
            AddHq(services, 30, 30);
            var cfg = MakeGeneratedConfig();

            Assert.That(RunStartResourceZoneGenerator.TryGenerateZones(services, cfg, 12345, out var a, out var errA), Is.True, errA);
            Assert.That(RunStartResourceZoneGenerator.TryGenerateZones(services, cfg, 12345, out var b, out var errB), Is.True, errB);

            Assert.That(Snapshot(a), Is.EqualTo(Snapshot(b)));
        }

        [Test]
        public void ResourceZoneGenerator_DifferentSeed_ProducesDifferentZones()
        {
            var services = MakeServices();
            AddHq(services, 30, 30);
            var cfg = MakeGeneratedConfig();

            Assert.That(RunStartResourceZoneGenerator.TryGenerateZones(services, cfg, 111, out var a, out var errA), Is.True, errA);
            Assert.That(RunStartResourceZoneGenerator.TryGenerateZones(services, cfg, 222, out var b, out var errB), Is.True, errB);

            Assert.That(Snapshot(a), Is.Not.EqualTo(Snapshot(b)));
        }

        [Test]
        public void RunStartZoneInitializer_GeneratedOnly_AppliesZonesAndUpdatesRuntimeCache()
        {
            var services = MakeServices();
            AddHq(services, 30, 30);
            services.RunStartRuntime.Seed = 777;
            var cfg = MakeGeneratedConfig();

            RunStartZoneInitializer.ApplyZones(services, cfg);
            RunStartRuntimeCacheBuilder.ApplyRuntimeZonesFromWorld(services);

            Assert.That(services.WorldState.Zones.Zones.Count, Is.GreaterThan(0));
            Assert.That(services.RunStartRuntime.Zones.Count, Is.EqualTo(services.WorldState.Zones.Zones.Count));
            Assert.That(services.RunStartRuntime.ResourceGenerationModeRequested, Is.EqualTo("GeneratedOnly"));
            Assert.That(services.RunStartRuntime.ResourceGenerationModeApplied, Is.EqualTo("Generated"));
            Assert.That(services.RunStartRuntime.OpeningQualityBand, Is.EqualTo("GeneratedUsable"));
            foreach (var pair in services.RunStartRuntime.Zones)
                Assert.That(pair.Value.Origin, Is.EqualTo("Generated"));
        }

        [Test]
        public void ResourceZoneGenerator_StarterGuarantee_ProducesWoodFoodAndStoneNearHq()
        {
            var services = MakeServices();
            AddHq(services, 30, 30);
            var cfg = MakeGeneratedConfig();

            Assert.That(RunStartResourceZoneGenerator.TryGenerateZones(services, cfg, 555, out var zones, out var err), Is.True, err);

            bool hasWood = false;
            bool hasFood = false;
            bool hasStone = false;
            var hq = new CellPos(30, 30);

            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                int minDist = MinDistanceToZone(hq, z);
                if (z.Resource == ResourceType.Wood && minDist <= 14) hasWood = true;
                if (z.Resource == ResourceType.Food && minDist <= 14) hasFood = true;
                if (z.Resource == ResourceType.Stone && minDist <= 16) hasStone = true;
            }

            Assert.That(hasWood, Is.True);
            Assert.That(hasFood, Is.True);
            Assert.That(hasStone, Is.True);
        }

        [Test]
        public void ResourceZoneGenerator_AllCellsStayWithinBounds()
        {
            var services = MakeServices();
            AddHq(services, 30, 30);
            var cfg = MakeGeneratedConfig();

            Assert.That(RunStartResourceZoneGenerator.TryGenerateZones(services, cfg, 999, out var zones, out var err), Is.True, err);

            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                for (int c = 0; c < z.Cells.Count; c++)
                {
                    var cell = z.Cells[c];
                    Assert.That(cell.X, Is.InRange(0, services.GridMap.Width - 1));
                    Assert.That(cell.Y, Is.InRange(0, services.GridMap.Height - 1));
                }
            }
        }

        [Test]
        public void RunStartZoneInitializer_AuthoredOnly_PreservesAuthoredZones()
        {
            var services = MakeServices();
            AddHq(services, 30, 30);
            var cfg = MakeGeneratedConfig();
            cfg.resourceGeneration.mode = "AuthoredOnly";
            cfg.zones = new[]
            {
                new ZoneDto
                {
                    zoneId = "zone_authored_wood",
                    type = "ForestTiles",
                    ownerBuildingHint = "bld_lumbercamp_t1",
                    cellsRect = new RectMinMaxDto { xMin = 10, yMin = 10, xMax = 11, yMax = 11 },
                    cellCount = 4
                }
            };

            RunStartZoneInitializer.ApplyZones(services, cfg);
            RunStartRuntimeCacheBuilder.ApplyRuntimeZonesFromWorld(services);

            Assert.That(services.WorldState.Zones.Zones.Count, Is.EqualTo(1));
            Assert.That(services.WorldState.Zones.Zones[0].Resource, Is.EqualTo(ResourceType.Wood));
            Assert.That(services.RunStartRuntime.Zones.Count, Is.EqualTo(1));
            Assert.That(services.RunStartRuntime.ResourceGenerationModeRequested, Is.EqualTo("AuthoredOnly"));
            Assert.That(services.RunStartRuntime.ResourceGenerationModeApplied, Is.EqualTo("AuthoredFallback"));
            Assert.That(services.RunStartRuntime.OpeningQualityBand, Is.EqualTo("AuthoredFallback"));
            foreach (var pair in services.RunStartRuntime.Zones)
                Assert.That(pair.Value.Origin, Is.EqualTo("AuthoredFallback"));
        }

        [Test]
        public void RunStartZoneInitializer_GeneratedOnly_FallsBackToAuthoredAndRecordsReason()
        {
            var services = MakeServices();
            var cfg = MakeGeneratedConfig();
            cfg.zones = new[]
            {
                new ZoneDto
                {
                    zoneId = "zone_authored_food",
                    type = "FarmPlots",
                    ownerBuildingHint = "bld_farmhouse_t1",
                    cellsRect = new RectMinMaxDto { xMin = 40, yMin = 40, xMax = 41, yMax = 41 },
                    cellCount = 4
                }
            };

            RunStartZoneInitializer.ApplyZones(services, cfg);
            RunStartRuntimeCacheBuilder.ApplyRuntimeZonesFromWorld(services);

            Assert.That(services.WorldState.Zones.Zones.Count, Is.EqualTo(1));
            Assert.That(services.RunStartRuntime.ResourceGenerationModeApplied, Is.EqualTo("AuthoredFallback"));
            Assert.That(services.RunStartRuntime.ResourceGenerationFailureReason, Is.Not.Null);
            Assert.That(services.RunStartRuntime.OpeningQualityBand, Is.EqualTo("AuthoredFallback"));
        }

        [Test]
        public void RunStartZoneInitializer_HybridWithoutGeneratedOrAuthored_UsesLegacyFallback()
        {
            var services = MakeServices();
            var cfg = MakeGeneratedConfig();
            cfg.resourceGeneration.mode = "Hybrid";
            cfg.zones = null;

            RunStartZoneInitializer.ApplyZones(services, cfg);
            RunStartRuntimeCacheBuilder.ApplyRuntimeZonesFromWorld(services);

            Assert.That(services.WorldState.Zones.Zones.Count, Is.EqualTo(4));
            Assert.That(services.RunStartRuntime.ResourceGenerationModeApplied, Is.EqualTo("LegacyFallback"));
            Assert.That(services.RunStartRuntime.OpeningQualityBand, Is.EqualTo("LegacyFallback"));
            foreach (var pair in services.RunStartRuntime.Zones)
                Assert.That(pair.Value.Origin, Is.EqualTo("LegacyFallback"));
        }

        private static int MinDistanceToZone(CellPos from, ZoneState z)
        {
            int best = int.MaxValue;
            for (int i = 0; i < z.Cells.Count; i++)
            {
                var c = z.Cells[i];
                int d = System.Math.Abs(from.X - c.X) + System.Math.Abs(from.Y - c.Y);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
