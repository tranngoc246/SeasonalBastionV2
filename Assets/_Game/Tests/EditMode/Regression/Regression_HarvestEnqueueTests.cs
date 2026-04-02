using NUnit.Framework;
using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_HarvestEnqueueTests
    {
        [Test]
        public void JobEnqueueService_Harvest_CreatesExactly2Jobs_WhenNpcCountIs3_AndSelectorReturnsValidCells()
        {
            var (enqueue, world, board, producerId) = CreateHarvestFixture(
                npcCount: 3,
                currentWood: 5,
                selector: new FakeHarvestTargetSelector(new CellPos(10, 9), new CellPos(11, 9)));

            var buildingIds = new List<BuildingId> { producerId };
            var workplacesWithNpc = new HashSet<int> { producerId.Value };
            var workplaceNpcCount = new Dictionary<int, int> { [producerId.Value] = 3 };
            var harvestMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);

            Assert.That(harvestMap.Count, Is.EqualTo(2));
            Assert.That(board.CountActiveJobs(JobArchetype.Harvest), Is.EqualTo(2));
        }

        [Test]
        public void JobEnqueueService_Harvest_Creates1Job_WhenNpcCountIs1()
        {
            var (enqueue, world, board, producerId) = CreateHarvestFixture(
                npcCount: 1,
                currentWood: 5,
                selector: new FakeHarvestTargetSelector(new CellPos(10, 9)));

            var buildingIds = new List<BuildingId> { producerId };
            var workplacesWithNpc = new HashSet<int> { producerId.Value };
            var workplaceNpcCount = new Dictionary<int, int> { [producerId.Value] = 1 };
            var harvestMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);

            Assert.That(harvestMap.Count, Is.EqualTo(1));
            Assert.That(board.CountActiveJobs(JobArchetype.Harvest), Is.EqualTo(1));
        }

        [Test]
        public void JobEnqueueService_Harvest_Creates0Jobs_WhenSelectorFails()
        {
            var selector = new FakeHarvestTargetSelector { AlwaysFail = true };
            var (enqueue, world, board, producerId) = CreateHarvestFixture(
                npcCount: 2,
                currentWood: 5,
                selector: selector);

            var buildingIds = new List<BuildingId> { producerId };
            var workplacesWithNpc = new HashSet<int> { producerId.Value };
            var workplaceNpcCount = new Dictionary<int, int> { [producerId.Value] = 2 };
            var harvestMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);

            Assert.That(harvestMap.Count, Is.EqualTo(0));
            Assert.That(board.CountActiveJobs(JobArchetype.Harvest), Is.EqualTo(0));
            Assert.That(selector.Calls, Is.EqualTo(2));
        }

        [Test]
        public void JobEnqueueService_Harvest_Creates0Jobs_WhenSelectorReturnsZeroZeroCell()
        {
            var selector = new FakeHarvestTargetSelector(new CellPos(0, 0));
            var (enqueue, world, board, producerId) = CreateHarvestFixture(
                npcCount: 2,
                currentWood: 5,
                selector: selector);

            var buildingIds = new List<BuildingId> { producerId };
            var workplacesWithNpc = new HashSet<int> { producerId.Value };
            var workplaceNpcCount = new Dictionary<int, int> { [producerId.Value] = 2 };
            var harvestMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);

            Assert.That(harvestMap.Count, Is.EqualTo(0));
            Assert.That(board.CountActiveJobs(JobArchetype.Harvest), Is.EqualTo(0));
            Assert.That(selector.Calls, Is.EqualTo(2));
        }

        [Test]
        public void JobEnqueueService_Harvest_Creates0Jobs_WhenLocalCapIsAlreadyFull()
        {
            var selector = new FakeHarvestTargetSelector(new CellPos(7, 6));
            var (enqueue, world, board, producerId) = CreateHarvestFixture(
                npcCount: 2,
                currentWood: 40,
                selector: selector);

            var buildingIds = new List<BuildingId> { producerId };
            var workplacesWithNpc = new HashSet<int> { producerId.Value };
            var workplaceNpcCount = new Dictionary<int, int> { [producerId.Value] = 2 };
            var harvestMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);

            Assert.That(harvestMap.Count, Is.EqualTo(0));
            Assert.That(board.CountActiveJobs(JobArchetype.Harvest), Is.EqualTo(0));
            Assert.That(selector.Calls, Is.EqualTo(0), "Selector should not be consulted when local harvest storage is already full.");
        }

        [Test]
        public void JobEnqueueService_Harvest_DoesNotDuplicateActiveNonTerminalJobs_ForSameWorkplaceSlot()
        {
            var selector = new FakeHarvestTargetSelector(new CellPos(10, 9), new CellPos(11, 9));
            var (enqueue, world, board, producerId) = CreateHarvestFixture(
                npcCount: 3,
                currentWood: 5,
                selector: selector);

            var buildingIds = new List<BuildingId> { producerId };
            var workplacesWithNpc = new HashSet<int> { producerId.Value };
            var workplaceNpcCount = new Dictionary<int, int> { [producerId.Value] = 3 };
            var harvestMap = new Dictionary<int, JobId>();

            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);
            enqueue.EnqueueHarvestJobsIfNeeded(buildingIds, workplacesWithNpc, workplaceNpcCount, harvestMap);

            Assert.That(harvestMap.Count, Is.EqualTo(2));
            Assert.That(board.CountActiveJobs(JobArchetype.Harvest), Is.EqualTo(2));
        }

        private static (JobEnqueueService enqueue, WorldState world, JobBoard board, BuildingId producerId) CreateHarvestFixture(
            int npcCount,
            int currentWood,
            IHarvestTargetSelector selector)
        {
            var data = new TestDataRegistry();
            data.Add(new BuildingDef
            {
                DefId = "bld_lumbercamp",
                IsProducer = true,
                WorkRoles = WorkRoleFlags.Harvest,
                CapWood = new StorageCapsByLevel { L1 = 20, L2 = 40, L3 = 60 },
                SizeX = 1,
                SizeY = 1,
                BaseLevel = 1,
                MaxHp = 20
            });

            var world = new WorldState();
            var board = new JobBoard();
            var cleanup = new JobStateCleanupService(new ClaimService());
            var workplacePolicy = new JobWorkplacePolicy(data);
            var resourcePolicy = new ResourceLogisticsPolicy();
            var services = new GameServices
            {
                EventBus = new TestEventBus(),
                DataRegistry = data,
                NotificationService = new NotificationService(new TestEventBus()),
                RunClock = new FakeRunClock(),
                RunOutcomeService = new FakeRunOutcomeService(),
                WorldState = world,
            };

            var enqueue = new JobEnqueueService(services, world, board, workplacePolicy, resourcePolicy, cleanup, selector);

            var producerId = world.Buildings.Create(new BuildingState
            {
                DefId = "bld_lumbercamp_t1",
                Anchor = new CellPos(10, 10),
                Rotation = Dir4.N,
                Level = 1,
                IsConstructed = true,
                Wood = currentWood,
                HP = 20,
                MaxHP = 20
            });

            var producer = world.Buildings.Get(producerId);
            producer.Id = producerId;
            world.Buildings.Set(producerId, producer);

            for (int i = 0; i < npcCount; i++)
            {
                var npcId = world.Npcs.Create(new NpcState
                {
                    DefId = $"npc_test_{i}",
                    Cell = new CellPos(10 + i, 9),
                    Workplace = producerId,
                    IsIdle = true
                });

                var npc = world.Npcs.Get(npcId);
                npc.Id = npcId;
                world.Npcs.Set(npcId, npc);
            }

            return (enqueue, world, board, producerId);
        }
    }
}
