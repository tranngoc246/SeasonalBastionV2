using System.Collections.Generic;
using NUnit.Framework;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class NpcPathfinderTests
    {
        [Test]
        public void TryFindPath_PrefersRoadCorridor_WhenGroundAlternativeExists()
        {
            var grid = new GridMap(7, 5);

            // Road corridor: (0,2) -> (6,2)
            for (int x = 0; x < 7; x++)
                grid.SetRoad(new CellPos(x, 2), true);

            var sut = new NpcPathfinder(grid);
            bool ok = sut.TryFindPath(new CellPos(0, 1), new CellPos(6, 1), out var path);

            Assert.That(ok, Is.True);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Count, Is.GreaterThan(0));

            bool touchesRoad = false;
            for (int i = 0; i < path.Count; i++)
            {
                if (grid.IsRoad(path[i]))
                {
                    touchesRoad = true;
                    break;
                }
            }

            Assert.That(touchesRoad, Is.True, "Expected weighted path to use road corridor.");
            Assert.That(path[path.Count - 1], Is.EqualTo(new CellPos(6, 1)));
        }

        [Test]
        public void TryFindPath_AvoidsBlockedBuildingCells()
        {
            var grid = new GridMap(5, 5);
            grid.SetBuilding(new CellPos(2, 1), new BuildingId(1));
            grid.SetBuilding(new CellPos(2, 2), new BuildingId(1));
            grid.SetBuilding(new CellPos(2, 3), new BuildingId(1));

            var sut = new NpcPathfinder(grid);
            bool ok = sut.TryFindPath(new CellPos(0, 2), new CellPos(4, 2), out var path);

            Assert.That(ok, Is.True);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Count, Is.GreaterThan(0));

            for (int i = 0; i < path.Count; i++)
                Assert.That(grid.IsBlocked(path[i]), Is.False, $"Path must not enter blocked cell {path[i]}.");

            Assert.That(path[path.Count - 1], Is.EqualTo(new CellPos(4, 2)));
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenTargetUnreachable()
        {
            var grid = new GridMap(5, 5);
            var target = new CellPos(2, 2);

            grid.SetBuilding(new CellPos(2, 1), new BuildingId(1));
            grid.SetBuilding(new CellPos(3, 2), new BuildingId(2));
            grid.SetBuilding(new CellPos(2, 3), new BuildingId(3));
            grid.SetBuilding(new CellPos(1, 2), new BuildingId(4));

            var sut = new NpcPathfinder(grid);
            bool ok = sut.TryFindPath(new CellPos(0, 0), target, out var path);

            Assert.That(ok, Is.False);
            Assert.That(path, Is.Null);
        }

        [Test]
        public void TryFindPath_ReturnsEmptyPath_WhenAlreadyAtTarget()
        {
            var grid = new GridMap(4, 4);
            var sut = new NpcPathfinder(grid);

            bool ok = sut.TryFindPath(new CellPos(1, 1), new CellPos(1, 1), out var path);

            Assert.That(ok, Is.True);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryEstimateCost_ReturnsWeightedPathCost()
        {
            var grid = new GridMap(4, 3);
            grid.SetRoad(new CellPos(1, 1), true);
            grid.SetRoad(new CellPos(2, 1), true);

            var sut = new NpcPathfinder(grid);
            bool ok = sut.TryEstimateCost(new CellPos(0, 1), new CellPos(3, 1), out int cost);

            Assert.That(ok, Is.True);
            Assert.That(cost, Is.EqualTo(NpcPathfinder.GroundCost + NpcPathfinder.RoadCost + NpcPathfinder.RoadCost));
        }
    }
}
