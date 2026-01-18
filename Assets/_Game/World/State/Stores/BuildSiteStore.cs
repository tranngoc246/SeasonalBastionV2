using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildSiteStore : EntityStore<SiteId, BuildSiteState>, IBuildSiteStore
    {
        public override int ToInt(SiteId id) => id.Value;
        public override SiteId FromInt(int v) => new SiteId(v);
    }
}
