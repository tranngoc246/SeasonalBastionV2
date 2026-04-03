namespace SeasonalBastion
{
    internal sealed class AmmoDebugHooks
    {
        private readonly AmmoService _owner;

        internal AmmoDebugHooks(AmmoService owner)
        {
            _owner = owner;
        }

        internal void Tick(float dt) => _owner.DevHookTick_Core(dt);
        internal void EnsureTestTowerExistsIfNeeded() => _owner.EnsureTestTowerExistsIfNeeded_Core();
    }
}
