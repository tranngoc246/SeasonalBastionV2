using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ResourcePatchService
    {
        private readonly Dictionary<int, ResourcePatchState> _byId = new();
        private readonly Dictionary<int, int> _cellToPatchId = new();
        private readonly List<ResourcePatchState> _ordered = new();

        public IReadOnlyList<ResourcePatchState> Patches => _ordered;

        public void RebuildFromZones(IReadOnlyList<ZoneState> zones)
        {
            Clear();
            if (zones == null)
                return;

            int nextId = 1;
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null || zone.Cells == null || zone.Cells.Count == 0)
                    continue;

                int total = ComputeInitialAmount(zone.Resource, zone.Cells.Count);
                var patch = new ResourcePatchState
                {
                    Id = nextId++,
                    Resource = zone.Resource,
                    Cells = new List<CellPos>(zone.Cells),
                    Anchor = ComputeCenter(zone.Cells),
                    TotalAmount = total,
                    RemainingAmount = total
                };

                _byId[patch.Id] = patch;
                _ordered.Add(patch);

                for (int c = 0; c < patch.Cells.Count; c++)
                    _cellToPatchId[PackCell(patch.Cells[c])] = patch.Id;
            }
        }

        public void Clear()
        {
            _byId.Clear();
            _cellToPatchId.Clear();
            _ordered.Clear();
        }

        public bool TryGetPatch(int patchId, out ResourcePatchState patch)
        {
            return _byId.TryGetValue(patchId, out patch);
        }

        public bool TryGetPatchAtCell(CellPos cell, out ResourcePatchState patch)
        {
            patch = default;
            if (!_cellToPatchId.TryGetValue(PackCell(cell), out var id))
                return false;

            return _byId.TryGetValue(id, out patch);
        }

        public int GetTotalAmount(int patchId)
        {
            return _byId.TryGetValue(patchId, out var patch) ? patch.TotalAmount : 0;
        }

        public int GetRemainingAmount(int patchId)
        {
            return _byId.TryGetValue(patchId, out var patch) ? patch.RemainingAmount : 0;
        }

        public bool TryGetNearestPatch(ResourceType rt, CellPos nearCell, out ResourcePatchState patch)
        {
            patch = default;
            bool found = false;
            int best = int.MaxValue;

            for (int i = 0; i < _ordered.Count; i++)
            {
                var p = _ordered[i];
                if (p.Resource != rt || p.RemainingAmount <= 0)
                    continue;

                int d = System.Math.Abs(p.Anchor.X - nearCell.X) + System.Math.Abs(p.Anchor.Y - nearCell.Y);
                if (!found || d < best)
                {
                    found = true;
                    best = d;
                    patch = p;
                }
            }

            return found;
        }

        public int Consume(int patchId, int amount)
        {
            if (amount <= 0)
                return 0;

            if (!_byId.TryGetValue(patchId, out var patch))
                return 0;

            int taken = amount > patch.RemainingAmount ? patch.RemainingAmount : amount;
            patch.RemainingAmount -= taken;
            _byId[patchId] = patch;

            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i].Id != patchId)
                    continue;

                _ordered[i] = patch;
                break;
            }

            return taken;
        }

        private static CellPos ComputeCenter(List<CellPos> cells)
        {
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (c.X < xMin) xMin = c.X;
                if (c.Y < yMin) yMin = c.Y;
                if (c.X > xMax) xMax = c.X;
                if (c.Y > yMax) yMax = c.Y;
            }
            return new CellPos(xMin + (xMax - xMin) / 2, yMin + (yMax - yMin) / 2);
        }

        private static int ComputeInitialAmount(ResourceType rt, int cellCount)
        {
            int perCell = rt switch
            {
                ResourceType.Wood => 10,
                ResourceType.Food => 8,
                ResourceType.Stone => 14,
                ResourceType.Iron => 12,
                _ => 1
            };
            return cellCount * perCell;
        }

        private static int PackCell(CellPos c)
        {
            return (c.Y << 16) ^ (c.X & 0xFFFF);
        }
    }
}
