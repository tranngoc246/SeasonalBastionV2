using NUnit.Framework;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class GridAgentMoverLiteTests
    {
        private sealed class TestDataRegistry : IDataRegistry
        {
            public NpcDef GetNpc(string id)
            {
                return new NpcDef { DefId = id, BaseMoveSpeed = 1f, RoadSpeedMultiplier = 2f };
            }

            public bool TryGetNpc(string id, out NpcDef def)
            {
                def = new NpcDef { DefId = id, BaseMoveSpeed = 1f, RoadSpeedMultiplier = 2f };
                return true;
            }

            public BuildingDef GetBuilding(string id) => throw new System.NotSupportedException();
            public bool TryGetBuilding(string id, out BuildingDef def) { def = default; return false; }
            public EnemyDef GetEnemy(string id) => throw new System.NotSupportedException();
            public bool TryGetEnemy(string id, out EnemyDef def) { def = default; return false; }
            public WaveDef GetWave(string id) => throw new System.NotSupportedException();
            public bool TryGetWave(string id, out WaveDef def) { def = default; return false; }
            public RewardDef GetReward(string id) => throw new System.NotSupportedException();
            public bool TryGetReward(string id, out RewardDef def) { def = default; return false; }
            public RecipeDef GetRecipe(string id) => throw new System.NotSupportedException();
            public bool TryGetRecipe(string id, out RecipeDef def) { def = default; return false; }
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

        private static GridAgentMoverLite MakeMover(GridMap grid)
        {
            return new GridAgentMoverLite(grid, new TestDataRegistry(), null);
        }

        private static NpcState MakeNpc(int id, int x, int y)
        {
            return new NpcState
            {
                Id = new NpcId(id),
                DefId = "npc_test",
                Cell = new CellPos(x, y),
                Workplace = default,
                CurrentJob = default,
                IsIdle = false,
            };
        }

        [Test]
        public void StepToward_TargetChange_RepathsAndHeadsToNewTarget()
        {
            var grid = new GridMap(8, 5);
            for (int x = 0; x < 8; x++)
                grid.SetRoad(new CellPos(x, 2), true);

            var mover = MakeMover(grid);
            var npc = MakeNpc(1, 0, 1);

            Assert.That(mover.StepToward(ref npc, new CellPos(7, 1), 2f), Is.False);
            var afterFirstMove = npc.Cell;
            Assert.That(afterFirstMove.X, Is.GreaterThan(0));

            bool arrived = false;
            for (int i = 0; i < 6; i++)
            {
                arrived = mover.StepToward(ref npc, new CellPos(0, 1), 1f);
                if (arrived) break;
            }

            Assert.That(arrived, Is.True, "NPC should eventually arrive at the new target after repath.");
            Assert.That(npc.Cell, Is.EqualTo(new CellPos(0, 1)));
            Assert.That(npc.Cell.X, Is.LessThanOrEqualTo(afterFirstMove.X), "NPC should re-orient toward the new target after target change.");
        }

        [Test]
        public void StepToward_RoadsDirty_RepathsToUseNewRoad()
        {
            var grid = new GridMap(7, 5);
            var mover = MakeMover(grid);
            var npc = MakeNpc(2, 0, 1);

            Assert.That(mover.StepToward(ref npc, new CellPos(6, 1), 1f), Is.False);
            var beforeRoad = npc.Cell;

            for (int x = 0; x < 7; x++)
                grid.SetRoad(new CellPos(x, 2), true);
            mover.NotifyRoadsDirty();

            for (int i = 0; i < 8; i++)
                mover.StepToward(ref npc, new CellPos(6, 1), 1f);

            Assert.That(npc.Cell.Y, Is.EqualTo(1).Or.EqualTo(2), "NPC should repath after roads become dirty.");
            Assert.That(npc.Cell.X, Is.GreaterThan(beforeRoad.X));
        }

        [Test]
        public void StepToward_RoadRemovedMidRoute_RepathsAndStillArrives()
        {
            var grid = new GridMap(8, 3);
            for (int x = 0; x < 8; x++)
                grid.SetRoad(new CellPos(x, 1), true);

            var mover = MakeMover(grid);
            var npc = MakeNpc(22, 0, 0);
            var target = new CellPos(7, 0);

            bool touchedRoadBeforeRemoval = false;
            for (int i = 0; i < 4; i++)
            {
                mover.StepToward(ref npc, target, 1f);
                touchedRoadBeforeRemoval |= grid.IsRoad(npc.Cell);
            }

            Assert.That(touchedRoadBeforeRemoval, Is.True, "NPC should already be using road before road removal.");

            grid.SetRoad(new CellPos(4, 1), false);
            grid.SetRoad(new CellPos(5, 1), false);
            mover.NotifyRoadsDirty();

            bool steppedOffRoadAfterRemoval = false;
            bool arrived = false;
            for (int i = 0; i < 12; i++)
            {
                arrived = mover.StepToward(ref npc, target, 1f);
                if (!grid.IsRoad(npc.Cell) && (npc.Cell.X != target.X || npc.Cell.Y != target.Y))
                    steppedOffRoadAfterRemoval = true;
                if (arrived) break;
            }

            Assert.That(steppedOffRoadAfterRemoval, Is.True, "NPC should repath off the broken road backbone when it is removed mid-route.");
            Assert.That(arrived, Is.True);
            Assert.That(npc.Cell, Is.EqualTo(target));
        }

        [Test]
        public void StepToward_RoadAddedMidRoute_RepathsOntoNewRoadBackbone()
        {
            var grid = new GridMap(8, 3);
            var mover = MakeMover(grid);
            var npc = MakeNpc(23, 0, 0);
            var target = new CellPos(7, 0);

            Assert.That(mover.StepToward(ref npc, target, 2f), Is.False);
            Assert.That(grid.IsRoad(npc.Cell), Is.False, "NPC starts on pure ground before road is added.");

            for (int x = 0; x < 8; x++)
                grid.SetRoad(new CellPos(x, 1), true);
            mover.NotifyRoadsDirty();

            bool touchedNewRoad = false;
            bool arrived = false;
            for (int i = 0; i < 12; i++)
            {
                arrived = mover.StepToward(ref npc, target, 1f);
                touchedNewRoad |= grid.IsRoad(npc.Cell);
                if (arrived) break;
            }

            Assert.That(touchedNewRoad, Is.True, "NPC should repath onto newly added road backbone.");
            Assert.That(arrived, Is.True);
            Assert.That(npc.Cell, Is.EqualTo(target));
        }

        [Test]
        public void StepToward_UsesRoadBackboneInBothDirections_WhenRoadRouteExists()
        {
            var grid = new GridMap(9, 3);
            for (int x = 0; x < 9; x++)
                grid.SetRoad(new CellPos(x, 1), true);

            var mover = MakeMover(grid);

            var npcForward = MakeNpc(30, 0, 0);
            var npcReverse = MakeNpc(31, 8, 0);

            bool forwardTouchedRoad = false;
            bool reverseTouchedRoad = false;

            for (int i = 0; i < 12; i++)
            {
                mover.StepToward(ref npcForward, new CellPos(8, 0), 1f);
                mover.StepToward(ref npcReverse, new CellPos(0, 0), 1f);

                forwardTouchedRoad |= grid.IsRoad(npcForward.Cell);
                reverseTouchedRoad |= grid.IsRoad(npcReverse.Cell);
            }

            Assert.That(forwardTouchedRoad, Is.True, "Forward direction should use road backbone.");
            Assert.That(reverseTouchedRoad, Is.True, "Reverse direction should use road backbone too.");
            Assert.That(npcForward.Cell, Is.EqualTo(new CellPos(8, 0)));
            Assert.That(npcReverse.Cell, Is.EqualTo(new CellPos(0, 0)));
        }

        [Test]
        public void StepToward_WhenNextStepBecomesBlocked_RepathsAroundObstacle()
        {
            var grid = new GridMap(6, 5);
            var mover = MakeMover(grid);
            var npc = MakeNpc(3, 0, 2);
            var target = new CellPos(5, 2);

            Assert.That(mover.StepToward(ref npc, target, 1f), Is.False);
            Assert.That(npc.Cell, Is.EqualTo(new CellPos(1, 2)));

            grid.SetBuilding(new CellPos(2, 2), new BuildingId(10));

            bool arrived = false;
            for (int i = 0; i < 12; i++)
            {
                arrived = mover.StepToward(ref npc, target, 1f);
                if (arrived) break;
            }

            Assert.That(arrived, Is.True);
            Assert.That(npc.Cell, Is.EqualTo(target));
        }

        [Test]
        public void ClearAll_ResetsCachedRoutes_SoNpcCanRestartFromCurrentCell()
        {
            var grid = new GridMap(6, 3);
            for (int x = 0; x < 6; x++)
                grid.SetRoad(new CellPos(x, 1), true);

            var mover = MakeMover(grid);
            var npc = MakeNpc(4, 0, 0);
            var target = new CellPos(5, 0);

            Assert.That(mover.StepToward(ref npc, target, 3f), Is.False);
            var movedCell = npc.Cell;
            Assert.That(movedCell, Is.Not.EqualTo(new CellPos(0, 0)));

            mover.ClearAll();

            bool arrived = false;
            for (int i = 0; i < 10; i++)
            {
                arrived = mover.StepToward(ref npc, target, 1f);
                if (arrived) break;
            }

            Assert.That(arrived, Is.True);
            Assert.That(npc.Cell, Is.EqualTo(target));
        }

        [Test]
        public void StepToward_StopReservation_SecondNpcWaitsWhileFirstHoldsTarget()
        {
            var grid = new GridMap(5, 3);
            var mover = MakeMover(grid);
            var target = new CellPos(2, 1);

            var npcA = MakeNpc(10, 1, 1);
            var npcB = MakeNpc(11, 3, 1);

            bool aArrived = mover.StepToward(ref npcA, target, 1f);
            Assert.That(aArrived, Is.True);
            Assert.That(npcA.Cell, Is.EqualTo(target));

            bool bArrived = mover.StepToward(ref npcB, target, 1f);
            Assert.That(bArrived, Is.False, "Second NPC should wait while target stop cell is reserved by first NPC.");
            Assert.That(npcB.Cell, Is.EqualTo(new CellPos(3, 1)), "Second NPC should not step into reserved target cell.");
        }

        [Test]
        public void StepToward_StopReservation_ReleasesWhenFirstNpcMovesAway_SoSecondNpcCanArrive()
        {
            var grid = new GridMap(6, 3);
            var mover = MakeMover(grid);
            var sharedTarget = new CellPos(2, 1);

            var npcA = MakeNpc(20, 1, 1);
            var npcB = MakeNpc(21, 4, 1);

            bool aArrived = mover.StepToward(ref npcA, sharedTarget, 1f);
            Assert.That(aArrived, Is.True);
            Assert.That(npcA.Cell, Is.EqualTo(sharedTarget));

            bool bArrivedEarly = mover.StepToward(ref npcB, sharedTarget, 2f);
            Assert.That(bArrivedEarly, Is.False);
            Assert.That(npcB.Cell, Is.EqualTo(new CellPos(3, 1)), "Second NPC should stop before entering reserved target cell.");

            bool aMovedAway = mover.StepToward(ref npcA, new CellPos(0, 1), 2f);
            Assert.That(npcA.Cell, Is.EqualTo(new CellPos(0, 1)));
            Assert.That(aMovedAway, Is.True);

            bool bArrived = false;
            for (int i = 0; i < 4; i++)
            {
                bArrived = mover.StepToward(ref npcB, sharedTarget, 1f);
                if (bArrived) break;
            }

            Assert.That(bArrived, Is.True, "Second NPC should acquire released stop cell and arrive.");
            Assert.That(npcB.Cell, Is.EqualTo(sharedTarget));
        }
    }
}
