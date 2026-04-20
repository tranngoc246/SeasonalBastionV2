using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion
{
    internal sealed class AmmoRequestQueue
    {
        private readonly GameServices _s;
        private readonly List<AmmoRequest> _urgent = new();
        private readonly List<AmmoRequest> _normal = new();
        private readonly HashSet<int> _pendingReqTower = new();
        private readonly Dictionary<int, AmmoRequestPriority> _pendingPriorityByTower = new();

        internal AmmoRequestQueue(GameServices s)
        {
            _s = s;
        }

        internal List<AmmoRequest> UrgentRequests => _urgent;
        internal List<AmmoRequest> NormalRequests => _normal;
        internal HashSet<int> PendingReqTower => _pendingReqTower;
        internal Dictionary<int, AmmoRequestPriority> PendingPriorityByTower => _pendingPriorityByTower;
        internal int PendingRequests => _urgent.Count + _normal.Count;

        internal void Enqueue(AmmoRequest req)
        {
            int tid = req.Tower.Value;
            if (tid == 0) return;

            if (_pendingReqTower.Contains(tid))
            {
                if (_pendingPriorityByTower.TryGetValue(tid, out var oldPri)
                    && oldPri == AmmoRequestPriority.Normal
                    && req.Priority == AmmoRequestPriority.Urgent)
                {
                    for (int i = 0; i < _normal.Count; i++)
                    {
                        if (_normal[i].Tower.Value == tid)
                        {
                            _normal.RemoveAt(i);
                            break;
                        }
                    }

                    _urgent.Add(req);
                    _pendingPriorityByTower[tid] = AmmoRequestPriority.Urgent;
                }

                return;
            }

            _pendingReqTower.Add(tid);
            _pendingPriorityByTower[tid] = req.Priority;

            if (req.Priority == AmmoRequestPriority.Urgent) _urgent.Add(req);
            else _normal.Add(req);
        }

        internal bool TryDequeueNext(out AmmoRequest req)
        {
            if (_urgent.Count > 0)
            {
                req = _urgent[0];
                _urgent.RemoveAt(0);
                RemovePending(req.Tower.Value);
                return true;
            }

            if (_normal.Count > 0)
            {
                req = _normal[0];
                _normal.RemoveAt(0);
                RemovePending(req.Tower.Value);
                return true;
            }

            req = default;
            return false;
        }

        internal bool TryPickBestRequest(IReadOnlyDictionary<int, JobId> resupplyJobByTower, out List<AmmoRequest> list, out int index, out AmmoRequest req, out TowerState towerState)
        {
            if (TryFindBestRequestIndex(_urgent, resupplyJobByTower, out index, out req, out towerState))
            {
                list = _urgent;
                return true;
            }

            if (TryFindBestRequestIndex(_normal, resupplyJobByTower, out index, out req, out towerState))
            {
                list = _normal;
                return true;
            }

            list = null;
            index = -1;
            req = default;
            towerState = default;
            return false;
        }

        internal void ConsumeRequestAt(List<AmmoRequest> list, int index)
        {
            if (list == null || index < 0 || index >= list.Count) return;
            int tid = list[index].Tower.Value;
            list.RemoveAt(index);
            RemovePending(tid);
        }

        internal void RotateRequestToBack(List<AmmoRequest> list, int index, AmmoRequest req)
        {
            if (list == null || index < 0 || index >= list.Count) return;
            list.RemoveAt(index);
            list.Add(req);
        }

        internal void PruneInvalidRequests(HashSet<int> towerNoJobLogged, HashSet<int> towerDeadlockLogged)
        {
            PruneInvalidRequests(_urgent, towerNoJobLogged, towerDeadlockLogged);
            PruneInvalidRequests(_normal, towerNoJobLogged, towerDeadlockLogged);
        }

        internal int CountEligibleRequests()
        {
            return CountEligibleRequests(_urgent) + CountEligibleRequests(_normal);
        }

        internal void Clear()
        {
            _urgent.Clear();
            _normal.Clear();
            _pendingReqTower.Clear();
            _pendingPriorityByTower.Clear();
        }

        internal void RemovePendingForTower(int towerId)
        {
            RemovePending(towerId);
        }

        private bool TryFindBestRequestIndex(List<AmmoRequest> src, IReadOnlyDictionary<int, JobId> resupplyJobByTower, out int bestIndex, out AmmoRequest bestReq, out TowerState bestTowerState)
        {
            bestIndex = -1;
            bestReq = default;
            bestTowerState = default;

            int bestStateRank = int.MaxValue;
            int bestAmmo = int.MaxValue;
            int bestTid = int.MaxValue;

            for (int i = 0; i < src.Count; i++)
            {
                var r = src[i];
                int tid = r.Tower.Value;
                if (tid == 0) continue;
                if (_s.WorldState == null || !_s.WorldState.Towers.Exists(r.Tower)) continue;

                if (resupplyJobByTower.TryGetValue(tid, out var inFlightJob))
                {
                    if (_s.JobBoard != null && _s.JobBoard.TryGet(inFlightJob, out var jj) && !AmmoService.IsTerminal(jj.Status))
                        continue;
                }

                var ts = _s.WorldState.Towers.Get(r.Tower);
                int need = ts.AmmoCap - ts.Ammo;
                if (ts.AmmoCap <= 0 || need <= 0) continue;

                int stateRank = ts.Ammo <= 0 ? 0 : 1;
                int ammo = ts.Ammo;
                if (stateRank < bestStateRank || (stateRank == bestStateRank && (ammo < bestAmmo || (ammo == bestAmmo && tid < bestTid))))
                {
                    bestStateRank = stateRank;
                    bestAmmo = ammo;
                    bestTid = tid;
                    bestIndex = i;
                    bestReq = r;
                    bestTowerState = ts;
                }
            }

            return bestIndex >= 0;
        }

        private int CountEligibleRequests(List<AmmoRequest> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var req = list[i];
                if (req.Tower.Value == 0) continue;
                if (_s.WorldState == null || !_s.WorldState.Towers.Exists(req.Tower)) continue;
                var tower = _s.WorldState.Towers.Get(req.Tower);
                if (tower.AmmoCap <= 0) continue;
                if (tower.Ammo >= tower.AmmoCap) continue;
                count++;
            }
            return count;
        }

        private void PruneInvalidRequests(List<AmmoRequest> list, HashSet<int> towerNoJobLogged, HashSet<int> towerDeadlockLogged)
        {
            if (list == null || list.Count == 0) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var r = list[i];
                int tid = r.Tower.Value;
                bool remove = false;

                if (tid == 0) remove = true;
                else if (_s.WorldState == null || !_s.WorldState.Towers.Exists(r.Tower)) remove = true;
                else
                {
                    var ts = _s.WorldState.Towers.Get(r.Tower);
                    if (ts.AmmoCap <= 0) remove = true;
                    else if ((ts.AmmoCap - ts.Ammo) <= 0) remove = true;
                }

                if (!remove) continue;

                list.RemoveAt(i);
                RemovePending(tid);
                if (tid != 0)
                {
                    towerNoJobLogged?.Remove(tid);
                    towerDeadlockLogged?.Remove(tid);
                }
            }
        }

        private void RemovePending(int tid)
        {
            if (tid == 0) return;
            _pendingReqTower.Remove(tid);
            _pendingPriorityByTower.Remove(tid);
        }
    }
}
