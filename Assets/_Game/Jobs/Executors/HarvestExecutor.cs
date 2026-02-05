using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SeasonalBastion
{
    public sealed class HarvestExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // jobId -> remaining work seconds
        private readonly Dictionary<int, float> _remaining = new();

        public HarvestExecutor(GameServices s) { _s = s; }


        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            // Hardening: external cancel -> cleanup local timer state
            if (job.Status == JobStatus.Cancelled)
            {
                _remaining.Remove(jid);
                return true;
            }

            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var producer = job.Workplace;
            if (producer.Value == 0 || !_s.WorldState.Buildings.Exists(producer))
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var bs = _s.WorldState.Buildings.Get(producer);
            if (!bs.IsConstructed)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            // Day12 harvest subset only
            if (!IsHarvestProducer(bs.DefId))
            {
                job.Status = JobStatus.Cancelled;
                return true;
            }

            var rt = HarvestResourceType(bs.DefId);
            GetHarvestParams(bs.DefId, NormalizeLevel(bs.Level), out float workSec, out int yield);

            // Day36: nếu local storage full thì cancel job để tránh loop fail/spam
            int cap = _s.StorageService.GetCap(producer, rt);
            int cur = _s.StorageService.GetAmount(producer, rt);
            if (cap > 0 && cur >= cap)
            {
                if (_s.NotificationService != null)
                {
                    _s.NotificationService.Push(
                        key: "producer.local.full",
                        title: "Producer full",
                        body: $"{bs.DefId}: {rt} full ({cur}/{cap})",
                        severity: NotificationSeverity.Info,
                        payload: new NotificationPayload(producer, default, null),
                        cooldownSeconds: 8f,
                        dedupeByKey: true);
                }

                job.Status = JobStatus.Cancelled;
                return true;
            }

            // 2-phase Harvest:
            // Phase A (job.Amount == 0): go to zone cell + work => set carry into job.Amount
            // Phase B (job.Amount  > 0): bring carry back to producer anchor => Add to local storage

            // Phase B: delivering carried resource to producer local
            if (job.Amount > 0)
            {
                var anchor = bs.Anchor;

                job.TargetCell = anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedAnchor = _s.AgentMover.StepToward(ref npcState, anchor, dt);
                if (!arrivedAnchor)
                    return true;

                int carried = job.Amount;
                int added = _s.StorageService.Add(producer, rt, carried);

                // Do not drop piles in this design: HQ/Warehouse haulers pull from local storage.
                // If Add fails due to full (race), just clamp: treat as delivered what could be stored.
                // Any leftover is discarded safely by cancelling; better than spawning piles that no one hauls.
                job.Amount = 0;

                job.Status = JobStatus.Completed;
                return true;
            }

            // Phase A: Move to zone cell (TargetCell is assigned by JobScheduler)
            var target = job.TargetCell;

            // HARDENING: never move toward default (0,0). Cancel so scheduler can recreate with valid target.
            if (target.X == 0 && target.Y == 0)
            {
                job.Status = JobStatus.Cancelled;
                _remaining.Remove(jid);
                return true;
            }

            // OPTIONAL: if you want extra safety and you have grid bounds
            if (_s.GridMap != null && !_s.GridMap.IsInside(target))
            {
                job.Status = JobStatus.Cancelled;
                _remaining.Remove(jid);
                return true;
            }

            // Claim producer node (held only during moving+working; released after work done)
            ClaimKey claimKey = default;
            bool hasClaimKey = false;

            if (_s.ClaimService != null)
            {
                claimKey = new ClaimKey(ClaimKind.ProducerNode, producer.Value, (int)rt);
                hasClaimKey = true;

                if (!_s.ClaimService.TryAcquire(claimKey, npc))
                    return false; // waiting
            }

            job.SourceBuilding = producer;
            job.ResourceType = rt;
            job.Status = JobStatus.InProgress;

            bool arrived = _s.AgentMover.StepToward(ref npcState, target, dt);

            if (!arrived)
                return true;

            // Work timer only after arrived
            if (!_remaining.TryGetValue(jid, out var rem))
                rem = workSec;

            rem -= dt;
            if (rem > 0f)
            {
                _remaining[jid] = rem;
                return true;
            }

            _remaining.Remove(jid);

            // Work done => compute carry (CLAMP by free local cap to avoid overflow races)
            int cap2 = _s.StorageService.GetCap(producer, rt);
            int cur2 = _s.StorageService.GetAmount(producer, rt);
            int free = (cap2 > 0) ? (cap2 - cur2) : 0;

            if (free <= 0)
            {
                // local became full due to parallel deliveries; stop safely
                if (hasClaimKey) _s.ClaimService.Release(claimKey, npc);
                job.Status = JobStatus.Cancelled;
                return true;
            }

            int carry = yield;
            if (carry > free) carry = free;

            // Release claim after producing carry so other workers can continue
            if (hasClaimKey) _s.ClaimService.Release(claimKey, npc);

            // Switch to Phase B: deliver to anchor
            job.Amount = carry;
            job.Status = JobStatus.InProgress;
            return true;
        }

        private static void GetHarvestParams(string defId, int level, out float workSec, out int yield)
        {
            // PART27 Day12 defaults (LOCKED tables)
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1"))
            {
                workSec = 6f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1"))
            {
                workSec = 4f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (EqualsIgnoreCase(defId, "bld_quarry_t1"))
            {
                workSec = 5f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (EqualsIgnoreCase(defId, "bld_ironhut_t1"))
            {
                workSec = 5.5f;
                yield = level == 1 ? 5 : level == 2 ? 7 : 9;
                return;
            }

            workSec = 6f;
            yield = 6;
        }

        private static bool IsHarvestProducer(string defId)
        {
            return EqualsIgnoreCase(defId, "bld_farmhouse_t1")
                || EqualsIgnoreCase(defId, "bld_lumbercamp_t1")
                || EqualsIgnoreCase(defId, "bld_quarry_t1")
                || EqualsIgnoreCase(defId, "bld_ironhut_t1");
        }

        private static ResourceType HarvestResourceType(string defId)
        {
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1")) return ResourceType.Food;
            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1")) return ResourceType.Wood;
            if (EqualsIgnoreCase(defId, "bld_quarry_t1")) return ResourceType.Stone;
            if (EqualsIgnoreCase(defId, "bld_ironhut_t1")) return ResourceType.Iron;
            return ResourceType.Food;
        }

        private static int NormalizeLevel(int level) => level <= 0 ? 1 : (level > 3 ? 3 : level);

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
