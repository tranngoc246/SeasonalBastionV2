using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadBuildablesGraphInternal(TextAsset ta)
        {
            if (ta == null) return;

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"BuildablesGraph TextAsset '{ta.name}' is empty");
                return;
            }

            BuildablesGraphRoot root;
            try { root = JsonUtility.FromJson<BuildablesGraphRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"BuildablesGraph parse failed: {e.Message}");
                return;
            }

            if (root == null)
            {
                _loadErrors.Add("BuildablesGraph parse returned null root");
                return;
            }

            _buildableNodes.Clear();
            _upgradeEdgesById.Clear();
            _upgradeEdgesFrom.Clear();

            if (root.nodes != null)
            {
                for (int i = 0; i < root.nodes.Length; i++)
                {
                    var n = root.nodes[i];
                    if (n == null || string.IsNullOrWhiteSpace(n.id)) continue;

                    _buildableNodes[n.id] = new BuildableNodeDef
                    {
                        Id = n.id,
                        Level = n.level <= 0 ? 1 : n.level,
                        Placeable = n.placeable
                    };
                }
            }

            if (root.upgrades != null)
            {
                for (int i = 0; i < root.upgrades.Length; i++)
                {
                    var e = root.upgrades[i];
                    if (e == null || string.IsNullOrWhiteSpace(e.id) || string.IsNullOrWhiteSpace(e.from) || string.IsNullOrWhiteSpace(e.to))
                        continue;

                    var ed = new UpgradeEdgeDef
                    {
                        Id = e.id,
                        From = e.from,
                        To = e.to,
                        WorkChunks = e.workChunks < 0 ? 0 : e.workChunks,
                        RequiresUnlocked = e.requiresUnlocked ?? ""
                    };

                    if (e.cost != null && e.cost.Length > 0)
                    {
                        var costs = new CostDef[e.cost.Length];
                        for (int k = 0; k < e.cost.Length; k++)
                        {
                            var c = e.cost[k];
                            if (c == null) continue;
                            costs[k] = new CostDef { Resource = (ResourceType)c.res, Amount = c.amt };
                        }
                        ed.Cost = costs;
                    }
                    else ed.Cost = Array.Empty<CostDef>();

                    _upgradeEdgesById[ed.Id] = ed;

                    if (!_upgradeEdgesFrom.TryGetValue(ed.From, out var list) || list == null)
                    {
                        list = new List<UpgradeEdgeDef>(4);
                        _upgradeEdgesFrom[ed.From] = list;
                    }
                    list.Add(ed);
                }

                foreach (var kv in _upgradeEdgesFrom)
                {
                    var list = kv.Value;
                    if (list == null || list.Count <= 1) continue;
                    list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                }
            }
        }
    }
}
