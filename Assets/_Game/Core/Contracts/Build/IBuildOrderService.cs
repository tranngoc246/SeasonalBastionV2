using System;

namespace SeasonalBastion.Contracts
{
    public interface IBuildOrderService
    {
        int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation);
        int CreateUpgradeOrder(BuildingId building);
        int CreateRepairOrder(BuildingId building);

        bool TryGet(int orderId, out BuildOrder order);
        void Cancel(int orderId);
        bool CancelBySite(SiteId siteId);
        bool CancelByBuilding(BuildingId buildingId);

        void ClearAll();

        // tick drives generating jobs (deliver/work)
        void Tick(float dt);

        event Action<int> OnOrderCompleted;
    }
}
