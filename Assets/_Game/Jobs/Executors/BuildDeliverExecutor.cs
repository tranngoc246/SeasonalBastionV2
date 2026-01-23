using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildDeliverExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        private const int CarryCap = 10;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        public BuildDeliverExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.WorldIndex == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            if (job.Site.Value == 0 || !_s.WorldState.Sites.Exists(job.Site))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            var site = _s.WorldState.Sites.Get(job.Site);
            if (!site.IsActive)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            var rt = job.ResourceType;

            // If already no remaining for this resource => complete (avoid stuck)
            int remaining = GetRemainingFor(site, rt);
            if (remaining <= 0)
            {
                job.Status = JobStatus.Completed;
                Cleanup(job.Id.Value);
                return true;
            }

            int jid = job.Id.Value;
            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                // Decide pickup amount (clamp)
                int want = job.Amount > 0 ? job.Amount : CarryCap;
                if (want > CarryCap) want = CarryCap;
                if (want > remaining) want = remaining;
                if (want <= 0)
                {
                    job.Status = JobStatus.Completed;
                    Cleanup(jid);
                    return true;
                }

                // choose source (warehouse/hq)
                if (job.SourceBuilding.Value == 0 || !_s.WorldState.Buildings.Exists(job.SourceBuilding))
                {
                    if (!TryPickBestStorageSource(npcState.Cell, job.Workplace, rt, want, out var src))
                        return true; // wait
                    job.SourceBuilding = src;
                }

                var srcId = job.SourceBuilding;
                if (!_s.WorldState.Buildings.Exists(srcId))
                    return true;

                var srcState = _s.WorldState.Buildings.Get(srcId);
                if (!srcState.IsConstructed)
                    return true;

                // Move to source
                job.TargetCell = srcState.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _s.AgentMover.StepToward(ref npcState, srcState.Anchor);
                if (!arrivedSrc)
                    return true;

                // pickup from storage
                int removed = _s.StorageService.Remove(srcId, rt, want);
                if (removed <= 0)
                {
                    // source ran dry => repick next tick
                    job.SourceBuilding = default;
                    return true;
                }

                _carry[jid] = removed;
                _phase[jid] = 1;

                job.Amount = removed;
                job.Status = JobStatus.InProgress;
                return true;
            }
            else
            {
                if (!_carry.TryGetValue(jid, out var carried) || carried <= 0)
                {
                    job.Status = JobStatus.Failed;
                    Cleanup(jid);
                    return true;
                }

                // Move to site anchor (delivery point)
                job.TargetCell = site.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedSite = _s.AgentMover.StepToward(ref npcState, site.Anchor);
                if (!arrivedSite)
                    return true;

                // Apply delivery to site (re-read to avoid race with other NPCs)
                if (!_s.WorldState.Sites.Exists(job.Site))
                {
                    // site vanished; refund best-effort
                    RefundToSource(job.SourceBuilding, rt, carried);
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                site = _s.WorldState.Sites.Get(job.Site);

                int remainingNow = GetRemainingFor(site, rt);
                int apply = carried;
                if (apply > remainingNow) apply = remainingNow;

                if (apply > 0)
                {
                    ApplyDelivered(ref site, rt, apply);
                    _s.WorldState.Sites.Set(job.Site, site);
                }

                int refund = carried - apply;
                if (refund > 0)
                    RefundToSource(job.SourceBuilding, rt, refund);

                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private void RefundToSource(BuildingId src, ResourceType rt, int amount)
        {
            if (amount <= 0) return;
            if (src.Value == 0) return;
            if (_s.WorldState.Buildings == null || !_s.WorldState.Buildings.Exists(src)) return;

            _s.StorageService.Add(src, rt, amount);
        }

        private static int GetRemainingFor(in BuildSiteState site, ResourceType rt)
        {
            if (site.RemainingCosts == null) return 0;
            for (int i = 0; i < site.RemainingCosts.Count; i++)
            {
                var c = site.RemainingCosts[i];
                if (c.Resource == rt) return c.Amount;
            }
            return 0;
        }

        private static void ApplyDelivered(ref BuildSiteState site, ResourceType rt, int amount)
        {
            if (site.RemainingCosts == null || amount <= 0) return;

            for (int i = 0; i < site.RemainingCosts.Count; i++)
            {
                var c = site.RemainingCosts[i];
                if (c.Resource != rt) continue;

                int left = c.Amount - amount;
                if (left <= 0)
                {
                    site.RemainingCosts.RemoveAt(i);
                }
                else
                {
                    c.Amount = left;
                    site.RemainingCosts[i] = c;
                }
                break;
            }

            if (site.RemainingCosts.Count == 0)
                site.RemainingCosts = null; // clean ready-to-work gate
        }

        private bool TryPickBestStorageSource(CellPos from, BuildingId workplace, ResourceType rt, int minRequired, out BuildingId best)
        {
            best = default;

            // 1) Prefer workplace if it can provide
            if (workplace.Value != 0 && _s.WorldState.Buildings.Exists(workplace))
            {
                var ws = _s.WorldState.Buildings.Get(workplace);
                if (ws.IsConstructed && _s.StorageService.GetAmount(workplace, rt) >= minRequired)
                {
                    best = workplace;
                    return true;
                }
            }

            // 2) Scan warehouses (includes HQ)
            var whs = _s.WorldIndex.Warehouses;
            if (whs == null || whs.Count == 0) return false;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (_s.StorageService.GetAmount(bid, rt) < minRequired) continue;

                int d = Manhattan(from, bs.Anchor);
                int idv = bid.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            return best.Value != 0;
        }

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
            // JobScheduler will ReleaseAll(npc) on terminal status (safe).
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
