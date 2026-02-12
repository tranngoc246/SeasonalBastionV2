namespace SeasonalBastion.Contracts
{
    public interface IJobExecutor
    {
        // returns true if progressed; false if waiting/stuck
        bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt);
    }
}
