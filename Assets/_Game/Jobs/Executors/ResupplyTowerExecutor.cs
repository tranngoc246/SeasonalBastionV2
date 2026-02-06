using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day26: Transporter resupply tower from Armory.
    /// Job:
    /// - Workplace: Armory (so Armory-role NPC claims)
    /// - SourceBuilding: Armory (pickup ammo)
    /// - Tower: target tower
    /// - ResourceType: Ammo
    /// - Amount: delivery chunk decided by provider; executor clamps by available / tower free.
    /// </summary>
    public sealed class ResupplyTowerExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        public ResupplyTowerExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (job.ResourceType != ResourceType.Ammo)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            // Resolve armory building (workplace preferred)
            BuildingId armoryBld = job.Workplace.Value != 0 ? job.Workplace : job.SourceBuilding;
            TowerId towerId = job.Tower;

            if (armoryBld.Value == 0 || towerId.Value == 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            int carriedAmmo = 0; // <-- IMPORTANT: chỉ khai báo 1 lần, tránh CS0136

            // Hardening: external cancel -> refund carry to armory (best-effort) + cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                RefundCarryBestEffort(jid, armoryBld);
                Cleanup(jid);
                return true;
            }

            if (!_s.WorldState.Buildings.Exists(armoryBld))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            var armSt = _s.WorldState.Buildings.Get(armoryBld);

            if (!armSt.IsConstructed)
            {
                job.Status = JobStatus.Cancelled;
                RefundCarryBestEffort(jid, armoryBld);
                Cleanup(jid);
                return true;
            }

            if (!_s.StorageService.CanStore(armoryBld, ResourceType.Ammo))
            {
                job.Status = JobStatus.Cancelled;
                RefundCarryBestEffort(jid, armoryBld);
                Cleanup(jid);
                return true;
            }

            if (!_phase.TryGetValue(jid, out byte ph)) ph = 0;

            // ---------------- Phase 0: pickup from Armory ----------------
            if (ph == 0)
            {
                int want = job.Amount > 0 ? job.Amount : 1;

                int avail = _s.StorageService.GetAmount(armoryBld, ResourceType.Ammo);
                if (avail <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                if (want > avail) want = avail;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                job.TargetCell = armSt.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, armSt.Anchor, dt);
                if (!arrived) return true;

                int removed = _s.StorageService.Remove(armoryBld, ResourceType.Ammo, want);
                if (removed <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                _carry[jid] = removed;
                _phase[jid] = 1;
                job.Amount = removed; // actual carry
                return true;
            }

            // ---------------- Phase 1: deliver to Tower ----------------
            if (!_carry.TryGetValue(jid, out carriedAmmo) || carriedAmmo <= 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            // Move tới tower trước khi deliver (đảm bảo giống pattern executor khác)
            // (Nếu project bạn đang có StepToward theo cell target)
            var tsPeekOk = _s.WorldState.Towers.Exists(towerId);
            if (!tsPeekOk)
            {
                // tower vanished -> refund to armory
                _s.StorageService.Add(armoryBld, ResourceType.Ammo, carriedAmmo);
                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            var tsForMove = _s.WorldState.Towers.Get(towerId);
            job.TargetCell = tsForMove.Cell;

            bool arrivedTower = _s.AgentMover.StepToward(ref npcState, tsForMove.Cell, dt);
            if (!arrivedTower) return true;

            // Re-fetch newest tower state to avoid stale overwrite if multiple deliveries happen.
            var tsNow = _s.WorldState.Towers.Get(towerId);

            int free = tsNow.AmmoCap - tsNow.Ammo;
            if (free < 0) free = 0;

            int add = carriedAmmo;
            if (add > free) add = free;

            if (add > 0)
            {
                tsNow.Ammo += add;
                _s.WorldState.Towers.Set(towerId, tsNow);

                TryMirrorTowerAmmoToBuilding(tsNow.Cell, tsNow.Ammo);

                // optional: update monitor/UI
                _s.AmmoService?.NotifyTowerAmmoChanged(towerId, tsNow.Ammo, tsNow.AmmoCap);
            }

            int refund = carriedAmmo - add;
            if (refund > 0)
                _s.StorageService.Add(armoryBld, ResourceType.Ammo, refund);

            job.Status = JobStatus.Completed;
            Cleanup(jid);
            return true;
        }

        private void TryMirrorTowerAmmoToBuilding(CellPos towerCell, int ammo)
        {
            var w = _s.WorldState;
            var data = _s.DataRegistry;
            if (w == null || w.Buildings == null || data == null) return;

            foreach (var bid in w.Buildings.Ids)
            {
                if (!w.Buildings.Exists(bid)) continue;

                var b = w.Buildings.Get(bid);
                if (!b.IsConstructed) continue;

                BuildingDef bdef = null;
                try { bdef = data.GetBuilding(b.DefId); } catch { }

                if (bdef == null || !bdef.IsTower) continue;

                int w0 = bdef.SizeX <= 0 ? 1 : bdef.SizeX;
                int h0 = bdef.SizeY <= 0 ? 1 : bdef.SizeY;

                bool contains = towerCell.X >= b.Anchor.X && towerCell.X < (b.Anchor.X + w0)
                             && towerCell.Y >= b.Anchor.Y && towerCell.Y < (b.Anchor.Y + h0);

                if (!contains) continue;

                b.Ammo = ammo;
                w.Buildings.Set(bid, b);
                return;
            }
        }

        private void RefundCarryBestEffort(int jobId, BuildingId armoryBld)
        {
            if (_s.WorldState == null || _s.StorageService == null) return;
            if (armoryBld.Value == 0) return;
            if (!_s.WorldState.Buildings.Exists(armoryBld)) return;

            if (_carry.TryGetValue(jobId, out int carried) && carried > 0)
                _s.StorageService.Add(armoryBld, ResourceType.Ammo, carried);
        }

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
        }
    }
}
