using System;
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
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly IDataRegistry _dataRegistry;
        private readonly IGridMap _gridMap;
        private readonly IAmmoService _ammoService;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        // jobId -> remaining settle seconds at pickup/deliver
        private readonly Dictionary<int, float> _settle = new();
        private const float ResupplySettleSec = 1.0f;

        public ResupplyTowerExecutor(
            IWorldState worldState,
            IStorageService storageService,
            IAgentMoverRuntime agentMover,
            IDataRegistry dataRegistry,
            IGridMap gridMap,
            IAmmoService ammoService)
        {
            _worldState = worldState;
            _storageService = storageService;
            _agentMover = agentMover;
            _dataRegistry = dataRegistry;
            _gridMap = gridMap;
            _ammoService = ammoService;
        }

        public ResupplyTowerExecutor(GameServices s)
            : this(s?.WorldState, s?.StorageService, s?.AgentMover, s?.DataRegistry, s?.GridMap, s?.AmmoService)
        {
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            int jid = job.Id.Value;

            if (_worldState == null || _storageService == null || _agentMover == null)
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

            int carriedAmmo = 0;

            // Hardening: external cancel -> refund carry to armory (best-effort) + cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                RefundCarryBestEffort(jid, armoryBld);
                Cleanup(jid);
                return true;
            }

            if (!_worldState.Buildings.Exists(armoryBld))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            var armSt = _worldState.Buildings.Get(armoryBld);

            if (!armSt.IsConstructed)
            {
                job.Status = JobStatus.Cancelled;
                RefundCarryBestEffort(jid, armoryBld);
                Cleanup(jid);
                return true;
            }

            if (!_storageService.CanStore(armoryBld, ResourceType.Ammo))
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

                int avail = _storageService.GetAmount(armoryBld, ResourceType.Ammo);
                if (avail <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // Tower free should be checked before pickup so we don't carry useless ammo.
                if (!_worldState.Towers.Exists(towerId))
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                var ts = _worldState.Towers.Get(towerId);
                int towerFree = ts.AmmoCap - ts.Ammo;
                if (towerFree <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                if (want > towerFree) want = towerFree;
                if (want > avail) want = avail;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                var armEntry = EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, armSt, npcState.Cell);

                job.TargetCell = armEntry;
                job.Status = JobStatus.InProgress;

                bool arrived = _agentMover.StepToward(ref npcState, armEntry, dt);
                if (!arrived) return true;

                // Stand still before pickup
                if (!_settle.TryGetValue(jid, out var remPick))
                    remPick = ResupplySettleSec;

                remPick -= dt;
                if (remPick > 0f)
                {
                    _settle[jid] = remPick;
                    return true;
                }
                _settle.Remove(jid);

                int removed = _storageService.Remove(armoryBld, ResourceType.Ammo, want);

                if (removed <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                _carry[jid] = removed;
                _phase[jid] = 1;
                job.Amount = removed;
                return true;
            }

            // ---------------- Phase 1: deliver to Tower ----------------
            if (!_carry.TryGetValue(jid, out carriedAmmo) || carriedAmmo <= 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            var tsPeekOk = _worldState.Towers.Exists(towerId);
            if (!tsPeekOk)
            {
                _storageService.Add(armoryBld, ResourceType.Ammo, carriedAmmo);
                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            var tsForMove = _worldState.Towers.Get(towerId);
            var towerApproach = ResolveTowerApproachCell(tsForMove.Cell, npcState.Cell);

            job.TargetCell = towerApproach;

            bool arrivedTower = _agentMover.StepToward(ref npcState, towerApproach, dt);
            if (!arrivedTower) return true;

            // Stand still before deliver
            if (!_settle.TryGetValue(jid, out var remDel))
                remDel = ResupplySettleSec;

            remDel -= dt;
            if (remDel > 0f)
            {
                _settle[jid] = remDel;
                return true;
            }
            _settle.Remove(jid);

            var tsNow = _worldState.Towers.Get(towerId);

            int free = tsNow.AmmoCap - tsNow.Ammo;
            if (free < 0) free = 0;

            int add = carriedAmmo;
            if (add > free) add = free;

            if (add > 0)
            {
                tsNow.Ammo += add;
                _worldState.Towers.Set(towerId, tsNow);

                TryMirrorTowerAmmoToBuilding(tsNow.Cell, tsNow.Ammo);

                _ammoService?.NotifyTowerAmmoChanged(towerId, tsNow.Ammo, tsNow.AmmoCap);
            }

            int refund = carriedAmmo - add;
            if (refund > 0)
                _storageService.Add(armoryBld, ResourceType.Ammo, refund);

            job.Status = JobStatus.Completed;
            Cleanup(jid);
            return true;
        }

        private CellPos ResolveTowerApproachCell(CellPos towerCell, CellPos from)
        {
            if (TryFindTowerBuildingByCell(towerCell, out var tbid, out var tbs))
                return EntryCellUtil.GetApproachCellForBuilding(_dataRegistry, _gridMap, tbs, from);

            return towerCell;
        }

        private bool TryFindTowerBuildingByCell(CellPos towerCell, out BuildingId bid, out BuildingState bs)
        {
            bid = default;
            bs = default;

            var w = _worldState;
            var data = _dataRegistry;
            if (w == null || w.Buildings == null || data == null) return false;

            foreach (var id in w.Buildings.Ids)
            {
                if (!w.Buildings.Exists(id)) continue;

                var b = w.Buildings.Get(id);
                if (!b.IsConstructed) continue;

                BuildingDef bdef = null;
                try { bdef = data.GetBuilding(b.DefId); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[ResupplyTowerExecutor] Failed to resolve building def '{b.DefId}' while matching tower cell: {ex}"); }

                if (bdef == null || !bdef.IsTower) continue;

                int w0 = bdef.SizeX <= 0 ? 1 : bdef.SizeX;
                int h0 = bdef.SizeY <= 0 ? 1 : bdef.SizeY;

                bool contains = towerCell.X >= b.Anchor.X && towerCell.X < (b.Anchor.X + w0)
                             && towerCell.Y >= b.Anchor.Y && towerCell.Y < (b.Anchor.Y + h0);

                if (!contains) continue;

                bid = id;
                bs = b;
                return true;
            }

            return false;
        }

        private void TryMirrorTowerAmmoToBuilding(CellPos towerCell, int ammo)
        {
            var w = _worldState;
            var data = _dataRegistry;
            if (w == null || w.Buildings == null || data == null) return;

            foreach (var bid in w.Buildings.Ids)
            {
                if (!w.Buildings.Exists(bid)) continue;

                var b = w.Buildings.Get(bid);
                if (!b.IsConstructed) continue;

                if (!data.TryGetBuilding(b.DefId, out var bdef) || bdef == null || !bdef.IsTower)
                    continue;

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
            if (_worldState == null || _storageService == null) return;
            if (armoryBld.Value == 0) return;
            if (!_worldState.Buildings.Exists(armoryBld)) return;

            if (_carry.TryGetValue(jobId, out int carried) && carried > 0)
                _storageService.Add(armoryBld, ResourceType.Ammo, carried);
        }

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
            _settle.Remove(jobId);
        }
    }
}
