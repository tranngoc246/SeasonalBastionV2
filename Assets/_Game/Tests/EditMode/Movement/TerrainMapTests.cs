using NUnit.Framework;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class TerrainMapTests
    {
        [Test]
        public void Get_DefaultsToSea()
        {
            var map = new TerrainMap(4, 3);

            Assert.That(map.Get(new CellPos(0, 0)), Is.EqualTo(TerrainType.Sea));
            Assert.That(map.Get(new CellPos(3, 2)), Is.EqualTo(TerrainType.Sea));
        }

        [Test]
        public void Set_ThenGet_ReturnsStoredTerrain()
        {
            var map = new TerrainMap(4, 3);

            map.Set(new CellPos(2, 1), TerrainType.Land);
            map.Set(new CellPos(1, 2), TerrainType.Shore);

            Assert.That(map.Get(new CellPos(2, 1)), Is.EqualTo(TerrainType.Land));
            Assert.That(map.Get(new CellPos(1, 2)), Is.EqualTo(TerrainType.Shore));
        }

        [Test]
        public void ClearAll_ResetsCellsToSea()
        {
            var map = new TerrainMap(4, 3);
            map.Set(new CellPos(2, 1), TerrainType.Land);
            map.Set(new CellPos(1, 2), TerrainType.Shore);

            map.ClearAll();

            Assert.That(map.Get(new CellPos(2, 1)), Is.EqualTo(TerrainType.Sea));
            Assert.That(map.Get(new CellPos(1, 2)), Is.EqualTo(TerrainType.Sea));
        }

        [Test]
        public void OutOfBounds_GetReturnsSea_AndSetIsIgnored()
        {
            var map = new TerrainMap(4, 3);

            map.Set(new CellPos(-1, 0), TerrainType.Land);
            map.Set(new CellPos(4, 1), TerrainType.Land);

            Assert.That(map.Get(new CellPos(-1, 0)), Is.EqualTo(TerrainType.Sea));
            Assert.That(map.Get(new CellPos(4, 1)), Is.EqualTo(TerrainType.Sea));
            Assert.That(map.Get(new CellPos(0, 0)), Is.EqualTo(TerrainType.Sea));
        }
    }
}
