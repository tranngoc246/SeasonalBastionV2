using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class ArmoryBufferPlanner
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;

        internal ArmoryBufferPlanner(AmmoService owner)
        {
            _owner = owner;
            _s = owner.Services;
        }

        internal bool TryStartCraft(BuildingId forge) => _owner.TryStartCraft(forge);

        internal bool HasCapForForgeInputs(BuildingId forge, RecipeDef recipe)
        {
            int capMain = _s.StorageService.GetCap(forge, recipe.InputType);
            if (capMain <= 0) return false;

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var c = extras[i];
                    if (c == null || c.Amount <= 0) continue;
                    int capX = _s.StorageService.GetCap(forge, c.Resource);
                    if (capX <= 0) return false;
                }
            }

            return true;
        }

        internal void EnsureForgeSupplyByRecipe(BuildingId forge, CellPos forgeAnchor, RecipeDef recipe)
        {
            int crafts = _owner.ForgeTargetCraftsValue;
            if (crafts < 1) crafts = 1;

            EnsureSupplyJobToForgeByTarget(forge, forgeAnchor, recipe.InputType, recipe.InputAmount, crafts);

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var c = extras[i];
                    if (c == null || c.Amount <= 0) continue;
                    EnsureSupplyJobToForgeByTarget(forge, forgeAnchor, c.Resource, c.Amount, crafts);
                }
            }
        }

        internal void EnsureArmoryAmmoBuffer()
        {
            var armories = _s.WorldIndex.Armories;
            if (armories == null || armories.Count == 0) return;

            for (int i = 0; i < armories.Count; i++)
            {
                var arm = armories[i];
                if (!_s.WorldState.Buildings.Exists(arm)) continue;

                var armSt = _s.WorldState.Buildings.Get(arm);
                if (!armSt.IsConstructed) continue;

                if (!_owner.WorkplacesWithNpc.Contains(arm.Value)) continue;
                if (!_s.StorageService.CanStore(arm, ResourceType.Ammo)) continue;

                int cap = _s.StorageService.GetCap(arm, ResourceType.Ammo);
                if (cap <= 0) continue;

                int cur = _s.StorageService.GetAmount(arm, ResourceType.Ammo);
                int target = (cap * 80) / 100;
                if (cur >= target) continue;

                if (_owner.HaulAmmoJobByArmory.TryGetValue(arm.Value, out var oldId))
                {
                    if (_s.JobBoard.TryGet(oldId, out var old) && !AmmoService.IsTerminal(old.Status))
                        continue;
                }

                if (!TryPickForgeAmmoSource(armSt.Anchor, out var forge, out var takeable))
                    continue;

                int free = cap - cur;
                if (free <= 0) continue;

                int need = target - cur;
                int chunk = _owner.GetArmoryChunkByLevel_Value(armSt.Level);

                int amount = chunk;
                if (amount > need) amount = need;
                if (amount > free) amount = free;
                if (amount > takeable) amount = takeable;
                if (amount <= 0) continue;

                var j = new Job
                {
                    Archetype = JobArchetype.HaulAmmoToArmory,
                    Status = JobStatus.Created,
                    Workplace = arm,
                    SourceBuilding = forge,
                    DestBuilding = arm,
                    ResourceType = ResourceType.Ammo,
                    Amount = amount,
                    TargetCell = default,
                    CreatedAt = 0
                };

                var id = _s.JobBoard.Enqueue(j);
                _owner.HaulAmmoJobByArmory[arm.Value] = id;
            }
        }

        internal bool TryPickForgeAmmoSource(CellPos refPos, out BuildingId bestForge, out int bestTakeable)
        {
            bestForge = default;
            bestTakeable = 0;

            var forges = _s.WorldIndex.Forges;
            if (forges == null || forges.Count == 0) return false;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < forges.Count; i++)
            {
                var f = forges[i];
                if (!_s.WorldState.Buildings.Exists(f)) continue;

                var fs = _s.WorldState.Buildings.Get(f);
                if (!fs.IsConstructed) continue;
                if (!_s.StorageService.CanStore(f, ResourceType.Ammo)) continue;

                int cap = _s.StorageService.GetCap(f, ResourceType.Ammo);
                if (cap <= 0) continue;

                int cur = _s.StorageService.GetAmount(f, ResourceType.Ammo);
                if (cur <= 0) continue;

                int keep = (cap * 20 + 99) / 100;
                if (keep < 1) keep = 1;
                if (cur < keep) continue;

                int takeable = cur - keep;
                if (takeable <= 0) continue;

                int d = AmmoService.Manhattan(refPos, fs.Anchor);
                int idv = f.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
                    bestDist = d;
                    bestId = idv;
                    bestForge = f;
                    bestTakeable = takeable;
                }
            }

            return bestForge.Value != 0;
        }

        private void EnsureSupplyJobToForgeByTarget(BuildingId forge, CellPos forgeAnchor, ResourceType rt, int perCraftAmount, int craftsTarget)
        {
            if (perCraftAmount <= 0) return;

            int cap = _s.StorageService.GetCap(forge, rt);
            int cur = _s.StorageService.GetAmount(forge, rt);
            if (cap <= 0) return;
            if (cur >= cap) return;

            int target = perCraftAmount * craftsTarget;
            if (target > cap) target = cap;

            int want = target - cur;
            if (want <= 0) return;

            int free = cap - cur;
            if (want > free) want = free;

            const int CarryCapFallback = 10;
            if (want > CarryCapFallback) want = CarryCapFallback;
            if (want <= 0) return;

            int key = forge.Value * 16 + (int)rt;
            if (_owner.SupplyJobByForgeAndType.TryGetValue(key, out var oldId))
            {
                if (_s.JobBoard.TryGet(oldId, out var old) && !AmmoService.IsTerminal(old.Status))
                    return;
            }

            if (!_owner.TryPickPreferredHaulerWorkplace_Core(forgeAnchor, out var workplace))
                return;

            var j = new Job
            {
                Archetype = JobArchetype.HaulToForge,
                Status = JobStatus.Created,
                Workplace = workplace,
                SourceBuilding = default,
                DestBuilding = forge,
                ResourceType = rt,
                Amount = want,
                TargetCell = default,
                CreatedAt = 0
            };

            var id = _s.JobBoard.Enqueue(j);
            _owner.SupplyJobByForgeAndType[key] = id;
        }
    }
}
