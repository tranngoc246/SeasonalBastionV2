using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day 10 (PART27):
    /// - TryPickSource / TryPickDest: pick best candidate by travel cost when possible, fallback to Manhattan, tie-break by BuildingId.Value (ascending)
    /// - Transfer: atomic (no negative, cap respected)
    /// Notes:
    /// - Uses WorldIndex lists (deterministic sorted by id)【WorldIndexService】
    /// - Refund path does NOT call StorageService.Add to avoid "delivered" events on refund to HQ/Warehouse.
    /// </summary>
    public sealed class ResourceFlowService : IResourceFlowService
    {
        private readonly IWorldState _w;
        private readonly IWorldIndex _index;
        private readonly IStorageService _storage;
        private readonly IPathfinderRuntime _pathfinder;

        public ResourceFlowService(IWorldState w, IWorldIndex index, IStorageService storage, IPathfinderRuntime pathfinder = null)
        {
            _w = w;
            _index = index;
            _storage = storage;
            _pathfinder = pathfinder;
        }

        public bool TryPickSource(CellPos from, ResourceType type, int minAmount, out StoragePick pick)
        {
            pick = default;

            // "or any amount" => treat <=0 as 1 (ignore empty sources)
            int need = minAmount <= 0 ? 1 : minAmount;

            var candidates = GetSourceCandidates(type);

            int bestCost = int.MaxValue;
            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;
            BuildingId best = default;

            for (int i = 0; i < candidates.Count; i++)
            {
                var bid = candidates[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bst = _w.Buildings.Get(bid);
                if (!bst.IsConstructed) continue;

                if (!_storage.CanStore(bid, type)) continue;

                int amt = _storage.GetAmount(bid, type);
                if (amt < need) continue;

                var approach = ResolveApproachCell(bst, from);
                int d = Manhattan(from, approach);
                if (!TryGetCandidateTravelCost(from, approach, d, out var cost))
                    continue;
                int idv = bid.Value;

                if (cost < bestCost
                    || (cost == bestCost && d < bestDist)
                    || (cost == bestCost && d == bestDist && idv < bestId))
                {
                    bestCost = cost;
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            if (best.Value == 0) return false;
            pick = new StoragePick(best, bestCost);
            return true;
        }

        public bool TryPickDest(CellPos from, ResourceType type, int minSpace, out StoragePick pick)
        {
            pick = default;

            int needSpace = minSpace <= 0 ? 1 : minSpace;

            var candidates = GetDestCandidates(type);

            int bestCost = int.MaxValue;
            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;
            BuildingId best = default;

            for (int i = 0; i < candidates.Count; i++)
            {
                var bid = candidates[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bst = _w.Buildings.Get(bid);
                if (!bst.IsConstructed) continue;

                if (!_storage.CanStore(bid, type)) continue;

                int cap = _storage.GetCap(bid, type);
                if (cap <= 0) continue;

                int amt = _storage.GetAmount(bid, type);
                int space = cap - amt;
                if (space < needSpace) continue;

                var approach = ResolveApproachCell(bst, from);
                int d = Manhattan(from, approach);
                if (!TryGetCandidateTravelCost(from, approach, d, out var cost))
                    continue;
                int idv = bid.Value;

                if (cost < bestCost
                    || (cost == bestCost && d < bestDist)
                    || (cost == bestCost && d == bestDist && idv < bestId))
                {
                    bestCost = cost;
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            if (best.Value == 0) return false;
            pick = new StoragePick(best, bestCost);
            return true;
        }

        public int Transfer(BuildingId src, BuildingId dst, ResourceType type, int amount)
        {
            if (amount <= 0) return 0;
            if (src.Value == 0 || dst.Value == 0) return 0;
            if (src.Value == dst.Value) return 0;

            if (!_w.Buildings.Exists(src) || !_w.Buildings.Exists(dst)) return 0;

            // storage rules
            if (!_storage.CanStore(src, type)) return 0;
            if (!_storage.CanStore(dst, type)) return 0;

            // 1) remove (clamped)
            int removed = _storage.Remove(src, type, amount);
            if (removed <= 0) return 0;

            // 2) add to dst (clamped by cap)
            int added = _storage.Add(dst, type, removed);

            // 3) refund remainder back to src (should always fit, but do NOT trigger delivered event)
            int refund = removed - added;
            if (refund > 0)
                RefundToSource_NoEvent(src, type, refund);

            return added;
        }

        // ----------------------------
        // Candidate sets (deterministic)
        // ----------------------------
        private IReadOnlyList<BuildingId> GetSourceCandidates(ResourceType type)
        {
            // ammo flow v0.1: source = Forge + Armory (armory may hold ammo too)
            if (type == ResourceType.Ammo)
                return MergeSortedById(_index.Forges, _index.Armories);

            // basic flow v0.1: source = Producers + Warehouses (HQ treated as warehouse in index)
            return MergeSortedById(_index.Producers, _index.Warehouses);
        }

        private IReadOnlyList<BuildingId> GetDestCandidates(ResourceType type)
        {
            // ammo destination v0.1: Armory only
            if (type == ResourceType.Ammo)
                return _index.Armories;

            // basic destination v0.1: Warehouses (HQ included)
            return _index.Warehouses;
        }

        private CellPos ResolveApproachCell(in BuildingState bst, CellPos from)
        {
            if (DefIdTierUtil.IsBase(bst.DefId, "bld_hq"))
            {
                var eN = ComputeEntryOutsideFootprint(bst.Anchor, 5, 5, Dir4.N);
                var eS = ComputeEntryOutsideFootprint(bst.Anchor, 5, 5, Dir4.S);
                var eE = ComputeEntryOutsideFootprint(bst.Anchor, 5, 5, Dir4.E);
                var eW = ComputeEntryOutsideFootprint(bst.Anchor, 5, 5, Dir4.W);
                return PickNearest(from, bst.Anchor, eN, eS, eE, eW);
            }

            int w = 1, h = 1;
            if (DefIdTierUtil.IsBase(bst.DefId, "bld_house")
                || DefIdTierUtil.IsBase(bst.DefId, "bld_farmhouse")
                || DefIdTierUtil.IsBase(bst.DefId, "bld_lumbercamp")
                || DefIdTierUtil.IsBase(bst.DefId, "bld_quarry")
                || DefIdTierUtil.IsBase(bst.DefId, "bld_ironhut")
                || DefIdTierUtil.IsBase(bst.DefId, "bld_warehouse"))
            {
                w = 3;
                h = 3;
            }

            var entry = ComputeEntryOutsideFootprint(bst.Anchor, w, h, bst.Rotation);
            return PickNearest(from, bst.Anchor, entry);
        }

        private bool TryEstimateTravelCost(CellPos from, CellPos to, out int cost)
        {
            cost = 0;
            if (_pathfinder == null) return false;
            return _pathfinder.TryEstimateCost(from, to, out cost);
        }

        private bool TryGetCandidateTravelCost(CellPos from, CellPos to, int fallbackManhattan, out int cost)
        {
            cost = 0;

            if (_pathfinder != null)
                return _pathfinder.TryEstimateCost(from, to, out cost);

            cost = fallbackManhattan;
            return true;
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static CellPos PickNearest(CellPos from, CellPos fallback, params CellPos[] candidates)
        {
            if (candidates == null || candidates.Length == 0) return fallback;

            int best = int.MaxValue;
            CellPos bestPos = fallback;
            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                int d = Manhattan(from, c);
                if (d < best)
                {
                    best = d;
                    bestPos = c;
                }
            }
            return bestPos;
        }

        private static CellPos ComputeEntryOutsideFootprint(CellPos anchor, int w, int h, Dir4 rot)
        {
            int cx = w / 2;
            int cy = h / 2;

            return rot switch
            {
                Dir4.N => new CellPos(anchor.X + cx, anchor.Y + h),
                Dir4.S => new CellPos(anchor.X + cx, anchor.Y - 1),
                Dir4.E => new CellPos(anchor.X + w, anchor.Y + cy),
                Dir4.W => new CellPos(anchor.X - 1, anchor.Y + cy),
                _ => new CellPos(anchor.X + cx, anchor.Y + h),
            };
        }

        /// <summary>
        /// Merge 2 lists already sorted by BuildingId.Value ascending into 1 sorted list (deterministic).
        /// </summary>
        private static IReadOnlyList<BuildingId> MergeSortedById(IReadOnlyList<BuildingId> a, IReadOnlyList<BuildingId> b)
        {
            if (a.Count == 0) return b;
            if (b.Count == 0) return a;

            var res = new List<BuildingId>(a.Count + b.Count);
            int i = 0, j = 0;

            while (i < a.Count || j < b.Count)
            {
                if (j >= b.Count) { res.Add(a[i++]); continue; }
                if (i >= a.Count) { res.Add(b[j++]); continue; }

                int av = a[i].Value;
                int bv = b[j].Value;

                if (av <= bv) res.Add(a[i++]);
                else res.Add(b[j++]);
            }

            return res;
        }

        /// <summary>
        /// Refund without firing StorageService.Add() side effects (avoid "delivered" event on refund).
        /// Safe because we removed first, so src always has enough room to put back exactly that amount.
        /// </summary>
        private void RefundToSource_NoEvent(BuildingId src, ResourceType type, int amount)
        {
            if (amount <= 0) return;
            if (!_w.Buildings.Exists(src)) return;

            var st = _w.Buildings.Get(src);

            switch (type)
            {
                case ResourceType.Wood: st.Wood += amount; break;
                case ResourceType.Food: st.Food += amount; break;
                case ResourceType.Stone: st.Stone += amount; break;
                case ResourceType.Iron: st.Iron += amount; break;
                case ResourceType.Ammo: st.Ammo += amount; break;
            }

            _w.Buildings.Set(src, st);
        }
    }
}