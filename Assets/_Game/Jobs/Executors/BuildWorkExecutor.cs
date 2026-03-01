using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildWorkExecutor : IJobExecutor
    {
#if UNITY_EDITOR
        private float _dbgWallT;
        private float _dbgLastWork;
        private int _dbgLastSite;
#endif

        private readonly GameServices _s;

        // jobId -> phase: 0 pickup, 1 deliver-to-site, 2 build
        private readonly Dictionary<int, byte> _phase = new();

        // jobId -> carried amount (for delivery phase)
        private readonly Dictionary<int, int> _carry = new();

        // jobId -> remaining settle seconds (used for pickup/deliver/build)
        // IMPORTANT:
        // - pickup/deliver: settle is per-action => remove after done
        // - build: settle should happen ONCE, then keep value = 0 (sentinel) so we don't restart settle every cycle
        private readonly Dictionary<int, float> _settle = new();

        // simple source claim (optional but prevents 2 builders fighting same source+rt)
        private readonly Dictionary<int, ClaimKey> _srcClaimByJob = new();

        // jobId -> locked build entry cell (avoid oscillation when HQ has 4 entries)
        private readonly Dictionary<int, CellPos> _buildEntry = new();

        private const float PickupSettleSec = 1.0f;
        private const float DeliverSettleSec = 1.0f;
        private const float BuildSettleSec = 1.0f;

        public BuildWorkExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.AgentMover == null || _s.StorageService == null || _s.WorldIndex == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            if (job.Site.Value == 0 || !_s.WorldState.Sites.Exists(job.Site))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            var site = _s.WorldState.Sites.Get(job.Site);
            if (!site.IsActive)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            int jid = job.Id.Value;
            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            // If still need deliveries => builder does pickup/deliver loop
            if (site.RemainingCosts != null && site.RemainingCosts.Count > 0)
            {
                // Always stage to site entry if no source picked yet (reduces idle)
                var siteEntry = EntryCellUtil.GetApproachCellForSite(_s, site, npcState.Cell);

                if (ph == 0)
                {
                    // Pick next required resource (deterministic)
                    int idx = PickNextRemainingIndex(site);
                    if (idx < 0)
                    {
                        // costs list dirty -> treat as ready
                        site.RemainingCosts = null;
                        _s.WorldState.Sites.Set(job.Site, site);
                        _phase[jid] = 2;

                        // entering build => clear settle (build settle should start fresh)
                        _settle.Remove(jid);
                        _buildEntry.Remove(jid);

                        return true;
                    }

                    var need = site.RemainingCosts[idx];
                    if (need.Amount <= 0)
                    {
                        // clean
                        site.RemainingCosts.RemoveAt(idx);
                        if (site.RemainingCosts.Count == 0) site.RemainingCosts = null;
                        _s.WorldState.Sites.Set(job.Site, site);
                        return true;
                    }

                    var rt = need.Resource;

                    int builderTier = 1;
                    if (_s.Balance != null && job.Workplace.Value != 0 && _s.WorldState.Buildings.Exists(job.Workplace))
                    {
                        var wp = _s.WorldState.Buildings.Get(job.Workplace);
                        builderTier = _s.Balance.GetTierFromLevel(wp.Level);
                    }
                    int cap = _s.Balance != null ? _s.Balance.GetCarryBuilder(builderTier) : 10;

                    int want = need.Amount;
                    if (want > cap) want = cap;

                    if (want <= 0) return true;

                    // choose source (prefer workplace if stocked, else nearest warehouse/hq)
                    if (job.SourceBuilding.Value == 0 || !_s.WorldState.Buildings.Exists(job.SourceBuilding) || job.ResourceType != rt)
                    {
                        if (!TryPickBestStorageSource(npcState.Cell, job.Workplace, rt, 1, out var src))
                        {
                            // No source currently => wait at site entry
                            job.TargetCell = siteEntry;
                            _s.AgentMover.StepToward(ref npcState, siteEntry, dt);
                            job.Status = JobStatus.InProgress;
                            return true;
                        }

                        job.SourceBuilding = src;
                        job.ResourceType = rt;
                    }

                    var srcId = job.SourceBuilding;
                    var srcState = _s.WorldState.Buildings.Get(srcId);
                    if (!srcState.IsConstructed)
                    {
                        job.SourceBuilding = default;
                        return true;
                    }

                    // reserve source (exclusive) - optional safety
                    if (!EnsureSourceClaim(npc, jid, srcId, rt))
                        return true;

                    // Move to source ENTRY
                    var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_s, srcState, npcState.Cell);
                    job.TargetCell = srcEntry;
                    job.Status = JobStatus.InProgress;

                    bool arrivedSrc = _s.AgentMover.StepToward(ref npcState, srcEntry, dt);
                    if (!arrivedSrc) return true;

                    // settle before pickup (per-action)
                    if (!_settle.TryGetValue(jid, out var remPick))
                        remPick = PickupSettleSec;

                    remPick -= dt;
                    if (remPick > 0f)
                    {
                        _settle[jid] = remPick;
                        return true;
                    }
                    _settle.Remove(jid);

                    // pickup from storage (clamp to want)
                    int removed = _s.StorageService.Remove(srcId, rt, want);

                    // release claim after attempt
                    ReleaseSourceClaimIfOwned(npc, jid);

                    if (removed <= 0)
                    {
                        // ran dry -> repick next tick
                        job.SourceBuilding = default;
                        return true;
                    }

                    _carry[jid] = removed;
                    job.Amount = removed;
                    _phase[jid] = 1;

                    return true;
                }
                else
                {
                    // ph == 1 : deliver carried to site
                    if (!_carry.TryGetValue(jid, out var carried) || carried <= 0)
                    {
                        _phase[jid] = 0;
                        return true;
                    }

                    var entry = siteEntry;
                    job.TargetCell = entry;
                    job.Status = JobStatus.InProgress;

                    bool arrivedSite = _s.AgentMover.StepToward(ref npcState, entry, dt);
                    if (!arrivedSite) return true;

                    // settle before applying delivery (per-action)
                    if (!_settle.TryGetValue(jid, out var remDel))
                        remDel = DeliverSettleSec;

                    remDel -= dt;
                    if (remDel > 0f)
                    {
                        _settle[jid] = remDel;
                        return true;
                    }
                    _settle.Remove(jid);

                    // re-read site (race-safe)
                    site = _s.WorldState.Sites.Get(job.Site);

                    // apply delivered
                    var rt = job.ResourceType;
                    int remainingNow = GetRemainingFor(site, rt);
                    int apply = carried;
                    if (apply > remainingNow) apply = remainingNow;

                    if (apply > 0)
                    {
                        ApplyDelivered(ref site, rt, apply);
                        _s.WorldState.Sites.Set(job.Site, site);

                        _s.EventBus?.Publish(new ResourceSpentEvent(rt, apply, job.SourceBuilding));
                    }

                    int refund = carried - apply;
                    if (refund > 0)
                    {
                        // best-effort refund to source
                        _s.StorageService.Add(job.SourceBuilding, rt, refund);
                    }

                    _carry.Remove(jid);
                    job.Amount = 0;

                    bool ready = (site.RemainingCosts == null || site.RemainingCosts.Count == 0);
                    _phase[jid] = ready ? (byte)2 : (byte)0;

                    // when switching to build, reset settle so build settle counts fresh and lock entry fresh
                    if (ready)
                    {
                        _settle.Remove(jid);
                        _buildEntry.Remove(jid);
                    }

                    return true;
                }
            }

            // Ready to build => phase 2 build
            _phase[jid] = 2;

            // LOCK build entry once per job to prevent target flipping (HQ 4-entries tie case)
            if (!_buildEntry.TryGetValue(jid, out var buildEntry))
            {
                buildEntry = EntryCellUtil.GetApproachCellForSite(_s, site, npcState.Cell);
                _buildEntry[jid] = buildEntry;

                // entering BUILD: ensure settle starts from full (once)
                _settle.Remove(jid);
            }

            job.TargetCell = buildEntry;
            job.Status = JobStatus.InProgress;

            bool arrived = _s.AgentMover.StepToward(ref npcState, buildEntry, dt);
            if (!arrived) return true;

            // BUILD settle should happen ONCE, then keep _settle[jid] = 0 so it doesn't restart.
            if (!_settle.TryGetValue(jid, out var remBuild))
            {
                remBuild = BuildSettleSec; // first time entering BUILD
            }

            if (remBuild > 0f)
            {
                remBuild -= dt;
                if (remBuild > 0f)
                {
                    _settle[jid] = remBuild;
                    return true;
                }

                // finished build settle => keep sentinel 0
                _settle[jid] = 0f;
            }

            if (dt > 0f)
            {
                site.WorkSecondsDone += dt;

#if UNITY_EDITOR
                {
                    // log mỗi ~1s wall-clock, không spam
                    float now = UnityEngine.Time.realtimeSinceStartup;
                    if (_dbgWallT <= 0f) _dbgWallT = now;

                    if (now - _dbgWallT >= 1f)
                    {
                        float ts = _s.RunClock != null ? _s.RunClock.TimeScale : -1f;
                        float dWork = site.WorkSecondsDone - _dbgLastWork;
                        _dbgLastWork = site.WorkSecondsDone;
                        _dbgLastSite = job.Site.Value;

                        _dbgWallT = now;
                    }
                }
#endif

                if (site.WorkSecondsDone > site.WorkSecondsTotal)
                    site.WorkSecondsDone = site.WorkSecondsTotal;

                _s.WorldState.Sites.Set(job.Site, site);
            }

            if (site.WorkSecondsDone + 1e-4f >= site.WorkSecondsTotal)
            {
                job.Status = JobStatus.Completed;
                Cleanup(jid, npc);
                return true;
            }

            return true;
        }

        private void Cleanup(int jobId, NpcId npc)
        {
            ReleaseSourceClaimIfOwned(npc, jobId);

            _phase.Remove(jobId);
            _carry.Remove(jobId);
            _settle.Remove(jobId);
            _srcClaimByJob.Remove(jobId);
            _buildEntry.Remove(jobId);
        }

        private bool EnsureSourceClaim(NpcId npc, int jobId, BuildingId src, ResourceType rt)
        {
            var claims = _s.ClaimService;
            if (claims == null) return true;

            var key = new ClaimKey(ClaimKind.StorageSource, src.Value, (int)rt);

            if (_srcClaimByJob.TryGetValue(jobId, out var old)
                && (old.Kind != key.Kind || old.A != key.A || old.B != key.B))
            {
                if (claims.IsOwnedBy(old, npc)) claims.Release(old, npc);
                _srcClaimByJob.Remove(jobId);
            }

            if (_srcClaimByJob.TryGetValue(jobId, out var owned))
            {
                if (claims.IsOwnedBy(owned, npc)) return true;
                _srcClaimByJob.Remove(jobId);
            }

            if (claims.TryAcquire(key, npc))
            {
                _srcClaimByJob[jobId] = key;
                return true;
            }

            return false;
        }

        private void ReleaseSourceClaimIfOwned(NpcId npc, int jobId)
        {
            var claims = _s.ClaimService;
            if (claims == null) return;

            if (_srcClaimByJob.TryGetValue(jobId, out var key))
            {
                if (claims.IsOwnedBy(key, npc)) claims.Release(key, npc);
                _srcClaimByJob.Remove(jobId);
            }
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

        private static int PickNextRemainingIndex(in BuildSiteState site)
        {
            if (site.RemainingCosts == null || site.RemainingCosts.Count == 0) return -1;

            int best = -1;
            int bestKey = int.MaxValue;

            for (int i = 0; i < site.RemainingCosts.Count; i++)
            {
                var c = site.RemainingCosts[i];
                if (c.Amount <= 0) continue;

                int key = (int)c.Resource;
                if (key < bestKey)
                {
                    bestKey = key;
                    best = i;
                }
            }

            return best;
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
            if (amount <= 0) return;

            if (site.DeliveredSoFar != null)
            {
                for (int i = 0; i < site.DeliveredSoFar.Count; i++)
                {
                    var c = site.DeliveredSoFar[i];
                    if (c.Resource != rt) continue;
                    c.Amount += amount;
                    site.DeliveredSoFar[i] = c;
                    break;
                }
            }

            if (site.RemainingCosts != null)
            {
                for (int i = 0; i < site.RemainingCosts.Count; i++)
                {
                    var c = site.RemainingCosts[i];
                    if (c.Resource != rt) continue;

                    int left = c.Amount - amount;
                    if (left <= 0) site.RemainingCosts.RemoveAt(i);
                    else { c.Amount = left; site.RemainingCosts[i] = c; }
                    break;
                }

                if (site.RemainingCosts.Count == 0)
                    site.RemainingCosts = null;
            }
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
