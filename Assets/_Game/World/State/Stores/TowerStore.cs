using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class TowerStore : EntityStore<TowerId, TowerState>, ITowerStore
    {
        public override int ToInt(TowerId id) => id.Value;
        public override TowerId FromInt(int v) => new TowerId(v);
    }
}
