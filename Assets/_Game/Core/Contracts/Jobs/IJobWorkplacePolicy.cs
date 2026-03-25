namespace SeasonalBastion.Contracts
{
    public interface IJobWorkplacePolicy
    {
        WorkRoleFlags GetAllowedRoles(string defId);
        bool HasRole(string defId, WorkRoleFlags required);
        bool IsJobAllowed(WorkRoleFlags allowed, JobArchetype archetype);
    }
}
