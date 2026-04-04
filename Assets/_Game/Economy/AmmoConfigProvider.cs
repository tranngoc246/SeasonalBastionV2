using System;

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
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj);
                if (v is int i) return i;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as int: {ex}");
            }
            return fallback;
        }

        internal bool GetBool(string section, string key, bool fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj);
                if (v is bool b) return b;
                if (v is int i) return i != 0;
                if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as bool: {ex}");
            }
            return fallback;
        }

        internal float GetFloat(string section, string key, float fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj);
                if (v is float f) return f;
                if (v is double d) return (float)d;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as float: {ex}");
            }
            return fallback;
        }

        internal string GetString(string section, string key, string fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as string: {ex}");
            }
            return fallback;
        }

        private object TryGetBalanceConfig()
        {
            if (_services == null) return null;

            try
            {
                var sType = _services.GetType();
                var balField = sType.GetField("Balance");
                if (balField == null) return null;

                var balObj = balField.GetValue(_services);
                if (balObj == null) return null;

                var balType = balObj.GetType();
                var cfgProp = balType.GetProperty("Config");
                if (cfgProp != null)
                    return cfgProp.GetValue(balObj, null);

                var cfgField = balType.GetField("Config");
                if (cfgField != null)
                    return cfgField.GetValue(balObj);

                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access GameServices.Balance.Config via reflection. Using fallback balance values. {ex}");
                return null;
            }
        }
    }
}
