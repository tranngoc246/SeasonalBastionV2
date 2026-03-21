using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        private static WorkRoleFlags ParseWorkRolesOrDerive(BuildingJson bj)
        {
            if (bj != null && bj.workRoles != null && bj.workRoles.Length > 0)
            {
                WorkRoleFlags f = WorkRoleFlags.None;
                for (int i = 0; i < bj.workRoles.Length; i++)
                {
                    var s = (bj.workRoles[i] ?? string.Empty).Trim();
                    if (s.Length == 0) continue;

                    if (string.Equals(s, "Harvest", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Harvest;
                    else if (string.Equals(s, "HaulBasic", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.HaulBasic;
                    else if (string.Equals(s, "Build", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Build;
                    else if (string.Equals(s, "Craft", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Craft;
                    else if (string.Equals(s, "Armory", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Armory;
                }

                return f;
            }

            WorkRoleFlags roles = WorkRoleFlags.None;
            if (bj == null) return roles;

            if (bj.isProducer) roles |= WorkRoleFlags.Harvest;
            if (bj.isWarehouse) roles |= WorkRoleFlags.HaulBasic;
            if (bj.isHQ) roles |= (WorkRoleFlags.Build | WorkRoleFlags.HaulBasic);
            if (bj.isForge) roles |= WorkRoleFlags.Craft;
            if (bj.isArmory) roles |= WorkRoleFlags.Armory;

            return roles;
        }
    }
}
