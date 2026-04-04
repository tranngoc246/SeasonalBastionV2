using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IAgentMoverRuntime
    {
        bool StepToward(ref NpcState st, CellPos target, float dt);
        void NotifyRoadsDirty();
        void ClearAll();
    }

    public interface IPathfinderRuntime
    {
        bool TryFindPath(CellPos from, CellPos target, out List<CellPos> path);
        bool TryEstimateCost(CellPos from, CellPos target, out int cost);
    }
}
