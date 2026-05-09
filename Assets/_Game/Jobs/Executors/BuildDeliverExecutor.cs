using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SeasonalBastion
{
    public sealed class BuildDeliverExecutor : IJobExecutor
    {
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IWorldIndex _worldIndex;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly BalanceService _balance;
        private readonly IClaimService _claimService;
        private readonly IEventBus _eventBus;
        private readonly IDataRegistry _dataRegistry;
        private readonly IGridMap _gridMap;
        private readonly IPathfinderRuntime _pathfinder;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        // Day20: "reserve" đơn giản bằng ClaimService (độc quyền theo source+resource)
        // Lưu key theo job để tick sau vẫn biết mình đã giữ claim.
        private readonly Dictionary<int, ClaimKey> _srcClaimByJob = new();

        // Reuse buffer cho refund (tránh alloc mỗi tick)
        private readonly List<BuildingId> _refundBuf = new(32);

        // jobId -> remaining settle seconds at interaction point (pickup/deliver)
        private readonly Dictionary<int, float> _settle = new();

        private const float DeliverSettleSec = 1.0f;

        public BuildDeliverExecutor(
            IWorldState worldState,
            IStorageService storageService,
            IWorldIndex worldIndex,
            IAgentMoverRuntime agentMover,
            BalanceService balance,
            IClaimService claimService,
            IEventBus eventBus,
            IDataRegistry dataRegistry,
            IGridMap gridMap,
            IPathfinderRuntime pathfinder)
        {
            _worldState = worldState;
            _storageService = storageService;
            _worldIndex = worldIndex;
            _agentMover = agentMover;
            _balance = balance;
            _claimService = claimService;
            _eventBus = eventBus;
            _dataRegistry = dataRegistry;
            _gridMap = gridMap;
            _pathfinder = pathfinder;
        }

        public BuildDeliverExecutor(GameServices s)
            : this(s?.WorldState, s?.StorageService, s?.WorldIndex, s?.AgentMover, s?.Balance, s?.ClaimService, s?.EventBus, s?.DataRegistry, s?.GridMap, s?.Pathfinder)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            // Hardening: if cancelled externally, rollback carry + cleanup without progressing movement/claims
            if (job.Status == JobStatus.Cancelled)
            {
                ReleaseSourceClaimIfOwned(npc, jid);
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                Cleanup(jid);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (_worldState == null || _storageService == null || _worldIndex == null || _agentMover == null)
            {
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (job.Site.Value == 0 || !_worldState.Sites.Exists(job.Site))
            {
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            var site = _worldState.Sites.Get(job.Site);
            if (!site.IsActive)
            {
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            var rt = job.ResourceType;

            int remaining = GetRemainingFor(site, rt);
            if (remaining <= 0)
            {
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, rt);
                InteractionCellExitHelper.TryStepOffSiteEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, site, dt);
                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }

            if (ph == 0)
            {
                int builderTier = 1;
                if (_balance != null && job.Workplace.Value != 0 && _worldState.Buildings.Exists(job.Workplace))
                {
                    var wp = _worldState.Buildings.Get(job.Workplace);
                    builderTier = _balance.GetTierFromLevel(wp.Level);
                }
                int cap = _balance != null ? _balance.GetCarryBuilder(builderTier) : 10;

                int want = job.Amount > 0 ? job.Amount : cap;
                if (want > cap) want = cap;

                if (want > remaining) want = remaining;
                if (want <= 0)
                {
                    InteractionCellExitHelper.TryStepOffSiteEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, site, dt);
                    job.Status = JobStatus.Completed;
                    Cleanup(jid);
                    return true;
                }

                if (job.SourceBuilding.Value == 0 || !_worldState.Buildings.Exists(job.SourceBuilding))
                {
                    if (!TryPickBestStorageSource(npcState.Cell, job.Workplace, rt, want, out var src))
                        return true;
                    job.SourceBuilding = src;
                }

                var srcId = job.SourceBuilding;
                if (!_worldState.Buildings.Exists(srcId))
                    return true;

                var srcState = _worldState.Buildings.Get(srcId);
                if (!srcState.IsConstructed)
                    return true;

                if (!EnsureSourceClaim(npc, jid, srcId, rt))
                    return true;

                var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, srcState, npcState.Cell);

                job.TargetCell = srcEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _agentMover.StepToward(ref npcState, srcEntry, dt);
                if (!arrivedSrc)
                    return true;

                if (!_settle.TryGetValue(jid, out var remS))
                    remS = DeliverSettleSec;

                remS -= dt;
                if (remS > 0f)
                {
                    _settle[jid] = remS;
                    return true;
                }
                _settle.Remove(jid);

                int removed = _storageService.Remove(srcId, rt, want);

                if (removed <= 0)
                {
                    ReleaseSourceClaimIfOwned(npc, jid);
                    job.SourceBuilding = default;
                    return true;
                }

                ReleaseSourceClaimIfOwned(npc, jid);

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

                var siteEntry = EntryCellUtil.GetApproachCellForSite(_dataRegistry, _gridMap, site, npcState.Cell);
                if (!IsReachable(npcState.Cell, siteEntry))
                {
                    RefundCarry(npcState.Cell, job.SourceBuilding, rt, carried);
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                job.TargetCell = siteEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSite = _agentMover.StepToward(ref npcState, siteEntry, dt);
                if (!arrivedSite)
                    return true;

                if (!_settle.TryGetValue(jid, out var remD))
                    remD = DeliverSettleSec;

                remD -= dt;
                if (remD > 0f)
                {
                    _settle[jid] = remD;
                    return true;
                }
                _settle.Remove(jid);

                if (!_worldState.Sites.Exists(job.Site))
                {
                    RefundCarry(npcState.Cell, job.SourceBuilding, rt, carried);
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                ClaimKey siteKey = default;
                if (_claimService != null)
                {
                    siteKey = new ClaimKey(ClaimKind.BuildSite, job.Site.Value, (int)rt);
                    if (!_claimService.TryAcquire(siteKey, npc))
                        return true;
                }

                site = _worldState.Sites.Get(job.Site);

                int remainingNow = GetRemainingFor(site, rt);
                int apply = carried;
                if (apply > remainingNow) apply = remainingNow;

                if (apply > 0)
                {
                    ApplyDelivered(ref site, rt, apply);
                    _worldState.Sites.Set(job.Site, site);

                    _eventBus?.Publish(new ResourceSpentEvent(rt, apply, job.SourceBuilding));
                }

                int refund = carried - apply;
                if (refund > 0)
                    RefundCarry(npcState.Cell, job.SourceBuilding, rt, refund);

                if (_claimService != null)
                    _claimService.Release(siteKey, npc);

                InteractionCellExitHelper.TryStepOffSiteEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, site, dt);
                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private bool EnsureSourceClaim(NpcId npc, int jobId, BuildingId src, ResourceType rt)
        {
            var claims = _claimService;
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

            if (!claims.TryAcquire(key, npc))
                return false;

            _srcClaimByJob[jobId] = key;
            return true;
        }

        private void ReleaseSourceClaimIfOwned(NpcId npc, int jobId)
        {
            var claims = _claimService;
            if (claims == null) return;

            if (_srcClaimByJob.TryGetValue(jobId, out var key))
            {
                if (claims.IsOwnedBy(key, npc)) claims.Release(key, npc);
                _srcClaimByJob.Remove(jobId);
            }
        }

        private void TryRefundCarry(int jobId, CellPos from, BuildingId preferredSource, ResourceType rt)
        {
            if (!_carry.TryGetValue(jobId, out var carried) || carried <= 0) return;
            RefundCarry(from, preferredSource, rt, carried);
            _carry.Remove(jobId);
            _phase.Remove(jobId);
            _srcClaimByJob.Remove(jobId);
        }

        private void RefundCarry(CellPos from, BuildingId preferredSource, ResourceType rt, int amount)
        {
            if (amount <= 0) return;
            if (_worldState == null || _storageService == null) return;

            int left = amount;

            if (preferredSource.Value != 0
                && _worldState.Buildings.Exists(preferredSource)
                && _storageService.CanStore(preferredSource, rt))
            {
                int added = _storageService.Add(preferredSource, rt, left);
                left -= added;
            }

            if (left <= 0) return;

            var whs = _worldIndex?.Warehouses;
            if (whs == null || whs.Count == 0) return;

            _refundBuf.Clear();
            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (!_worldState.Buildings.Exists(bid)) continue;
                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_storageService.CanStore(bid, rt)) continue;
                _refundBuf.Add(bid);
            }

            _refundBuf.Sort((a, b) =>
            {
                var aa = _worldState.Buildings.Get(a).Anchor;
                var bb = _worldState.Buildings.Get(b).Anchor;
                int da = Manhattan(from, aa);
                int db = Manhattan(from, bb);
                if (da != db) return da.CompareTo(db);
                return a.Value.CompareTo(b.Value);
            });

            for (int i = 0; i < _refundBuf.Count && left > 0; i++)
            {
                int added = _storageService.Add(_refundBuf[i], rt, left);
                left -= added;
            }
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
                site.RemainingCosts = null;
        }

        private bool TryPickBestStorageSource(CellPos from, BuildingId workplace, ResourceType rt, int minRequired, out BuildingId best)
        {
            best = default;

            if (workplace.Value != 0 && _worldState.Buildings.Exists(workplace))
            {
                var ws = _worldState.Buildings.Get(workplace);
                if (ws.IsConstructed && _storageService.GetAmount(workplace, rt) >= minRequired)
                {
                    best = workplace;
                    return true;
                }
            }

            var whs = _worldIndex.Warehouses;
            if (whs == null || whs.Count == 0) return false;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (!_worldState.Buildings.Exists(bid)) continue;

                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (_storageService.GetAmount(bid, rt) < minRequired) continue;

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
            _srcClaimByJob.Remove(jobId);
            _settle.Remove(jobId);
        }

        private bool IsReachable(CellPos from, CellPos to)
        {
            if (_pathfinder == null)
                return true;

            return _pathfinder.TryEstimateCost(from, to, out _);
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
