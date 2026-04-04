using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugHUDHub
    {
        private void DrawCurrentHoverTargetInfo()
        {
            if (_gs == null)
                return;

            string buildingLabel = "Building: (none)";
            string towerLabel = "Tower: (none)";
            string lockLabel = _lockedTargetBuilding.Value != 0 ? $"Lock: #{_lockedTargetBuilding.Value}" : "Lock: hover only";

            if (TryFindBuildingFromHover(out var bid, out var bs))
            {
                EnsureHp(ref bs);
                buildingLabel = $"Building: {bs.DefId} #{bid.Value}  HP {bs.HP}/{bs.MaxHP}";

                if (TryResolveTowerForBuilding(bid, bs, out var tid, out var ts))
                    towerLabel = $"Tower: #{tid.Value}  Ammo {ts.Ammo}/{ts.AmmoCap}  HP {ts.Hp}/{ts.HpMax}";
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("CURRENT TARGET");
            GUILayout.Label(buildingLabel);
            GUILayout.Label(towerLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label(lockLabel, GUILayout.Width(180));
            if (GUILayout.Button("Clear Lock", GUILayout.Width(100))) _lockedTargetBuilding = default;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void Quick_CompleteAllBuildSites()
        {
            if (_gs?.WorldState == null) return;
            var sites = _gs.WorldState.Sites;
            if (sites == null) return;

            _siteIdsTmp.Clear();
            foreach (var sid in sites.Ids) _siteIdsTmp.Add(sid);

            int changed = 0;
            for (int i = 0; i < _siteIdsTmp.Count; i++)
            {
                var sid = _siteIdsTmp[i];
                if (!sites.Exists(sid)) continue;
                var st = sites.Get(sid);
                if (!st.IsActive) continue;

                st.RemainingCosts?.Clear();
                st.RemainingCosts = null;
                if (st.WorkSecondsTotal <= 0f) st.WorkSecondsTotal = 0.1f;
                st.WorkSecondsDone = st.WorkSecondsTotal;

                sites.Set(sid, st);
                changed++;
            }

            if (_gs.BuildOrderService is BuildOrderService bos)
                bos.Tick(0.0001f);

            _gs.NotificationService?.Push("debug_build_instant", "Debug", $"Instant completed {changed} build sites.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_CompleteCurrentSiteUnderMouse()
        {
            if (_gs?.WorldState == null || _gs.GridMap == null) return;
            if (!MouseCellSharedState.HasValue)
            {
                _gs.NotificationService?.Push("debug_site_none", "Debug", "No hover cell (MouseCellSharedState).", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            if (!TryFindActiveSiteFromHover(out var sid, out var st))
            {
                _gs.NotificationService?.Push("debug_site_notfound", "Debug", "No active build/upgrade site found under mouse.", NotificationSeverity.Info, default, 0.2f, true);
                return;
            }

            ForceCompleteSite(sid, st);
        }

        private bool TryFindActiveSiteFromHover(out SiteId sid, out BuildSiteState site)
        {
            sid = default;
            site = default;

            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var s = _gs.WorldState.Sites.Get(occ.Site);
                if (s.IsActive)
                {
                    sid = occ.Site;
                    site = s;
                    return true;
                }
            }

            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0)
            {
                foreach (var x in _gs.WorldState.Sites.Ids)
                {
                    if (!_gs.WorldState.Sites.Exists(x)) continue;
                    var s = _gs.WorldState.Sites.Get(x);
                    if (!s.IsActive) continue;
                    if (s.TargetBuilding.Value == occ.Building.Value)
                    {
                        sid = x;
                        site = s;
                        return true;
                    }
                }
            }

            return false;
        }

        private void ForceCompleteSite(SiteId sid, BuildSiteState st)
        {
            var sites = _gs.WorldState.Sites;
            if (!sites.Exists(sid)) return;

            st.RemainingCosts?.Clear();
            st.RemainingCosts = null;
            if (st.WorkSecondsTotal <= 0f) st.WorkSecondsTotal = 0.1f;
            st.WorkSecondsDone = st.WorkSecondsTotal;
            sites.Set(sid, st);

            if (_gs.BuildOrderService is BuildOrderService bos)
                bos.Tick(0.0001f);

            _gs.NotificationService?.Push("debug_site_complete_one", "Debug",
                $"Completed site {sid.Value} (kind={(st.IsUpgrade ? "Upgrade" : "PlaceNew")}).",
                NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_DamageBuildingUnderMouse(int damage)
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_damage_none", "Debug", "No building under mouse (need MouseCellSharedState).",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            EnsureHp(ref bs);
            bs.HP -= Mathf.Max(1, damage);
            if (bs.HP < 0) bs.HP = 0;
            _gs.WorldState.Buildings.Set(bid, bs);

            _gs.NotificationService?.Push("dbg_damage_ok", "Debug",
                $"Damaged {bs.DefId} #{bid.Value}: {bs.HP}/{bs.MaxHP}",
                NotificationSeverity.Warning, default, 0.1f, true);
        }

        private void Quick_SetHpUnderMouse(int hp)
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_sethp_none", "Debug", "No building under mouse.",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            EnsureHp(ref bs);
            bs.HP = Mathf.Clamp(hp, 0, bs.MaxHP);
            _gs.WorldState.Buildings.Set(bid, bs);

            _gs.NotificationService?.Push("dbg_sethp_ok", "Debug",
                $"Set HP {bs.DefId} #{bid.Value}: {bs.HP}/{bs.MaxHP}",
                NotificationSeverity.Info, default, 0.1f, true);
        }

        private void Quick_HealBuildingUnderMouse()
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_heal_none", "Debug", "No building under mouse.",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            EnsureHp(ref bs);
            bs.HP = bs.MaxHP;
            _gs.WorldState.Buildings.Set(bid, bs);

            _gs.NotificationService?.Push("dbg_heal_ok", "Debug",
                $"Healed {bs.DefId} #{bid.Value}: {bs.HP}/{bs.MaxHP}",
                NotificationSeverity.Info, default, 0.1f, true);
        }

        private void Quick_CreateRepairOrderUnderMouse()
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_repair_none", "Debug", "No building under mouse.",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            int id = _gs.BuildOrderService.CreateRepairOrder(bid);
            if (id > 0)
                _gs.NotificationService?.Push("dbg_repair_ok", "Debug", $"Repair order #{id} created for #{bid.Value}",
                    NotificationSeverity.Info, default, 0.1f, true);
            else
                _gs.NotificationService?.Push("dbg_repair_fail", "Debug", "Repair order not created (full HP / invalid / duplicate).",
                    NotificationSeverity.Warning, default, 0.25f, true);
        }

        private void Quick_CompleteHoveredBuildingIfSiteOrRepairTarget()
        {
            if (TryFindActiveSiteFromHover(out _, out _))
            {
                Quick_CompleteCurrentSiteUnderMouse();
                return;
            }

            if (!TryFindBuildingFromHover(out var bid, out var bs))
                return;

            EnsureHp(ref bs);
            bs.HP = bs.MaxHP;
            _gs.WorldState.Buildings.Set(bid, bs);
            _gs.NotificationService?.Push("dbg_finish_hovered", "Debug", $"Finished/Healed hovered building #{bid.Value}.", NotificationSeverity.Info, default, 0.15f, true);
        }
    }
}
