using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IWorldIndex
    {
        // storages
        IReadOnlyList<BuildingId> Warehouses { get; }
        IReadOnlyList<BuildingId> Producers  { get; }
        IReadOnlyList<BuildingId> Houses     { get; }
        IReadOnlyList<BuildingId> Forges     { get; }
        IReadOnlyList<BuildingId> Armories   { get; }
        IReadOnlyList<TowerId>    Towers     { get; }

        // rebuild
        void RebuildAll();
        void OnBuildingCreated(BuildingId id);
        void OnBuildingDestroyed(BuildingId id);
    }
}
