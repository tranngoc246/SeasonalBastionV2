using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using static SeasonalBastion.BalanceConfig;

namespace SeasonalBastion
{
    public sealed class BalanceService
    {
        private readonly GameServices _s;
        private readonly BalanceConfig _cfg;

        private readonly HashSet<string> _builderDefs = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _warehouseDefs = new(StringComparer.OrdinalIgnoreCase);

        // reuse buffer to avoid alloc
        private readonly List<BuildingId> _tmpB = new(64);

        public BalanceService(GameServices s, BalanceConfig cfg)
        {
            _s = s;
            _cfg = cfg;

            if (_cfg?.builderWorkplace?.builderDefIds != null)
                for (int i = 0; i < _cfg.builderWorkplace.builderDefIds.Length; i++)
                    _builderDefs.Add(_cfg.builderWorkplace.builderDefIds[i]);

            if (_cfg?.warehouseWorkplace?.warehouseDefIds != null)
                for (int i = 0; i < _cfg.warehouseWorkplace.warehouseDefIds.Length; i++)
                    _warehouseDefs.Add(_cfg.warehouseWorkplace.warehouseDefIds[i]);
        }

        public BalanceConfig Config => _cfg;

        public float DefaultMoveSpeed => _cfg?.movement?.defaultBaseMoveSpeed ?? 1f;
        public float DefaultRoadMult => _cfg?.movement?.defaultRoadSpeedMultiplier ?? 1.3f;

        public float BuildChunkSec => _cfg?.build?.workChunkSec ?? 6f;
        public int FallbackBuildChunksL1 => _cfg?.build?.fallbackBuildChunksL1 ?? 2;

        public float RepairChunkSec => _cfg?.repair?.workChunkSec ?? 4f;
        public float RepairHealPct => _cfg?.repair?.healPctPerChunk ?? 0.15f;
        public float RepairCostFactor => _cfg?.repair?.costFactorOfBuildCost ?? 0.30f;

        public string AmmoRecipeId => _cfg?.crafting?.ammoRecipeId ?? "ForgeAmmo";

        public int AmmoLowPct => _cfg?.ammoMonitor?.lowAmmoPct ?? 25;
        public float AmmoReqCooldownLowSec => _cfg?.ammoMonitor?.reqCooldownLowSec ?? 8f;
        public float AmmoReqCooldownEmptySec => _cfg?.ammoMonitor?.reqCooldownEmptySec ?? 4f;
        public float AmmoNotifyCooldownLowSec => _cfg?.ammoMonitor?.notifyCooldownLowSec ?? 6f;
        public float AmmoNotifyCooldownEmptySec => _cfg?.ammoMonitor?.notifyCooldownEmptySec ?? 4f;

        public int ForgeTargetCrafts => _cfg?.ammoSupply?.forgeTargetCrafts ?? 5;

        public int GetTierFromLevel(int level)
        {
            int lvl = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return lvl;
        }

        private int GetLevel3(Level3Int v, int tier)
        {
            if (v == null) return 0;
            return tier <= 1 ? v.L1 : (tier == 2 ? v.L2 : v.L3);
        }

        private float GetLevel3(Level3Float v, int tier)
        {
            if (v == null) return 1f;
            return tier <= 1 ? v.L1 : (tier == 2 ? v.L2 : v.L3);
        }

        public BuildingId ResolveBuilderWorkplace()
        {
            // Prefer best builder workplace by defId list (level desc, id asc)
            if (TryPickBestBuildingByDefSet(_builderDefs, out var best))
                return best;

            // Fallback: HQ
            if (TryPickHQ(out var hq))
                return hq;

            return default;
        }

        public int GetBuilderTier()
        {
            var b = ResolveBuilderWorkplace();
            if (b.Value == 0) return 1;
            var st = _s.WorldState.Buildings.Get(b);
            return GetTierFromLevel(st.Level);
        }

        public float GetBuildSpeedMult(int builderTier)
            => GetLevel3(_cfg?.builderWorkplace?.buildSpeedMult, builderTier);

        public float GetRepairTimeMult(int builderTier)
            => GetLevel3(_cfg?.builderWorkplace?.repairTimeMult, builderTier);

        public float GetRepairCostMult(int builderTier)
            => GetLevel3(_cfg?.builderWorkplace?.repairCostMult, builderTier);

        public int GetCarryBuilder(int builderTier)
            => GetLevel3(_cfg?.carry?.builder, builderTier);

        public int GetCarryHaulBasic(int warehouseTier)
            => GetLevel3(_cfg?.carry?.haulBasic, warehouseTier);

        public int GetCarryHarvest(int tier)
            => GetLevel3(_cfg?.carry?.harvest, tier);

        public int GetArmoryAmmoCarry(int armoryTier)
            => GetLevel3(_cfg?.carry?.armoryAmmo, armoryTier);

        public int GetArmoryResupplyTripAmmo(int armoryTier)
            => GetLevel3(_cfg?.armory?.resupplyTripAmmo, armoryTier);

        public int GetWarehouseTier()
        {
            // Warehouse only (do NOT use HQ tier unless no warehouse exists)
            if (TryPickBestBuildingByDefSet(_warehouseDefs, out var wh))
            {
                var st = _s.WorldState.Buildings.Get(wh);
                return GetTierFromLevel(st.Level);
            }

            // Fallback: HQ tier
            if (TryPickHQ(out var hq))
            {
                var st = _s.WorldState.Buildings.Get(hq);
                return GetTierFromLevel(st.Level);
            }

            return 1;
        }

        private bool TryPickHQ(out BuildingId hq)
        {
            hq = default;
            if (_s?.WorldState?.Buildings == null) return false;

            BuildingId best = default;
            int bestId = int.MaxValue;

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                if (!_s.WorldState.Buildings.Exists(bid)) continue;
                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (!_s.DataRegistry.TryGetBuilding(bs.DefId, out var def) || def == null || !def.IsHQ)
                    continue;

                if (bid.Value < bestId)
                {
                    best = bid;
                    bestId = bid.Value;
                }
            }

            hq = best;
            return hq.Value != 0;
        }

        private bool TryPickBestBuildingByDefSet(HashSet<string> defSet, out BuildingId best)
        {
            best = default;
            if (defSet == null || defSet.Count == 0) return false;
            if (_s?.WorldState?.Buildings == null) return false;

            int bestLevel = -1;
            int bestId = int.MaxValue;

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                if (!_s.WorldState.Buildings.Exists(bid)) continue;
                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!defSet.Contains(bs.DefId)) continue;

                int lvl = GetTierFromLevel(bs.Level);
                int idv = bid.Value;

                if (lvl > bestLevel || (lvl == bestLevel && idv < bestId))
                {
                    bestLevel = lvl;
                    bestId = idv;
                    best = bid;
                }
            }

            return best.Value != 0;
        }
    }
}