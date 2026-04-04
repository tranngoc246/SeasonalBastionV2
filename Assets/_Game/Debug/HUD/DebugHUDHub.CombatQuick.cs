using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugHUDHub
    {
        private void Quick_DrainAllTowersToZero()
        {
            if (_gs?.WorldState == null) return;
            var towers = _gs.WorldState.Towers;
            if (towers == null) return;

            _towerIdsTmp.Clear();
            foreach (var tid in towers.Ids) _towerIdsTmp.Add(tid);

            int changed = 0;
            for (int i = 0; i < _towerIdsTmp.Count; i++)
            {
                var tid = _towerIdsTmp[i];
                if (!towers.Exists(tid)) continue;
                var ts = towers.Get(tid);
                ts.Ammo = 0;
                towers.Set(tid, ts);
                _gs.AmmoService?.NotifyTowerAmmoChanged(ts.Id, ts.Ammo, ts.AmmoCap);
                changed++;
            }

            _gs.NotificationService?.Push("debug_ammo_drain", "Debug", $"Drained {changed} towers to 0.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_RefillAllTowers()
        {
            if (_gs?.WorldState == null) return;
            var towers = _gs.WorldState.Towers;
            if (towers == null) return;

            _towerIdsTmp.Clear();
            foreach (var tid in towers.Ids) _towerIdsTmp.Add(tid);

            int changed = 0;
            for (int i = 0; i < _towerIdsTmp.Count; i++)
            {
                var tid = _towerIdsTmp[i];
                if (!towers.Exists(tid)) continue;
                var ts = towers.Get(tid);
                ts.Ammo = ts.AmmoCap;
                towers.Set(tid, ts);
                _gs.AmmoService?.NotifyTowerAmmoChanged(ts.Id, ts.Ammo, ts.AmmoCap);
                changed++;
            }

            _gs.NotificationService?.Push("debug_ammo_refill", "Debug", $"Refilled {changed} towers.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_SpawnEnemy(string enemyDefId, int laneId, int count)
        {
            if (_gs?.WorldState == null || _gs.DataRegistry == null || _gs.RunStartRuntime == null) return;
            if (_gs.RunStartRuntime.Lanes == null || !_gs.RunStartRuntime.Lanes.TryGetValue(laneId, out var lane))
            {
                Debug.LogWarning($"[DebugHUDHub] Lane {laneId} not found.");
                return;
            }

            EnemyDef def;
            try { def = _gs.DataRegistry.GetEnemy(enemyDefId); }
            catch
            {
                Debug.LogWarning($"[DebugHUDHub] EnemyDef not found: '{enemyDefId}'");
                return;
            }

            if (_gs.CombatService is CombatService combat && !combat.IsActive)
                combat.OnDefendPhaseStarted();

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                var st = new EnemyState
                {
                    DefId = enemyDefId,
                    Cell = lane.StartCell,
                    Hp = def.MaxHp,
                    Lane = laneId,
                    MoveProgress01 = 0f
                };

                var id = _gs.WorldState.Enemies.Create(st);
                st.Id = id;
                _gs.WorldState.Enemies.Set(id, st);
                spawned++;
            }

            string phaseNote = _gs.RunClock != null ? $" phase={_gs.RunClock.CurrentPhase}" : string.Empty;
            _gs.NotificationService?.Push("debug_spawn_enemy", "Debug", $"Spawned {spawned} '{enemyDefId}' lane {laneId}; combat auto-enabled.{phaseNote}", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_KillAllEnemies()
        {
            if (_gs?.WorldState == null) return;
            var enemies = _gs.WorldState.Enemies;
            if (enemies == null) return;

            _enemyIdsTmp.Clear();
            foreach (var eid in enemies.Ids) _enemyIdsTmp.Add(eid);

            int killed = 0;
            for (int i = _enemyIdsTmp.Count - 1; i >= 0; i--)
            {
                var eid = _enemyIdsTmp[i];
                if (!enemies.Exists(eid)) continue;
                enemies.Destroy(eid);
                killed++;
            }

            _gs.NotificationService?.Push("debug_kill_enemies", "Debug", $"Killed {killed} enemies.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void DrawEnemyPresetButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Swarmling", GUILayout.Width(100))) _quickEnemyDefId = "Swarmling";
            if (GUILayout.Button("Raider", GUILayout.Width(100))) _quickEnemyDefId = "Raider";
            if (GUILayout.Button("Bruiser", GUILayout.Width(100))) _quickEnemyDefId = "Bruiser";
            if (GUILayout.Button("Archer", GUILayout.Width(100))) _quickEnemyDefId = "Archer";
            if (GUILayout.Button("Sapper", GUILayout.Width(100))) _quickEnemyDefId = "Sapper";
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SiegeBrute", GUILayout.Width(100))) _quickEnemyDefId = "SiegeBrute";
            GUILayout.Label("Selected: " + _quickEnemyDefId);
            GUILayout.EndHorizontal();
        }

        private void DrawQuickLaneButtons()
        {
            GUILayout.BeginHorizontal();
            bool any = false;
            if (_gs?.RunStartRuntime?.Lanes != null)
            {
                foreach (var kv in _gs.RunStartRuntime.Lanes)
                {
                    any = true;
                    int laneId = kv.Key;
                    if (GUILayout.Button($"Lane {laneId}", GUILayout.Width(85)))
                        Quick_SpawnEnemy(_quickEnemyDefId, laneId, _quickSpawnCount);
                }
            }

            if (!any)
                GUILayout.Label("No lane cache; use manual lane in Advanced.");
            GUILayout.EndHorizontal();
        }

        private bool TryResolveTowerForBuilding(BuildingId bid, BuildingState bs, out TowerId tid, out TowerState ts)
        {
            tid = default;
            ts = default;
            if (_gs?.WorldState?.Towers == null || _gs.DataRegistry == null) return false;
            if (bid.Value == 0) return false;

            int bw = 1, bh = 1;
            try
            {
                if (_gs.DataRegistry.TryGetBuilding(bs.DefId, out var def) && def != null)
                {
                    bw = Mathf.Max(1, def.SizeX);
                    bh = Mathf.Max(1, def.SizeY);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DebugHUDHub] Failed to resolve linked tower footprint for building '{bs.DefId}' ({bid.Value}): {ex}");
            }

            var towerCell = new CellPos(bs.Anchor.X + (bw / 2), bs.Anchor.Y + (bh / 2));
            foreach (var id in _gs.WorldState.Towers.Ids)
            {
                if (!_gs.WorldState.Towers.Exists(id)) continue;
                var t = _gs.WorldState.Towers.Get(id);
                if (t.Cell.X == towerCell.X && t.Cell.Y == towerCell.Y)
                {
                    tid = id;
                    ts = t;
                    return true;
                }
            }

            return false;
        }

        private void Quick_DrainTowerUnderMouse()
        {
            if (!TryFindBuildingFromHover(out var bid, out var bs) || !TryResolveTowerForBuilding(bid, bs, out var tid, out var ts))
            {
                _gs?.NotificationService?.Push("dbg_tower_drain_none", "Debug", "Current building target has no linked tower.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            int amount = 30;
            if (!int.TryParse(_quickAmmoDrainAmtStr, out amount) || amount <= 0) amount = 30;
            ts.Ammo = Mathf.Max(0, ts.Ammo - amount);
            _gs.WorldState.Towers.Set(tid, ts);

            try
            {
                bs.Ammo = ts.Ammo;
                _gs.WorldState.Buildings.Set(bid, bs);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DebugHUDHub] Failed to mirror drained ammo to building {bid.Value}: {ex}");
            }

            _gs.AmmoService?.NotifyTowerAmmoChanged(tid, ts.Ammo, ts.AmmoCap);
            _gs.NotificationService?.Push("dbg_tower_drain_one", "Debug", $"{bs.DefId} #{bid.Value} ammo -> {ts.Ammo}/{ts.AmmoCap}", NotificationSeverity.Info, default, 0.1f, true);
        }

        private void Quick_RefillTowerUnderMouse()
        {
            if (!TryFindBuildingFromHover(out var bid, out var bs) || !TryResolveTowerForBuilding(bid, bs, out var tid, out var ts))
            {
                _gs?.NotificationService?.Push("dbg_tower_refill_none", "Debug", "Current building target has no linked tower.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            ts.Ammo = ts.AmmoCap;
            _gs.WorldState.Towers.Set(tid, ts);

            try
            {
                bs.Ammo = ts.Ammo;
                _gs.WorldState.Buildings.Set(bid, bs);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DebugHUDHub] Failed to mirror refilled ammo to building {bid.Value}: {ex}");
            }

            _gs.AmmoService?.NotifyTowerAmmoChanged(tid, ts.Ammo, ts.AmmoCap);
            _gs.NotificationService?.Push("dbg_tower_refill_one", "Debug", $"{bs.DefId} #{bid.Value} ammo -> {ts.Ammo}/{ts.AmmoCap}", NotificationSeverity.Info, default, 0.1f, true);
        }
    }
}
