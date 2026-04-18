using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoRecoveryService
    {
        private readonly AmmoService _owner;
        private readonly HashSet<int> _towerNoSourceLogged = new();
        private readonly HashSet<int> _towerNoJobLogged = new();
        private readonly HashSet<int> _towerDeadlockLogged = new();

        internal AmmoRecoveryService(AmmoService owner)
        {
            _owner = owner;
        }

        internal HashSet<int> TowerNoSourceLogged => _towerNoSourceLogged;
        internal HashSet<int> TowerNoJobLogged => _towerNoJobLogged;
        internal HashSet<int> TowerDeadlockLogged => _towerDeadlockLogged;

        internal void LogPotentialResupplyDeadlock()
        {
            var metrics = _owner.CurrentMetrics;
            if (metrics.TowersWithoutAmmo <= 0)
            {
                _towerDeadlockLogged.Clear();
                return;
            }

            if (metrics.ArmoryAvailableAmmo <= 0)
                return;

            if (metrics.ActiveResupplyJobs > 0)
            {
                _towerDeadlockLogged.Clear();
                return;
            }

            int eligibleRequests = _owner.CountEligibleResupplyRequests();
            if (eligibleRequests <= 0)
                return;

            LogDeadlockForRequests(_owner.UrgentRequests);
            LogDeadlockForRequests(_owner.NormalRequests);
        }

        internal void MaybeRequeueTowerAmmoRequest(TowerId tower)
        {
            if (tower.Value == 0) return;
            if (_owner.Services.WorldState == null || !_owner.Services.WorldState.Towers.Exists(tower)) return;

            var ts = _owner.Services.WorldState.Towers.Get(tower);
            int cap = ts.AmmoCap;
            if (cap <= 0) return;

            int cur = ts.Ammo;
            int need = cap - cur;
            if (need <= 0) return;

            ResetRequestStateForTower(tower.Value);

            int thr = _owner.GetLowAmmoThresholdValue(cap);
            AmmoRequestPriority pri = cur <= 0 ? AmmoRequestPriority.Urgent
                : (cur <= thr ? AmmoRequestPriority.Normal : (AmmoRequestPriority)(-1));
            if ((int)pri < 0) return;

            _owner.EnqueueRequest(new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = pri,
                CreatedAt = _owner.SimTime
            });

            if (_owner.DebugAmmoLogsValue)
                Log.E($"[Ammo] resupply requeued tower={tower.Value} ammo={cur}/{cap} priority={pri}");
        }

        internal void ResetRequestStateForTower(int towerId)
        {
            _owner.Cooldowns.ResetForTower(towerId);
            _owner.Requests.RemovePendingForTower(towerId);
            _towerNoJobLogged.Remove(towerId);
            _towerDeadlockLogged.Remove(towerId);
        }

        internal void ClearTowerLogs(int towerId)
        {
            if (towerId == 0) return;
            _towerNoSourceLogged.Remove(towerId);
            _towerNoJobLogged.Remove(towerId);
            _towerDeadlockLogged.Remove(towerId);
        }

        internal void ClearNeedLogs(int towerId)
        {
            if (towerId == 0) return;
            _towerNoSourceLogged.Remove(towerId);
            _towerNoJobLogged.Remove(towerId);
        }

        internal void ClearAll()
        {
            _towerNoSourceLogged.Clear();
            _towerNoJobLogged.Clear();
            _towerDeadlockLogged.Clear();
        }

        private void LogDeadlockForRequests(List<AmmoRequest> list)
        {
            if (list == null) return;

            var metrics = _owner.CurrentMetrics;
            for (int i = 0; i < list.Count; i++)
            {
                int tid = list[i].Tower.Value;
                if (tid == 0) continue;
                if (_towerDeadlockLogged.Add(tid))
                {
                    Log.E($"[Ammo] Armory has ammo but no job created. tower={tid} totalTowers={metrics.TotalTowers} emptyTowers={metrics.TowersWithoutAmmo} activeResupplyJobs={metrics.ActiveResupplyJobs} armoryAmmo={metrics.ArmoryAvailableAmmo} pending={_owner.PendingRequests}");
                    _owner.Services.NotificationService?.Push(
                        key: $"ammo.resupply.blocked.{tid}",
                        title: "Tiếp tế ammo đang bị kẹt",
                        body: "Một tower cần ammo nhưng lệnh tiếp tế vẫn chưa thể bắt đầu.",
                        severity: NotificationSeverity.Warning,
                        payload: default,
                        cooldownSeconds: 12f,
                        dedupeByKey: true);
                }
            }
        }
    }
}
