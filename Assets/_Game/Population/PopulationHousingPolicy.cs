using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class PopulationHousingPolicy
    {
        private readonly IDataRegistry _dataRegistry;

        public PopulationHousingPolicy(IDataRegistry dataRegistry)
        {
            _dataRegistry = dataRegistry;
        }

        public bool ShouldRebuildForPlaced(string defId)
        {
            if (_dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return true;

            return _dataRegistry.TryGetBuilding(defId, out var def) && def != null && def.IsHouse;
        }

        public bool ShouldRebuildForUpgrade(string fromDefId, string toDefId)
        {
            if (_dataRegistry == null)
                return true;

            bool fromIsHouse = !string.IsNullOrWhiteSpace(fromDefId)
                && _dataRegistry.TryGetBuilding(fromDefId, out var fromDef)
                && fromDef != null
                && fromDef.IsHouse;

            bool toIsHouse = !string.IsNullOrWhiteSpace(toDefId)
                && _dataRegistry.TryGetBuilding(toDefId, out var toDef)
                && toDef != null
                && toDef.IsHouse;

            return fromIsHouse || toIsHouse;
        }

        public int CountPopulationCap(IWorldState worldState)
        {
            if (worldState?.Buildings == null || _dataRegistry == null)
                return 0;

            int cap = 0;
            foreach (var buildingId in worldState.Buildings.Ids)
            {
                if (!worldState.Buildings.Exists(buildingId))
                    continue;

                var building = worldState.Buildings.Get(buildingId);
                if (!building.IsConstructed)
                    continue;

                var def = _dataRegistry.GetBuilding(building.DefId);
                if (def == null || !def.IsHouse)
                    continue;

                int level = building.Level;
                if (level < 1) level = 1;
                if (level > 3) level = 3;

                cap += level switch
                {
                    1 => 2,
                    2 => 4,
                    3 => 6,
                    _ => 2
                };
            }

            return cap;
        }
    }
}
