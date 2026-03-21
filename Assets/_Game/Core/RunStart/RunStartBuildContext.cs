using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal sealed class RunStartBuildContext
    {
        public Dictionary<string, BuildingId> DefIdToBuildingId { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
