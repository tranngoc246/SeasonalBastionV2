using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class PlacementService : IPlacementService
    {
        private readonly IGridMap _grid;
        private readonly IWorldState _world;
        private readonly IDataRegistry _data;
        private readonly IWorldIndex _index;
        private readonly IEventBus _bus;

        private IBuildOrderService _buildOrders;

        public void BindBuildOrders(IBuildOrderService buildOrders)
        {
            _buildOrders = buildOrders;
        }

        public PlacementService(IGridMap grid, IWorldState world, IDataRegistry data, IWorldIndex index, IEventBus bus)
        { _grid = grid; _world = world; _data = data; _index = index; _bus = bus; }

        private static CellPos Add(CellPos a, int dx, int dy) => new CellPos(a.X + dx, a.Y + dy);

        private CellPos ComputeEntryCell(CellPos anchor, int w, int h, Dir4 rot)
        {
            // EntryCell = cell immediately OUTSIDE the footprint, at mid-edge (deterministic).
            int cx = (w - 1) / 2;
            int cy = (h - 1) / 2;

            return rot switch
            {
                Dir4.N => new CellPos(anchor.X + cx, anchor.Y + h),
                Dir4.S => new CellPos(anchor.X + cx, anchor.Y - 1),
                Dir4.E => new CellPos(anchor.X + w, anchor.Y + cy),
                Dir4.W => new CellPos(anchor.X - 1, anchor.Y + cy),
                _ => new CellPos(anchor.X + cx, anchor.Y + h),
            };
        }

        private bool HasRoadInCross(CellPos entry)
        {
            var n = Add(entry, 0, 1);
            var e = Add(entry, 1, 0);
            var s = Add(entry, 0, -1);
            var w = Add(entry, -1, 0);

            return (_grid.IsInside(n) && _grid.IsRoad(n))
                || (_grid.IsInside(e) && _grid.IsRoad(e))
                || (_grid.IsInside(s) && _grid.IsRoad(s))
                || (_grid.IsInside(w) && _grid.IsRoad(w));
        }

        public bool CanPlaceRoad(CellPos c)
        {
            if (!_grid.IsInside(c)) return false;
            if (_grid.IsBlocked(c)) return false;
            // orthogonal rule is tool-level
            return true;
        }

        public void PlaceRoad(CellPos c)
        {
            if (!CanPlaceRoad(c)) return;
            _grid.SetRoad(c, true);
            _bus.Publish(new RoadPlacedEvent(c));
        }

        public PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            // Provide a non-default suggested cell even for invalid rotation (debug-friendly)
            if (rotation != Dir4.N && rotation != Dir4.E && rotation != Dir4.S && rotation != Dir4.W)
                return new PlacementResult(false, PlacementFailReason.InvalidRotation, anchor);

            BuildingDef def;
            try { def = _data.GetBuilding(buildingDefId); }
            catch { return new PlacementResult(false, PlacementFailReason.Unknown, anchor); }

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);

            // Compute entry early so ALL failure paths can return a meaningful suggested cell
            var entry = ComputeEntryCell(anchor, w, h, rotation);

            // 1) footprint validation 
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new CellPos(anchor.X + dx, anchor.Y + dy);

                    if (!_grid.IsInside(c))
                        return new PlacementResult(false, PlacementFailReason.OutOfBounds, entry);

                    // road overlap invalid
                    if (_grid.IsRoad(c))
                        return new PlacementResult(false, PlacementFailReason.Overlap, entry);

                    var occ = _grid.Get(c);
                    if (occ.Kind == CellOccupancyKind.Site)
                        return new PlacementResult(false, PlacementFailReason.BlockedBySite, entry);

                    if (occ.Kind == CellOccupancyKind.Building)
                        return new PlacementResult(false, PlacementFailReason.Overlap, entry);
                }
            }

            // 2) entry/road connectivity (len=1)

            // If entry is outside map -> cannot connect
            if (!_grid.IsInside(entry))
                return new PlacementResult(false, PlacementFailReason.NoRoadConnection, entry);

            // Driveway (entry) MUST be empty cell (NOT road, NOT site, NOT building).
            if (_grid.IsRoad(entry))
                return new PlacementResult(false, PlacementFailReason.Overlap, entry);

            var entryOcc = _grid.Get(entry);
            if (entryOcc.Kind == CellOccupancyKind.Site)
                return new PlacementResult(false, PlacementFailReason.BlockedBySite, entry);

            if (entryOcc.Kind == CellOccupancyKind.Building)
                return new PlacementResult(false, PlacementFailReason.Overlap, entry);

            if (entryOcc.Kind != CellOccupancyKind.Empty)
                return new PlacementResult(false, PlacementFailReason.Overlap, entry);

            // Road must be adjacent (N/E/S/W) to driveway (entry).
            if (!HasRoadInCross(entry))
                return new PlacementResult(false, PlacementFailReason.NoRoadConnection, entry);

            // Ok. SuggestedRoadCell = entry (driveway conversion target)
            return new PlacementResult(true, PlacementFailReason.None, entry);
        }

        public BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            var vr = ValidateBuilding(buildingDefId, anchor, rotation);
            if (!vr.Ok) return default;

            if (_buildOrders == null) return default;

            // 1) Create build order FIRST (will create placeholder building + site footprint)
            int orderId = _buildOrders.CreatePlaceOrder(buildingDefId, anchor, rotation);
            if (orderId <= 0) return default;

            // resolve building id from order
            if (!_buildOrders.TryGet(orderId, out var order) || order.TargetBuilding.Value == 0)
                return default;

            // 2) Auto-create driveway AFTER order (so CreatePlaceOrder re-validate won't fail)
            var driveway = vr.SuggestedRoadCell;
            bool drivewayWasCreated = false;

            if (_grid.IsInside(driveway))
            {
                // driveway must be empty. If not empty, skip auto-road (don't break placement)
                if (!_grid.IsRoad(driveway))
                {
                    var occ = _grid.Get(driveway);
                    if (occ.Kind == CellOccupancyKind.Empty)
                    {
                        _grid.SetRoad(driveway, true);
                        _bus.Publish(new RoadPlacedEvent(driveway));
                        drivewayWasCreated = true;
                    }
                }
            }

            // 3) Record auto-created driveway for cancel rollback
            if (drivewayWasCreated)
                _bus?.Publish(new BuildOrderAutoRoadCreatedEvent(orderId, driveway));

            return order.TargetBuilding;
        }
    }
}
