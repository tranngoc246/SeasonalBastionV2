using NUnit.Framework;
using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    public sealed class Regression_SaveMigrationTests
    {
        [Test]
        public void SaveMigrator_RunSave_V1_UpgradesToCurrentSchema_AndBackfillsNewDtoBranches()
        {
            var migrator = new SeasonalBastion.SaveMigrator();
            var legacy = new RunSaveDTO
            {
                schemaVersion = 1,
                season = "NotASeason",
                dayIndex = 0,
                timeScale = 0f,
                yearIndex = 0,
                dayTimer = -3f,
                world = new WorldDTO
                {
                    Buildings = new List<BuildingState>
                    {
                        new BuildingState
                        {
                            Id = new BuildingId(1),
                            DefId = "bld_hq_t1",
                            Anchor = new CellPos(4, 4),
                            Rotation = Dir4.N,
                            Level = 0,
                            IsConstructed = true,
                            HP = -50,
                            MaxHP = 0,
                        }
                    },
                    Towers = new List<TowerState>
                    {
                        new TowerState { Id = new TowerId(3), Cell = new CellPos(2, 2), Hp = -8, HpMax = 0 }
                    },
                    Enemies = new List<EnemyState>
                    {
                        new EnemyState { Id = new EnemyId(4), DefId = "enemy_test", Cell = new CellPos(1, 1), Hp = -7 }
                    },
                    Roads = null,
                    Npcs = null,
                },
                build = new BuildDTO
                {
                    Sites = new List<BuildSiteState>
                    {
                        new BuildSiteState
                        {
                            Id = new SiteId(2),
                            BuildingDefId = "bld_house_t1",
                            TargetLevel = 0,
                            Anchor = new CellPos(6, 6),
                            Rotation = Dir4.N,
                            IsActive = true,
                            WorkSecondsDone = 12f,
                            WorkSecondsTotal = 5f,
                            DeliveredSoFar = null,
                            RemainingCosts = null,
                        }
                    }
                },
                combat = null,
                rewards = null,
                population = null,
            };

            bool ok = migrator.TryMigrate(legacy, out var migrated);

            Assert.That(ok, Is.True);
            Assert.That(migrated, Is.SameAs(legacy), "Migration currently mutates the incoming DTO in-place.");
            Assert.That(migrated.schemaVersion, Is.EqualTo(migrator.CurrentSchemaVersion));
            Assert.That(migrated.schemaVersion, Is.EqualTo(SeasonalBastion.SaveMigrator.CurrentSaveSchemaVersion));
            Assert.That(migrated.season, Is.EqualTo(Season.Spring.ToString()));
            Assert.That(migrated.dayIndex, Is.EqualTo(1));
            Assert.That(migrated.timeScale, Is.EqualTo(1f));
            Assert.That(migrated.yearIndex, Is.EqualTo(1));
            Assert.That(migrated.dayTimer, Is.EqualTo(0f));
            Assert.That(migrated.combat, Is.Not.Null);
            Assert.That(migrated.rewards, Is.Not.Null);
            Assert.That(migrated.population, Is.Not.Null);
            Assert.That(migrated.world, Is.Not.Null);
            Assert.That(migrated.world.Roads, Is.Not.Null);
            Assert.That(migrated.world.Npcs, Is.Not.Null);
            Assert.That(migrated.world.Towers, Is.Not.Null);
            Assert.That(migrated.world.Enemies, Is.Not.Null);
            Assert.That(migrated.build, Is.Not.Null);
            Assert.That(migrated.build.Sites, Is.Not.Null);
            Assert.That(migrated.build.Sites[0].DeliveredSoFar, Is.Not.Null);
            Assert.That(migrated.build.Sites[0].RemainingCosts, Is.Not.Null);
            Assert.That(migrated.build.Sites[0].TargetLevel, Is.EqualTo(1));
            Assert.That(migrated.build.Sites[0].WorkSecondsDone, Is.EqualTo(5f));
            Assert.That(migrated.build.Sites[0].WorkSecondsTotal, Is.EqualTo(5f));
            Assert.That(migrated.world.Buildings[0].Level, Is.EqualTo(1));
            Assert.That(migrated.world.Buildings[0].HP, Is.EqualTo(0));
            Assert.That(migrated.world.Buildings[0].MaxHP, Is.EqualTo(0));
            Assert.That(migrated.world.Towers[0].Hp, Is.EqualTo(0));
            Assert.That(migrated.world.Towers[0].HpMax, Is.EqualTo(0));
            Assert.That(migrated.world.Enemies[0].Hp, Is.EqualTo(0));
        }

        [Test]
        public void SaveMigrator_MetaSave_V1_UpgradesToCurrentSchema_AndBackfillsCollections()
        {
            var migrator = new SeasonalBastion.SaveMigrator();
            var legacy = new MetaSaveDTO
            {
                schemaVersion = 1,
                currency = -5,
                unlockIds = null,
                perkLevels = null,
            };

            bool ok = migrator.TryMigrate(legacy, out var migrated);

            Assert.That(ok, Is.True);
            Assert.That(migrated.schemaVersion, Is.EqualTo(migrator.CurrentSchemaVersion));
            Assert.That(migrated.currency, Is.EqualTo(0));
            Assert.That(migrated.unlockIds, Is.Not.Null);
            Assert.That(migrated.perkLevels, Is.Not.Null);
        }

        [Test]
        public void SaveMigrator_RunSave_LowercaseSeason_IsAccepted_AndCanonicalized()
        {
            var migrator = new SeasonalBastion.SaveMigrator();
            var legacy = new RunSaveDTO
            {
                schemaVersion = 1,
                season = "winter",
                dayIndex = 1,
                timeScale = 1f,
                yearIndex = 1,
                dayTimer = 0f,
                world = new WorldDTO(),
                build = new BuildDTO(),
                combat = new CombatDTO(),
                rewards = new RewardsDTO(),
                population = new PopulationDTO(),
            };

            bool ok = migrator.TryMigrate(legacy, out var migrated);

            Assert.That(ok, Is.True);
            Assert.That(migrated.season, Is.EqualTo(Season.Winter.ToString()));
        }

        [Test]
        public void SaveMigrator_RunSave_NegativeSchemaVersion_Fails()
        {
            var migrator = new SeasonalBastion.SaveMigrator();
            var legacy = new RunSaveDTO
            {
                schemaVersion = -1
            };

            bool ok = migrator.TryMigrate(legacy, out var migrated);

            Assert.That(ok, Is.False);
            Assert.That(migrated, Is.SameAs(legacy));
        }
    }
}
