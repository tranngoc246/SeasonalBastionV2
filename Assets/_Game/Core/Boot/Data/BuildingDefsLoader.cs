using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadBuildingsInternal(TextAsset ta)
        {
            if (ta == null)
            {
                _loadErrors.Add("Buildings TextAsset is null (DefsCatalog.Buildings)");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Buildings TextAsset '{ta.name}' is empty");
                return;
            }

            BuildingsRoot root;
            try { root = JsonUtility.FromJson<BuildingsRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Buildings parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.buildings : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Buildings JSON missing/empty 'buildings' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var bj = arr[i];
                if (bj == null) continue;

                var id = (bj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    _loadErrors.Add($"Buildings[{i}] has empty id");
                    continue;
                }

                if (!seen.Add(id))
                    _loadErrors.Add($"Duplicate Building id '{id}' in Buildings.json");

                int w = Mathf.Max(1, bj.sizeX);
                int h = Mathf.Max(1, bj.sizeY);
                int lvl = Mathf.Max(1, bj.baseLevel);

                var def = new BuildingDef
                {
                    DefId = id,
                    SizeX = w,
                    SizeY = h,
                    BaseLevel = lvl,
                    MaxHp = Mathf.Max(1, bj.hp),
                    IsHQ = bj.isHQ,
                    IsWarehouse = bj.isWarehouse,
                    IsProducer = bj.isProducer,
                    IsHouse = bj.isHouse,
                    IsForge = bj.isForge,
                    IsArmory = bj.isArmory,
                    IsTower = bj.isTower,
                    WorkRoles = ParseWorkRolesOrDerive(bj),
                    CapWood = ToCaps(bj.capWood),
                    CapFood = ToCaps(bj.capFood),
                    CapStone = ToCaps(bj.capStone),
                    CapIron = ToCaps(bj.capIron),
                    CapAmmo = ToCaps(bj.capAmmo),
                    BuildCostsL1 = ToCosts(bj.buildCostsL1, ctx: $"Building '{id}' buildCostsL1"),
                    BuildChunksL1 = Mathf.Max(0, bj.buildChunksL1)
                };

                _buildings[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Buildings: {added} (TextAsset: {ta.name})");
        }
    }
}
