using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        private RunSaveFile CreateImmutableRunSnapshot(IWorldState world, IRunClock clock)
        {
            var rc = clock as RunClockService;
            int resolvedSeed = ResolveRunSeed(clock);
            LastLoadedOrSavedSeed = resolvedSeed;

            var file = new RunSaveFile
            {
                schemaVersion = CurrentSchemaVersion,
                seed = resolvedSeed,
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
                rewards = new RewardsFile(),
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

            if (_services?.RewardService != null)
            {
                file.rewards.pickedRewardDefIds = new List<string>(_services.RewardService.PickedRewardDefIds);
                file.rewards.offeredA = _services.RewardService.CurrentOffer.A;
                file.rewards.offeredB = _services.RewardService.CurrentOffer.B;
                file.rewards.offeredC = _services.RewardService.CurrentOffer.C;
                file.rewards.isSelectionActive = _services.RewardService.IsSelectionActive;
            }

            return file;
        }
    }
}
