using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SeasonalBastion
{
    public sealed class BuildDeliverExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        private const int CarryCap = 10;

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

        public BuildDeliverExecutor(GameServices s) { _s = s; }

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

            if (_s.WorldState == null || _s.StorageService == null || _s.WorldIndex == null || _s.AgentMover == null)
            {
                // Best-effort rollback nếu đã pickup.
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (job.Site.Value == 0 || !_s.WorldState.Sites.Exists(job.Site))
            {
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            var site = _s.WorldState.Sites.Get(job.Site);
            if (!site.IsActive)
            {
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, job.ResourceType);
                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            var rt = job.ResourceType;

            // If already no remaining for this resource => complete (avoid stuck)
            int remaining = GetRemainingFor(site, rt);
            if (remaining <= 0)
            {
                // Nếu đã pickup nhưng site đã đủ bởi NPC khác -> hoàn trả về kho.
                TryRefundCarry(jid, npcState.Cell, job.SourceBuilding, rt);
                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }

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

                // Day20: reserve source (exclusive) để tránh 2 NPC cùng hút 1 resource cùng lúc.
                if (!EnsureSourceClaim(npc, jid, srcId, rt))
                    return true;

                // Move to source ENTRY (driveway)
                var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_s, srcState, npcState.Cell);

                job.TargetCell = srcEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _s.AgentMover.StepToward(ref npcState, srcEntry, dt);
                if (!arrivedSrc)
                    return true;

                // Stand still before pickup
                if (!_settle.TryGetValue(jid, out var remS))
                    remS = DeliverSettleSec;

                remS -= dt;
                if (remS > 0f)
                {
                    _settle[jid] = remS;
                    return true;
                }
                _settle.Remove(jid);

                // pickup from storage
                int removed = _s.StorageService.Remove(srcId, rt, want);

                if (removed <= 0)
                {
                    // source ran dry => repick next tick
                    ReleaseSourceClaimIfOwned(npc, jid);
                    job.SourceBuilding = default;
                    return true;
                }

                // consume xong thì nhả claim
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

                // Move to site ENTRY (delivery point)
                var siteEntry = EntryCellUtil.GetApproachCellForSite(_s, site, npcState.Cell);

                job.TargetCell = siteEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSite = _s.AgentMover.StepToward(ref npcState, siteEntry, dt);
                if (!arrivedSite)
                    return true;

                // Stand still before applying delivery
                if (!_settle.TryGetValue(jid, out var remD))
                    remD = DeliverSettleSec;

                remD -= dt;
                if (remD > 0f)
                {
                    _settle[jid] = remD;
                    return true;
                }
                _settle.Remove(jid);

                // Apply delivery to site (re-read to avoid race with other NPCs)
                if (!_s.WorldState.Sites.Exists(job.Site))
                {
                    // site vanished; refund best-effort
                    RefundCarry(npcState.Cell, job.SourceBuilding, rt, carried);
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // Day20: serialize apply per site+resource để tránh over-deliver do race.
                ClaimKey siteKey = default;
                if (_s.ClaimService != null)
                {
                    siteKey = new ClaimKey(ClaimKind.BuildSite, job.Site.Value, (int)rt);
                    if (!_s.ClaimService.TryAcquire(siteKey, npc))
                        return true; // wait
                }

                site = _s.WorldState.Sites.Get(job.Site);

                int remainingNow = GetRemainingFor(site, rt);
                int apply = carried;
                if (apply > remainingNow) apply = remainingNow;

                if (apply > 0)
                {
                    ApplyDelivered(ref site, rt, apply);
                    _s.WorldState.Sites.Set(job.Site, site);

                    // Day40 metrics: build spent = material actually applied to site (not refunded)
                    _s.EventBus?.Publish(new ResourceSpentEvent(rt, apply, job.SourceBuilding));
                }

                int refund = carried - apply;
                if (refund > 0)
                    RefundCarry(npcState.Cell, job.SourceBuilding, rt, refund);

                if (_s.ClaimService != null)
                    _s.ClaimService.Release(siteKey, npc);

                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private bool EnsureSourceClaim(NpcId npc, int jobId, BuildingId src, ResourceType rt)
        {
            var claims = _s.ClaimService;
            if (claims == null) return true;

            var key = new ClaimKey(ClaimKind.StorageSource, src.Value, (int)rt);

            // nếu job đổi source/rt giữa chừng => nhả claim cũ
            if (_srcClaimByJob.TryGetValue(jobId, out var old)
                && (old.Kind != key.Kind || old.A != key.A || old.B != key.B))
            {
                if (claims.IsOwnedBy(old, npc)) claims.Release(old, npc);
                _srcClaimByJob.Remove(jobId);
            }

            if (_srcClaimByJob.TryGetValue(jobId, out var owned))
            {
                // scheduler có thể ReleaseAll khi terminal; nếu mất claim thì acquire lại
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
            var claims = _s.ClaimService;
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
            // claim (nếu có) sẽ được scheduler ReleaseAll khi terminal, nhưng nhả sớm cũng tốt.
            _srcClaimByJob.Remove(jobId);
        }

        private void RefundCarry(CellPos from, BuildingId preferredSource, ResourceType rt, int amount)
        {
            if (amount <= 0) return;
            if (_s.WorldState == null || _s.StorageService == null) return;

            int left = amount;

            // 1) ưu tiên hoàn về source (nếu còn tồn tại và cho store)
            if (preferredSource.Value != 0
                && _s.WorldState.Buildings.Exists(preferredSource)
                && _s.StorageService.CanStore(preferredSource, rt))
            {
                int added = _s.StorageService.Add(preferredSource, rt, left);
                left -= added;
            }

            if (left <= 0) return;

            // 2) fallback: đẩy vào kho gần nhất (HQ/Warehouse) theo dist + id (deterministic)
            var whs = _s.WorldIndex?.Warehouses;
            if (whs == null || whs.Count == 0) return;

            _refundBuf.Clear();
            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;
                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_s.StorageService.CanStore(bid, rt)) continue;
                _refundBuf.Add(bid);
            }

            _refundBuf.Sort((a, b) =>
            {
                var aa = _s.WorldState.Buildings.Get(a).Anchor;
                var bb = _s.WorldState.Buildings.Get(b).Anchor;
                int da = Manhattan(from, aa);
                int db = Manhattan(from, bb);
                if (da != db) return da.CompareTo(db);
                return a.Value.CompareTo(b.Value);
            });

            for (int i = 0; i < _refundBuf.Count && left > 0; i++)
            {
                int added = _s.StorageService.Add(_refundBuf[i], rt, left);
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
            _srcClaimByJob.Remove(jobId);
            _settle.Remove(jobId);
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
