using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class AmmoConfigProvider
    {
        private readonly GameServices _services;

        internal AmmoConfigProvider(GameServices services)
        {
            _services = services;
        }

        internal int GetInt(string section, string key, int fallback)
        {
            var balance = TryGetBalanceService();
            if (balance == null)
                return fallback;

            return (section, key) switch
            {
                ("ammoMonitor", "lowAmmoPct") => balance.AmmoLowPct,
                ("ammoSupply", "forgeTargetCrafts") => balance.ForgeTargetCrafts,
                _ => fallback,
            };
        }

        internal bool GetBool(string section, string key, bool fallback)
        {
            var balance = TryGetBalanceService();
            if (balance == null)
                return fallback;

            return (section, key) switch
            {
                ("ammoMonitor", "debugLogs") => fallback,
                _ => fallback,
            };
        }

        internal float GetFloat(string section, string key, float fallback)
        {
            var balance = TryGetBalanceService();
            if (balance == null)
                return fallback;

            return (section, key) switch
            {
                ("ammoMonitor", "reqCooldownLowSec") => balance.AmmoReqCooldownLowSec,
                ("ammoMonitor", "reqCooldownEmptySec") => balance.AmmoReqCooldownEmptySec,
                ("ammoMonitor", "notifyCooldownLowSec") => balance.AmmoNotifyCooldownLowSec,
                ("ammoMonitor", "notifyCooldownEmptySec") => balance.AmmoNotifyCooldownEmptySec,
                _ => fallback,
            };
        }

        internal string GetString(string section, string key, string fallback)
        {
            var balance = TryGetBalanceService();
            if (balance == null)
                return fallback;

            return (section, key) switch
            {
                ("crafting", "ammoRecipeId") => string.IsNullOrWhiteSpace(balance.AmmoRecipeId) ? fallback : balance.AmmoRecipeId,
                _ => fallback,
            };
        }

        private BalanceService TryGetBalanceService()
        {
            var balance = _services?.Balance;
            if (balance == null)
                Debug.LogWarning("[AmmoService] GameServices.Balance is missing. Using fallback ammo config values.");
            return balance;
        }
    }
}
