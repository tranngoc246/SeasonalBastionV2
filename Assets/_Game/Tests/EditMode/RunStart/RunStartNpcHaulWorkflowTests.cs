using System.Linq;
using NUnit.Framework;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class RunStartNpcHaulWorkflowTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public BuildingDef GetBuilding(string id)
            {
                return id switch
                {
                    "bld_hq_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 5, SizeY = 5, IsHQ = true, IsWarehouse = true, WorkRoles = WorkRoleFlags.Build | WorkRoleFlags.HaulBasic, CapWood = new StorageCapsByLevel { L1 = 120 }, CapFood = new StorageCapsByLevel { L1 = 120 }, CapStone = new StorageCapsByLevel { L1 = 120 }, CapIron = new StorageCapsByLevel { L1 = 120 }, MaxHp = 100 },
                    "bld_house_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsHouse = true, MaxHp = 60 },
                    "bld_farmhouse_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapFood = new StorageCapsByLevel { L1 = 30 }, MaxHp = 80 },
                    "bld_lumbercamp_t1" => new BuildingDef { DefId = id, BaseLevel = 1, SizeX = 3, SizeY = 3, IsProducer = true, WorkRoles = WorkRoleFlags.Harvest, CapWood = new StorageCapsByLevel { L1 = 40 }, MaxHp = 80 },
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
        public void RunStart_HqNpc_HaulWorkflow_ExposesSpawnAssignAndExecuteStages()
        {
            var services = CreateServices();
            AssertRunStartApplied(services);

            var hq = FindBuilding(services, "bld_hq_t1");
            var farm = FindBuilding(services, "bld_farmhouse_t1");
            var hqNpcId = FindNpcAssignedTo(services, hq);

            Assert.That(hq.Value, Is.Not.EqualTo(0));
            Assert.That(farm.Value, Is.Not.EqualTo(0));
            Assert.That(hqNpcId.Value, Is.Not.EqualTo(0));

            for (int i = 0; i < 60; i++)
                services.JobScheduler.Tick(0.1f);

            int farmFoodAfterHarvestWindow = services.StorageService.GetAmount(farm, ResourceType.Food);
            var jobsAfterHarvestWindow = services.JobBoard.EnumerateAllJobs().ToList();
            var foodHaulJob = jobsAfterHarvestWindow.FirstOrDefault(j => j.Workplace.Value == hq.Value && j.Archetype == JobArchetype.HaulBasic && j.ResourceType == ResourceType.Food);
            var hqNpcAfterAssign = services.WorldState.Npcs.Get(hqNpcId);

            Assert.That(farmFoodAfterHarvestWindow, Is.GreaterThan(0), "Stage 1 failed: farm never accumulated local food, so haul cannot begin.");
            Assert.That(foodHaulJob.Id.Value, Is.Not.EqualTo(0), "Stage 2 failed: HQ never enqueued a HaulBasic(Food) job.");
            Assert.That(hqNpcAfterAssign.CurrentJob.Value, Is.EqualTo(foodHaulJob.Id.Value), "Stage 3 failed: HQ NPC never claimed the queued food haul job.");

            int initialHqFood = services.StorageService.GetAmount(hq, ResourceType.Food);
            for (int i = 0; i < 540; i++)
                services.JobScheduler.Tick(0.1f);

            int hqFood = services.StorageService.GetAmount(hq, ResourceType.Food);
            Assert.That(hqFood, Is.GreaterThan(initialHqFood), "Stage 4 failed: HQ NPC claimed haul job but never delivered food back to HQ.");
        }

        private static GameServices CreateServices()
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
                WorldIndex = new WorldIndexService(world, data),
                StorageService = new StorageService(world, data, bus)
            };

            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++)
                    services.TerrainMap.Set(new CellPos(x, y), TerrainType.Land);

            services.Pathfinder = new NpcPathfinder(services.GridMap, services.TerrainMap);
            services.AgentMover = new GridAgentMoverLite(services.GridMap, services.DataRegistry, null, services.TerrainMap);
            services.ResourceFlowService = new ResourceFlowService(world, services.WorldIndex, services.StorageService, services.Pathfinder);
            services.ClaimService = new ClaimService();
            services.JobBoard = new JobBoard();
            services.JobWorkplacePolicy = new JobWorkplacePolicy(data);
            services.JobScheduler = new JobScheduler(services, world, services.JobBoard, services.ClaimService, new JobExecutorRegistry(services), bus, data, services.NotificationService, services.JobWorkplacePolicy);
            return services;
        }

        private static void AssertRunStartApplied(GameServices services)
        {
            string configPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets", "_Game", "Resources", "RunStart", "StartMapConfig_Island_96x96_v1.json");
            string json = System.IO.File.ReadAllText(configPath);
            var ok = RunStartFacade.TryApply(services, json, out var error);
            Assert.That(ok, Is.True, error);
        }

        private static BuildingId FindBuilding(GameServices services, string defId)
        {
            foreach (var bid in services.WorldState.Buildings.Ids)
            {
                var st = services.WorldState.Buildings.Get(bid);
                if (st.DefId == defId)
                    return bid;
            }

            return default;
        }

        private static NpcId FindNpcAssignedTo(GameServices services, BuildingId workplace)
        {
            foreach (var nid in services.WorldState.Npcs.Ids)
            {
                var st = services.WorldState.Npcs.Get(nid);
                if (st.Workplace.Value == workplace.Value)
                    return nid;
            }

            return default;
        }
    }
}
