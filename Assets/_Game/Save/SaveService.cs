// _Game/Save/SaveService.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveService : ISaveService
    {
        private readonly SaveMigrator _migrator;
        private readonly IDataRegistry _data;
        private readonly IGridMap _grid;

        public int CurrentSchemaVersion => _migrator.CurrentSchemaVersion;

        private string RunPath => Path.Combine(Application.persistentDataPath, "run_save.json");
        private string MetaPath => Path.Combine(Application.persistentDataPath, "meta_save.json");

        public SaveService(SaveMigrator migrator, IDataRegistry data, IGridMap grid)
        {
            _migrator = migrator;
            _data = data;
            _grid = grid;
        }

        public bool HasRunSave() => File.Exists(RunPath);

        public void DeleteRunSave()
        {
            if (File.Exists(RunPath)) File.Delete(RunPath);
        }

        public SaveResult SaveRun(IWorldState world, IRunClock clock)
        {
            try
            {
                if (world == null || clock == null)
                    return new SaveResult(SaveResultCode.Failed, "world/clock null");

                var rc = clock as RunClockService;
                var file = new RunSaveFile
                {
                    schemaVersion = CurrentSchemaVersion,
                    seed = 0,
                    season = clock.CurrentSeason.ToString(),
                    dayIndex = clock.DayIndex,
                    timeScale = clock.TimeScale,
                    yearIndex = rc != null ? rc.YearIndex : 1,
                    dayTimer = rc != null ? rc.DayTimerSeconds : 0f,
                    world = new WorldFile(),
                    build = new BuildFile(),
                };

                file.combat = new CombatFile
                {
                    currentWaveIndex = 0, // Reset-wave option: always restart from begin
                    isDefendActive = (clock.CurrentPhase == Phase.Defend)
                };

                // Buildings
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

                // Sites
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
                        remaining = new List<SaveCost>()
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

                // NPCs
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

                // Towers (TowerState không có DefId/HP, dùng Hp/HpMax)
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

                // Enemies (Day33: persist enemies so load reset-wave can "carry" leftovers)
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

                // Roads
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

                Debug.Log($"[SaveService] Save roads = {file.roads.Count}");

                var json = JsonUtility.ToJson(file, true);
                File.WriteAllText(RunPath, json);

                return new SaveResult(SaveResultCode.Ok, "Saved run");
            }
            catch (Exception e)
            {
                return new SaveResult(SaveResultCode.Failed, e.Message);
            }
        }

        public SaveResult LoadRun(out RunSaveDTO dto)
        {
            dto = null;
            try
            {
                if (!File.Exists(RunPath))
                    return new SaveResult(SaveResultCode.NotFound, "No run save");

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
                };

                // Day33: combat snapshot minimal (reset-wave option)
                // If file has combat, use it; else derive from season/phase.
                dto.combat.CurrentWaveIndex = file.combat != null ? file.combat.currentWaveIndex : 0;

                bool derivedDefend =
                    dto.season == Season.Autumn.ToString() ||
                    dto.season == Season.Winter.ToString();

                dto.combat.IsDefendActive = file.combat != null ? file.combat.isDefendActive : derivedDefend;

                // Roads
                if (file.roads != null)
                {
                    for (int i = 0; i < file.roads.Count; i++)
                    {
                        var c = file.roads[i];
                        dto.world.Roads.Add(new CellPosI32(c.x, c.y));
                    }
                }

                Debug.Log($"[SaveService] Load roads = {dto.world.Roads.Count}");

                // Buildings
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

                // Sites
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
                            RemainingCosts = new List<CostDef>()
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

                // NPCs
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

                // Towers
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

                // Enemies
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
                return new SaveResult(SaveResultCode.Failed, e.Message);
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
