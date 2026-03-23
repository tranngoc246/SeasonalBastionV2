using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobWorkplacePolicy
    {
        private readonly IDataRegistry _data;

        internal JobWorkplacePolicy(IDataRegistry data)
        {
            _data = data;
        }

        internal WorkRoleFlags GetAllowedRoles(string defId)
        {
            if (_data == null || string.IsNullOrEmpty(defId)) return WorkRoleFlags.None;

            if (_data.TryGetBuilding(defId, out var exact) && exact != null)
                return exact.WorkRoles;

            var baseId = DefIdTierUtil.BaseId(defId);
            if (!string.IsNullOrEmpty(baseId) && _data.TryGetBuilding(baseId, out var canonical) && canonical != null)
                return canonical.WorkRoles;

            return WorkRoleFlags.None;
        }

        internal bool HasRole(string defId, WorkRoleFlags required)
        {
            var roles = GetAllowedRoles(defId);
            return (roles & required) != 0;
        }

        internal bool IsJobAllowed(WorkRoleFlags allowed, JobArchetype archetype)
        {
            return archetype switch
            {
                JobArchetype.Harvest => (allowed & WorkRoleFlags.Harvest) != 0,
                JobArchetype.HaulBasic => (allowed & WorkRoleFlags.HaulBasic) != 0,
                JobArchetype.HaulToForge => (allowed & (WorkRoleFlags.HaulBasic | WorkRoleFlags.Armory)) != 0,
                JobArchetype.BuildDeliver or JobArchetype.BuildWork => (allowed & WorkRoleFlags.Build) != 0,
                JobArchetype.CraftAmmo => (allowed & WorkRoleFlags.Craft) != 0,
                JobArchetype.ResupplyTower => (allowed & WorkRoleFlags.Armory) != 0,
                _ => true,
            };
        }
    }
}
