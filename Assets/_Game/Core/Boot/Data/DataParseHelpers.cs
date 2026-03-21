using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        private static StorageCapsByLevel ToCaps(StorageCapsJson j)
        {
            if (j == null) return default;
            return new StorageCapsByLevel { L1 = j.L1, L2 = j.L2, L3 = j.L3 };
        }

        private Season ParseSeason(string s, string ctx)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                _loadErrors.Add($"{ctx}: empty season");
                return Season.Spring;
            }

            if (Enum.TryParse<Season>(s.Trim(), ignoreCase: true, out var season))
                return season;

            _loadErrors.Add($"{ctx}: invalid season '{s}'");
            return Season.Spring;
        }

        private ResourceType ParseResourceType(int v, string ctx)
        {
            if (v < 0 || v > 5)
            {
                _loadErrors.Add($"{ctx}: invalid resource enum value {v}");
                return ResourceType.Wood;
            }

            return (ResourceType)v;
        }

        private CostDef[] ToCosts(BuildingCostJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0) return null;

            var outArr = new CostDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i] ?? new BuildingCostJson();
                var res = ParseResourceType(c.res, $"{ctx}[{i}].res");
                outArr[i] = new CostDef { Resource = res, Amount = Mathf.Max(0, c.amt) };
            }
            return outArr;
        }

        private CostDef[] ToCosts(TowerCostJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0) return null;

            var outArr = new CostDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i] ?? new TowerCostJson();
                var res = ParseResourceType(c.res, $"{ctx}[{i}].res");
                outArr[i] = new CostDef { Resource = res, Amount = Mathf.Max(0, c.amt) };
            }
            return outArr;
        }

        private CostDef[] ToCosts(RecipeCostJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0) return null;

            var outArr = new CostDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i] ?? new RecipeCostJson();
                var res = ParseResourceType(c.type, $"{ctx}[{i}].type");
                outArr[i] = new CostDef { Resource = res, Amount = Mathf.Max(0, c.amount) };
            }
            return outArr;
        }

        private UnlockDef ToUnlock(TowerUnlockJson u, string ctx)
        {
            if (u == null)
            {
                _loadErrors.Add($"{ctx}: missing unlock object");
                return default;
            }

            return new UnlockDef
            {
                Year = Mathf.Max(1, u.year),
                Season = ParseSeason(u.season, ctx + ".season"),
                Day = Mathf.Max(1, u.day)
            };
        }

        private WaveEntryDef[] ToWaveEntries(WaveEntryJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add($"{ctx}: empty entries array");
                return Array.Empty<WaveEntryDef>();
            }

            var outArr = new WaveEntryDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var e = arr[i];
                var enemyId = (e != null ? e.enemyId : null) ?? string.Empty;
                enemyId = enemyId.Trim();
                if (enemyId.Length == 0)
                    _loadErrors.Add($"{ctx}[{i}] has empty enemyId");

                outArr[i] = new WaveEntryDef { EnemyId = enemyId, Count = Mathf.Max(1, e != null ? e.count : 1) };
            }
            return outArr;
        }
    }
}
