using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day23: Haul input resources (Wood/Iron/...) from nearest storage (HQ/Warehouse)
    /// into a Forge (DestBuilding), so CraftAmmo can use local inputs.
    ///
    /// Workplace: who does the hauling (Armory preferred, else HQ/Warehouse)
    /// DestBuilding: Forge
    /// ResourceType: Wood or Iron
    /// Amount: optional "at least" request; executor will clamp by carry & dest free
    /// </summary>
    public sealed class HaulToForgeExecutor : IJobExecutor
    {
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IResourceFlowService _resourceFlowService;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly IClaimService _claimService;
        private readonly BalanceService _balance;
        private readonly IDataRegistry _dataRegistry;
        private readonly IGridMap _gridMap;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        private readonly Dictionary<int, float> _settle = new();
        private const float HaulForgeSettleSec = 1.0f;

        public HaulToForgeExecutor(
            IWorldState worldState,
            IStorageService storageService,
            IResourceFlowService resourceFlowService,
            IAgentMoverRuntime agentMover,
            IClaimService claimService,
            BalanceService balance,
            IDataRegistry dataRegistry,
            IGridMap gridMap)
        {
            _worldState = worldState;
            _storageService = storageService;
            _resourceFlowService = resourceFlowService;
            _agentMover = agentMover;
            _claimService = claimService;
            _balance = balance;
            _dataRegistry = dataRegistry;
            _gridMap = gridMap;
        }

        public HaulToForgeExecutor(GameServices s)
            : this(s?.WorldState, s?.StorageService, s?.ResourceFlowService, s?.AgentMover, s?.ClaimService, s?.Balance, s?.DataRegistry, s?.GridMap)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_worldState == null || _storageService == null || _resourceFlowService == null || _agentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            var rt = job.ResourceType;

            // Dest must be Forge
            var dst = job.DestBuilding;
            if (dst.Value == 0 || !_worldState.Buildings.Exists(dst))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            var dstState = _worldState.Buildings.Get(dst);
            if (!dstState.IsConstructed)
                return false;

            // Must be storable at dest (Forge must have capWood/capIron)
            if (!_storageService.CanStore(dst, rt))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            int cap = _storageService.GetCap(dst, rt);
            int cur = _storageService.GetAmount(dst, rt);
            int free = cap - cur;
            if (free <= 0)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            int jid = job.Id.Value;

            // Hardening: external cancel -> refund carry to source (best-effort) + cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                RefundToSourceIfCarrying(jid, ref job, rt);
                Cleanup(jid, npc);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            // Hold dest claim during job to reduce collisions
            if (_claimService != null)
            {
                var destKey = new ClaimKey(ClaimKind.StorageDest, dst.Value, (int)rt);
                if (!_claimService.TryAcquire(destKey, npc))
                    return false;
            }

            if (ph == 0)
            {
                int whTier = _balance != null ? _balance.GetWarehouseTier() : 1;
                int carryCap = _balance != null ? _balance.GetCarryHaulBasic(whTier) : 10;

                int want = job.Amount > 0 ? job.Amount : carryCap;
                if (want > carryCap) want = carryCap;

                if (want > free) want = free;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid, npc);
                    return true;
                }

                // Pick/validate source storage
                if (job.SourceBuilding.Value == 0 || !_worldState.Buildings.Exists(job.SourceBuilding))
                {
                    if (!_resourceFlowService.TryPickSource(dstState.Anchor, rt, 1, out var pick))
                        return false;

                    job.SourceBuilding = pick.Building;
                }

                var src = job.SourceBuilding;
                if (src.Value == 0 || !_worldState.Buildings.Exists(src))
                    return false;

                var srcState = _worldState.Buildings.Get(src);
                if (!srcState.IsConstructed)
                    return false;

                if (!_storageService.CanStore(src, rt))
                {
                    job.SourceBuilding = default;
                    return false;
                }

                int avail = _storageService.GetAmount(src, rt);
                if (avail <= 0)
                {
                    job.SourceBuilding = default;
                    return false;
                }

                if (avail < want) want = avail;

                // Move to source ENTRY
                var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, srcState, npcState.Cell);

                job.TargetCell = srcEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _agentMover.StepToward(ref npcState, srcEntry, dt);
                if (!arrivedSrc)
                    return true;

                // Stand still before pickup
                if (!_settle.TryGetValue(jid, out var remP))
                    remP = HaulForgeSettleSec;

                remP -= dt;
                if (remP > 0f)
                {
                    _settle[jid] = remP;
                    return true;
                }
                _settle.Remove(jid);

                // Claim source during remove
                if (_claimService != null)
                {
                    var srcKey = new ClaimKey(ClaimKind.StorageSource, src.Value, (int)rt);
                    if (!_claimService.TryAcquire(srcKey, npc))
                        return false;

                    int removed = _storageService.Remove(src, rt, want);

                    _claimService.Release(srcKey, npc);

                    if (removed <= 0)
                        return false;

                    _carry[jid] = removed;
                }
                else
                {
                    int removed = _storageService.Remove(src, rt, want);
                    if (removed <= 0)
                        return false;
                    _carry[jid] = removed;
                }

                _phase[jid] = 1;
                return true;
            }
            else
            {
                if (!_carry.TryGetValue(jid, out int carrying) || carrying <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid, npc);
                    return true;
                }

                // Move to dest ENTRY (Forge)
                var dstEntry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, dstState, npcState.Cell);

                job.TargetCell = dstEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedDst = _agentMover.StepToward(ref npcState, dstEntry, dt);
                if (!arrivedDst)
                    return true;

                // Stand still before deposit
                if (!_settle.TryGetValue(jid, out var remD))
                    remD = HaulForgeSettleSec;

                remD -= dt;
                if (remD > 0f)
                {
                    _settle[jid] = remD;
                    return true;
                }
                _settle.Remove(jid);

                int added = _storageService.Add(dst, rt, carrying);
                // if added < carrying, remainder is dropped (should be rare since we checked free, but safe)

                job.Amount = added;
                job.Status = JobStatus.Completed;
                Cleanup(jid, npc);
                return true;
            }
        }

        private void RefundToSourceIfCarrying(int jobId, ref Job job, ResourceType rt)
        {
            if (_worldState == null || _storageService == null) return;
            if (!_carry.TryGetValue(jobId, out int carried) || carried <= 0) return;

            var src = job.SourceBuilding;
            if (src.Value != 0 && _worldState.Buildings.Exists(src))
                _storageService.Add(src, rt, carried);

            _carry.Remove(jobId);
            _phase.Remove(jobId);
        }

        private void Cleanup(int jobId, NpcId npc)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
            _settle.Remove(jobId);
            // Claims will also be released by JobScheduler.ReleaseAll on terminal,
            // so we don't need extra releases here.
        }
    }
}
