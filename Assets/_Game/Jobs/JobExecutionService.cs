using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobExecutionService
    {
        private readonly GameServices _s;
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly JobExecutorRegistry _exec;
        private readonly JobStateCleanupService _cleanupService;

        internal JobExecutionService(
            GameServices s,
            IWorldState w,
            IJobBoard board,
            JobExecutorRegistry exec,
            JobStateCleanupService cleanupService)
        {
            _s = s;
            _w = w;
            _board = board;
            _exec = exec;
            _cleanupService = cleanupService;
        }

        internal void TickCurrentJobs(IReadOnlyList<NpcId> npcIds, float dt)
        {
            for (int i = 0; i < npcIds.Count; i++)
            {
                var nid = npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;

                var ns = _w.Npcs.Get(nid);
                if (ns.CurrentJob.Value == 0)
                {
                    InteractionCellExitHelper.ContinuePendingStepOff(_s, ref ns, dt);
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                if (!_board.TryGet(ns.CurrentJob, out var job))
                {
                    _cleanupService.CleanupNpcJob(nid, ref ns);
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                var executor = _exec.Get(job.Archetype);
                executor.Tick(nid, ref ns, ref job, dt);

                _board.Update(job);

                if (_cleanupService.IsTerminal(job.Status))
                    _cleanupService.CleanupNpcJob(nid, ref ns);

                _w.Npcs.Set(nid, ns);
            }
        }

    }
}
