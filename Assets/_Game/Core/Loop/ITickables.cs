// PATCH v0.1.2 — runtime tick/reset helpers (NOT contracts)
namespace SeasonalBastion
{
    internal interface ITickable
    {
        void Tick(float dt);
    }

    internal interface IResettable
    {
        void Reset();
    }
}
