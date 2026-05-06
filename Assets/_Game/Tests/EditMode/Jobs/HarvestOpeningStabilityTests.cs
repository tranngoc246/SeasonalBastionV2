using NUnit.Framework;
using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode.Jobs
{
    public sealed class HarvestOpeningStabilityTests
    {
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
    }
}
