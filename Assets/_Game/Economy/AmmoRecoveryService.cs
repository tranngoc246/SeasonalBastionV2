using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoRecoveryService
    {
        private readonly IWorldState _worldState;
        private readonly INotificationService _notificationService;
        private readonly AmmoCooldownManager _cooldowns;
        private readonly AmmoRequestQueue _requestQueue;
        private readonly System.Func<AmmoMetricsSnapshot> _getMetrics;
        private readonly System.Func<int> _getPendingRequests;
        private readonly System.Func<int> _countEligibleRequests;
        private readonly System.Func<List<AmmoRequest>> _getUrgentRequests;
        private readonly System.Func<List<AmmoRequest>> _getNormalRequests;
        private readonly System.Func<int, int> _getLowAmmoThreshold;
        private readonly System.Action<AmmoRequest> _enqueueRequest;
        private readonly System.Func<float> _getSimTime;
        private readonly System.Func<bool> _debugAmmoLogs;
        private readonly HashSet<int> _towerNoSourceLogged = new();
        private readonly HashSet<int> _towerNoJobLogged = new();
        private readonly HashSet<int> _towerDeadlockLogged = new();

        internal AmmoRecoveryService(
            IWorldState worldState,
            INotificationService notificationService,
            AmmoCooldownManager cooldowns,
            AmmoRequestQueue requestQueue,
            System.Func<AmmoMetricsSnapshot> getMetrics,
            System.Func<int> getPendingRequests,
            System.Func<int> countEligibleRequests,
            System.Func<List<AmmoRequest>> getUrgentRequests,
            System.Func<List<AmmoRequest>> getNormalRequests,
            System.Func<int, int> getLowAmmoThreshold,
            System.Action<AmmoRequest> enqueueRequest,
            System.Func<float> getSimTime,
            System.Func<bool> debugAmmoLogs)
        {
            _worldState = worldState;
            _notificationService = notificationService;
            _cooldowns = cooldowns;
            _requestQueue = requestQueue;
            _getMetrics = getMetrics;
            _getPendingRequests = getPendingRequests;
            _countEligibleRequests = countEligibleRequests;
            _getUrgentRequests = getUrgentRequests;
            _getNormalRequests = getNormalRequests;
            _getLowAmmoThreshold = getLowAmmoThreshold;
            _enqueueRequest = enqueueRequest;
            _getSimTime = getSimTime;
            _debugAmmoLogs = debugAmmoLogs;
        }

        internal HashSet<int> TowerNoSourceLogged => _towerNoSourceLogged;
        internal HashSet<int> TowerNoJobLogged => _towerNoJobLogged;
        internal HashSet<int> TowerDeadlockLogged => _towerDeadlockLogged;

        internal void LogPotentialResupplyDeadlock()
        {
            var metrics = _getMetrics();
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

            if (_countEligibleRequests() <= 0)
                return;

            LogDeadlockForRequests(_getUrgentRequests(), metrics);
            LogDeadlockForRequests(_getNormalRequests(), metrics);
        }

        internal void MaybeRequeueTowerAmmoRequest(TowerId tower)
        {
            if (tower.Value == 0 || _worldState == null || !_worldState.Towers.Exists(tower))
                return;

            var towerState = _worldState.Towers.Get(tower);
            int cap = towerState.AmmoCap;
            if (cap <= 0)
                return;

            int current = towerState.Ammo;
            int need = cap - current;
            if (need <= 0)
                return;

            ResetRequestStateForTower(tower.Value);

            int threshold = _getLowAmmoThreshold(cap);
            AmmoRequestPriority priority = current <= 0 ? AmmoRequestPriority.Urgent
                : (current <= threshold ? AmmoRequestPriority.Normal : (AmmoRequestPriority)(-1));
            if ((int)priority < 0)
                return;

            _enqueueRequest(new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = priority,
                CreatedAt = _getSimTime()
            });

            if (_debugAmmoLogs())
                Log.E($"[Ammo] resupply requeued tower={tower.Value} ammo={current}/{cap} priority={priority}");
        }

        internal void ResetRequestStateForTower(int towerId)
        {
            _cooldowns.ResetForTower(towerId);
            _requestQueue.RemovePendingForTower(towerId);
            _towerNoJobLogged.Remove(towerId);
            _towerDeadlockLogged.Remove(towerId);
        }

        internal void ClearTowerLogs(int towerId)
        {
            if (towerId == 0)
                return;

            _towerNoSourceLogged.Remove(towerId);
            _towerNoJobLogged.Remove(towerId);
            _towerDeadlockLogged.Remove(towerId);
        }

        internal void ClearNeedLogs(int towerId)
        {
            if (towerId == 0)
                return;

            _towerNoSourceLogged.Remove(towerId);
            _towerNoJobLogged.Remove(towerId);
        }

        internal void ClearAll()
        {
            _towerNoSourceLogged.Clear();
            _towerNoJobLogged.Clear();
            _towerDeadlockLogged.Clear();
        }

        private void LogDeadlockForRequests(List<AmmoRequest> list, AmmoMetricsSnapshot metrics)
        {
            if (list == null)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                int towerId = list[i].Tower.Value;
                if (towerId == 0)
                    continue;

                if (_towerDeadlockLogged.Add(towerId))
                {
                    Log.E($"[Ammo] Armory has ammo but no job created. tower={towerId} totalTowers={metrics.TotalTowers} emptyTowers={metrics.TowersWithoutAmmo} activeResupplyJobs={metrics.ActiveResupplyJobs} armoryAmmo={metrics.ArmoryAvailableAmmo} pending={_getPendingRequests()}");
                    _notificationService?.Push(
                        key: $"ammo.resupply.blocked.{towerId}",
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
