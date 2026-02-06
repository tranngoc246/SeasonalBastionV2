using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day30 — TowerCombatSystem: target + fire + consume ammo
    /// Deterministic:
    /// - iterate towers by TowerId asc
    /// - target: nearest in range, tie by EnemyId
    /// Timing:
    /// - respects RunClock.TimeScale (pause => no fire)
    /// Ammo:
    /// - consume AmmoPerShot; if ammo < AmmoPerShot => no fire
    /// - updates TowerState.Ammo and mirrors into BuildingState.Ammo (for UI/debug consistency)
    /// </summary>
    public sealed class TowerCombatSystem
    {
        private readonly GameServices _s;

        // Per-tower cooldown seconds (sim-time)
        private readonly Dictionary<int, float> _cdByTower = new();

        // temp deterministic id lists
        private readonly List<TowerId> _towerIds = new(64);
        private readonly List<EnemyId> _enemyIds = new(128);
        private readonly List<BuildingId> _buildingIds = new(64);

        public TowerCombatSystem(GameServices s) { _s = s; }

        public void Tick(float dt)
        {
            var w = _s.WorldState;
            var data = _s.DataRegistry;
            var clock = _s.RunClock;

            if (w == null || data == null || clock == null) return;
            if (w.Towers == null || w.Enemies == null) return;

            float ts = clock.TimeScale;
            if (ts <= 0f) return;

            float simDt = dt * ts;
            if (simDt <= 0f) return;

            if (w.Towers.Count <= 0 || w.Enemies.Count <= 0)
            {
                // still decay cooldowns (optional) - but keep simple
                return;
            }

            // Deterministic tower order
            _towerIds.Clear();
            foreach (var tid in w.Towers.Ids) _towerIds.Add(tid);
            _towerIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Deterministic enemy order once per tick
            _enemyIds.Clear();
            foreach (var eid in w.Enemies.Ids) _enemyIds.Add(eid);
            _enemyIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < _towerIds.Count; i++)
            {
                var tid = _towerIds[i];
                if (!w.Towers.Exists(tid)) continue;

                var t = w.Towers.Get(tid);

                // cooldown update
                float cd = 0f;
                if (_cdByTower.TryGetValue(tid.Value, out var c0)) cd = c0;
                cd -= simDt;
                if (cd < 0f) cd = 0f;

                // Resolve TowerDef (prefer building at same anchor, else fallback by known id)
                if (!TryResolveTowerDefId(t.Cell, out var towerDefId))
                {
                    // fallback: common v0.1
                    towerDefId = "bld_tower_arrow_t1";
                }

                TowerDef tdef;
                try { tdef = data.GetTower(towerDefId); }
                catch { continue; }

                if (tdef == null) continue;

                // ammo gate
                int ammoPerShot = Math.Max(1, tdef.AmmoPerShot);
                if (t.Ammo < ammoPerShot)
                {
                    _cdByTower[tid.Value] = cd;
                    continue;
                }

                // rate-of-fire gate
                float rof = Math.Max(0f, tdef.Rof);
                if (rof <= 0.001f)
                {
                    _cdByTower[tid.Value] = cd;
                    continue;
                }

                float fireInterval = 1f / rof;
                if (cd > 0f)
                {
                    _cdByTower[tid.Value] = cd;
                    continue;
                }

                // select target
                if (!TryPickTargetInRange(t.Cell, tdef.Range, w, out var targetId))
                {
                    _cdByTower[tid.Value] = cd; // stays 0, so will fire immediately when a target appears
                    continue;
                }

                // apply damage
                var e = w.Enemies.Get(targetId);
                int dmg = Math.Max(0, tdef.Damage);
                if (dmg > 0)
                {
                    e.Hp -= dmg;
                    if (e.Hp <= 0)
                    {
                        _s.EventBus?.Publish(new EnemyKilledEvent(e.DefId, 1));
                        w.Enemies.Destroy(targetId);
                    }
                    else
                    {
                        w.Enemies.Set(targetId, e);
                    }
                }

                // consume ammo
                t.Ammo -= ammoPerShot;
                _s.EventBus?.Publish(new AmmoUsedEvent(ammoPerShot));
                if (t.Ammo < 0) t.Ammo = 0;
                w.Towers.Set(tid, t);

                // mirror ammo to building state for UI/debug convenience (same as RunStartApplier did at init)
                TryMirrorTowerAmmoToBuilding(t.Cell, t.Ammo);

                // reset cooldown
                cd = fireInterval;
                _cdByTower[tid.Value] = cd;
            }
        }

        // --------------------------
        // Target selection (deterministic)
        // --------------------------

        private bool TryPickTargetInRange(CellPos towerCell, float range, IWorldState w, out EnemyId best)
        {
            best = default;
            if (w == null || w.Enemies == null) return false;
            if (_enemyIds.Count == 0) return false;

            float r = range;
            if (r < 0f) r = 0f;
            float r2 = r * r;

            int bestDist2 = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < _enemyIds.Count; i++)
            {
                var eid = _enemyIds[i];
                if (!w.Enemies.Exists(eid)) continue;

                var e = w.Enemies.Get(eid);
                if (e.Hp <= 0) continue;

                int dx = e.Cell.X - towerCell.X;
                int dy = e.Cell.Y - towerCell.Y;
                int d2 = dx * dx + dy * dy;

                // range check
                if (d2 > r2) continue;

                int idv = eid.Value;

                if (d2 < bestDist2 || (d2 == bestDist2 && idv < bestId))
                {
                    bestDist2 = d2;
                    bestId = idv;
                    best = eid;
                }
            }

            return best.Value != 0;
        }

        // --------------------------
        // TowerDefId resolution & building mirror
        // --------------------------

        private bool TryResolveTowerDefId(CellPos towerCell, out string towerDefId)
        {
            towerDefId = null;

            var w = _s.WorldState;
            var data = _s.DataRegistry;
            if (w == null || w.Buildings == null || data == null) return false;

            // deterministic scan buildings by id asc
            _buildingIds.Clear();
            foreach (var bid in w.Buildings.Ids) _buildingIds.Add(bid);
            _buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!w.Buildings.Exists(bid)) continue;

                var b = w.Buildings.Get(bid);
                if (!b.IsConstructed) continue;

                BuildingDef bdef = null;
                try { bdef = data.GetBuilding(b.DefId); } catch { }

                if (bdef == null || !bdef.IsTower) continue;

                // TowerState.Cell là CENTER cell. Match bằng footprint.
                if (!FootprintContainsCell(b.Anchor, bdef.SizeX, bdef.SizeY, towerCell))
                    continue;

                towerDefId = b.DefId;
                return true;
            }

            return false;
        }

        private void TryMirrorTowerAmmoToBuilding(CellPos towerCell, int ammo)
        {
            var w = _s.WorldState;
            var data = _s.DataRegistry;
            if (w == null || w.Buildings == null || data == null) return;

            // deterministic scan buildings by id asc
            _buildingIds.Clear();
            foreach (var bid in w.Buildings.Ids) _buildingIds.Add(bid);
            _buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!w.Buildings.Exists(bid)) continue;

                var b = w.Buildings.Get(bid);
                if (!b.IsConstructed) continue;

                BuildingDef bdef = null;
                try { bdef = data.GetBuilding(b.DefId); } catch { }

                if (bdef == null || !bdef.IsTower) continue;

                if (!FootprintContainsCell(b.Anchor, bdef.SizeX, bdef.SizeY, towerCell))
                    continue;

                b.Ammo = ammo;
                w.Buildings.Set(bid, b);
                return;
            }
        }

        private static bool FootprintContainsCell(CellPos anchor, int sizeX, int sizeY, CellPos c)
        {
            int w = sizeX <= 0 ? 1 : sizeX;
            int h = sizeY <= 0 ? 1 : sizeY;

            return c.X >= anchor.X && c.X < (anchor.X + w)
                && c.Y >= anchor.Y && c.Y < (anchor.Y + h);
        }
    }
}
