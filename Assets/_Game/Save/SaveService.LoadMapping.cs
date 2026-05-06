using System;
using System.Collections.Generic;
using System.IO;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        public SaveResult LoadRun(out RunSaveDTO dto)
        {
            int latest = GetLatestValidSlot();
            if (latest != 0)
                return LoadRunFromSlot(latest, out dto, allowBackup: true);

            dto = null;
            try
            {
                var res = TryReadRunFile(RunPath, RunBackupPath, allowBackup: true, out var file, out var sourcePath);
                if (res.Code != SaveResultCode.Ok)
                    return res;
                if (file == null)
                    return new SaveResult(SaveResultCode.Failed, "Invalid json");

                dto = MapRunDto(file);

                if (!_migrator.TryMigrate(dto, out var migrated))
                    return new SaveResult(SaveResultCode.IncompatibleSchema, "Migrate failed");

                dto = migrated;
                return new SaveResult(SaveResultCode.Ok, $"Loaded run from {Path.GetFileName(sourcePath)}");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] LoadRun failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult LoadRunFromSlot(int slot, out RunSaveDTO dto, bool allowBackup = true)
        {
            dto = null;
            try
            {
                int safeSlot = Mathf.Max(1, slot);
                var res = TryReadRunFile(GetSlotPath(safeSlot), GetSlotBackupPath(safeSlot), allowBackup, out var file, out var sourcePath);
                if (res.Code != SaveResultCode.Ok)
                    return res;

                dto = MapRunDto(file);

                if (!_migrator.TryMigrate(dto, out var migrated))
                    return new SaveResult(SaveResultCode.IncompatibleSchema, "Migrate failed");

                dto = migrated;
                return new SaveResult(SaveResultCode.Ok, $"Loaded slot {safeSlot} from {Path.GetFileName(sourcePath)}");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] LoadRunFromSlot failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        private RunSaveDTO MapRunDto(RunSaveFile file)
        {
            var dto = new RunSaveDTO
            {
                schemaVersion = file.schemaVersion,
                seed = file.seed,
                season = file.season,
                dayIndex = file.dayIndex,
                timeScale = file.timeScale,
                yearIndex = file.yearIndex,
                dayTimer = file.dayTimer,
                world = new WorldDTO(),
                build = new BuildDTO(),
                combat = new CombatDTO(),
                rewards = new RewardsDTO(),
                population = new PopulationDTO(),
            };

            LastLoadedOrSavedSeed = file.seed;

            dto.combat.CurrentWaveIndex = file.combat != null ? file.combat.currentWaveIndex : 0;

            bool derivedDefend =
                dto.season == Season.Autumn.ToString() ||
                dto.season == Season.Winter.ToString();

            dto.combat.IsDefendActive = file.combat != null ? file.combat.isDefendActive : derivedDefend;

            if (file.population != null)
            {
                dto.population.GrowthProgressDays = file.population.growthProgressDays;
                dto.population.StarvationDays = file.population.starvationDays;
                dto.population.StarvedToday = file.population.starvedToday;
            }

            if (file.roads != null)
            {
                for (int i = 0; i < file.roads.Count; i++)
                {
                    var c = file.roads[i];
                    dto.world.Roads.Add(new CellPosI32(c.x, c.y));
                }
            }

            if (file.world?.buildings != null)
            {
                for (int i = 0; i < file.world.buildings.Count; i++)
                {
                    var b = file.world.buildings[i];
                    dto.world.Buildings.Add(new BuildingState
                    {
                        Id = new BuildingId(b.id),
                        DefId = b.defId,
                        Anchor = new CellPos(b.ax, b.ay),
                        Rotation = (Dir4)b.rot,
                        Level = b.level,
                        IsConstructed = b.isConstructed,
                        HP = b.hp,
                        MaxHP = b.maxHp,
                        Wood = b.wood,
                        Food = b.food,
                        Stone = b.stone,
                        Iron = b.iron,
                        Ammo = b.ammo
                    });
                }
            }

            if (file.build?.sites != null)
            {
                for (int i = 0; i < file.build.sites.Count; i++)
                {
                    var s = file.build.sites[i];
                    var st = new BuildSiteState
                    {
                        Id = new SiteId(s.id),
                        BuildingDefId = s.buildingDefId,
                        TargetLevel = s.targetLevel,
                        Anchor = new CellPos(s.ax, s.ay),
                        Rotation = (Dir4)s.rot,
                        IsActive = s.isActive,
                        WorkSecondsDone = s.workDone,
                        WorkSecondsTotal = s.workTotal,
                        DeliveredSoFar = new List<CostDef>(),
                        RemainingCosts = new List<CostDef>(),
                        Kind = (byte)s.kind,
                        TargetBuilding = new BuildingId(s.targetBuildingId),
                        FromDefId = s.fromDefId,
                        EdgeId = s.edgeId
                    };

                    if (s.delivered != null)
                    {
                        for (int k = 0; k < s.delivered.Count; k++)
                        {
                            var c = s.delivered[k];
                            st.DeliveredSoFar.Add(new CostDef { Resource = (ResourceType)c.res, Amount = c.amt });
                        }
                    }

                    if (s.remaining != null)
                    {
                        for (int k = 0; k < s.remaining.Count; k++)
                        {
                            var c = s.remaining[k];
                            st.RemainingCosts.Add(new CostDef { Resource = (ResourceType)c.res, Amount = c.amt });
                        }
                    }

                    dto.build.Sites.Add(st);
                }
            }

            if (file.world?.npcs != null)
            {
                for (int i = 0; i < file.world.npcs.Count; i++)
                {
                    var n = file.world.npcs[i];
                    dto.world.Npcs.Add(new NpcState
                    {
                        Id = new NpcId(n.id),
                        DefId = n.defId,
                        Cell = new CellPos(n.cellX, n.cellY),
                        Workplace = new BuildingId(n.workplaceBuildingId),
                        CurrentJob = new JobId(n.currentJobId),
                        IsIdle = n.isIdle
                    });
                }
            }

            if (file.world?.towers != null)
            {
                for (int i = 0; i < file.world.towers.Count; i++)
                {
                    var t = file.world.towers[i];
                    dto.world.Towers.Add(new TowerState
                    {
                        Id = new TowerId(t.id),
                        Cell = new CellPos(t.cellX, t.cellY),
                        Ammo = t.ammo,
                        AmmoCap = t.ammoCap,
                        Hp = t.hp,
                        HpMax = t.hpMax
                    });
                }
            }

            if (file.world?.enemies != null)
            {
                for (int i = 0; i < file.world.enemies.Count; i++)
                {
                    var e = file.world.enemies[i];
                    dto.world.Enemies.Add(new EnemyState
                    {
                        Id = new EnemyId(e.id),
                        DefId = e.defId,
                        Cell = new CellPos(e.cellX, e.cellY),
                        Hp = e.hp,
                        Lane = e.lane,
                        MoveProgress01 = e.move01
                    });
                }
            }

            if (file.rewards != null)
            {
                dto.rewards.PickedRewardDefIds = file.rewards.pickedRewardDefIds ?? new List<string>();
                dto.rewards.OfferedA = file.rewards.offeredA;
                dto.rewards.OfferedB = file.rewards.offeredB;
                dto.rewards.OfferedC = file.rewards.offeredC;
                dto.rewards.IsSelectionActive = file.rewards.isSelectionActive;
            }

            return dto;
        }
    }
}
