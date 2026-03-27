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
        private Label _summary;
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
            _summary = Root.Q<Label>("LblAssignSummary");
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
            if (IsRunEnded())
            {
                Set(_target, "Target: -");
                Set(_workers, "Workers: -");
                Set(_summary, "Summary: -");
                Set(_hint, "Run has ended.");
                if (_list != null) _list.Clear();
                return;
            }
            int sel = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;

            if (s?.WorldState?.Buildings == null || s.WorldState.Npcs == null)
            {
                Set(_target, "Target: -");
                Set(_workers, "Workers: -");
                Set(_summary, "Summary: -");
                Set(_hint, "WorldState missing");
                if (_list != null) _list.Clear();
                return;
            }

            if (sel < 0)
            {
                Set(_target, "Target: (no selection)");
                Set(_workers, "Workers: -");
                Set(_summary, "Summary: -");
                Set(_hint, "Chọn 1 building trước.");
                if (_list != null) _list.Clear();
                return;
            }

            var bid = new BuildingId(sel);
            if (!s.WorldState.Buildings.Exists(bid))
            {
                Set(_target, $"Target: #{sel} (missing)");
                Set(_workers, "Workers: -");
                Set(_summary, "Summary: -");
                Set(_hint, "Building not found.");
                if (_list != null) _list.Clear();
                return;
            }

            var bs = s.WorldState.Buildings.Get(bid);
            var def = SafeGetBuildingDef(s, bs.DefId);

            int lvl = WorkforceAssignmentRules.NormalizeLevel(bs.Level);
            int max = WorkforceAssignmentRules.GetMaxAssignedFor(def, lvl);
            int assigned = WorkforceAssignmentRules.CountAssignedToBuilding(s.WorldState, bid, excludeNpc: default);
            int unassigned = WorkforceAssignmentRules.CountUnassigned(s.WorldState);

            Set(_target, $"Target: #{bid.Value}  {bs.DefId}  Lv{lvl}");
            Set(_workers, max > 0 ? $"Workers: {assigned}/{max}" : $"Workers: {assigned}/-");
            Set(_summary, $"Summary: Unassigned {unassigned} | Assigned here {assigned}");

            string reason = "";
            if (!bs.IsConstructed) reason = "Công trình chưa xây xong: không thể assign.";
            else if (def == null) reason = "Missing BuildingDef.";
            else if (max <= 0) reason = "Building này không nhận worker.";
            else if (assigned >= max) reason = $"Workplace đã full ({assigned}/{max}). Unassign hoặc move NPC khác trước khi thêm mới.";
            else if (unassigned > 0) reason = $"Có {unassigned} NPC chưa được assign. Chọn 'Assign here' để thêm worker vào building này.";
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

            _npcIds.Sort((a, b) =>
            {
                var aState = s.WorldState.Npcs.Get(a);
                var bState = s.WorldState.Npcs.Get(b);
                int aBucket = GetSortBucket(aState, target);
                int bBucket = GetSortBucket(bState, target);
                if (aBucket != bBucket) return aBucket.CompareTo(bBucket);
                return a.Value.CompareTo(b.Value);
            });

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

                string statusText = GetNpcAssignmentStatus(s, ns, target);
                string jobText = ns.CurrentJob.Value == 0 ? "-" : $"#{ns.CurrentJob.Value}";
                string idleText = ns.IsIdle ? "Idle" : "Busy";
                var sub = new Label($"{statusText} | Workplace: {wpText} | Job: {jobText} | {idleText}");
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
                    btn.tooltip = "Bỏ NPC này khỏi workplace hiện tại.";
                    btn.clicked += () =>
                    {
                        UnassignNpc(nid);
                        Refresh();
                    };
                }
                else
                {
                    bool isUnassigned = ns.Workplace.Value == 0;
                    btn.text = isUnassigned ? "Assign here" : "Move here";
                    btn.AddToClassList("btn-primary");

                    bool allowAssign = WorkforceAssignmentRules.CanAssignToTarget(s.WorldState, bs, def, target, nid, out var assignReason);
                    if (!allowAssign)
                    {
                        if (assignReason.Contains("Đã đủ worker")) btn.text = "Full";
                        else if (assignReason.Contains("không nhận worker") || assignReason.Contains("chưa xây xong")) btn.text = "Blocked";
                    }

                    btn.SetEnabled(allowAssign);
                    btn.clicked += () =>
                    {
                        AssignNpc(nid, target);
                        Refresh();
                    };

                    btn.tooltip = allowAssign
                        ? (isUnassigned ? "Assign NPC chưa có workplace vào building này." : "Chuyển NPC từ workplace hiện tại sang building này.")
                        : assignReason;
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
            if (IsRunEnded()) return;
            var s = _s;
            if (s?.WorldState?.Npcs == null || s.WorldState.Buildings == null) return;
            if (!s.WorldState.Npcs.Exists(npc) || !s.WorldState.Buildings.Exists(workplace)) return;

            var target = s.WorldState.Buildings.Get(workplace);
            var targetDef = SafeGetBuildingDef(s, target.DefId);
            if (!WorkforceAssignmentRules.CanAssignToTarget(s.WorldState, target, targetDef, workplace, npc, out var reason))
            {
                s.NotificationService?.Push(
                    key: $"NpcAssignBlocked_{npc.Value}_{workplace.Value}",
                    title: "NPC",
                    body: reason,
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(workplace, default, "npc_assign_blocked"),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true);
                return;
            }

            var ns = s.WorldState.Npcs.Get(npc);

            if (ns.CurrentJob.Value != 0)
                s.JobBoard?.Cancel(ns.CurrentJob);

            ns.CurrentJob = default;
            ns.IsIdle = true;

            s.ClaimService?.ReleaseAll(npc);

            bool wasUnassigned = ns.Workplace.Value == 0;
            ns.Workplace = workplace;
            s.WorldState.Npcs.Set(npc, ns);

            s.EventBus?.Publish(new NPCAssignedEvent(npc, workplace));
            s.NotificationService?.Push(
                key: $"NpcAssigned_{npc.Value}",
                title: "NPC",
                body: wasUnassigned
                    ? $"NPC #{npc.Value} assigned to Building #{workplace.Value}"
                    : $"NPC #{npc.Value} moved to Building #{workplace.Value}",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(workplace, default, "npc_assign"),
                cooldownSeconds: 0.2f,
                dedupeByKey: true);
        }

        private void UnassignNpc(NpcId npc)
        {
            if (IsRunEnded()) return;
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

        private static int GetSortBucket(NpcState ns, BuildingId target)
        {
            if (ns.Workplace.Value == target.Value) return 0;
            if (ns.Workplace.Value == 0) return 1;
            return 2;
        }

        private static string GetNpcAssignmentStatus(GameServices s, NpcState ns, BuildingId target)
        {
            if (ns.Workplace.Value == 0) return "Unassigned";
            if (ns.Workplace.Value == target.Value) return "Assigned here";

            string defId = "?";
            if (s?.WorldState?.Buildings != null && s.WorldState.Buildings.Exists(ns.Workplace))
                defId = s.WorldState.Buildings.Get(ns.Workplace).DefId;

            return $"Assigned elsewhere ({defId})";
        }

        private static BuildingDef SafeGetBuildingDef(GameServices s, string defId)
        {
            if (s?.DataRegistry == null || string.IsNullOrEmpty(defId)) return null;
            try { return s.DataRegistry.GetBuilding(defId); }
            catch { return null; }
        }

        private bool IsRunEnded()
        {
            return _s?.RunOutcomeService != null && _s.RunOutcomeService.Outcome != RunOutcome.Ongoing;
        }
    }
}