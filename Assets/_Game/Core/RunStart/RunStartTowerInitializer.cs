using System;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartTowerInitializer
    {
        internal const string TowerArrowDefId = "bld_tower_arrow_t1";

        internal static bool IsArrowTowerLike(string defId)
        {
            return !string.IsNullOrEmpty(defId) && defId.Equals(TowerArrowDefId, StringComparison.OrdinalIgnoreCase);
        }

        internal static void TryCreateArrowTowerState(GameServices s, in BuildingState building, InitialBuildingDto b)
        {
            if (s.WorldState?.Towers == null) return;

            int hpMax = 260;
            int ammoMax = 90;

            if (s.DataRegistry.TryGetTower(TowerArrowDefId, out var tdef) && tdef != null)
            {
                hpMax = Mathf.Max(1, tdef.MaxHp);
                ammoMax = Mathf.Max(0, tdef.AmmoMax);
            }

            int ammo = ResolveAmmo(ammoMax, b);

            int bw = 1, bh = 1;
            if (s.DataRegistry.TryGetBuilding(building.DefId, out var bdef) && bdef != null)
            {
                bw = Math.Max(1, bdef.SizeX);
                bh = Math.Max(1, bdef.SizeY);
            }

            var towerCell = new CellPos(building.Anchor.X + (bw / 2), building.Anchor.Y + (bh / 2));
            var st = new TowerState { Cell = towerCell, Hp = hpMax, HpMax = hpMax, Ammo = ammo, AmmoCap = ammoMax };

            var id = s.WorldState.Towers.Create(st);
            st.Id = id;
            s.WorldState.Towers.Set(id, st);

            try
            {
                var bs = s.WorldState.Buildings.Get(building.Id);
                bs.Ammo = ammo;
                s.WorldState.Buildings.Set(building.Id, bs);
            }
            catch { }
        }

        internal static void TryCreateArrowTowerStandalone(GameServices s, CellPos desiredCell, InitialBuildingDto b)
        {
            if (s == null || s.WorldState?.Towers == null) return;
            if (!TryPickValidTowerCell(s, desiredCell, out var finalCell)) return;

            int hpMax = 260;
            int ammoMax = 90;

            try
            {
                var tdef = s.DataRegistry?.GetTower(TowerArrowDefId);
                if (tdef != null)
                {
                    hpMax = Mathf.Max(1, tdef.MaxHp);
                    ammoMax = Mathf.Max(0, tdef.AmmoMax);
                }
            }
            catch { }

            int ammo = ResolveAmmo(ammoMax, b);
            var st = new TowerState { Cell = finalCell, Hp = hpMax, HpMax = hpMax, Ammo = ammo, AmmoCap = ammoMax };

            var id = s.WorldState.Towers.Create(st);
            st.Id = id;
            s.WorldState.Towers.Set(id, st);

            try { s.WorldIndex?.RebuildAll(); } catch { }
        }

        private static int ResolveAmmo(int ammoMax, InitialBuildingDto b)
        {
            int ammo = ammoMax;
            if (b != null && b.initialStateOverrides != null)
            {
                if (!string.IsNullOrEmpty(b.initialStateOverrides.ammo)
                    && b.initialStateOverrides.ammo.Equals("FULL", StringComparison.OrdinalIgnoreCase))
                {
                    ammo = ammoMax;
                }
                else if (b.initialStateOverrides.ammoPercent > 0f)
                {
                    ammo = ClampToInt(b.initialStateOverrides.ammoPercent * ammoMax, 0, ammoMax);
                }
            }
            return ammo;
        }

        private static bool TryPickValidTowerCell(GameServices s, CellPos desired, out CellPos finalCell)
        {
            finalCell = desired;
            if (s?.GridMap == null) return false;

            bool IsOk(CellPos c)
            {
                if (!s.GridMap.IsInside(c)) return false;
                var occ = s.GridMap.Get(c);
                return occ.Kind != CellOccupancyKind.Building && occ.Kind != CellOccupancyKind.Site;
            }

            if (IsOk(desired)) { finalCell = desired; return true; }

            const int maxR = 8;
            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int dx1 = r - Mathf.Abs(dy);
                    int dx2 = -dx1;

                    var c1 = new CellPos(desired.X + dx1, desired.Y + dy);
                    if (IsOk(c1)) { finalCell = c1; return true; }

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(desired.X + dx2, desired.Y + dy);
                        if (IsOk(c2)) { finalCell = c2; return true; }
                    }
                }
            }

            return false;
        }

        private static int ClampToInt(float v, int min, int max)
        {
            int x = (int)Mathf.Round(v);
            if (x < min) return min;
            if (x > max) return max;
            return x;
        }
    }
}
