// _Game/Save/SaveService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveService : ISaveService
    {
        private readonly SaveMigrator _migrator;
        private readonly IDataRegistry _data;
        private readonly IGridMap _grid;
        private readonly IPopulationService _population;

        public int CurrentSchemaVersion => _migrator.CurrentSchemaVersion;

        private string RunPath => Path.Combine(Application.persistentDataPath, "run_save.json");
        private string RunTempPath => Path.Combine(Application.persistentDataPath, "run_save.tmp");
        private string RunBackupPath => Path.Combine(Application.persistentDataPath, "run_save.bak");
        private string MetaPath => Path.Combine(Application.persistentDataPath, "meta_save.json");

        public SaveService(SaveMigrator migrator, IDataRegistry data, IGridMap grid, IPopulationService population = null)
        {
            _migrator = migrator;
            _data = data;
            _grid = grid;
            _population = population;
        }

        public bool HasRunSave() => File.Exists(RunPath);

        public void DeleteRunSave()
        {
            if (File.Exists(RunPath)) File.Delete(RunPath);
            if (File.Exists(RunTempPath)) File.Delete(RunTempPath);
            if (File.Exists(RunBackupPath)) File.Delete(RunBackupPath);
        }

        public SaveResult SaveRun(IWorldState world, IRunClock clock)
        {
            try
            {
                if (world == null || clock == null)
                    return new SaveResult(SaveResultCode.Failed, "world/clock null");

                var file = CreateImmutableRunSnapshot(world, clock);
                var json = JsonUtility.ToJson(file, true);
                AtomicWriteRunSave(json);

                return new SaveResult(SaveResultCode.Ok, "Saved run");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] SaveRun failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult LoadRun(out RunSaveDTO dto)
        {
            dto = null;
            try
            {
                if (!File.Exists(RunPath))
                {
                    if (File.Exists(RunBackupPath))
                    {
                        Debug.LogWarning("[SaveLoad] Primary run save missing, attempting backup restore from run_save.bak.");
                        File.Copy(RunBackupPath, RunPath, overwrite: true);
                    }
                    else
                    {
                        return new SaveResult(SaveResultCode.NotFound, "No run save");
                    }
                }

                var json = File.ReadAllText(RunPath);
                var file = JsonUtility.FromJson<RunSaveFile>(json);
                if (file == null)
                    return new SaveResult(SaveResultCode.Failed, "Invalid json");

                dto = new RunSaveDTO
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

                if (!_migrator.TryMigrate(dto, out var migrated))
                    return new SaveResult(SaveResultCode.IncompatibleSchema, "Migrate failed");

                dto = migrated;
                return new SaveResult(SaveResultCode.Ok, "Loaded run");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] LoadRun failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult SaveMeta(MetaSaveDTO dto)
        {
            try
            {
                if (dto == null) return new SaveResult(SaveResultCode.Failed, "meta null");

                var file = new MetaSaveFile
                {
                    schemaVersion = CurrentSchemaVersion,
                    currency = dto.currency,
                    unlockIds = dto.unlockIds ?? new List<string>(),
                    perkLevels = new List<PerkKV>()
                };

                if (dto.perkLevels != null)
                {
                    foreach (var kv in dto.perkLevels)
                        file.perkLevels.Add(new PerkKV { key = kv.Key, value = kv.Value });
                }

                File.WriteAllText(MetaPath, JsonUtility.ToJson(file, true));
                return new SaveResult(SaveResultCode.Ok, "Saved meta");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] SaveMeta failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult LoadMeta(out MetaSaveDTO dto)
        {
            dto = null;
            try
            {
                if (!File.Exists(MetaPath))
                    return new SaveResult(SaveResultCode.NotFound, "No meta save");

                var json = File.ReadAllText(MetaPath);
                var file = JsonUtility.FromJson<MetaSaveFile>(json);
                if (file == null) return new SaveResult(SaveResultCode.Failed, "Invalid meta json");

                dto = new MetaSaveDTO
                {
                    schemaVersion = file.schemaVersion,
                    currency = file.currency,
                    unlockIds = file.unlockIds ?? new List<string>(),
                    perkLevels = new Dictionary<string, int>()
                };

                if (file.perkLevels != null)
                {
                    for (int i = 0; i < file.perkLevels.Count; i++)
                    {
                        var p = file.perkLevels[i];
                        if (!string.IsNullOrEmpty(p.key))
                            dto.perkLevels[p.key] = p.value;
                    }
                }

                if (!_migrator.TryMigrate(dto, out var migrated))
                    return new SaveResult(SaveResultCode.IncompatibleSchema, "Meta migrate failed");

                dto = migrated;
                return new SaveResult(SaveResultCode.Ok, "Loaded meta");
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoad] LoadMeta failed: " + e);
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        private RunSaveFile CreateImmutableRunSnapshot(IWorldState world, IRunClock clock)
        {
            var rc = clock as RunClockService;
            var file = new RunSaveFile
            {
                schemaVersion = CurrentSchemaVersion,
                seed = ResolveRunSeed(clock),
                season = clock.CurrentSeason.ToString(),
                dayIndex = clock.DayIndex,
                timeScale = clock.TimeScale,
                yearIndex = rc != null ? rc.YearIndex : 1,
                dayTimer = rc != null ? rc.DayTimerSeconds : 0f,
                world = new WorldFile(),
                build = new BuildFile(),
                combat = new CombatFile
                {
                    currentWaveIndex = 0,
                    isDefendActive = (clock.CurrentPhase == Phase.Defend)
                },
                population = new PopulationFile(),
                roads = new List<CellPosI32>()
            };

            if (_population != null)
            {
                var pop = _population.State;
                file.population.growthProgressDays = pop.GrowthProgressDays;
                file.population.starvationDays = pop.StarvationDays;
                file.population.starvedToday = pop.StarvedToday;
            }

            foreach (var id in world.Buildings.Ids)
            {
                var b = world.Buildings.Get(id);
                file.world.buildings.Add(new SaveBuilding
                {
                    id = b.Id.Value,
                    defId = b.DefId,
                    ax = b.Anchor.X,
                    ay = b.Anchor.Y,
                    rot = (int)b.Rotation,
                    level = b.Level,
                    isConstructed = b.IsConstructed,
                    hp = b.HP,
                    maxHp = b.MaxHP,
                    wood = b.Wood,
                    food = b.Food,
                    stone = b.Stone,
                    iron = b.Iron,
                    ammo = b.Ammo
                });
            }

            foreach (var id in world.Sites.Ids)
            {
                var s = world.Sites.Get(id);
                var sf = new SaveSite
                {
                    id = s.Id.Value,
                    buildingDefId = s.BuildingDefId,
                    targetLevel = s.TargetLevel,
                    ax = s.Anchor.X,
                    ay = s.Anchor.Y,
                    rot = (int)s.Rotation,
                    isActive = s.IsActive,
                    workDone = s.WorkSecondsDone,
                    workTotal = s.WorkSecondsTotal,
                    delivered = new List<SaveCost>(),
                    remaining = new List<SaveCost>(),
                    kind = s.Kind,
                    targetBuildingId = s.TargetBuilding.Value,
                    fromDefId = s.FromDefId,
                    edgeId = s.EdgeId
                };

                if (s.DeliveredSoFar != null)
                {
                    for (int i = 0; i < s.DeliveredSoFar.Count; i++)
                    {
                        var c = s.DeliveredSoFar[i];
                        sf.delivered.Add(new SaveCost { res = (int)c.Resource, amt = c.Amount });
                    }
                }

                if (s.RemainingCosts != null)
                {
                    for (int i = 0; i < s.RemainingCosts.Count; i++)
                    {
                        var c = s.RemainingCosts[i];
                        sf.remaining.Add(new SaveCost { res = (int)c.Resource, amt = c.Amount });
                    }
                }

                file.build.sites.Add(sf);
            }

            foreach (var id in world.Npcs.Ids)
            {
                var n = world.Npcs.Get(id);
                file.world.npcs.Add(new SaveNpc
                {
                    id = n.Id.Value,
                    defId = n.DefId,
                    cellX = n.Cell.X,
                    cellY = n.Cell.Y,
                    workplaceBuildingId = n.Workplace.Value,
                    currentJobId = n.CurrentJob.Value,
                    isIdle = n.IsIdle
                });
            }

            foreach (var id in world.Towers.Ids)
            {
                var t = world.Towers.Get(id);
                file.world.towers.Add(new SaveTower
                {
                    id = t.Id.Value,
                    cellX = t.Cell.X,
                    cellY = t.Cell.Y,
                    ammo = t.Ammo,
                    ammoCap = t.AmmoCap,
                    hp = t.Hp,
                    hpMax = t.HpMax
                });
            }

            foreach (var id in world.Enemies.Ids)
            {
                var e = world.Enemies.Get(id);
                file.world.enemies.Add(new SaveEnemy
                {
                    id = e.Id.Value,
                    defId = e.DefId,
                    cellX = e.Cell.X,
                    cellY = e.Cell.Y,
                    hp = e.Hp,
                    lane = e.Lane,
                    move01 = e.MoveProgress01
                });
            }

            if (_grid != null)
            {
                for (int y = 0; y < _grid.Height; y++)
                {
                    for (int x = 0; x < _grid.Width; x++)
                    {
                        var c = new CellPos(x, y);
                        if (_grid.IsRoad(c))
                            file.roads.Add(new CellPosI32(x, y));
                    }
                }
            }

            return file;
        }

        private int ResolveRunSeed(IRunClock clock)
        {
            // Current save contract has no reliable scene-agnostic seed source.
            // Keep this deterministic and compile-safe until seed is exposed by runtime contracts.
            return 0;
        }

        private void AtomicWriteRunSave(string json)
        {
            var dir = Path.GetDirectoryName(RunPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(RunTempPath))
                File.Delete(RunTempPath);

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var fs = new FileStream(RunTempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, utf8NoBom))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(flushToDisk: true);
            }

            if (File.Exists(RunPath))
            {
                File.Replace(RunTempPath, RunPath, RunBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(RunTempPath, RunPath);
                File.Copy(RunPath, RunBackupPath, overwrite: true);
            }
        }

        // ---------- Disk file types (JsonUtility-friendly) ----------

        [Serializable]
        private sealed class RunSaveFile
        {
            public int schemaVersion;
            public int seed;
            public string season;
            public int dayIndex;
            public float timeScale;
            public int yearIndex;
            public float dayTimer;
            public WorldFile world;
            public BuildFile build;
            public CombatFile combat;
            public PopulationFile population;
            public List<CellPosI32> roads = new();
        }

        [Serializable]
        private sealed class WorldFile
        {
            public List<SaveBuilding> buildings = new();
            public List<SaveNpc> npcs = new();
            public List<SaveTower> towers = new();
            public List<SaveEnemy> enemies = new();
        }

        [Serializable]
        private sealed class BuildFile
        {
            public List<SaveSite> sites = new();
        }

        [Serializable]
        private struct SaveBuilding
        {
            public int id;
            public string defId;
            public int ax, ay;
            public int rot;
            public int level;
            public bool isConstructed;
            public int hp, maxHp;
            public int wood, food, stone, iron, ammo;
        }

        [Serializable]
        private struct SaveNpc
        {
            public int id;
            public string defId;
            public int cellX, cellY;
            public int workplaceBuildingId;
            public int currentJobId;
            public bool isIdle;
        }

        [Serializable]
        private struct SaveTower
        {
            public int id;
            public int cellX, cellY;
            public int ammo, ammoCap;
            public int hp, hpMax;
        }

        [Serializable]
        private struct SaveSite
        {
            public int id;
            public string buildingDefId;
            public int targetLevel;
            public int ax, ay;
            public int rot;
            public bool isActive;
            public float workDone, workTotal;
            public int kind;
            public int targetBuildingId;
            public string fromDefId;
            public string edgeId;
            public List<SaveCost> delivered;
            public List<SaveCost> remaining;
        }

        [Serializable]
        private struct SaveCost
        {
            public int res;
            public int amt;
        }

        [Serializable]
        private sealed class MetaSaveFile
        {
            public int schemaVersion;
            public int currency;
            public List<string> unlockIds;
            public List<PerkKV> perkLevels;
        }

        [Serializable]
        private struct PerkKV
        {
            public string key;
            public int value;
        }

        [Serializable]
        private sealed class CombatFile
        {
            public int currentWaveIndex;
            public bool isDefendActive;
        }

        [Serializable]
        private sealed class PopulationFile
        {
            public float growthProgressDays;
            public int starvationDays;
            public bool starvedToday;
        }

        [Serializable]
        private struct SaveEnemy
        {
            public int id;
            public string defId;
            public int cellX, cellY;
            public int hp;
            public int lane;
            public float move01;
        }
    }
}
