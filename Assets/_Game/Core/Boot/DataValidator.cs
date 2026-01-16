// PATCH v0.1.1 — DataValidator implements Part25 IDataValidator
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class DataValidator : IDataValidator
    {
        public bool ValidateAll(IDataRegistry reg, List<string> errors)
        {
            // v0.1: Keep it permissive so you can boot.
            // Later: implement real validation (unique ids, non-negative costs, wave refs, etc.)
            return true;
        }
    }
}
