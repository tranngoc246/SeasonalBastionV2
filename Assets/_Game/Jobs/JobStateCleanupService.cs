using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobStateCleanupService
    {
        private readonly IClaimService _claims;

        internal JobStateCleanupService(IClaimService claims)
        {
            _claims = claims;
        }

        internal bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed
                || s == JobStatus.Failed
                || s == JobStatus.Cancelled;
        }

        internal void CleanupNpcJob(NpcId npc, ref NpcState ns)
        {
            ns.CurrentJob = default;
            ns.IsIdle = true;
            _claims?.ReleaseAll(npc);
        }
    }
}
