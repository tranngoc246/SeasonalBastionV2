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
            return TryGetBestPatch(rt, nearCell, out patch);
        }

        public bool TryGetNearestAvailablePatch(ResourceType rt, CellPos nearCell, out ResourcePatchState patch)
        {
            return TryGetBestPatch(rt, nearCell, out patch);
        }

        public bool TryGetBestPatch(ResourceType rt, CellPos origin, out ResourcePatchState patch)
        {
            patch = default;
            bool found = false;
            int bestScore = int.MaxValue;

            for (int i = 0; i < _ordered.Count; i++)
            {
                var p = _ordered[i];
                if (p.Resource != rt || p.RemainingAmount <= 0)
                    continue;

                int dist = System.Math.Abs(p.Anchor.X - origin.X) + System.Math.Abs(p.Anchor.Y - origin.Y);
                int richness = p.RemainingAmount;
                int score = dist * 12 - (richness > 200 ? 200 : richness);
                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    patch = p;
                }
            }

            return found;
        }

        public bool TryPickCellInPatch(int patchId, CellPos origin, int variationSeed, out CellPos cell)
        {
            cell = default;
            if (!_byId.TryGetValue(patchId, out var patch) || patch.Cells == null || patch.Cells.Count == 0)
                return false;

            CellPos[] bestCells = new CellPos[4];
            int[] bestScores = new int[4] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
            int foundCount = 0;

            for (int i = 0; i < patch.Cells.Count; i++)
            {
                var c = patch.Cells[i];
                int dist = System.Math.Abs(c.X - origin.X) + System.Math.Abs(c.Y - origin.Y);
                int score = dist * 8 + ((Mix(variationSeed, c.X, c.Y) & 7));
                TryInsertBestCell(c, score, bestCells, bestScores, ref foundCount);
            }

            if (foundCount == 0)
                return false;

            int pick = (Mix(variationSeed, origin.X, origin.Y) & 0x7fffffff) % foundCount;
            cell = bestCells[pick];
            return true;
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

        private static void TryInsertBestCell(CellPos cell, int score, CellPos[] bestCells, int[] bestScores, ref int foundCount)
        {
            for (int i = 0; i < bestScores.Length; i++)
            {
                if (score >= bestScores[i])
                    continue;

                for (int j = bestScores.Length - 1; j > i; j--)
                {
                    bestScores[j] = bestScores[j - 1];
                    bestCells[j] = bestCells[j - 1];
                }

                bestScores[i] = score;
                bestCells[i] = cell;
                if (foundCount < bestScores.Length)
                    foundCount++;
                return;
            }

            if (foundCount < bestScores.Length)
            {
                bestScores[foundCount] = score;
                bestCells[foundCount] = cell;
                foundCount++;
            }
        }

        private static int Mix(int a, int b, int c)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a;
                h = h * 31 + b;
                h = h * 31 + c;
                return h;
            }
        }

        private static int PackCell(CellPos c)
        {
            return (c.Y << 16) ^ (c.X & 0xFFFF);
        }
    }
}
