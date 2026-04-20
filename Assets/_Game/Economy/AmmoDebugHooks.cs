using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoDebugHooks
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;

        internal AmmoDebugHooks(AmmoService owner)
        {
            _owner = owner;
            _s = owner.Services;
        }

        internal void Tick(float dt)
        {
            if (!_owner.DevHook_Enabled) return;

            _owner.DevHookTimer -= dt;
            if (_owner.DevHookTimer > 0f) return;

            _owner.DevHookTimer += (_owner.DevHook_ShotInterval > 0f ? _owner.DevHook_ShotInterval : 0.5f);

            var towers = _s.WorldIndex.Towers;
            if (towers == null) return;

            for (int i = 0; i < towers.Count; i++)
            {
                var tid = towers[i];
                if (!_s.WorldState.Towers.Exists(tid)) continue;

                var ts = _s.WorldState.Towers.Get(tid);
                if (ts.AmmoCap <= 0) continue;
                if (ts.Ammo <= 0) continue;

                int dec = _owner.DevHook_AmmoPerShot <= 0 ? 1 : _owner.DevHook_AmmoPerShot;
                int newAmmo = ts.Ammo - dec;
                if (newAmmo < 0) newAmmo = 0;

                ts.Ammo = newAmmo;
                _s.WorldState.Towers.Set(tid, ts);

                _owner.RecordTowerSnapshot(tid, newAmmo, ts.AmmoCap);

                _owner.NotifyTowerAmmoChanged(tid, newAmmo, ts.AmmoCap);
                break;
            }
        }

        internal void EnsureTestTowerExistsIfNeeded()
        {
            if (!_owner.DevHook_Enabled) return;
            if (_s.WorldState == null || _s.WorldIndex == null || _s.GridMap == null || _s.DataRegistry == null) return;

            if (_s.WorldIndex.Towers != null && _s.WorldIndex.Towers.Count > 0)
                return;

            CellPos center = default;
            bool foundHQ = false;

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                var bs = _s.WorldState.Buildings.Get(bid);
                if (bs.IsConstructed && DefIdTierUtil.IsBase(bs.DefId, "bld_hq"))
                {
                    center = bs.Anchor;
                    foundHQ = true;
                    break;
                }
            }

            if (!foundHQ) center = new CellPos(0, 0);

            CellPos spawn = default;
            bool found = false;

            const int R = 12;
            for (int r = 1; r <= R && !found; r++)
            {
                for (int dx = -r; dx <= r && !found; dx++)
                {
                    var c1 = new CellPos(center.X + dx, center.Y + r);
                    var c2 = new CellPos(center.X + dx, center.Y - r);

                    if (_s.GridMap.IsInside(c1) && _s.GridMap.Get(c1).Kind == CellOccupancyKind.Empty) { spawn = c1; found = true; break; }
                    if (_s.GridMap.IsInside(c2) && _s.GridMap.Get(c2).Kind == CellOccupancyKind.Empty) { spawn = c2; found = true; break; }
                }

                for (int dy = -r + 1; dy <= r - 1 && !found; dy++)
                {
                    var c1 = new CellPos(center.X + r, center.Y + dy);
                    var c2 = new CellPos(center.X - r, center.Y + dy);

                    if (_s.GridMap.IsInside(c1) && _s.GridMap.Get(c1).Kind == CellOccupancyKind.Empty) { spawn = c1; found = true; break; }
                    if (_s.GridMap.IsInside(c2) && _s.GridMap.Get(c2).Kind == CellOccupancyKind.Empty) { spawn = c2; found = true; break; }
                }
            }

            if (!found) return;

            TowerDef def = null;
            _s.DataRegistry.TryGetTower("bld_tower_arrow_t1", out def);

            int ammoCap = def != null ? def.AmmoMax : 60;
            int hpMax = def != null ? def.MaxHp : 200;

            var st = new TowerState
            {
                Id = default,
                Cell = spawn,
                Ammo = ammoCap,
                AmmoCap = ammoCap,
                Hp = hpMax,
                HpMax = hpMax
            };

            var tid = _s.WorldState.Towers.Create(st);
            st.Id = tid;
            _s.WorldState.Towers.Set(tid, st);

            if (_s.WorldIndex is WorldIndexService worldIndex)
                worldIndex.OnTowerCreated(tid);
            else
                _s.WorldIndex.RebuildAll();

            _s.NotificationService?.Push(
                key: $"Dev_TowerSpawn_{tid.Value}",
                title: "Debug",
                body: "Đã tạo một tower thử nghiệm để kiểm tra flow ammo debug.",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 5f,
                dedupeByKey: true
            );
        }
    }
}
