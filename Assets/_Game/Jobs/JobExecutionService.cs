using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobExecutionService
    {
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly JobExecutorRegistry _exec;
        private readonly JobStateCleanupService _cleanupService;
        private readonly IAgentMoverRuntime _agentMover;
        private readonly IGridMap _gridMap;

        internal JobExecutionService(
            IWorldState w,
            IJobBoard board,
            JobExecutorRegistry exec,
            JobStateCleanupService cleanupService,
            IAgentMoverRuntime agentMover,
            IGridMap gridMap)
        {
            _w = w;
            _board = board;
            _exec = exec;
            _cleanupService = cleanupService;
            _agentMover = agentMover;
            _gridMap = gridMap;
        }

        internal JobExecutionService(
            GameServices services,
            IWorldState w,
            IJobBoard board,
            JobExecutorRegistry exec,
            JobStateCleanupService cleanupService)
            : this(w, board, exec, cleanupService, services?.AgentMover, services?.GridMap)
        {
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
                    InteractionCellExitHelper.ContinuePendingStepOff(_agentMover, _gridMap, ref ns, dt);
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
