using NUnit.Framework;
using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode.Jobs
{
    public sealed class HarvestOpeningStabilityTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public BuildingDef GetBuilding(string id)
            {
                return id switch
                {
                    "bld_lumbercamp_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 40 }, MaxHp = 80 },
                    _ => null
                };
            }

            public bool TryGetBuilding(string id, out BuildingDef def) { def = GetBuilding(id); return def != null; }
            public EnemyDef GetEnemy(string id) => throw new System.NotSupportedException();
            public bool TryGetEnemy(string id, out EnemyDef def) { def = default; return false; }
            public WaveDef GetWave(string id) => throw new System.NotSupportedException();
            public bool TryGetWave(string id, out WaveDef def) { def = default; return false; }
            public RewardDef GetReward(string id) => throw new System.NotSupportedException();
            public bool TryGetReward(string id, out RewardDef def) { def = default; return false; }
            public RecipeDef GetRecipe(string id) => throw new System.NotSupportedException();
            public bool TryGetRecipe(string id, out RecipeDef def) { def = default; return false; }
            public NpcDef GetNpc(string id) => new NpcDef { DefId = id, BaseMoveSpeed = 1f, RoadSpeedMultiplier = 1.2f };
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

        private static ZoneState MakeZone(int id, ResourceType rt, int xMin, int yMin, int xMax, int yMax, string origin, string bucket)
        {
            var zone = new ZoneState
            {
                Id = id,
                Resource = rt,
                Origin = origin,
                Bucket = bucket,
                Cells = new List<CellPos>()
            };

            for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                    zone.Cells.Add(new CellPos(x, y));

            return zone;
        }

        [Test]
        public void ResourcePatchService_RebuildFromZones_PreservesStarterMetadata()
        {
            var service = new ResourcePatchService();
            var zones = new List<ZoneState>
            {
                MakeZone(1, ResourceType.Wood, 10, 10, 12, 12, "Generated", "starter-generated"),
                MakeZone(2, ResourceType.Wood, 20, 20, 22, 22, "Generated", "bonus-generated")
            };

            service.RebuildFromZones(zones);

            Assert.That(service.Patches.Count, Is.EqualTo(2));
            Assert.That(service.Patches[0].GenerationBucket, Is.EqualTo("starter-generated"));
            Assert.That(service.Patches[0].IsStarterLike, Is.True);
            Assert.That(service.Patches[1].GenerationBucket, Is.EqualTo("bonus-generated"));
            Assert.That(service.Patches[1].IsStarterLike, Is.False);
            Assert.That(service.Patches[0].TotalAmount, Is.GreaterThan(service.Patches[1].TotalAmount));
        }

        [Test]
        public void ResourcePatchService_TryGetBestPatch_PrefersStarterPatchWhenDistanceClose()
        {
            var service = new ResourcePatchService();
            var zones = new List<ZoneState>
            {
                MakeZone(1, ResourceType.Wood, 10, 10, 12, 12, "Generated", "starter-generated"),
                MakeZone(2, ResourceType.Wood, 14, 10, 16, 12, "Generated", "bonus-generated")
            };

            service.RebuildFromZones(zones);

            bool ok = service.TryGetBestPatch(ResourceType.Wood, new CellPos(9, 9), out var patch);

            Assert.That(ok, Is.True);
            Assert.That(patch.GenerationBucket, Is.EqualTo("starter-generated"));
        }

        [Test]
        public void ResourcePatchService_GetRemainingPatchesByBucket_FiltersCorrectly()
        {
            var service = new ResourcePatchService();
            var zones = new List<ZoneState>
            {
                MakeZone(1, ResourceType.Wood, 10, 10, 12, 12, "Generated", "starter-generated"),
                MakeZone(2, ResourceType.Wood, 20, 20, 22, 22, "Generated", "bonus-generated")
            };

            service.RebuildFromZones(zones);
            var starter = service.GetRemainingPatchesByBucket("starter-generated");
            var bonus = service.GetRemainingPatchesByBucket("bonus-generated");

            Assert.That(starter.Count, Is.EqualTo(1));
            Assert.That(bonus.Count, Is.EqualTo(1));
        }

        [Test]
        public void HarvestTargetSelectionHelper_TryPickBestHarvestTarget_PrefersStarterPatch()
        {
            var services = CreateHarvestServices();
            services.ResourcePatchService.RebuildFromZones(new List<ZoneState>
            {
                MakeZone(1, ResourceType.Wood, 10, 10, 12, 12, "Generated", "starter-generated"),
                MakeZone(2, ResourceType.Wood, 13, 10, 15, 12, "Generated", "bonus-generated")
            });

            bool ok = HarvestTargetSelectionHelper.TryPickBestHarvestTarget(services.ResourcePatchService, services.Pathfinder, services.WorldState, ResourceType.Wood, new CellPos(9, 9), 1, 0, out var zoneCell);

            Assert.That(ok, Is.True);
            Assert.That(services.ResourcePatchService.TryGetPatchAtCell(zoneCell, out var chosenPatch), Is.True);
            Assert.That(chosenPatch.GenerationBucket, Is.EqualTo("starter-generated"));
        }

        [Test]
        public void HarvestExecutor_WhenStarterPatchDepletes_RetargetsToAnotherAvailablePatch()
        {
            var services = CreateHarvestServices();
            var workplaceId = CreateWorkplace((WorldState)services.WorldState);
            services.ResourcePatchService.RebuildFromZones(new List<ZoneState>
            {
                MakeZone(1, ResourceType.Wood, 10, 10, 10, 10, "Generated", "starter-generated"),
                MakeZone(2, ResourceType.Wood, 14, 10, 16, 12, "Generated", "bonus-generated")
            });

            var patchAtTarget = services.ResourcePatchService.Patches[0];
            services.ResourcePatchService.Consume(patchAtTarget.Id, patchAtTarget.TotalAmount);

            var executor = new HarvestExecutor(services);
            var npcId = services.WorldState.Npcs.Create(new NpcState
            {
                DefId = "npc_worker",
                Cell = new CellPos(10, 10),
                Workplace = workplaceId,
                IsIdle = false
            });
            var npc = services.WorldState.Npcs.Get(npcId);
            npc.Id = npcId;
            services.WorldState.Npcs.Set(npcId, npc);

            var job = new Job
            {
                Id = new JobId(101),
                Archetype = JobArchetype.Harvest,
                Status = JobStatus.InProgress,
                Workplace = workplaceId,
                TargetCell = new CellPos(10, 10),
                Amount = 0
            };

            bool done = executor.Tick(npcId, ref npc, ref job, 10f);

            Assert.That(done, Is.True);
            Assert.That(job.Status, Is.EqualTo(JobStatus.InProgress));
            Assert.That(job.TargetCell, Is.Not.EqualTo(new CellPos(10, 10)));
            Assert.That(services.ResourcePatchService.TryGetPatchAtCell(job.TargetCell, out var newPatch), Is.True);
            Assert.That(newPatch.GenerationBucket, Is.EqualTo("bonus-generated"));
        }

        private static GameServices CreateHarvestServices()
        {
            var bus = new EventBus();
            var data = new TestDataRegistry();
            var world = new WorldState();
            var grid = new GridMap(32, 32);
            var terrain = new TerrainMap(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    terrain.Set(new CellPos(x, y), TerrainType.Land);

            var services = new GameServices
            {
                EventBus = bus,
                DataRegistry = data,
                NotificationService = new NotificationService(bus),
                RunClock = new RunClockService(bus),
                RunOutcomeService = new RunOutcomeService(bus, world, data),
                WorldState = world,
                GridMap = grid,
                TerrainMap = terrain,
                RuntimeMapSize = new MapSize(32, 32),
                ResourcePatchService = new ResourcePatchService(),
                StorageService = new StorageService(world, data, bus),
                ClaimService = new ClaimService(),
                Pathfinder = new NpcPathfinder(grid, terrain),
                AgentMover = new GridAgentMoverLite(grid, data, null, terrain)
            };

            return services;
        }

        private static BuildingId CreateWorkplace(WorldState world)
        {
            var id = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_lumbercamp_t1",
                Anchor = new CellPos(8, 8),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                HP = 80,
                MaxHP = 80
            });
            var st = world.Buildings.Get(id);
            st.Id = id;
            world.Buildings.Set(id, st);
            return id;
        }
    }
}
