using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        private sealed class RunSaveFile
        {
            public int schemaVersion;
            public int seed;
            public string season;
            public int dayIndex;
            public float timeScale;
            public int yearIndex;
            public float dayTimer;
            public string timestampUtc;
            public WorldFile world;
            public BuildFile build;
            public CombatFile combat;
            public RewardsFile rewards;
            public PopulationFile population;
            public System.Collections.Generic.List<CellPosI32> roads = new();
        }

        private sealed class WorldFile
        {
            public System.Collections.Generic.List<SaveBuilding> buildings = new();
            public System.Collections.Generic.List<SaveNpc> npcs = new();
            public System.Collections.Generic.List<SaveTower> towers = new();
            public System.Collections.Generic.List<SaveEnemy> enemies = new();
        }

        private sealed class BuildFile
        {
            public System.Collections.Generic.List<SaveSite> sites = new();
        }

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

        private struct SaveNpc
        {
            public int id;
            public string defId;
            public int cellX, cellY;
            public int workplaceBuildingId;
            public int currentJobId;
            public bool isIdle;
        }

        private struct SaveTower
        {
            public int id;
            public int cellX, cellY;
            public int ammo, ammoCap;
            public int hp, hpMax;
        }

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
            public System.Collections.Generic.List<SaveCost> delivered;
            public System.Collections.Generic.List<SaveCost> remaining;
        }

        private struct SaveCost
        {
            public int res;
            public int amt;
        }

        private sealed class MetaSaveFile
        {
            public int schemaVersion;
            public int currency;
            public System.Collections.Generic.List<string> unlockIds;
            public System.Collections.Generic.List<PerkKV> perkLevels;
        }

        private struct PerkKV
        {
            public string key;
            public int value;
        }

        private sealed class CombatFile
        {
            public int currentWaveIndex;
            public bool isDefendActive;
        }

        private sealed class RewardsFile
        {
            public System.Collections.Generic.List<string> pickedRewardDefIds = new();
            public string offeredA;
            public string offeredB;
            public string offeredC;
            public bool isSelectionActive;
        }

        private sealed class PopulationFile
        {
            public float growthProgressDays;
            public int starvationDays;
            public bool starvedToday;
        }

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
