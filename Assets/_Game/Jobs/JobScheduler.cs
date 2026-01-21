using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobScheduler : IJobScheduler, ITickable
    {
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly IClaimService _claims;
        private readonly JobExecutorRegistry _exec;
        private readonly IEventBus _bus;

        public int AssignedThisTick { get; private set; }

        // Reuse buffers (avoid per-tick GC)
        private readonly List<NpcId> _npcIds = new(64);
        private readonly List<BuildingId> _buildingIds = new(128);
        private readonly HashSet<int> _workplacesWithNpc = new();

        // Prevent enqueue spam
        private readonly Dictionary<int, JobId> _harvestJobByWorkplace = new();
        private readonly Dictionary<int, JobId> _haulJobByWorkplaceAndType = new(); // key = workplace*16 + type

        public JobScheduler(IWorldState w, IJobBoard board, IClaimService claims, JobExecutorRegistry exec, IEventBus bus)
        { _w = w; _board = board; _claims = claims; _exec = exec; _bus = bus; }

        public void Tick(float dt)
        {
            AssignedThisTick = 0;

            BuildSortedNpcIds();
            BuildSortedBuildingIds();
            BuildWorkplaceHasNpcSet();

            // 0) Ensure jobs exist (Harvest + HaulBasic)
            EnqueueHarvestJobsIfNeeded();
            EnqueueHaulJobsIfNeeded();

            // 1) Assign jobs to idle NPCs (deterministic)
            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;

                var ns = _w.Npcs.Get(nid);

                if (!ns.IsIdle || ns.CurrentJob.Value != 0) { _w.Npcs.Set(nid, ns); continue; }
                if (ns.Workplace.Value == 0) { _w.Npcs.Set(nid, ns); continue; }

                if (TryAssignInternal(nid, ref ns))
                    AssignedThisTick++;

                _w.Npcs.Set(nid, ns);
            }

            // 2) Tick current jobs (deterministic)
            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;

                var ns = _w.Npcs.Get(nid);
                if (ns.CurrentJob.Value == 0) { _w.Npcs.Set(nid, ns); continue; }

                if (!_board.TryGet(ns.CurrentJob, out var job))
                {
                    CleanupNpcJob(nid, ref ns);
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                var executor = _exec.Get(job.Archetype);
                executor.Tick(nid, ref ns, ref job, dt);

                _board.Update(job);

                if (IsTerminal(job.Status))
                    CleanupNpcJob(nid, ref ns);

                _w.Npcs.Set(nid, ns);
            }
        }

        public bool TryAssign(NpcId npc, out Job assigned)
        {
            assigned = default;
            if (!_w.Npcs.Exists(npc)) return false;

            var ns = _w.Npcs.Get(npc);
            if (!ns.IsIdle || ns.CurrentJob.Value != 0) return false;
            if (ns.Workplace.Value == 0) return false;

            if (!TryAssignInternal(npc, ref ns)) return false;
            _w.Npcs.Set(npc, ns);

            return _board.TryGet(ns.CurrentJob, out assigned);
        }

        private bool TryAssignInternal(NpcId npc, ref NpcState ns)
        {
            if (!_board.TryPeekForWorkplace(ns.Workplace, out var peek))
                return false;

            // Preflight for HaulBasic: avoid claiming when nothing to haul (prevents stuck claimed jobs).
            if (peek.Archetype == JobArchetype.HaulBasic && !AnyHarvestProducerHasAmount(peek.ResourceType))
                return false;

            if (!_board.TryClaim(peek.Id, npc))
                return false;

            // Refresh job after claim (board updated internal copy)
            if (!_board.TryGet(peek.Id, out var job))
                return false;

            job.Status = JobStatus.InProgress;
            job.ClaimedBy = npc;
            _board.Update(job);

            ns.CurrentJob = job.Id;
            ns.IsIdle = false;
            return true;
        }

        private void CleanupNpcJob(NpcId npc, ref NpcState ns)
        {
            ns.CurrentJob = default;
            ns.IsIdle = true;
            _claims.ReleaseAll(npc); // safest against claim leak
        }

        // -------------------------
        // Enqueue logic (Day 12)
        // -------------------------

        private void EnqueueHarvestJobsIfNeeded()
        {
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                // Day12: harvest subset ONLY (do not harvest Forge)
                if (!IsHarvestProducer(bs.DefId)) continue;

                // only if there is at least 1 NPC assigned to this workplace
                if (!_workplacesWithNpc.Contains(bid.Value)) continue;

                // 1 pending job per workplace
                if (_harvestJobByWorkplace.TryGetValue(bid.Value, out var oldId))
                {
                    if (_board.TryGet(oldId, out var old) && !IsTerminal(old.Status))
                        continue;
                }

                // Only enqueue if local not full (LOCKED caps)
                var rt = HarvestResourceType(bs.DefId);
                int cap = HarvestLocalCap(bs.DefId, NormalizeLevel(bs.Level));
                int cur = GetAmountFromBuilding(bs, rt);
                if (cap > 0 && cur >= cap) continue;

                var j = new Job
                {
                    Archetype = JobArchetype.Harvest,
                    Status = JobStatus.Created,
                    Workplace = bid,
                    SourceBuilding = bid,
                    DestBuilding = default,
                    Site = default,
                    Tower = default,
                    ResourceType = rt,
                    Amount = 0,
                    TargetCell = bs.Anchor,
                    CreatedAt = 0
                };

                var newId = _board.Enqueue(j);
                _harvestJobByWorkplace[bid.Value] = newId;
            }
        }

        private void EnqueueHaulJobsIfNeeded()
        {
            // Enqueue hauling jobs for Warehouse/HQ workplaces with at least one NPC.
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var wid = _buildingIds[i];
                if (!_w.Buildings.Exists(wid)) continue;

                var bs = _w.Buildings.Get(wid);
                if (!bs.IsConstructed) continue;
                if (!IsWarehouseWorkplace(bs.DefId)) continue;
                if (!_workplacesWithNpc.Contains(wid.Value)) continue;

                // NEW: pass dest state so we can gate enqueue by free capacity (prevents full-dest flicker loops)
                TryEnsureHaulJob(wid, bs, ResourceType.Wood);
                TryEnsureHaulJob(wid, bs, ResourceType.Food);
                TryEnsureHaulJob(wid, bs, ResourceType.Stone);
                TryEnsureHaulJob(wid, bs, ResourceType.Iron);
            }
        }

        private void TryEnsureHaulJob(BuildingId workplace, in BuildingState destState, ResourceType rt)
        {
            // Only enqueue if there is something to haul now
            if (!AnyHarvestProducerHasAmount(rt)) return;

            // IMPORTANT (Day12 upgrade): Do NOT gate by workplace free capacity here,
            // because hauler can reroute to other Warehouse/HQ that still has space.
            // Executor will cancel if ALL destinations are full.

            int key = workplace.Value * 16 + (int)rt;

            if (_haulJobByWorkplaceAndType.TryGetValue(key, out var oldId))
            {
                if (_board.TryGet(oldId, out var old) && !IsTerminal(old.Status))
                    return;
            }

            var j = new Job
            {
                Archetype = JobArchetype.HaulBasic,
                Status = JobStatus.Created,
                Workplace = workplace,

                SourceBuilding = default,

                // Allow executor to pick a destination dynamically.
                // We'll still set default dest = workplace as "preferred".
                DestBuilding = workplace,

                Site = default,
                Tower = default,
                ResourceType = rt,
                Amount = 0,
                TargetCell = default,
                CreatedAt = 0
            };

            var newId = _board.Enqueue(j);
            _haulJobByWorkplaceAndType[key] = newId;
        }

        private bool AnyHarvestProducerHasAmount(ResourceType rt)
        {
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!IsHarvestProducer(bs.DefId)) continue;
                if (HarvestResourceType(bs.DefId) != rt) continue;

                if (GetAmountFromBuilding(bs, rt) > 0) return true;
            }
            return false;
        }

        // -------------------------
        // Determinism helpers
        // -------------------------

        private void BuildSortedNpcIds()
        {
            _npcIds.Clear();
            foreach (var id in _w.Npcs.Ids) _npcIds.Add(id);
            _npcIds.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        private void BuildSortedBuildingIds()
        {
            _buildingIds.Clear();
            foreach (var id in _w.Buildings.Ids) _buildingIds.Add(id);
            _buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        private void BuildWorkplaceHasNpcSet()
        {
            _workplacesWithNpc.Clear();
            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;
                var ns = _w.Npcs.Get(nid);
                if (ns.Workplace.Value != 0) _workplacesWithNpc.Add(ns.Workplace.Value);
            }
        }

        private static bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed
                || s == JobStatus.Failed
                || s == JobStatus.Cancelled;
        }

        // -------------------------
        // Day12 mapping (Buildings.json)
        // -------------------------

        private static bool IsWarehouseWorkplace(string defId)
        {
            return EqualsIgnoreCase(defId, "Warehouse")
                || EqualsIgnoreCase(defId, "HQ");
        }

        // Harvest subset ONLY (do not include Forge)
        private static bool IsHarvestProducer(string defId)
        {
            return EqualsIgnoreCase(defId, "Farm")
                || EqualsIgnoreCase(defId, "Lumber")
                || EqualsIgnoreCase(defId, "Quarry")
                || EqualsIgnoreCase(defId, "IronHut");
        }

        private static ResourceType HarvestResourceType(string defId)
        {
            if (EqualsIgnoreCase(defId, "Farm")) return ResourceType.Food;
            if (EqualsIgnoreCase(defId, "Lumber")) return ResourceType.Wood;
            if (EqualsIgnoreCase(defId, "Quarry")) return ResourceType.Stone;
            if (EqualsIgnoreCase(defId, "IronHut")) return ResourceType.Iron;
            return ResourceType.Food;
        }

        private static int HarvestLocalCap(string defId, int level)
        {
            // Local Storage Caps (LOCKED)
            if (EqualsIgnoreCase(defId, "Farm")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            if (EqualsIgnoreCase(defId, "Lumber")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (EqualsIgnoreCase(defId, "Quarry")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (EqualsIgnoreCase(defId, "IronHut")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            return 0;
        }

        // NEW: Dest caps for hauling (LOCKED)
        //private static int DestCap(string defId, int level, ResourceType rt)
        //{
        //    // Warehouse: 300/600/1000 each (Wood/Food/Stone/Iron), Ammo=0
        //    if (EqualsIgnoreCase(defId, "Warehouse"))
        //    {
        //        return rt switch
        //        {
        //            ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
        //                => level == 1 ? 300 : level == 2 ? 600 : 1000,
        //            _ => 0
        //        };
        //    }

        //    // HQ: 120/180/240 each (core only), Ammo=0
        //    if (EqualsIgnoreCase(defId, "HQ"))
        //    {
        //        return rt switch
        //        {
        //            ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
        //                => level == 1 ? 120 : level == 2 ? 180 : 240,
        //            _ => 0
        //        };
        //    }

        //    return 0;
        //}

        private static int GetAmountFromBuilding(in BuildingState bs, ResourceType rt)
        {
            return rt switch
            {
                ResourceType.Wood => bs.Wood,
                ResourceType.Food => bs.Food,
                ResourceType.Stone => bs.Stone,
                ResourceType.Iron => bs.Iron,
                ResourceType.Ammo => bs.Ammo,
                _ => 0
            };
        }

        private static int NormalizeLevel(int level) => level <= 0 ? 1 : (level > 3 ? 3 : level);

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
