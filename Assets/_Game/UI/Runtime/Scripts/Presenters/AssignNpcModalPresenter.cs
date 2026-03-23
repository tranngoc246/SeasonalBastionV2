using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class AssignNpcModalPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Label _title;
        private Label _target;
        private Label _workers;
        private Label _hint;
        private ScrollView _list;
        private Button _btnClose;

        private readonly List<NpcId> _npcIds = new(64);

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _title = Root.Q<Label>("LblAssignTitle");
            _target = Root.Q<Label>("LblAssignTarget");
            _workers = Root.Q<Label>("LblAssignWorkers");
            _hint = Root.Q<Label>("LblAssignHint");
            _list = Root.Q<ScrollView>("NpcList");
            _btnClose = Root.Q<Button>("BtnAssignClose");

            if (_btnClose != null) _btnClose.clicked += OnClose;
        }

        protected override void OnUnbind()
        {
            if (_btnClose != null) _btnClose.clicked -= OnClose;
            _s = null;
        }

        protected override void OnRefresh()
        {
            if (_title != null) _title.text = "ASSIGN NPC";

            var s = _s;
            int sel = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;

            if (s?.WorldState?.Buildings == null || s.WorldState.Npcs == null)
            {
                Set(_target, "Target: -");
                Set(_workers, "Workers: -");
                Set(_hint, "WorldState missing");
                if (_list != null) _list.Clear();
                return;
            }

            if (sel < 0)
            {
                Set(_target, "Target: (no selection)");
                Set(_workers, "Workers: -");
                Set(_hint, "Chọn 1 building trước.");
                if (_list != null) _list.Clear();
                return;
            }

            var bid = new BuildingId(sel);
            if (!s.WorldState.Buildings.Exists(bid))
            {
                Set(_target, $"Target: #{sel} (missing)");
                Set(_workers, "Workers: -");
                Set(_hint, "Building not found.");
                if (_list != null) _list.Clear();
                return;
            }

            var bs = s.WorldState.Buildings.Get(bid);
            var def = SafeGetBuildingDef(s, bs.DefId);

            int lvl = NormalizeLevel(bs.Level);
            int max = GetMaxAssignedFor(def, lvl);
            int assigned = CountAssignedToBuilding(s, bid, excludeNpc: default);

            Set(_target, $"Target: #{bid.Value}  {bs.DefId}  Lv{lvl}");
            Set(_workers, max > 0 ? $"Workers: {assigned}/{max}" : $"Workers: {assigned}/-");

            string reason = "";
            if (!bs.IsConstructed) reason = "Công trình chưa xây xong: không thể assign.";
            else if (def == null) reason = "Missing BuildingDef.";
            else if (max <= 0) reason = "Building này không nhận worker.";
            Set(_hint, reason);

            RebuildNpcList(s, bid, bs, def, max, assigned);
        }

        private void RebuildNpcList(GameServices s, BuildingId target, BuildingState bs, BuildingDef def, int max, int assigned)
        {
            if (_list == null) return;

            _list.Clear();
            _npcIds.Clear();

            foreach (var nid in s.WorldState.Npcs.Ids)
                _npcIds.Add(nid);

            _npcIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            bool canAssignToTarget = bs.IsConstructed && def != null && max > 0;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!s.WorldState.Npcs.Exists(nid)) continue;

                var ns = s.WorldState.Npcs.Get(nid);
                bool isAssignedHere = ns.Workplace.Value == target.Value;

                var row = new VisualElement();
                row.AddToClassList("npc-row");
                row.AddToClassList(UiKeys.Class_BlockWorld);
                row.pickingMode = PickingMode.Position;

                var left = new VisualElement();
                left.AddToClassList("npc-left");

                var title = new Label($"NPC #{nid.Value}");
                title.AddToClassList("npc-title");

                string wpText = ns.Workplace.Value == 0 ? "-" : $"#{ns.Workplace.Value}";
                if (ns.Workplace.Value != 0 && s.WorldState.Buildings.Exists(ns.Workplace))
                {
                    var wps = s.WorldState.Buildings.Get(ns.Workplace);
                    wpText = $"#{ns.Workplace.Value} ({wps.DefId})";
                }

                string jobText = ns.CurrentJob.Value == 0 ? "-" : $"#{ns.CurrentJob.Value}";
                string idleText = ns.IsIdle ? "Idle" : "Busy";
                var sub = new Label($"Workplace: {wpText} | Job: {jobText} | {idleText}");
                sub.AddToClassList("npc-sub");

                left.Add(title);
                left.Add(sub);

                var right = new VisualElement();
                right.AddToClassList("npc-right");

                var btn = new Button();
                btn.AddToClassList(UiKeys.Class_BlockWorld);
                btn.pickingMode = PickingMode.Position;

                if (isAssignedHere)
                {
                    btn.text = "Unassign";
                    btn.AddToClassList("btn-warn");
                    btn.clicked += () =>
                    {
                        UnassignNpc(nid);
                        Refresh();
                    };
                }
                else
                {
                    btn.text = "Assign";
                    btn.AddToClassList("btn-primary");

                    int assignedNow = CountAssignedToBuilding(s, target, excludeNpc: nid);
                    bool full = canAssignToTarget && assignedNow >= max;

                    btn.SetEnabled(canAssignToTarget && !full);
                    btn.clicked += () =>
                    {
                        AssignNpc(nid, target);
                        Refresh();
                    };

                    if (!canAssignToTarget) btn.tooltip = "Không thể assign vào building này.";
                    else if (full) btn.tooltip = $"Đã đủ worker ({assigned}/{max}).";
                }

                right.Add(btn);

                row.Add(left);
                row.Add(right);

                _list.Add(row);
            }

            if (_npcIds.Count == 0)
                _list.Add(new Label("(No NPCs)") { pickingMode = PickingMode.Ignore });
        }

        private void AssignNpc(NpcId npc, BuildingId workplace)
        {
            var s = _s;
            if (s?.WorldState?.Npcs == null) return;
            if (!s.WorldState.Npcs.Exists(npc)) return;

            var ns = s.WorldState.Npcs.Get(npc);

            if (ns.CurrentJob.Value != 0)
                s.JobBoard?.Cancel(ns.CurrentJob);

            ns.CurrentJob = default;
            ns.IsIdle = true;

            s.ClaimService?.ReleaseAll(npc);

            ns.Workplace = workplace;
            s.WorldState.Npcs.Set(npc, ns);

            s.EventBus?.Publish(new NPCAssignedEvent(npc, workplace));
            s.NotificationService?.Push(
                key: $"NpcAssigned_{npc.Value}",
                title: "NPC",
                body: $"NPC #{npc.Value} assigned to Building #{workplace.Value}",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(workplace, default, "npc_assign"),
                cooldownSeconds: 0.2f,
                dedupeByKey: true);
        }

        private void UnassignNpc(NpcId npc)
        {
            var s = _s;
            if (s?.WorldState?.Npcs == null) return;
            if (!s.WorldState.Npcs.Exists(npc)) return;

            var ns = s.WorldState.Npcs.Get(npc);

            if (ns.CurrentJob.Value != 0)
                s.JobBoard?.Cancel(ns.CurrentJob);

            ns.CurrentJob = default;
            ns.IsIdle = true;
            s.ClaimService?.ReleaseAll(npc);

            ns.Workplace = default;
            s.WorldState.Npcs.Set(npc, ns);

            s.EventBus?.Publish(new NPCAssignedEvent(npc, default));
            s.NotificationService?.Push(
                key: $"NpcUnassigned_{npc.Value}",
                title: "NPC",
                body: $"NPC #{npc.Value} unassigned",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(default, default, "npc_unassign"),
                cooldownSeconds: 0.2f,
                dedupeByKey: true);
        }

        private void OnClose()
        {
            Ctx?.Modals?.Pop();
        }

        private static void Set(Label l, string t) { if (l != null) l.text = t ?? ""; }

        private static int CountAssignedToBuilding(GameServices s, BuildingId buildingId, NpcId excludeNpc)
        {
            int assigned = 0;
            foreach (var nid in s.WorldState.Npcs.Ids)
            {
                if (!s.WorldState.Npcs.Exists(nid)) continue;
                if (excludeNpc.Value != 0 && nid.Value == excludeNpc.Value) continue;
                var ns = s.WorldState.Npcs.Get(nid);
                if (ns.Workplace.Value == buildingId.Value) assigned++;
            }
            return assigned;
        }

        private static BuildingDef SafeGetBuildingDef(GameServices s, string defId)
        {
            if (s?.DataRegistry == null || string.IsNullOrEmpty(defId)) return null;
            try { return s.DataRegistry.GetBuilding(defId); }
            catch { return null; }
        }

        private static int NormalizeLevel(int level)
        {
            if (level < 1) return 1;
            if (level > 3) return 3;
            return level;
        }

        private static int GetMaxAssignedFor(BuildingDef def, int level)
        {
            if (def == null) return 0;
            if (def.WorkRoles == WorkRoleFlags.None) return 0;
            if (def.IsHouse || def.IsTower) return 0;

            if (level < 1) level = 1;
            else if (level > 3) level = 3;

            if (def.IsHQ) return level switch { 1 => 2, 2 => 3, 3 => 4, _ => 2 };
            if (def.IsWarehouse) return level switch { 1 => 1, 2 => 2, 3 => 3, _ => 1 };

            if ((def.WorkRoles & WorkRoleFlags.Harvest) != 0) return level switch { 1 => 1, 2 => 2, 3 => 3, _ => 1 };
            if (def.IsForge || (def.WorkRoles & WorkRoleFlags.Craft) != 0) return level switch { 1 => 1, 2 => 2, 3 => 2, _ => 1 };
            if (def.IsArmory || (def.WorkRoles & WorkRoleFlags.Armory) != 0) return level switch { 1 => 1, 2 => 2, 3 => 2, _ => 1 };

            return 1;
        }
    }
}