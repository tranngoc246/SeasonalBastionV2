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
        private readonly GameServices _s;

        // jobId -> whether we've already paid repair cost (upfront)
        private readonly Dictionary<int, byte> _paid = new();
        private readonly List<BuildingId> _payBuf = new(32);

        // jobId -> accumulated fractional repair HP (continuous progress like BuildWork)
        private readonly Dictionary<int, float> _acc = new();

        // jobId -> remaining settle seconds at entry before starting repair
        private readonly Dictionary<int, float> _settle = new();
        private const float RepairSettleSec = 1.5f;
        
        public RepairWorkExecutor(GameServices s) { _s = s; }

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

            if (_s.WorldState == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            var w = _s.WorldState;

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
                if (_s.DataRegistry.TryGetBuilding(bs.DefId, out var repairDef) && repairDef != null)
                    mhp = Math.Max(1, repairDef.MaxHp);
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                w.Buildings.Set(job.DestBuilding, bs);
            }

            if (bs.HP >= bs.MaxHP)
            {
                InteractionCellExitHelper.TryStepOffBuildingEntry(_s, ref npcState, bs, dt);
                job.Status = JobStatus.Completed;
                _acc.Remove(jid);
                _settle.Remove(jid);
                _paid.Remove(jid);
                return true;
            }

            // Move to building ENTRY (driveway) instead of anchor
            var entry = EntryCellUtil.GetApproachCellForBuilding(_s, bs, npcState.Cell);

            if (npcState.Cell.X != entry.X || npcState.Cell.Y != entry.Y)
            {
                job.TargetCell = entry;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, entry, dt);
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
                if (_s.StorageService == null || _s.WorldIndex == null)
                {
                    job.Status = JobStatus.Failed;
                    _acc.Remove(jid);
                    _settle.Remove(jid);
                    _paid.Remove(jid);
                    return true;
                }

                var def = _s.DataRegistry.GetBuilding(bs.DefId);
                var costs = def.BuildCostsL1;

                if (costs != null && costs.Length > 0)
                {
                    float missingRatio = (bs.MaxHP - bs.HP) / (float)bs.MaxHP;
                    if (missingRatio < 0f) missingRatio = 0f;
                    if (missingRatio > 1f) missingRatio = 1f;

                    int builderTier = 1;
                    if (_s.Balance != null && job.Workplace.Value != 0 && w.Buildings.Exists(job.Workplace))
                    {
                        var wp = w.Buildings.Get(job.Workplace);
                        builderTier = _s.Balance.GetTierFromLevel(wp.Level);
                    }

                    float factor = (_s.Balance != null ? _s.Balance.RepairCostFactor : 0.30f);
                    float costMult = (_s.Balance != null ? _s.Balance.GetRepairCostMult(builderTier) : 1f);

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
                        (needWood <= 0 || _s.StorageService.GetTotal(ResourceType.Wood) >= needWood) &&
                        (needStone <= 0 || _s.StorageService.GetTotal(ResourceType.Stone) >= needStone) &&
                        (needIron <= 0 || _s.StorageService.GetTotal(ResourceType.Iron) >= needIron) &&
                        (needFood <= 0 || _s.StorageService.GetTotal(ResourceType.Food) >= needFood);

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

            float chunkSec = _s.Balance != null ? _s.Balance.RepairChunkSec : 4f;
            float healPct = _s.Balance != null ? _s.Balance.RepairHealPct : 0.15f;

            int builderTier2 = 1;
            if (_s.Balance != null && job.Workplace.Value != 0 && w.Buildings.Exists(job.Workplace))
            {
                var wp = w.Buildings.Get(job.Workplace);
                builderTier2 = _s.Balance.GetTierFromLevel(wp.Level);
            }
            float timeMult = _s.Balance != null ? _s.Balance.GetRepairTimeMult(builderTier2) : 1f;
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
                InteractionCellExitHelper.TryStepOffBuildingEntry(_s, ref npcState, bs, dt);
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
            var list = _s.WorldIndex.Warehouses;
            for (int i = 0; i < list.Count; i++)
            {
                var bid = list[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;
                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_s.StorageService.CanStore(bid, rt)) continue;
                _payBuf.Add(bid);
            }

            // sort by distance then id (deterministic)
            _payBuf.Sort((a, b) =>
            {
                var aa = _s.WorldState.Buildings.Get(a).Anchor;
                var bb = _s.WorldState.Buildings.Get(b).Anchor;
                int da = System.Math.Abs(refPos.X - aa.X) + System.Math.Abs(refPos.Y - aa.Y);
                int db = System.Math.Abs(refPos.X - bb.X) + System.Math.Abs(refPos.Y - bb.Y);
                if (da != db) return da.CompareTo(db);
                return a.Value.CompareTo(b.Value);
            });

            int left = amount;
            for (int i = 0; i < _payBuf.Count && left > 0; i++)
            {
                var dst = _payBuf[i];
                int removed = _s.StorageService.Remove(dst, rt, left);
                left -= removed;
            }
        }
    }
}
