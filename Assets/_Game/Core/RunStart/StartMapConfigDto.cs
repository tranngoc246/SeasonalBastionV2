using System;

namespace SeasonalBastion.RunStart
{
    [Serializable]
    internal sealed class StartMapConfigDto
    {
        public int schemaVersion;
        public CoordSystemDto coordSystem;
        public MapDto map;

        public TerrainRectDto[] terrainRects;

        public RoadCellDto[] roads;
        public SpawnGateDto[] spawnGates;
        public LandingGateDto[] landingGates;
        public ZoneDto[] zones;
        public ResourceGenerationDto resourceGeneration;

        public InitialBuildingDto[] initialBuildings;
        public InitialNpcDto[] initialNpcs;

        public StartHintDto[] startHints;
        public string[] lockedInvariants;
    }

    [Serializable]
    internal sealed class CoordSystemDto
    {
        public string origin;
        public string indexing;
        public string notes;
    }

    [Serializable]
    internal sealed class MapDto
    {
        public int width;
        public int height;
        public RectMinMaxDto buildableRect;
    }

    [Serializable]
    internal sealed class RectMinMaxDto
    {
        public int xMin;
        public int yMin;
        public int xMax;
        public int yMax;
    }

    [Serializable]
    internal sealed class CellDto
    {
        public int x;
        public int y;
    }

    [Serializable]
    internal sealed class TerrainRectDto
    {
        public string terrain;
        public RectMinMaxDto rect;
        public string notes;
    }

    [Serializable]
    internal sealed class RoadCellDto
    {
        public int x;
        public int y;
    }

    [Serializable]
    internal sealed class SpawnGateDto
    {
        public int lane;
        public CellDto cell;
        public string dirToHQ;
    }

    [Serializable]
    internal sealed class LandingGateDto
    {
        public int lane;
        public CellDto cell;
        public string dirToHQ;
        public string notes;
    }

    [Serializable]
    internal sealed class ZoneDto
    {
        public string zoneId;
        public string type;
        public string ownerBuildingHint;
        public RectMinMaxDto cellsRect;
        public int cellCount;
    }

    [Serializable]
    internal sealed class ResourceGenerationDto
    {
        public string mode;
        public int seedOffset;
        public ResourceSpawnRuleDto[] starterRules;
        public ResourceSpawnRuleDto[] bonusRules;
    }

    [Serializable]
    internal sealed class ResourceSpawnRuleDto
    {
        public string resourceType;

        public int countMin;
        public int countMax;

        public int minDistanceFromHQ;
        public int maxDistanceFromHQ;

        public int rectWidthMin;
        public int rectWidthMax;
        public int rectHeightMin;
        public int rectHeightMax;

        public string notes;
    }

    [Serializable]
    internal sealed class InitialBuildingDto
    {
        public string defId;
        public CellDto anchor;
        public string rotation;
        public InitialBuildingOverridesDto initialStateOverrides;
        public string notes;
    }

    [Serializable]
    internal sealed class InitialBuildingOverridesDto
    {
        public string ammo;
        public float ammoPercent;
    }

    [Serializable]
    internal sealed class InitialNpcDto
    {
        public string npcDefId;
        public CellDto spawnCell;
        public string assignedWorkplaceDefId;
        public string jobProfile;
        public string notes;
    }

    [Serializable]
    internal sealed class StartHintDto
    {
        public string hintId;
        public string trigger;
        public string title;
        public string body;
        public string notificationKey;
    }
}
