using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day24: Move Ammo from Forge -> Armory in chunks.
    /// Job:
    /// - Workplace: Armory (so Armory-role NPC claims)
    /// - SourceBuilding: Forge
    /// - DestBuilding: Armory (usually equals Workplace)
    /// - ResourceType: Ammo
    /// - Amount: requested chunk (provider computes); executor clamps by available/free.
    /// </summary>
    public sealed class HaulAmmoToArmoryExecutor : IJobExecutor
    {
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly BalanceService _balance;
        private readonly IDataRegistry _dataRegistry;
        private readonly IGridMap _gridMap;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        private readonly Dictionary<int, float> _settle = new();
        private const float AmmoHaulSettleSec = 1.0f;

        public HaulAmmoToArmoryExecutor(
            IWorldState worldState,
            IStorageService storageService,
            IAgentMoverRuntime agentMover,
            BalanceService balance,
            IDataRegistry dataRegistry,
            IGridMap gridMap)
        {
            _worldState = worldState;
            _storageService = storageService;
            _agentMover = agentMover;
            _balance = balance;
            _dataRegistry = dataRegistry;
            _gridMap = gridMap;
        }

        public HaulAmmoToArmoryExecutor(GameServices s)
            : this(s?.WorldState, s?.StorageService, s?.AgentMover, s?.Balance, s?.DataRegistry, s?.GridMap)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_worldState == null || _storageService == null || _agentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            if (job.ResourceType != ResourceType.Ammo)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var src = job.SourceBuilding;
            var dst = job.DestBuilding.Value != 0 ? job.DestBuilding : job.Workplace;

            if (src.Value == 0 || dst.Value == 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            if (!_worldState.Buildings.Exists(src) || !_worldState.Buildings.Exists(dst))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var srcState = _worldState.Buildings.Get(src);
            var dstState = _worldState.Buildings.Get(dst);

            if (!srcState.IsConstructed || !dstState.IsConstructed)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            // Hard gate: must be able to store ammo at both ends (StorageService enforces ammo only Forge/Armory)
            if (!_storageService.CanStore(src, ResourceType.Ammo) || !_storageService.CanStore(dst, ResourceType.Ammo))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            int dstFree = _storageService.GetCap(dst, ResourceType.Ammo) - _storageService.GetAmount(dst, ResourceType.Ammo);
            if (dstFree <= 0)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            int jid = job.Id.Value;

            // Hardening: external cancel -> refund ammo back to source (best-effort) + cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                if (_worldState != null && _storageService != null)
                {
                    if (_carry.TryGetValue(jid, out int carried) && carried > 0 && job.SourceBuilding.Value != 0
                        && _worldState.Buildings.Exists(job.SourceBuilding))
                    {
                        _storageService.Add(job.SourceBuilding, ResourceType.Ammo, carried);
                    }
                }

                Cleanup(jid);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                int want = job.Amount > 0 ? job.Amount : 1;

                int tier = _balance != null ? _balance.GetTierFromLevel(dstState.Level) : 1;
                int cap = _balance != null ? _balance.GetArmoryAmmoCarry(tier) : 80;
                if (want > cap) want = cap;

                if (want > dstFree) want = dstFree;

                int srcAvail = _storageService.GetAmount(src, ResourceType.Ammo);
                if (srcAvail <= 0) return false;
                if (want > srcAvail) want = srcAvail;
                if (want <= 0) return false;

                // Move to source ENTRY
                var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, srcState, npcState.Cell);

                job.TargetCell = srcEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _agentMover.StepToward(ref npcState, srcEntry, dt);
                if (!arrivedSrc) return true;

                // Stand still before pickup
                if (!_settle.TryGetValue(jid, out var remP))
                    remP = AmmoHaulSettleSec;

                remP -= dt;
                if (remP > 0f)
                {
                    _settle[jid] = remP;
                    return true;
                }
                _settle.Remove(jid);

                int removed = _storageService.Remove(src, ResourceType.Ammo, want);

                if (removed <= 0) return false;

                _carry[jid] = removed;
                _phase[jid] = 1;
                job.Amount = removed;
                return true;
            }
            else
            {
                if (!_carry.TryGetValue(jid, out int carried) || carried <= 0)
                {
                    job.Status = JobStatus.Failed;
                    Cleanup(jid);
                    return true;
                }

                // Move to dest ENTRY
                var dstEntry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, dstState, npcState.Cell);

                job.TargetCell = dstEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedDst = _agentMover.StepToward(ref npcState, dstEntry, dt);
                if (!arrivedDst) return true;

                // Stand still before deposit
                if (!_settle.TryGetValue(jid, out var remD))
                    remD = AmmoHaulSettleSec;

                remD -= dt;
                if (remD > 0f)
                {
                    _settle[jid] = remD;
                    return true;
                }
                _settle.Remove(jid);

                int added = _storageService.Add(dst, ResourceType.Ammo, carried);

                // Refund remainder back to source (best-effort)
                int refund = carried - added;
                if (refund > 0 && _worldState.Buildings.Exists(src))
                    _storageService.Add(src, ResourceType.Ammo, refund);

                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
            _settle.Remove(jobId);
        }
    }
}
