using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoMonitorPolicy
    {
        private readonly INotificationService _notificationService;
        private readonly ICombatService _combatService;
        private readonly AmmoCooldownManager _cooldownManager;
        private readonly AmmoTowerStateTracker _towerStateTracker;
        private readonly AmmoRecoveryService _recoveryService;
        private readonly System.Action<AmmoRequest> _enqueueRequest;
        private readonly System.Func<JobId, bool> _hasActiveResupplyJob;
        private readonly System.Func<int> _getLowAmmoPercent;
        private readonly System.Func<float> _getNotifyCooldownLow;
        private readonly System.Func<float> _getNotifyCooldownEmpty;
        private readonly System.Func<float> _getSimTime;
        private readonly System.Func<bool> _debugAmmoLogs;

        public AmmoMonitorPolicy(
            INotificationService notificationService,
            ICombatService combatService,
            AmmoCooldownManager cooldownManager,
            AmmoTowerStateTracker towerStateTracker,
            AmmoRecoveryService recoveryService,
            System.Action<AmmoRequest> enqueueRequest,
            System.Func<JobId, bool> hasActiveResupplyJob,
            System.Func<int> getLowAmmoPercent,
            System.Func<float> getNotifyCooldownLow,
            System.Func<float> getNotifyCooldownEmpty,
            System.Func<float> getSimTime,
            System.Func<bool> debugAmmoLogs)
        {
            _notificationService = notificationService;
            _combatService = combatService;
            _cooldownManager = cooldownManager;
            _towerStateTracker = towerStateTracker;
            _recoveryService = recoveryService;
            _enqueueRequest = enqueueRequest;
            _hasActiveResupplyJob = hasActiveResupplyJob;
            _getLowAmmoPercent = getLowAmmoPercent;
            _getNotifyCooldownLow = getNotifyCooldownLow;
            _getNotifyCooldownEmpty = getNotifyCooldownEmpty;
            _getSimTime = getSimTime;
            _debugAmmoLogs = debugAmmoLogs;
        }

        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max, JobId? inFlightResupplyJob)
        {
            if (tower.Value == 0 || max <= 0)
                return;

            if (inFlightResupplyJob.HasValue && _hasActiveResupplyJob(inFlightResupplyJob.Value))
                return;

            int towerId = tower.Value;
            int threshold = GetLowAmmoThreshold(max);
            byte stateNow = current <= 0 ? (byte)2 : current <= threshold ? (byte)1 : (byte)0;

            _towerStateTracker.SetState(towerId, stateNow);
            PushNotificationIfNeeded(towerId, stateNow);

            if (_debugAmmoLogs() && stateNow != 0 && _towerStateTracker.TryMarkNeedLogged(towerId))
            {
                Log.E($"[Ammo] tower {towerId} requests resupply ammo={current}/{max} state={(stateNow == 2 ? "empty" : "low")} thr={threshold}");
                _recoveryService.ClearNeedLogs(towerId);
            }

            if (stateNow == 0)
            {
                _towerStateTracker.ClearNeedLogged(towerId);
                _recoveryService.ClearNeedLogs(towerId);
                return;
            }

            var priority = stateNow == 2 ? AmmoRequestPriority.Urgent : AmmoRequestPriority.Normal;
            if (!_cooldownManager.TryConsumeRequestCooldown(tower, priority))
                return;

            int need = max - current;
            if (need <= 0)
                return;

            _enqueueRequest(new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = priority,
                CreatedAt = _getSimTime()
            });
        }

        internal int GetLowAmmoThreshold(int max)
        {
            int threshold = (max * _getLowAmmoPercent() + 99) / 100;
            if (threshold < 1) threshold = 1;
            return threshold;
        }

        private void PushNotificationIfNeeded(int towerId, byte stateNow)
        {
            if (_notificationService == null || stateNow == 0)
                return;

            bool combatActive = _combatService != null && _combatService.IsActive;
            if (stateNow == 2)
            {
                _notificationService.Push(
                    key: $"TowerAmmo_Empty_{towerId}",
                    title: "Tower hết ammo",
                    body: combatActive
                        ? "Một tower đã hết ammo trong lúc đang phòng thủ. Cần tiếp tế ngay."
                        : "Một tower đã hết ammo. Hãy chuẩn bị tiếp tế trước đợt tiếp theo.",
                    severity: combatActive ? NotificationSeverity.Error : NotificationSeverity.Warning,
                    payload: default,
                    cooldownSeconds: _getNotifyCooldownEmpty(),
                    dedupeByKey: true);
                return;
            }

            _notificationService.Push(
                key: $"TowerAmmo_Low_{towerId}",
                title: "Tower sắp cạn ammo",
                body: combatActive
                    ? "Một tower đang gần hết ammo trong lúc phòng thủ. Hãy chuẩn bị tiếp tế."
                    : "Một tower đang gần hết ammo. Nên bổ sung trước khi wave tới.",
                severity: combatActive ? NotificationSeverity.Warning : NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: _getNotifyCooldownLow(),
                dedupeByKey: true);
        }
    }
}
