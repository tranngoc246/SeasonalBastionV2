using NUnit.Framework;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class GameServicesFactoryTests
    {
        [Test]
        public void Create_UsesProvidedRuntimeMapSize_ForGridAndTerrain()
        {
            LogAssert.Expect(LogType.Error, "[DataRegistry] Load finished with 2 error(s). Use DebugHUDHub -> Validate Data to inspect.");
            var services = GameServicesFactory.Create(catalog: null, runtimeMapSize: new MapSize(96, 96));

            Assert.That(services.GridMap, Is.Not.Null);
            Assert.That(services.TerrainMap, Is.Not.Null);
            Assert.That(services.GridMap.Width, Is.EqualTo(96));
            Assert.That(services.GridMap.Height, Is.EqualTo(96));
            Assert.That(services.TerrainMap.Width, Is.EqualTo(96));
            Assert.That(services.TerrainMap.Height, Is.EqualTo(96));
            Assert.That(services.RuntimeMapSize.Width, Is.EqualTo(96));
            Assert.That(services.RuntimeMapSize.Height, Is.EqualTo(96));
        }

        [Test]
        public void Create_UsesDefaultRuntimeMapSize_WhenNotProvided()
        {
            LogAssert.Expect(LogType.Error, "[DataRegistry] Load finished with 2 error(s). Use DebugHUDHub -> Validate Data to inspect.");
            var services = GameServicesFactory.Create(catalog: null);

            Assert.That(services.GridMap.Width, Is.EqualTo(MapSize.Default.Width));
            Assert.That(services.GridMap.Height, Is.EqualTo(MapSize.Default.Height));
            Assert.That(services.TerrainMap.Width, Is.EqualTo(MapSize.Default.Width));
            Assert.That(services.TerrainMap.Height, Is.EqualTo(MapSize.Default.Height));
        }
    }
}
