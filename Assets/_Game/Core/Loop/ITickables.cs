namespace SeasonalBastion
{
    public interface ITickable
    {
        void Tick(float dt);
    }

    internal interface IResettable
    {
        void Reset();
    }
}
