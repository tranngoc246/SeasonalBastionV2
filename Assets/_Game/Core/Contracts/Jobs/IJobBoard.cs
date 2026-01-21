namespace SeasonalBastion.Contracts
{
    public interface IJobBoard
    {
        JobId Enqueue(Job job);
        bool TryPeekForWorkplace(BuildingId workplace, out Job job); // deterministic order
        bool TryClaim(JobId id, NpcId npc);

        bool TryGet(JobId id, out Job job);
        void Update(Job job);        // status transitions
        void Cancel(JobId id);

        int CountForWorkplace(BuildingId workplace);
    }
}
