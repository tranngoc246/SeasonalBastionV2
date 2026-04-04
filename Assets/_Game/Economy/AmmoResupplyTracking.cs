using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SeasonalBastion
{
    internal sealed class AmmoResupplyTracking
    {
        private readonly GameServices _s;
        private readonly Dictionary<int, JobId> _resupplyJobByArmory = new();
        private readonly Dictionary<int, JobId> _resupplyJobByTower = new();
        private readonly List<int> _tmpKeys = new(64);

        internal AmmoResupplyTracking(GameServices s)
        {
            _s = s;
        }

        internal Dictionary<int, JobId> ResupplyJobByArmory => _resupplyJobByArmory;
        internal Dictionary<int, JobId> ResupplyJobByTower => _resupplyJobByTower;
        internal List<int> TempKeys => _tmpKeys;
        internal int InFlightCount => _resupplyJobByTower.Count;

        internal void RebuildFromJobBoard()
        {
            _resupplyJobByArmory.Clear();
            _resupplyJobByTower.Clear();

            var board = _s?.JobBoard;
            if (board == null)
                return;

            try
            {
                var jobsField = board.GetType().GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic);
                if (jobsField?.GetValue(board) is not Dictionary<int, Job> jobs)
                    return;

                foreach (var kv in jobs)
                {
                    var job = kv.Value;
                    if (job.Archetype != JobArchetype.ResupplyTower) continue;
                    if (AmmoService.IsTerminal(job.Status)) continue;
                    if (job.Tower.Value == 0 || job.Workplace.Value == 0) continue;
                    if (_s.WorldState?.Towers == null || !_s.WorldState.Towers.Exists(job.Tower)) continue;
                    if (_s.WorldState?.Buildings == null || !_s.WorldState.Buildings.Exists(job.Workplace)) continue;

                    _resupplyJobByTower[job.Tower.Value] = job.Id;
                    _resupplyJobByArmory[job.Workplace.Value] = job.Id;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to rebuild in-flight resupply jobs after load: {ex}");
            }
        }

        internal int CountTrackedActiveJobs()
        {
            int count = 0;
            foreach (var kv in _resupplyJobByTower)
            {
                if (!_s.JobBoard.TryGet(kv.Value, out var job))
                    continue;
                if (!AmmoService.IsTerminal(job.Status))
                    count++;
            }
            return count;
        }

        internal void RemoveTower(int towerId)
        {
            if (towerId != 0)
                _resupplyJobByTower.Remove(towerId);
        }

        internal void RemoveArmory(int buildingId)
        {
            if (buildingId != 0)
                _resupplyJobByArmory.Remove(buildingId);
        }

        internal void RemoveArmoryMappingByJob(JobId jobId)
        {
            if (_resupplyJobByArmory.Count == 0) return;

            _tmpKeys.Clear();
            foreach (var kv in _resupplyJobByArmory)
            {
                if (kv.Value.Value == jobId.Value)
                    _tmpKeys.Add(kv.Key);
            }

            for (int i = 0; i < _tmpKeys.Count; i++)
                _resupplyJobByArmory.Remove(_tmpKeys[i]);
        }

        internal void CleanupArmoryMappings()
        {
            if (_resupplyJobByArmory.Count == 0) return;

            _tmpKeys.Clear();
            foreach (var kv in _resupplyJobByArmory)
                _tmpKeys.Add(kv.Key);

            for (int i = 0; i < _tmpKeys.Count; i++)
            {
                int armoryId = _tmpKeys[i];
                var jid = _resupplyJobByArmory[armoryId];
                if (!_s.JobBoard.TryGet(jid, out var j) || AmmoService.IsTerminal(j.Status))
                    _resupplyJobByArmory.Remove(armoryId);
            }
        }

        internal void Clear()
        {
            _resupplyJobByArmory.Clear();
            _resupplyJobByTower.Clear();
            _tmpKeys.Clear();
        }
    }
}
