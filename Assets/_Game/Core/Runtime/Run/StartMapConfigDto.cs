using System;
using UnityEngine;

[Serializable]
public class StartMapConfigRootDto
{
    public int schemaVersion;
    public CoordSystemDto coordSystem;
    public MapRootDto map;

    public RoadCellDto[] roads;
    public SpawnGateDto[] spawnGates;
    public ZoneDto[] zones;

    public InitialBuildingDto[] initialBuildings;
    public InitialNpcDto[] initialNpcs;

    public StartHintDto[] startHints;
    public string[] lockedInvariants;
}

[Serializable] public class CoordSystemDto { public string origin; public string indexing; public string notes; }

[Serializable]
public class MapRootDto
{
    public int width;
    public int height;
    public RectMinMaxDto buildableRect;
}

[Serializable] public class RectMinMaxDto { public int xMin; public int yMin; public int xMax; public int yMax; }

[Serializable] public class CellDto { public int x; public int y; }

[Serializable] public class RoadCellDto { public int x; public int y; }

[Serializable]
public class SpawnGateDto
{
    public int lane;
    public CellDto cell;
    public string dirToHQ; // "S","W","E"
}

[Serializable]
public class ZoneDto
{
    public string zoneId;
    public string type;
    public string ownerBuildingHint;
    public RectMinMaxDto cellsRect;
    public int cellCount;
}

[Serializable]
public class InitialBuildingDto
{
    public string defId;
    public CellDto anchor;
    public string rotation; // "N"
    public InitialBuildingOverridesDto initialStateOverrides;
    public string notes;
}

[Serializable]
public class InitialBuildingOverridesDto
{
    public string ammo;       // "FULL"
    public float ammoPercent; // 1.0
}

[Serializable]
public class InitialNpcDto
{
    public string npcDefId;
    public CellDto spawnCell;
    public string assignedWorkplaceDefId;
    public string jobProfile;
    public string notes;
}

[Serializable]
public class StartHintDto
{
    public string hintId;
    public string trigger;
    public string title;
    public string body;
    public string notificationKey;
}
