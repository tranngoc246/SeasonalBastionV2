using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class NpcIdleRoamService
    {
        private struct IdleRoamState
        {
            public CellPos Anchor;
            public CellPos Target;
            public float WaitTimer;
            public bool HasTarget;
        }

        private readonly GameServices _s;
        private readonly IWorldState _w;
        private readonly Dictionary<int, IdleRoamState> _stateByNpc = new();

        private const float IdleWaitMin = 0.8f;
        private const float IdleWaitMax = 2.2f;
        private const int IdleRadiusMin = 3;
        private const int IdleRadiusMax = 6;
        private const int BuilderIdleRadiusMin = 4;
        private const int BuilderIdleRadiusMax = 8;

        internal NpcIdleRoamService(GameServices s, IWorldState w)
        {
            _s = s;
            _w = w;
        }

        internal void TickIdleNpc(NpcId npc, ref NpcState ns, float dt)
        {
            if (_s?.AgentMover == null || _s.GridMap == null)
                return;

            var st = GetOrCreateState(npc, ns);
            st.Anchor = ResolveIdleAnchor(ns);

            if (st.WaitTimer > 0f)
            {
                st.WaitTimer -= dt;
                if (st.WaitTimer < 0f) st.WaitTimer = 0f;
                _stateByNpc[npc.Value] = st;
                return;
            }

            if (!st.HasTarget || !IsValidIdleCell(ns, st.Target, st.Anchor))
            {
                if (!TryPickIdleTarget(npc, ns, st.Anchor, out var next))
                {
                    st.HasTarget = false;
                    st.WaitTimer = ComputeIdleWait(npc);
                    _stateByNpc[npc.Value] = st;
                    return;
                }

                st.Target = next;
                st.HasTarget = true;
            }

            bool arrived = _s.AgentMover.StepToward(ref ns, st.Target, dt);
            if (arrived || Same(ns.Cell, st.Target))
            {
                st.HasTarget = false;
                st.WaitTimer = ComputeIdleWait(npc);
            }

            _stateByNpc[npc.Value] = st;
        }

        internal void ClearNpc(NpcId npc)
        {
            _stateByNpc.Remove(npc.Value);
        }

        private IdleRoamState GetOrCreateState(NpcId npc, in NpcState ns)
        {
            if (_stateByNpc.TryGetValue(npc.Value, out var st))
                return st;

            st = new IdleRoamState
            {
                Anchor = ResolveIdleAnchor(ns),
                Target = default,
                WaitTimer = 0f,
                HasTarget = false
            };

            _stateByNpc[npc.Value] = st;
            return st;
        }

        private CellPos ResolveIdleAnchor(in NpcState ns)
        {
            if (_w != null && ns.Workplace.Value != 0 && _w.Buildings.Exists(ns.Workplace))
            {
                if (TryFindNearestActiveSiteForBuilder(ns, out var siteAnchor))
                    return siteAnchor;

                var bs = _w.Buildings.Get(ns.Workplace);
                return bs.Anchor;
            }

            var hq = TryFindHQ();
            if (hq.HasValue)
                return hq.Value;

            return ns.Cell;
        }

        private CellPos? TryFindHQ()
        {
            if (_w == null)
                return null;

            foreach (var bid in _w.Buildings.Ids)
            {
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (DefIdTierUtil.IsBase(bs.DefId, "bld_hq"))
                    return bs.Anchor;
            }

            return null;
        }

        private bool TryPickIdleTarget(NpcId npc, in NpcState ns, CellPos anchor, out CellPos target)
        {
            target = default;

            int seed = Hash(npc.Value, anchor.X, anchor.Y);
            GetIdleRadiusRange(ns, out int minRadius, out int maxRadius);
            int radius = LerpInt(minRadius, maxRadius, seed);

            CellPos[] bestCells = new CellPos[4];
            int[] bestScores = new int[4] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
            int foundCount = 0;

            for (int r = 1; r <= radius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r)
                            continue;

                        var c = new CellPos(anchor.X + dx, anchor.Y + dy);

                        if (!IsValidIdleCell(ns, c, anchor))
                            continue;

                        if (Same(c, ns.Cell))
                            continue;

                        int score = ScoreIdleCell(seed, ns.Cell, anchor, c);
                        TryInsertBestCandidate(c, score, bestCells, bestScores, ref foundCount);
                    }
                }
            }

            if (foundCount == 0)
                return false;

            int pick = (Hash(seed, ns.Cell.X, ns.Cell.Y) & 0x7fffffff) % foundCount;
            target = bestCells[pick];
            return true;
        }

        private bool IsValidIdleCell(in NpcState ns, CellPos c, CellPos anchor)
        {
            if (_s?.GridMap == null)
                return false;

            if (!_s.GridMap.IsInside(c))
                return false;

            if (_s.GridMap.IsBlocked(c))
                return false;

            var occ = _s.GridMap.Get(c).Kind;
            if (occ != CellOccupancyKind.Empty && occ != CellOccupancyKind.Road)
                return false;

            if (IsInteractionLikeCell(c))
                return false;

            if (IsNearInteractionCell(c))
                return false;

            int maxRadius = IsBuilderNpc(ns) ? BuilderIdleRadiusMax : IdleRadiusMax;
            if (Manhattan(c, anchor) > maxRadius)
                return false;

            return true;
        }

        private bool IsInteractionLikeCell(CellPos c)
        {
            if (_w == null)
                return false;

            foreach (var bid in _w.Buildings.Ids)
            {
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                var entry = EntryCellUtil.GetApproachCellForBuilding(_s, bs, c);
                if (Same(entry, c))
                    return true;
            }

            foreach (var sid in _w.Sites.Ids)
            {
                if (!_w.Sites.Exists(sid)) continue;

                var site = _w.Sites.Get(sid);
                if (!site.IsActive) continue;

                var entry = EntryCellUtil.GetApproachCellForSite(_s, site, c);
                if (Same(entry, c))
                    return true;
            }

            return false;
        }

        private float ComputeIdleWait(NpcId npc)
        {
            int seed = Hash(npc.Value, _stateByNpc.Count, 17);
            float t = (seed & 1023) / 1023f;
            return IdleWaitMin + (IdleWaitMax - IdleWaitMin) * t;
        }

        private int ScoreIdleCell(int seed, CellPos from, CellPos anchor, CellPos c)
        {
            int score = 0;
            int distFrom = Manhattan(from, c);
            int distAnchor = Manhattan(anchor, c);

            score += System.Math.Abs(distFrom - 5) * 6;
            score += System.Math.Abs(distAnchor - 4) * 5;

            if (_s.GridMap.IsRoad(c))
                score += 10;

            if (IsNearInteractionCell(c))
                score += 20;

            score += Hash(seed, c.X, c.Y) & 3;
            return score;
        }

        private bool IsNearInteractionCell(CellPos c)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    if (System.Math.Abs(dx) + System.Math.Abs(dy) != 1)
                        continue;

                    var n = new CellPos(c.X + dx, c.Y + dy);
                    if (IsInteractionLikeCell(n))
                        return true;
                }
            }

            return false;
        }

        private static void TryInsertBestCandidate(CellPos cell, int score, CellPos[] bestCells, int[] bestScores, ref int foundCount)
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

        private void GetIdleRadiusRange(in NpcState ns, out int min, out int max)
        {
            if (IsBuilderNpc(ns))
            {
                min = BuilderIdleRadiusMin;
                max = BuilderIdleRadiusMax;
                return;
            }

            min = IdleRadiusMin;
            max = IdleRadiusMax;
        }

        private bool IsBuilderNpc(in NpcState ns)
        {
            if (_w == null || ns.Workplace.Value == 0 || !_w.Buildings.Exists(ns.Workplace))
                return false;

            var bs = _w.Buildings.Get(ns.Workplace);
            return DefIdTierUtil.IsBase(bs.DefId, "bld_builderhut");
        }

        private bool TryFindNearestActiveSiteForBuilder(in NpcState ns, out CellPos siteAnchor)
        {
            siteAnchor = default;
            if (!IsBuilderNpc(ns) || _w == null)
                return false;

            bool found = false;
            int bestPriority = int.MaxValue;
            int bestDist = int.MaxValue;

            foreach (var sid in _w.Sites.Ids)
            {
                if (!_w.Sites.Exists(sid)) continue;

                var site = _w.Sites.Get(sid);
                if (!site.IsActive) continue;

                int pri = GetBuilderSitePriority(site);
                int dist = Manhattan(ns.Cell, site.Anchor);
                if (!found || pri < bestPriority || (pri == bestPriority && dist < bestDist))
                {
                    found = true;
                    bestPriority = pri;
                    bestDist = dist;
                    siteAnchor = site.Anchor;
                }
            }

            return found;
        }

        private static int GetBuilderSitePriority(in BuildSiteState site)
        {
            bool needsDelivery = site.RemainingCosts != null && site.RemainingCosts.Count > 0;
            bool hasWorkLeft = site.WorkSecondsDone + 1e-4f < site.WorkSecondsTotal;

            if (needsDelivery)
                return 0;

            if (hasWorkLeft)
                return 1;

            return 2;
        }

        private static int LerpInt(int min, int max, int seed)
        {
            int span = max - min + 1;
            return min + ((seed & 0x7fffffff) % span);
        }

        private static int Hash(int a, int b, int c)
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

        private static bool Same(CellPos a, CellPos b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
