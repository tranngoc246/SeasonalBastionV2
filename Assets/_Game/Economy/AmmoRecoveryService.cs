using System.Collections.Generic;

namespace SeasonalBastion
{
    internal sealed class AmmoRecoveryService
    {
        private readonly AmmoService _owner;

        internal AmmoRecoveryService(AmmoService owner)
        {
            _owner = owner;
        }

        internal void LogPotentialResupplyDeadlock()
        {
            if (_owner.Debug_TowersWithoutAmmo <= 0)
            {
                _owner.TowerDeadlockLogged.Clear();
                return;
            }

            if (_owner.Debug_ArmoryAvailableAmmo <= 0)
                return;

            if (_owner.Debug_ActiveResupplyJobs > 0)
            {
                _owner.TowerDeadlockLogged.Clear();
                return;
            }

            int eligibleRequests = _owner.CountEligibleResupplyRequests();
            if (eligibleRequests <= 0)
                return;

            LogDeadlockForRequests(_owner.UrgentRequests);
            LogDeadlockForRequests(_owner.NormalRequests);
        }

        internal void MaybeRequeueTowerAmmoRequest(SeasonalBastion.Contracts.TowerId tower)
        {
            if (tower.Value == 0) return;
            if (_owner.Services.WorldState == null || !_owner.Services.WorldState.Towers.Exists(tower)) return;

            var ts = _owner.Services.WorldState.Towers.Get(tower);
            int cap = ts.AmmoCap;
            if (cap <= 0) return;

            int cur = ts.Ammo;
            int need = cap - cur;
            if (need <= 0) return;

            _owner.ResetRequestStateForTower(tower.Value);

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

        private void LogDeadlockForRequests(List<AmmoRequest> list)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                int tid = list[i].Tower.Value;
                if (tid == 0) continue;
                if (_owner.TowerDeadlockLogged.Add(tid))
                    Log.E($"[Ammo] Armory has ammo but no job created. tower={tid} totalTowers={_owner.Debug_TotalTowers} emptyTowers={_owner.Debug_TowersWithoutAmmo} activeResupplyJobs={_owner.Debug_ActiveResupplyJobs} armoryAmmo={_owner.Debug_ArmoryAvailableAmmo} pending={_owner.PendingRequests}");
            }
        }
    }
}
