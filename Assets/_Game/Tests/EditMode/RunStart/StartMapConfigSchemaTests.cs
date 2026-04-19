using NUnit.Framework;
using SeasonalBastion.RunStart;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class StartMapConfigSchemaTests
    {
        [Test]
        public void InputParser_ParsesTerrainRects_AndLandingGates()
        {
            const string json = @"{
  ""schemaVersion"": 1,
  ""coordSystem"": { ""origin"": ""bottom-left"", ""indexing"": ""0-based"", ""notes"": ""test"" },
  ""map"": { ""width"": 96, ""height"": 96, ""buildableRect"": { ""xMin"": 10, ""yMin"": 10, ""xMax"": 80, ""yMax"": 80 } },
  ""terrainRects"": [
    { ""terrain"": ""Sea"", ""rect"": { ""xMin"": 0, ""yMin"": 0, ""xMax"": 95, ""yMax"": 95 }, ""notes"": ""fill"" },
    { ""terrain"": ""Land"", ""rect"": { ""xMin"": 20, ""yMin"": 20, ""xMax"": 70, ""yMax"": 70 }, ""notes"": ""core"" }
  ],
  ""landingGates"": [
    { ""lane"": 0, ""cell"": { ""x"": 48, ""y"": 77 }, ""dirToHQ"": ""S"", ""notes"": ""north"" }
  ],
  ""roads"": [],
  ""spawnGates"": [],
  ""zones"": [],
  ""resourceGeneration"": { ""mode"": ""AuthoredOnly"", ""seedOffset"": 0, ""starterRules"": [], ""bonusRules"": [] },
  ""initialBuildings"": [],
  ""initialNpcs"": [],
  ""startHints"": [],
  ""lockedInvariants"": [""test""]
}";

            var ok = RunStartInputParser.TryParseConfig(json, out var cfg, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(cfg, Is.Not.Null);
            Assert.That(cfg.terrainRects, Is.Not.Null);
            Assert.That(cfg.terrainRects.Length, Is.EqualTo(2));
            Assert.That(cfg.terrainRects[0].terrain, Is.EqualTo("Sea"));
            Assert.That(cfg.landingGates, Is.Not.Null);
            Assert.That(cfg.landingGates.Length, Is.EqualTo(1));
            Assert.That(cfg.landingGates[0].lane, Is.EqualTo(0));
            Assert.That(cfg.landingGates[0].cell.x, Is.EqualTo(48));
        }
    }
}
