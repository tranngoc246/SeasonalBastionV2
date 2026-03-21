using System;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadBalanceInternal(TextAsset json)
        {
            _balance = null;

            if (json == null)
            {
                _loadErrors.Add("Balance JSON is missing (DefsCatalog.Balance is null).");
                return;
            }

            try
            {
                _balance = JsonUtility.FromJson<BalanceConfig>(json.text);
                if (_balance == null)
                    _loadErrors.Add("Balance JSON parsed null.");
            }
            catch (Exception e)
            {
                _loadErrors.Add("Balance JSON parse failed: " + e.Message);
                _balance = null;
            }
        }
    }
}
