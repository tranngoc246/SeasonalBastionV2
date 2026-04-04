using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugHUDHub
    {
        private void CacheHoverTarget()
        {
            if (_gs?.WorldState?.Buildings == null || _gs.GridMap == null) return;
            if (!MouseCellSharedState.HasValue) return;

            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0 && _gs.WorldState.Buildings.Exists(occ.Building))
            {
                _cachedHoverBuilding = occ.Building;
                _cachedHoverTime = Time.unscaledTime;
                return;
            }

            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites != null && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var st = _gs.WorldState.Sites.Get(occ.Site);
                if (st.TargetBuilding.Value != 0 && _gs.WorldState.Buildings.Exists(st.TargetBuilding))
                {
                    _cachedHoverBuilding = st.TargetBuilding;
                    _cachedHoverTime = Time.unscaledTime;
                }
            }
        }

        private bool TryGetCachedBuilding(out BuildingId bid, out BuildingState bs)
        {
            bid = default;
            bs = default;

            if (_gs?.WorldState?.Buildings == null) return false;
            if (_cachedHoverBuilding.Value == 0) return false;
            if (Time.unscaledTime - _cachedHoverTime > HoverCacheTTL) return false;
            if (!_gs.WorldState.Buildings.Exists(_cachedHoverBuilding)) return false;

            bid = _cachedHoverBuilding;
            bs = _gs.WorldState.Buildings.Get(bid);
            return true;
        }

        private void TryLockTargetFromClick()
        {
            if (_gs?.WorldState?.Buildings == null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!MouseCellSharedState.HasValue) return;

            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0 && _gs.WorldState.Buildings.Exists(occ.Building))
            {
                _lockedTargetBuilding = occ.Building;
                _cachedHoverBuilding = occ.Building;
                _cachedHoverTime = Time.unscaledTime;
                return;
            }

            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites != null && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var st = _gs.WorldState.Sites.Get(occ.Site);
                if (st.TargetBuilding.Value != 0 && _gs.WorldState.Buildings.Exists(st.TargetBuilding))
                {
                    _lockedTargetBuilding = st.TargetBuilding;
                    _cachedHoverBuilding = st.TargetBuilding;
                    _cachedHoverTime = Time.unscaledTime;
                }
            }
        }

        private bool TryFindBuildingFromHover(out BuildingId bid, out BuildingState bs)
        {
            if (_lockedTargetBuilding.Value != 0 && _gs?.WorldState?.Buildings != null && _gs.WorldState.Buildings.Exists(_lockedTargetBuilding))
            {
                bid = _lockedTargetBuilding;
                bs = _gs.WorldState.Buildings.Get(bid);
                return true;
            }

            if (TryGetCachedBuilding(out bid, out bs))
                return true;

            bid = default;
            bs = default;
            if (_gs?.WorldState?.Buildings == null || _gs.GridMap == null) return false;
            if (!MouseCellSharedState.HasValue) return false;

            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0 && _gs.WorldState.Buildings.Exists(occ.Building))
            {
                bid = occ.Building;
                bs = _gs.WorldState.Buildings.Get(bid);
                return true;
            }

            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites != null && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var st = _gs.WorldState.Sites.Get(occ.Site);
                if (st.TargetBuilding.Value != 0 && _gs.WorldState.Buildings.Exists(st.TargetBuilding))
                {
                    bid = st.TargetBuilding;
                    bs = _gs.WorldState.Buildings.Get(bid);
                    return true;
                }
            }

            return false;
        }
    }
}
