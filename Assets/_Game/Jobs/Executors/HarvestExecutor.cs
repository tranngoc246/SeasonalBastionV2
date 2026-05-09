using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SeasonalBastion
{
    public sealed class HarvestExecutor : IJobExecutor
    {
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly INotificationService _notificationService;
        private readonly IClaimService _claimService;
        private readonly ResourcePatchService _resourcePatchService;
        private readonly IPathfinderRuntime _pathfinder;
        private readonly IDataRegistry _dataRegistry;
        private readonly IGridMap _gridMap;

        // jobId -> remaining work seconds
        private readonly Dictionary<int, float> _remaining = new();

        // jobId -> remaining settle seconds for deposit back to producer
        private readonly Dictionary<int, float> _depositSettle = new();
        private const float HarvestDepositSettleSec = 1.0f;

        public HarvestExecutor(
            IWorldState worldState,
            IStorageService storageService,
            IAgentMoverRuntime agentMover,
            INotificationService notificationService,
            IClaimService claimService,
            ResourcePatchService resourcePatchService,
            IPathfinderRuntime pathfinder,
            IDataRegistry dataRegistry,
            IGridMap gridMap)
        {
            _worldState = worldState;
            _storageService = storageService;
            _agentMover = agentMover;
            _notificationService = notificationService;
            _claimService = claimService;
            _resourcePatchService = resourcePatchService;
            _pathfinder = pathfinder;
            _dataRegistry = dataRegistry;
            _gridMap = gridMap;
        }

        public HarvestExecutor(GameServices s)
            : this(s?.WorldState, s?.StorageService, s?.AgentMover, s?.NotificationService, s?.ClaimService, s?.ResourcePatchService, s?.Pathfinder, s?.DataRegistry, s?.GridMap)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            // Hardening: external cancel -> cleanup local timer state
            if (job.Status == JobStatus.Cancelled)
            {
                _remaining.Remove(jid);
                _depositSettle.Remove(jid);
                return true;
            }

            if (_worldState == null || _storageService == null || _agentMover == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var producer = job.Workplace;
            if (producer.Value == 0 || !_worldState.Buildings.Exists(producer))
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var bs = _worldState.Buildings.Get(producer);
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
            int cap = _storageService.GetCap(producer, rt);
            int cur = _storageService.GetAmount(producer, rt);
            if (cap > 0 && cur >= cap)
            {
                if (_notificationService != null)
                {
                    _notificationService.Push(
                        key: "producer.local.full",
                        title: "Kho nội bộ đã đầy",
                        body: "Công trình này đang đầy hàng, nên tạm thời chưa thể sản xuất thêm.",
                        severity: NotificationSeverity.Info,
                        payload: new NotificationPayload(producer, default, null),
                        cooldownSeconds: 12f,
                        dedupeByKey: true);
                }

                InteractionCellExitHelper.TryStepOffBuildingEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, bs, dt);
                job.Status = JobStatus.Cancelled;
                return true;
            }

            // 2-phase Harvest:
            // Phase A (job.Amount == 0): go to zone cell + work => set carry into job.Amount
            // Phase B (job.Amount  > 0): bring carry back to producer anchor => Add to local storage

            // Phase B: delivering carried resource to producer local
            if (job.Amount > 0)
            {
                var entry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, bs, npcState.Cell);

                job.TargetCell = entry;
                job.Status = JobStatus.InProgress;

                bool arrivedEntry = _agentMover.StepToward(ref npcState, entry, dt);
                if (!arrivedEntry)
                {
                    int dx = npcState.Cell.X - entry.X; if (dx < 0) dx = -dx;
                    int dy = npcState.Cell.Y - entry.Y; if (dy < 0) dy = -dy;
                    bool adjacentToEntry = (dx + dy) == 1;
                    if (!adjacentToEntry)
                        return true;
                }

                // Stand still before deposit
                if (!_depositSettle.TryGetValue(jid, out var remDep))
                    remDep = HarvestDepositSettleSec;

                remDep -= dt;
                if (remDep > 0f)
                {
                    _depositSettle[jid] = remDep;
                    return true;
                }
                _depositSettle.Remove(jid);

                int carried = job.Amount;
                int added = _storageService.Add(producer, rt, carried);

                job.Amount = 0; // reset carry

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
                _depositSettle.Remove(jid);
                return true;
            }

            // OPTIONAL: if you want extra safety and you have grid bounds
            if (_gridMap != null && !_gridMap.IsInside(target))
            {
                job.Status = JobStatus.Cancelled;
                _remaining.Remove(jid);
                _depositSettle.Remove(jid);
                return true;
            }

            // Claim producer node (held only during moving+working; released after work done)
            ClaimKey claimKey = default;
            bool hasClaimKey = false;

            if (_claimService != null)
            {
                claimKey = new ClaimKey(ClaimKind.ProducerNode, producer.Value, (int)rt);
                hasClaimKey = true;

                if (!_claimService.TryAcquire(claimKey, npc))
                    return false; // waiting
            }

            job.SourceBuilding = producer;
            job.ResourceType = rt;
            job.Status = JobStatus.InProgress;

            bool arrived = _agentMover.StepToward(ref npcState, target, dt);

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
            _depositSettle.Remove(jid);

            // Work done => compute carry (CLAMP by free local cap to avoid overflow races)
            int cap2 = _storageService.GetCap(producer, rt);
            int cur2 = _storageService.GetAmount(producer, rt);
            int free = (cap2 > 0) ? (cap2 - cur2) : 0;

            if (free <= 0)
            {
                // local became full due to parallel deliveries; stop safely
                if (hasClaimKey) _claimService.Release(claimKey, npc);
                job.Status = JobStatus.Cancelled;
                return true;
            }

            int carry = yield;
            if (carry > free) carry = free;

            if (_resourcePatchService != null)
            {
                if (_resourcePatchService.TryGetPatchAtCell(target, out var patch))
                {
                    carry = _resourcePatchService.Consume(patch.Id, carry);
                }
                else
                {
                    carry = 0;
                }
            }

            if (carry <= 0)
            {
                if (hasClaimKey) _claimService.Release(claimKey, npc);

                if (HarvestTargetSelectionHelper.TryPickBestHarvestTarget(_resourcePatchService, _pathfinder, _worldState, rt, bs.Anchor, producer.Value, slot: 0, out var nextTarget))
                {
                    job.TargetCell = nextTarget;
                    job.Amount = 0;
                    job.Status = JobStatus.InProgress;
                    _remaining.Remove(jid);
                    _depositSettle.Remove(jid);
                    _agentMover.StepToward(ref npcState, nextTarget, 0f);
                    return true;
                }

                job.Status = JobStatus.Cancelled;
                return true;
            }

            // Release claim after producing carry so other workers can continue
            if (hasClaimKey) _claimService.Release(claimKey, npc);

            // Switch to Phase B: deliver to anchor
            job.Amount = carry;
            job.Status = JobStatus.InProgress;
            return true;
        }

        private static void GetHarvestParams(string defId, int level, out float workSec, out int yield)
        {
            // PART27 Day12 defaults (LOCKED tables)
            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse"))
            {
                workSec = 6f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp"))
            {
                workSec = 4f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (DefIdTierUtil.IsBase(defId, "bld_quarry"))
            {
                workSec = 5f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (DefIdTierUtil.IsBase(defId, "bld_ironhut"))
            {
                workSec = 6f;
                yield = level == 1 ? 4 : level == 2 ? 6 : 8;
                return;
            }

            workSec = 6f;
            yield = 6;
        }

        private static bool IsHarvestProducer(string defId)
        {
            return DefIdTierUtil.IsBase(defId, "bld_farmhouse")
                || DefIdTierUtil.IsBase(defId, "bld_lumbercamp")
                || DefIdTierUtil.IsBase(defId, "bld_quarry")
                || DefIdTierUtil.IsBase(defId, "bld_ironhut");
        }

        private static ResourceType HarvestResourceType(string defId)
        {
            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse")) return ResourceType.Food;
            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp")) return ResourceType.Wood;
            if (DefIdTierUtil.IsBase(defId, "bld_quarry")) return ResourceType.Stone;
            if (DefIdTierUtil.IsBase(defId, "bld_ironhut")) return ResourceType.Iron;
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
