using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartNpcSpawner
    {
        public static void Apply(GameServices s, StartMapConfigRootDto cfg, RunStartBuildContext ctx)
        {
            if (cfg.initialNpcs == null) return;

            for (int i = 0; i < cfg.initialNpcs.Length; i++)
            {
                var n = cfg.initialNpcs[i];
                if (n == null || n.spawnCell == null || string.IsNullOrEmpty(n.npcDefId)) continue;

                BuildingId workplace = default;
                if (!string.IsNullOrEmpty(n.assignedWorkplaceDefId))
                    ctx.DefIdToBuildingId.TryGetValue(n.assignedWorkplaceDefId, out workplace);

                var st = new NpcState
                {
                    DefId = n.npcDefId,
                    Cell = new CellPos(n.spawnCell.x, n.spawnCell.y),
                    Workplace = workplace,
                    CurrentJob = default,
                    IsIdle = true
                };

                var id = s.WorldState.Npcs.Create(st);
                st.Id = id;
                s.WorldState.Npcs.Set(id, st);
            }
        }
    }
}
