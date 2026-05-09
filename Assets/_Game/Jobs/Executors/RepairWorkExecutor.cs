using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// VS2 Day22: time-only minimal repair (no resource consumption).
    /// NPC goes to DestBuilding anchor, then repairs in chunks.
    /// </summary>
    public sealed class RepairWorkExecutor : IJobExecutor
    {
        private readonly IWorldState _worldState;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly IDataRegistry _dataRegistry;
        private readonly IStorageService _storageService;
        private readonly IWorldIndex _worldIndex;
        private readonly BalanceService _balance;
        private readonly IGridMap _gridMap;

        // jobId -> whether we've already paid repair cost (upfront)
        private readonly Dictionary<int, byte> _paid = new();
        private readonly List<BuildingId> _payBuf = new(32);

        // jobId -> accumulated fractional repair HP (continuous progress like BuildWork)
        private readonly Dictionary<int, float> _acc = new();

        // jobId -> remaining settle seconds at entry before starting repair
        private readonly Dictionary<int, float> _settle = new();
        private const float RepairSettleSec = 1.5f;

        public RepairWorkExecutor(
            IWorldState worldState,
            IAgentMoverRuntime agentMover,
            IDataRegistry dataRegistry,
            IStorageService storageService,
            IWorldIndex worldIndex,
            BalanceService balance,
            IGridMap gridMap)
        {
            _worldState = worldState;
            _agentMover = agentMover;
            _dataRegistry = dataRegistry;
            _storageService = storageService;
            _worldIndex = worldIndex;
            _balance = balance;
            _gridMap = gridMap;
        }

        public RepairWorkExecutor(GameServices s)
            : this(s?.WorldState, s?.AgentMover, s?.DataRegistry, s?.StorageService, s?.WorldIndex, s?.Balance, s?.GridMap)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            // Hardening: if already terminal, cleanup local state
            if (job.Status == JobStatus.Cancelled || job.Status == JobStatus.Failed || job.Status == JobStatus.Completed)
            {
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            if (_worldState == null || _agentMover == null)
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            var w = _worldState;

            if (job.DestBuilding.Value == 0 || !w.Buildings.Exists(job.DestBuilding))
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            var bs = w.Buildings.Get(job.DestBuilding);
            if (!bs.IsConstructed)
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            // Fix-up maxHP from def if missing
            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                if (_dataRegistry != null && _dataRegistry.TryGetBuilding(bs.DefId, out var repairDef) && repairDef != null)
                    mhp = Math.Max(1, repairDef.MaxHp);
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                w.Buildings.Set(job.DestBuilding, bs);
            }

            if (bs.HP >= bs.MaxHP)
            {
                InteractionCellExitHelper.TryStepOffBuildingEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, bs, dt);
                job.Status = JobStatus.Completed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            // Move to building ENTRY (driveway) instead of anchor
            var entry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, bs, npcState.Cell);

            if (npcState.Cell.X != entry.X || npcState.Cell.Y != entry.Y)
            {
                job.TargetCell = entry;
                job.Status = JobStatus.InProgress;

                bool arrived = _agentMover.StepToward(ref npcState, entry, dt);
                if (!arrived)
                    return true;
                // arrived this tick -> continue below
            }

            // Stand still a bit before starting repair
            if (!_settle.TryGetValue(jid, out var remSettle))
                remSettle = RepairSettleSec;

            remSettle -= dt;
            if (remSettle > 0f)
            {
                _settle[jid] = remSettle;
                job.Status = JobStatus.InProgress;
                return true;
            }
            _settle.Remove(jid);

            // Pay repair cost ONCE (upfront) when starting actual repair work
            if (!_paid.TryGetValue(jid, out var paid) || paid == 0)
            {
                if (_storageService == null || _worldIndex == null || _dataRegistry == null)
                {
                    job.Status = JobStatus.Failed;
                    _acc.Remove(jid);
                    _settle.Remove(jid);
                    _paid.Remove(jid);
                    return true;
                }

                var def = _dataRegistry.GetBuilding(bs.DefId);
                var costs = def.BuildCostsL1;

                if (costs != null && costs.Length > 0)
                {
                    float missingRatio = (bs.MaxHP - bs.HP) / (float)bs.MaxHP;
                    if (missingRatio < 0f) missingRatio = 0f;
                    if (missingRatio > 1f) missingRatio = 1f;

                    int builderTier = 1;
                    if (_balance != null && job.Workplace.Value != 0 && w.Buildings.Exists(job.Workplace))
                    {
                        var wp = w.Buildings.Get(job.Workplace);
                        builderTier = _balance.GetTierFromLevel(wp.Level);
                    }

                    float factor = (_balance != null ? _balance.RepairCostFactor : 0.30f);
                    float costMult = (_balance != null ? _balance.GetRepairCostMult(builderTier) : 1f);

                    // Pre-check totals to avoid partial deduct
                    int needWood = 0, needStone = 0, needIron = 0, needFood = 0;

                    for (int i = 0; i < costs.Length; i++)
                    {
                        var c = costs[i];
                        if (c == null || c.Amount <= 0) continue;

                        int need = (int)Math.Ceiling(c.Amount * missingRatio * factor * costMult);
                        if (need <= 0) continue;

                        switch (c.Resource)
                        {
                            case ResourceType.Wood: needWood += need; break;
                            case ResourceType.Stone: needStone += need; break;
                            case ResourceType.Iron: needIron += need; break;
                            case ResourceType.Food: needFood += need; break;
                        }
                    }

                    bool ok =
                        (needWood <= 0 || _storageService.GetTotal(ResourceType.Wood) >= needWood) &&
                        (needStone <= 0 || _storageService.GetTotal(ResourceType.Stone) >= needStone) &&
                        (needIron <= 0 || _storageService.GetTotal(ResourceType.Iron) >= needIron) &&
                        (needFood <= 0 || _storageService.GetTotal(ResourceType.Food) >= needFood);

                    if (!ok)
                    {
                        job.Status = JobStatus.Cancelled;
                        _acc.Remove(jid);
                        _settle.Remove(jid);
                        _paid.Remove(jid);
                        return true;
                    }

                    // Pay from nearest warehouses/HQ deterministically
                    PayNearest(bs.Anchor, ResourceType.Wood, needWood);
                    PayNearest(bs.Anchor, ResourceType.Stone, needStone);
                    PayNearest(bs.Anchor, ResourceType.Iron, needIron);
                    PayNearest(bs.Anchor, ResourceType.Food, needFood);
                }

                _paid[jid] = 1;
            }

            // Work continuously like BuildWork: progress every tick instead of waiting for a full chunk pulse.
            // Keep the same overall pacing by converting chunk settings into heal-per-second.
            if (!_acc.TryGetValue(jid, out var hpFrac)) hpFrac = 0f;

            float chunkSec = _balance != null ? _balance.RepairChunkSec : 4f;
            float healPct = _balance != null ? _balance.RepairHealPct : 0.15f;

            int builderTier2 = 1;
            if (_balance != null && job.Workplace.Value != 0 && w.Buildings.Exists(job.Workplace))
            {
                var wp = w.Buildings.Get(job.Workplace);
                builderTier2 = _balance.GetTierFromLevel(wp.Level);
            }
            float timeMult = _balance != null ? _balance.GetRepairTimeMult(builderTier2) : 1f;
            if (timeMult < 0.1f) timeMult = 0.1f;

            float effChunkSec = chunkSec * timeMult;
            if (effChunkSec < 0.01f) effChunkSec = 0.01f;

            float healPerChunk = Math.Max(1f, (float)Math.Ceiling(bs.MaxHP * healPct));
            float healPerSecond = healPerChunk / effChunkSec;

            hpFrac += healPerSecond * dt;

            int applyHeal = (int)Math.Floor(hpFrac);
            if (applyHeal > 0)
            {
                hpFrac -= applyHeal;
                bs.HP += applyHeal;
                if (bs.HP > bs.MaxHP) bs.HP = bs.MaxHP;
                w.Buildings.Set(job.DestBuilding, bs);
            }

            if (bs.HP >= bs.MaxHP)
            {
                InteractionCellExitHelper.TryStepOffBuildingEntry(_dataRegistry, _gridMap, _agentMover, ref npcState, bs, dt);
                job.Status = JobStatus.Completed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            _acc[jid] = hpFrac;
            job.Status = JobStatus.InProgress;
            return true;
        }

        private void PayNearest(CellPos refPos, ResourceType rt, int amount)
        {
            if (amount <= 0) return;

            _payBuf.Clear();

            // Use WorldIndex.Warehouses (includes HQ in your v0.1 index)
            var list = _worldIndex.Warehouses;
            for (int i = 0; i < list.Count; i++)
            {
                var bid = list[i];
                if (!_worldState.Buildings.Exists(bid)) continue;
                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_storageService.CanStore(bid, rt)) continue;
                _payBuf.Add(bid);
            }

            // sort by distance then id (deterministic)
            _payBuf.Sort((a, b) =>
            {
                var aa = _worldState.Buildings.Get(a).Anchor;
                var bb = _worldState.Buildings.Get(b).Anchor;
                int da = System.Math.Abs(refPos.X - aa.X) + System.Math.Abs(refPos.Y - aa.Y);
                int db = System.Math.Abs(refPos.X - bb.X) + System.Math.Abs(refPos.Y - bb.Y);
                if (da != db) return da.CompareTo(db);
                return a.Value.CompareTo(b.Value);
            });

            int left = amount;
            for (int i = 0; i < _payBuf.Count && left > 0; i++)
            {
                var dst = _payBuf[i];
                int removed = _storageService.Remove(dst, rt, left);
                left -= removed;
            }
        }
    }
}
