namespace SeasonalBastion.Contracts
{
    public interface IBuildJobOrchestrator
    {
        void EnsureBuildJobsForSite(SiteId siteId, BuildSiteState site, BuildingId workplace);
        void CancelTrackedJobsForSite(SiteId siteId);
    }
}
