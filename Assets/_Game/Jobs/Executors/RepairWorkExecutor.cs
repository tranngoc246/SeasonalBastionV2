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

        // jobId -> accumulated work seconds
        private readonly Dictionary<int, float> _acc = new();

        // Tuning (Day22 minimal)
        private const float ChunkSec = 4f;      // 4 seconds per repair chunk
        private const float HealPctPerChunk = 0.15f; // heal ~15% maxHP per chunk

        public RepairWorkExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            // Hardening: if already terminal, cleanup local state
            if (job.Status == JobStatus.Cancelled || job.Status == JobStatus.Failed || job.Status == JobStatus.Completed)
            {
                _acc.Remove(jid);
                return true;
            }

            if (_s.WorldState == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                return true;
            }

            var w = _s.WorldState;

            if (job.DestBuilding.Value == 0 || !w.Buildings.Exists(job.DestBuilding))
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                return true;
            }

            var bs = w.Buildings.Get(job.DestBuilding);
            if (!bs.IsConstructed)
            {
                job.Status = JobStatus.Failed;
                _acc.Remove(jid);
                return true;
            }

            // Fix-up maxHP from def if missing
            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                try { mhp = Math.Max(1, _s.DataRegistry.GetBuilding(bs.DefId).MaxHp); } catch { }
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                w.Buildings.Set(job.DestBuilding, bs);
            }

            if (bs.HP >= bs.MaxHP)
            {
                job.Status = JobStatus.Completed;
                _acc.Remove(jid);
                return true;
            }

            // Move to target (GridAgentMoverLite: 1 cell / tick, deterministic X then Y)
            var target = bs.Anchor;
            if (npcState.Cell.X != target.X || npcState.Cell.Y != target.Y)
            {
                job.TargetCell = target;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, target);
                if (!arrived)
                    return true;

                // arrived this tick -> continue to work below
            }


            // Work at site
            if (!_acc.TryGetValue(jid, out var t)) t = 0f;
            t += dt;

            while (t >= ChunkSec)
            {
                t -= ChunkSec;

                int heal = Math.Max(1, (int)Math.Ceiling(bs.MaxHP * HealPctPerChunk));
                bs.HP += heal;
                if (bs.HP > bs.MaxHP) bs.HP = bs.MaxHP;

                w.Buildings.Set(job.DestBuilding, bs);

                if (bs.HP >= bs.MaxHP)
                {
                    job.Status = JobStatus.Completed;
                    _acc.Remove(jid);
                    return true;
                }
            }

            _acc[jid] = t;
            job.Status = JobStatus.InProgress;
            return true;
        }
    }
}
