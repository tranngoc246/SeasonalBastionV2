using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class ArmoryBufferPlanner
    {
        private readonly IWorldState _worldState;
        private readonly IWorldIndex _worldIndex;
        private readonly IStorageService _storageService;
        private readonly IJobBoard _jobBoard;
        private readonly Dictionary<int, JobId> _supplyJobByForgeAndType;
        private readonly Dictionary<int, JobId> _haulAmmoJobByArmory;
        private readonly Func<HashSet<int>> _getWorkplacesWithNpc;
        private readonly Func<int> _getForgeTargetCrafts;
        private readonly Func<int, int> _getArmoryChunkByLevel;
        private readonly Func<CellPos, BuildingId> _pickPreferredHaulerWorkplace;
        private readonly Func<CellPos, (bool found, BuildingId forge, int takeable)> _pickForgeAmmoSource;
        private readonly Func<BuildingId, bool> _tryStartCraft;

        internal ArmoryBufferPlanner(
            IWorldState worldState,
            IWorldIndex worldIndex,
            IStorageService storageService,
            IJobBoard jobBoard,
            Dictionary<int, JobId> supplyJobByForgeAndType,
            Dictionary<int, JobId> haulAmmoJobByArmory,
            Func<HashSet<int>> getWorkplacesWithNpc,
            Func<int> getForgeTargetCrafts,
            Func<int, int> getArmoryChunkByLevel,
            Func<CellPos, BuildingId> pickPreferredHaulerWorkplace,
            Func<CellPos, (bool found, BuildingId forge, int takeable)> pickForgeAmmoSource,
            Func<BuildingId, bool> tryStartCraft)
        {
            _worldState = worldState;
            _worldIndex = worldIndex;
            _storageService = storageService;
            _jobBoard = jobBoard;
            _supplyJobByForgeAndType = supplyJobByForgeAndType;
            _haulAmmoJobByArmory = haulAmmoJobByArmory;
            _getWorkplacesWithNpc = getWorkplacesWithNpc;
            _getForgeTargetCrafts = getForgeTargetCrafts;
            _getArmoryChunkByLevel = getArmoryChunkByLevel;
            _pickPreferredHaulerWorkplace = pickPreferredHaulerWorkplace;
            _pickForgeAmmoSource = pickForgeAmmoSource;
            _tryStartCraft = tryStartCraft;
        }

        internal bool TryStartCraft(BuildingId forge) => _tryStartCraft != null && _tryStartCraft(forge);

        internal bool HasCapForForgeInputs(BuildingId forge, RecipeDef recipe)
        {
            int capMain = _storageService.GetCap(forge, recipe.InputType);
            if (capMain <= 0)
                return false;

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var cost = extras[i];
                    if (cost == null || cost.Amount <= 0)
                        continue;

                    int extraCap = _storageService.GetCap(forge, cost.Resource);
                    if (extraCap <= 0)
                        return false;
                }
            }

            return true;
        }

        internal void EnsureForgeSupplyByRecipe(BuildingId forge, CellPos forgeAnchor, RecipeDef recipe)
        {
            int crafts = _getForgeTargetCrafts();
            if (crafts < 1)
                crafts = 1;

            EnsureSupplyJobToForgeByTarget(forge, forgeAnchor, recipe.InputType, recipe.InputAmount, crafts);

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var cost = extras[i];
                    if (cost == null || cost.Amount <= 0)
                        continue;

                    EnsureSupplyJobToForgeByTarget(forge, forgeAnchor, cost.Resource, cost.Amount, crafts);
                }
            }
        }

        internal void EnsureArmoryAmmoBuffer()
        {
            var armories = _worldIndex.Armories;
            if (armories == null || armories.Count == 0)
                return;

            var workplacesWithNpc = _getWorkplacesWithNpc();
            for (int i = 0; i < armories.Count; i++)
            {
                var armory = armories[i];
                if (!_worldState.Buildings.Exists(armory))
                    continue;

                var armoryState = _worldState.Buildings.Get(armory);
                if (!armoryState.IsConstructed)
                    continue;

                if (workplacesWithNpc == null || !workplacesWithNpc.Contains(armory.Value))
                    continue;
                if (!_storageService.CanStore(armory, ResourceType.Ammo))
                    continue;

                int cap = _storageService.GetCap(armory, ResourceType.Ammo);
                if (cap <= 0)
                    continue;

                int current = _storageService.GetAmount(armory, ResourceType.Ammo);
                int target = (cap * 80) / 100;
                if (current >= target)
                    continue;

                if (_haulAmmoJobByArmory.TryGetValue(armory.Value, out var existingId))
                {
                    if (_jobBoard.TryGet(existingId, out var existingJob) && !AmmoService.IsTerminal(existingJob.Status))
                        continue;
                }

                var source = _pickForgeAmmoSource(armoryState.Anchor);
                if (!source.found)
                    continue;

                int free = cap - current;
                if (free <= 0)
                    continue;

                int need = target - current;
                int amount = _getArmoryChunkByLevel(armoryState.Level);
                if (amount > need) amount = need;
                if (amount > free) amount = free;
                if (amount > source.takeable) amount = source.takeable;
                if (amount <= 0)
                    continue;

                var job = new Job
                {
                    Archetype = JobArchetype.HaulAmmoToArmory,
                    Status = JobStatus.Created,
                    Workplace = armory,
                    SourceBuilding = source.forge,
                    DestBuilding = armory,
                    ResourceType = ResourceType.Ammo,
                    Amount = amount,
                    TargetCell = default,
                    CreatedAt = 0
                };

                var jobId = _jobBoard.Enqueue(job);
                _haulAmmoJobByArmory[armory.Value] = jobId;
            }
        }

        private void EnsureSupplyJobToForgeByTarget(BuildingId forge, CellPos forgeAnchor, ResourceType resourceType, int perCraftAmount, int craftsTarget)
        {
            if (perCraftAmount <= 0)
                return;

            int cap = _storageService.GetCap(forge, resourceType);
            int current = _storageService.GetAmount(forge, resourceType);
            if (cap <= 0 || current >= cap)
                return;

            int target = perCraftAmount * craftsTarget;
            if (target > cap)
                target = cap;

            int want = target - current;
            if (want <= 0)
                return;

            int free = cap - current;
            if (want > free)
                want = free;

            const int CarryCapFallback = 10;
            if (want > CarryCapFallback)
                want = CarryCapFallback;
            if (want <= 0)
                return;

            int key = forge.Value * 16 + (int)resourceType;
            if (_supplyJobByForgeAndType.TryGetValue(key, out var existingId))
            {
                if (_jobBoard.TryGet(existingId, out var existingJob) && !AmmoService.IsTerminal(existingJob.Status))
                    return;
            }

            var workplace = _pickPreferredHaulerWorkplace(forgeAnchor);
            if (workplace.Value == 0)
                return;

            var job = new Job
            {
                Archetype = JobArchetype.HaulToForge,
                Status = JobStatus.Created,
                Workplace = workplace,
                SourceBuilding = default,
                DestBuilding = forge,
                ResourceType = resourceType,
                Amount = want,
                TargetCell = default,
                CreatedAt = 0
            };

            var jobId = _jobBoard.Enqueue(job);
            _supplyJobByForgeAndType[key] = jobId;
        }
    }
}
