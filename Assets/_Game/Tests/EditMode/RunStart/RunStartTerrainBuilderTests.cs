using NUnit.Framework;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class RunStartTerrainBuilderTests
    {
        private static GameServices MakeServices(int width = 96, int height = 96)
        {
            return new GameServices
            {
                TerrainMap = new TerrainMap(width, height),
                GridMap = new GridMap(width, height)
            };
        }

        [Test]
        public void ApplyTerrain_AppliesRectsInOrder_LaterRectsOverrideEarlier()
        {
            var services = MakeServices();
            var cfg = new StartMapConfigDto
            {
                map = new MapDto { width = 96, height = 96 },
                terrainRects = new[]
                {
                    new TerrainRectDto { terrain = "Sea", rect = new RectMinMaxDto { xMin = 0, yMin = 0, xMax = 95, yMax = 95 } },
                    new TerrainRectDto { terrain = "Land", rect = new RectMinMaxDto { xMin = 20, yMin = 20, xMax = 70, yMax = 70 } },
                    new TerrainRectDto { terrain = "Shore", rect = new RectMinMaxDto { xMin = 20, yMin = 20, xMax = 24, yMax = 24 } }
                }
            };

            var ok = RunStartTerrainBuilder.ApplyTerrain(services, cfg, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(services.TerrainMap.Get(new CellPos(10, 10)), Is.EqualTo(TerrainType.Sea));
            Assert.That(services.TerrainMap.Get(new CellPos(30, 30)), Is.EqualTo(TerrainType.Land));
            Assert.That(services.TerrainMap.Get(new CellPos(22, 22)), Is.EqualTo(TerrainType.Shore));
        }

        [Test]
        public void ApplyTerrain_FailsWhenRectIsOutOfBounds()
        {
            var services = MakeServices();
            var cfg = new StartMapConfigDto
            {
                map = new MapDto { width = 96, height = 96 },
                terrainRects = new[]
                {
                    new TerrainRectDto { terrain = "Land", rect = new RectMinMaxDto { xMin = 0, yMin = 0, xMax = 96, yMax = 95 } }
                }
            };

            var ok = RunStartTerrainBuilder.ApplyTerrain(services, cfg, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("out of bounds"));
        }
    }
}
