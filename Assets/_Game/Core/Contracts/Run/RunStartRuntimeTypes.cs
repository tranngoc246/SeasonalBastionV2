using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// Runtime cache populated from StartMapConfig at StartNewRun().
    /// Kept in Contracts so Core/runtime modules can share the data shape
    /// without forcing a dependency on the RunStart implementation module.
    /// </summary>
    public sealed class RunStartRuntime
    {
        public int Seed;
        public int MapWidth;
        public int MapHeight;

        public string ResourceGenerationModeRequested;
        public string ResourceGenerationModeApplied;
        public string ResourceGenerationFailureReason;
        public string OpeningQualityBand;

        public IntRect BuildableRect;

        public readonly List<SpawnGate> SpawnGates = new(4);
        public readonly Dictionary<string, ZoneRect> Zones = new();
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
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
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
        public readonly string Origin;

        public ZoneRect(string zoneId, string type, string ownerBuildingHint, IntRect rect, int cellCount, string origin = null)
        {
            ZoneId = zoneId;
            Type = type;
            OwnerBuildingHint = ownerBuildingHint;
            Rect = rect;
            CellCount = cellCount;
            Origin = origin;
        }
    }
}
