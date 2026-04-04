using SeasonalBastion.Contracts;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugNpcTool
    {
        /// <summary>
        /// Minimal quick UI for Hub: spawn + select + show current job + unassign/release.
        /// </summary>
        public void DrawQuickGUI()
        {
            if (_s == null || _world == null)
            {
                GUILayout.Label("NPC Tool: not bound (GameServices null)");
                return;
            }

            GUILayout.Label("Spawn NPC (near HQ):");
            GUILayout.BeginHorizontal();
            GUILayout.Label("DefId", GUILayout.Width(45));
            _uiNpcDefId = GUILayout.TextField(_uiNpcDefId ?? _npcDefId, GUILayout.Width(140));
            GUILayout.Label("Count", GUILayout.Width(50));
            _uiSpawnCount = GUILayout.TextField(_uiSpawnCount ?? _spawnBurstCount.ToString(), GUILayout.Width(50));
            if (GUILayout.Button("Spawn", GUILayout.Width(80)))
            {
                if (string.IsNullOrWhiteSpace(_uiNpcDefId)) _uiNpcDefId = _npcDefId;
                if (!int.TryParse(_uiSpawnCount, out var n)) n = _spawnBurstCount;
                DebugSpawn(_uiNpcDefId, n);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"Tool Assign Mode: {(_enabled ? "ON (LMB assign)" : "OFF")} | Selected: {(_hasSelectedNpc ? $"#{_selectedNpc.Value}" : "none")}");

            if (_hasSelectedNpc && _world.Npcs.Exists(_selectedNpc))
            {
                var npc = _world.Npcs.Get(_selectedNpc);
                string wp = npc.Workplace.Value == 0 ? "none" : $"#{npc.Workplace.Value}";
                GUILayout.Label($"WP: {wp}  Idle:{npc.IsIdle}  Cell:({npc.Cell.X},{npc.Cell.Y})");

                if (npc.CurrentJob.Value != 0 && _jobs != null && _jobs.TryGet(npc.CurrentJob, out var job))
                    GUILayout.Label($"Job: #{job.Id.Value} {job.Archetype} {job.Status} Res:{job.ResourceType} Amt:{job.Amount}");
                else
                    GUILayout.Label($"Job: {(npc.CurrentJob.Value == 0 ? "none" : $"#{npc.CurrentJob.Value} (missing in JobBoard)")}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Unassign", GUILayout.Width(110)))
                {
                    npc.Workplace = default;
                    npc.IsIdle = true;
                    npc.CurrentJob = default;
                    _world.Npcs.Set(_selectedNpc, npc);
                    _bus?.Publish(new NPCAssignedEvent(_selectedNpc, default));
                    _noti?.Push($"NpcUnassigned_{_selectedNpc.Value}", "NPC", $"NPC #{_selectedNpc.Value} unassigned", NotificationSeverity.Info, default, 0.2f, true);
                }
                if (GUILayout.Button("Release Claims", GUILayout.Width(140)))
                    ForceReleaseSelectedNpc("Quick", keepWorkplace: false);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            var npcIds = _world.Npcs.Ids;
            var tmp = new List<NpcId>(npcIds.Count());
            foreach (var id in npcIds) tmp.Add(id);
            tmp.Sort((a, b) => a.Value.CompareTo(b.Value));

            int show = tmp.Count > 10 ? 10 : tmp.Count;
            for (int i = 0; i < show; i++)
            {
                var id = tmp[i];
                var st = _world.Npcs.Get(id);
                string wp = st.Workplace.Value == 0 ? "none" : $"#{st.Workplace.Value}";
                string job = st.CurrentJob.Value == 0 ? "-" : $"#{st.CurrentJob.Value}";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"#{id.Value} {st.DefId} WP:{wp} Job:{job}", GUILayout.Width(320));
                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    _selectedNpc = id;
                    _hasSelectedNpc = true;
                    _noti?.Push("NpcSelect", "NPC", $"Selected NPC #{id.Value}", NotificationSeverity.Info, default, 0.1f, true);
                }
                GUILayout.EndHorizontal();
            }

            if (tmp.Count > show)
                GUILayout.Label($"... ({tmp.Count - show} more)");
        }

        public void DrawHubGUI()
        {
            GUILayout.Label($"Tool: {(_enabled ? "ON (N)" : "OFF (N)")} | Spawn: P | Hover: {(_hasHover ? $"({_hoverCell.X},{_hoverCell.Y})" : "none")}");

            if (_hasHover && _grid != null && _grid.IsInside(_hoverCell))
            {
                var o = _grid.Get(_hoverCell);
                string extra = o.Kind == CellOccupancyKind.Building ? $"B#{o.Building.Value}" :
                               o.Kind == CellOccupancyKind.Site ? $"S#{o.Site.Value}" : "-";
                GUILayout.Label($"Hover Occ: {o.Kind} {extra}");
            }

            GUILayout.Space(6);

            var npcIds = _world.Npcs.Ids;
            var tmp = new List<NpcId>(npcIds.Count());
            foreach (var npcid in npcIds) tmp.Add(npcid);
            tmp.Sort((a, b) => a.Value.CompareTo(b.Value));

            int unassigned = 0;
            for (int i = 0; i < tmp.Count; i++)
            {
                var st = _world.Npcs.Get(tmp[i]);
                if (st.Workplace.Value == 0) unassigned++;
            }

            GUILayout.Label($"NPCs: {tmp.Count} | Unassigned: {unassigned}");
            GUILayout.Label($"Selected: {(_hasSelectedNpc ? $"#{_selectedNpc.Value}" : "none")}");

            GUILayout.Space(6);
            GUILayout.Label("Claims / Jobs:");

            if (_claims != null)
                GUILayout.Label($"ActiveClaimsCount: {_claims.ActiveClaimsCount}");
            else
                GUILayout.Label("ClaimService = null");

            if (_hasSelectedNpc && _world.Npcs.Exists(_selectedNpc))
            {
                if (GUILayout.Button("Release All Claims (Selected NPC)", GUILayout.Width(260)))
                    ForceReleaseSelectedNpc("Button", keepWorkplace: false);

                var npc = _world.Npcs.Get(_selectedNpc);
                var wp = npc.Workplace;
                GUILayout.Label($"Workplace: {(wp.Value == 0 ? "none" : $"#{wp.Value}")}");

                if (_jobs != null && wp.Value != 0)
                {
                    int c = _jobs.CountForWorkplace(wp);
                    GUILayout.Label($"Jobs in workplace queue: {c}");

                    if (_jobs.TryPeekForWorkplace(wp, out var j))
                        GUILayout.Label($"Peek: #{j.Id.Value} {j.Archetype} {j.Status} Amt:{j.Amount} Res:{j.ResourceType}");
                    else
                        GUILayout.Label("Peek: (none)");
                }
                else if (_jobs == null)
                {
                    GUILayout.Label("JobBoard = null");
                }
            }
            else
            {
                GUILayout.Label("Select an NPC to enable ReleaseAllClaims + Workplace job view.");
            }

            int show = tmp.Count > 12 ? 12 : tmp.Count;
            for (int i = 0; i < show; i++)
            {
                var id = tmp[i];
                var st = _world.Npcs.Get(id);

                GUILayout.BeginHorizontal();
                string wp = st.Workplace.Value == 0 ? "none" : $"#{st.Workplace.Value}";
                GUILayout.Label($"#{id.Value} {st.DefId} @({st.Cell.X},{st.Cell.Y})  WP:{wp}", GUILayout.Width(320));

                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    _selectedNpc = id;
                    _hasSelectedNpc = true;
                    _noti?.Push("NpcSelect", "NPC", $"Selected NPC #{id.Value}", NotificationSeverity.Info, default, 0.1f, true);
                }

                GUILayout.EndHorizontal();
            }

            if (tmp.Count > show)
                GUILayout.Label($"... ({tmp.Count - show} more)");
        }
    }
}
