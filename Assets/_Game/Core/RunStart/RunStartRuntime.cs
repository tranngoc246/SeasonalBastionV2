using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    /// <summary>
    /// Runtime cache populated from StartMapConfig at StartNewRun().
    /// VS2 uses this later (spawn gates, zones, buildable rect).
    /// Not part of Contracts.
    /// </summary>
    public sealed class RunStartRuntime
    {
        public int MapWidth;
        public int MapHeight;

        public IntRect BuildableRect;

        public readonly List<SpawnGate> SpawnGates = new(4);
        public readonly Dictionary<string, ZoneRect> Zones = new();

        // Day27: lanes/spawn gates runtime table
        // laneId -> lane runtime (start cell, dir to HQ, target cell)
        public readonly Dictionary<int, LaneRuntime> Lanes = new(8);

        public readonly List<string> LockedInvariants = new(16);
    }

    public readonly struct IntRect
    {
        public readonly int XMin;
        public readonly int YMin;
        public readonly int XMax;
        public readonly int YMax;

        public IntRect(int xMin, int yMin, int xMax, int yMax)
        {
            XMin = xMin; YMin = yMin; XMax = xMax; YMax = yMax;
        }

        public bool Contains(CellPos c)
        {
            return c.X >= XMin && c.Y >= YMin && c.X <= XMax && c.Y <= YMax;
        }
    }

    public readonly struct SpawnGate
    {
        public readonly int Lane;
        public readonly CellPos Cell;
        public readonly Dir4 DirToHQ;

        public SpawnGate(int lane, CellPos cell, Dir4 dirToHQ)
        {
            Lane = lane;
            Cell = cell;
            DirToHQ = dirToHQ;
        }
    }

    // Day27: resolved lane runtime row
    public readonly struct LaneRuntime
    {
        public readonly int LaneId;
        public readonly CellPos StartCell;
        public readonly Dir4 DirToHQ;
        public readonly CellPos TargetHQ;

        public LaneRuntime(int laneId, CellPos startCell, Dir4 dirToHQ, CellPos targetHQ)
        {
            LaneId = laneId;
            StartCell = startCell;
            DirToHQ = dirToHQ;
            TargetHQ = targetHQ;
        }
    }

    public readonly struct ZoneRect
    {
        public readonly string ZoneId;
        public readonly string Type;
        public readonly string OwnerBuildingHint;
        public readonly IntRect Rect;
        public readonly int CellCount;

        public ZoneRect(string zoneId, string type, string ownerBuildingHint, IntRect rect, int cellCount)
        {
            ZoneId = zoneId;
            Type = type;
            OwnerBuildingHint = ownerBuildingHint;
            Rect = rect;
            CellCount = cellCount;
        }
    }
}
