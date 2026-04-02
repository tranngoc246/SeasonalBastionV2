using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveMigrator
    {
        public const int LegacySchemaVersion = 1;
        public const int CurrentSaveSchemaVersion = 2;

        public int CurrentSchemaVersion => CurrentSaveSchemaVersion;

        public bool TryMigrate(RunSaveDTO dto, out RunSaveDTO migrated)
        {
            migrated = dto;
            if (dto == null)
                return false;

            int version = NormalizeVersion(dto.schemaVersion);
            if (version > CurrentSaveSchemaVersion)
                return false;

            EnsureRunDefaults(dto, version);

            while (version < CurrentSaveSchemaVersion)
            {
                switch (version)
                {
                    case LegacySchemaVersion:
                        if (!MigrateRunV1ToV2(dto))
                            return false;
                        version = 2;
                        break;

                    default:
                        return false;
                }
            }

            dto.schemaVersion = CurrentSaveSchemaVersion;
            migrated = dto;
            return true;
        }

        public bool TryMigrate(MetaSaveDTO dto, out MetaSaveDTO migrated)
        {
            migrated = dto;
            if (dto == null)
                return false;

            int version = NormalizeVersion(dto.schemaVersion);
            if (version > CurrentSaveSchemaVersion)
                return false;

            EnsureMetaDefaults(dto);

            while (version < CurrentSaveSchemaVersion)
            {
                switch (version)
                {
                    case LegacySchemaVersion:
                        if (!MigrateMetaV1ToV2(dto))
                            return false;
                        version = 2;
                        break;

                    default:
                        return false;
                }
            }

            dto.schemaVersion = CurrentSaveSchemaVersion;
            migrated = dto;
            return true;
        }

        private static int NormalizeVersion(int schemaVersion)
        {
            return schemaVersion <= 0 ? LegacySchemaVersion : schemaVersion;
        }

        private static void EnsureRunDefaults(RunSaveDTO dto, int version)
        {
            // Migration currently mutates the provided DTO in-place.
            // Keep this explicit so callers/tests do not assume copy-on-write semantics.
            dto.season = NormalizeSeason(dto.season);
            dto.dayIndex = Math.Max(1, dto.dayIndex);
            dto.timeScale = dto.timeScale <= 0f ? 1f : dto.timeScale;
            dto.yearIndex = Math.Max(1, dto.yearIndex);
            dto.dayTimer = Math.Max(0f, dto.dayTimer);
            dto.world ??= new WorldDTO();
            dto.build ??= new BuildDTO();
            dto.combat ??= new CombatDTO();
            dto.rewards ??= new RewardsDTO();
            dto.population ??= new PopulationDTO();

            dto.world.Buildings ??= new List<BuildingState>();
            dto.world.Npcs ??= new List<NpcState>();
            dto.world.Towers ??= new List<TowerState>();
            dto.world.Enemies ??= new List<EnemyState>();
            dto.world.Roads ??= new List<CellPosI32>();
            dto.build.Sites ??= new List<BuildSiteState>();
            dto.rewards.PickedRewardDefIds ??= new List<string>();

            if (version <= LegacySchemaVersion)
            {
                dto.combat.CurrentWaveIndex = Math.Max(0, dto.combat.CurrentWaveIndex);
            }
        }

        private static void EnsureMetaDefaults(MetaSaveDTO dto)
        {
            dto.unlockIds ??= new List<string>();
            dto.perkLevels ??= new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private static bool MigrateRunV1ToV2(RunSaveDTO dto)
        {
            EnsureRunDefaults(dto, LegacySchemaVersion);

            // v2 canonicalized null/legacy collections and normalized fields so older saves
            // remain loadable after DTO growth (rewards/population/combat additions).
            for (int i = 0; i < dto.world.Buildings.Count; i++)
            {
                var b = dto.world.Buildings[i];
                b.Level = Math.Max(1, b.Level);
                b.HP = Math.Max(0, b.HP);
                b.MaxHP = Math.Max(b.MaxHP, b.HP);
                dto.world.Buildings[i] = b;
            }

            for (int i = 0; i < dto.world.Towers.Count; i++)
            {
                var t = dto.world.Towers[i];
                t.Hp = Math.Max(0, t.Hp);
                t.HpMax = Math.Max(t.HpMax, t.Hp);
                dto.world.Towers[i] = t;
            }

            for (int i = 0; i < dto.world.Enemies.Count; i++)
            {
                var e = dto.world.Enemies[i];
                e.Hp = Math.Max(0, e.Hp);
                dto.world.Enemies[i] = e;
            }

            for (int i = 0; i < dto.build.Sites.Count; i++)
            {
                var s = dto.build.Sites[i];
                s.TargetLevel = Math.Max(1, s.TargetLevel);
                s.WorkSecondsDone = Math.Max(0f, s.WorkSecondsDone);
                s.WorkSecondsTotal = Math.Max(0f, s.WorkSecondsTotal);
                if (s.WorkSecondsDone > s.WorkSecondsTotal)
                    s.WorkSecondsDone = s.WorkSecondsTotal;
                s.DeliveredSoFar ??= new List<CostDef>();
                s.RemainingCosts ??= new List<CostDef>();
                dto.build.Sites[i] = s;
            }

            dto.combat.CurrentWaveIndex = Math.Max(0, dto.combat.CurrentWaveIndex);
            dto.population.GrowthProgressDays = Math.Max(0f, dto.population.GrowthProgressDays);
            dto.population.StarvationDays = Math.Max(0, dto.population.StarvationDays);
            return true;
        }

        private static string NormalizeSeason(string seasonText)
        {
            if (!string.IsNullOrWhiteSpace(seasonText) && Enum.TryParse<Season>(seasonText, out var parsed))
                return parsed.ToString();

            return Season.Spring.ToString();
        }

        private static bool MigrateMetaV1ToV2(MetaSaveDTO dto)
        {
            EnsureMetaDefaults(dto);
            dto.currency = Math.Max(0, dto.currency);
            return true;
        }
    }
}
