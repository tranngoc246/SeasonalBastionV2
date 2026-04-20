using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class InspectPanelPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Label _info;
        private Button _btnClear;
        private Button _btnClose;

        private Label _id, _def, _hp;
        private Label _wood, _food, _stone, _iron, _ammo;

        // P0.4 actions
        private Label _workers;
        private Label _actionHint;
        private Button _btnUpgrade;
        private Button _btnRepair;
        private Button _btnAssignNpc;
        private Button _btnCancelConstruction;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _info = Root.Q<Label>("Info");
            _btnClear = Root.Q<Button>("BtnClear");
            _btnClose = Root.Q<Button>("BtnInspectClose");

            _id = Root.Q<Label>("LblInspectId");
            _def = Root.Q<Label>("LblInspectDef");
            _hp = Root.Q<Label>("LblInspectHP");

            _wood = Root.Q<Label>("LblInspectWood");
            _food = Root.Q<Label>("LblInspectFood");
            _stone = Root.Q<Label>("LblInspectStone");
            _iron = Root.Q<Label>("LblInspectIron");
            _ammo = Root.Q<Label>("LblInspectAmmo");

            _workers = Root.Q<Label>("LblInspectWorkers");
            _actionHint = Root.Q<Label>("LblInspectActionHint");
            _btnUpgrade = Root.Q<Button>("BtnInspectUpgrade");
            _btnRepair = Root.Q<Button>("BtnInspectRepair");
            _btnAssignNpc = Root.Q<Button>("BtnInspectAssignNpc");
            _btnCancelConstruction = Root.Q<Button>("BtnInspectCancelConstruction");

            if (_btnClear != null) _btnClear.clicked += OnClear;
            if (_btnClose != null) _btnClose.clicked += OnClear;

            if (_btnUpgrade != null) _btnUpgrade.clicked += OnUpgrade;
            if (_btnRepair != null) _btnRepair.clicked += OnRepair;
            if (_btnAssignNpc != null) _btnAssignNpc.clicked += OnAssignNpc;
            if (_btnCancelConstruction != null) _btnCancelConstruction.clicked += OnCancelConstruction;

            if (Ctx?.Store != null)
                Ctx.Store.SelectionRefChanged += OnSelectionChanged;
        }

        protected override void OnUnbind()
        {
            if (_btnClear != null) _btnClear.clicked -= OnClear;
            if (_btnClose != null) _btnClose.clicked -= OnClear;

            if (_btnUpgrade != null) _btnUpgrade.clicked -= OnUpgrade;
            if (_btnRepair != null) _btnRepair.clicked -= OnRepair;
            if (_btnAssignNpc != null) _btnAssignNpc.clicked -= OnAssignNpc;
            if (_btnCancelConstruction != null) _btnCancelConstruction.clicked -= OnCancelConstruction;

            if (Ctx?.Store != null)
                Ctx.Store.SelectionRefChanged -= OnSelectionChanged;

            _s = null;
        }

        protected override void OnRefresh()
        {
            var selected = Ctx?.Store != null ? Ctx.Store.Selected : SelectionRef.None;

            if (selected.IsNone)
            {
                if (_info != null) _info.text = "Nothing selected";
                SetRow(_id, "");
                SetRow(_def, "Select a building or resource patch");
                SetRow(_hp, "");
                SetRow(_wood, "");
                SetRow(_food, "");
                SetRow(_stone, "");
                SetRow(_iron, "");
                SetRow(_ammo, "");

                SetRow(_workers, "");
                SetActionHint("");
                SetEnabled(_btnUpgrade, false);
                SetEnabled(_btnRepair, false);
                SetEnabled(_btnAssignNpc, false);
                SetEnabled(_btnCancelConstruction, false);
                return;
            }

            var s = _s;
            if (selected.Kind == SelectionKind.ResourcePatch)
            {
                RenderResourcePatch(s, selected.Id);
                return;
            }

            int id = selected.Id;
            if (s?.WorldState?.Buildings == null)
            {
                if (_info != null) _info.text = $"Selected: {id} (WorldState missing)";
                return;
            }

            var bid = new BuildingId(id);
            if (!s.WorldState.Buildings.Exists(bid))
            {
                if (_info != null) _info.text = $"Selected: {id} (not found)";
                SetEnabled(_btnUpgrade, false);
                SetEnabled(_btnRepair, false);
                SetEnabled(_btnAssignNpc, false);
                SetEnabled(_btnCancelConstruction, false);
                return;
            }

            var bs = s.WorldState.Buildings.Get(bid);

            if (_info != null) _info.text = bs.IsConstructed ? "BUILDING" : "CONSTRUCTION";
            SetRow(_id, ToDisplayName(bs.DefId));
            SetRow(_def, $"Lv {NormalizeLevel(bs.Level)}{(bs.IsConstructed ? "" : " • Under construction")}");
            SetRow(_hp, bs.MaxHP > 0 ? $"HP: {bs.HP}/{bs.MaxHP}" : "");

            SetRow(_wood, bs.Wood > 0 ? $"Wood: {bs.Wood}" : "");
            SetRow(_food, bs.Food > 0 ? $"Food: {bs.Food}" : "");
            SetRow(_stone, bs.Stone > 0 ? $"Stone: {bs.Stone}" : "");
            SetRow(_iron, bs.Iron > 0 ? $"Iron: {bs.Iron}" : "");
            SetRow(_ammo, bs.Ammo > 0 ? $"Ammo: {bs.Ammo}" : "");

            RenderActions(s, bid, bs);
        }

        private void RenderResourcePatch(GameServices s, int patchId)
        {
            if (s?.ResourcePatchService == null || !s.ResourcePatchService.TryGetPatch(patchId, out var patch))
            {
                if (_info != null) _info.text = "RESOURCE PATCH";
                SetRow(_id, "Resource patch");
                SetRow(_def, "Unavailable");
                SetRow(_hp, "");
                SetRow(_wood, "");
                SetRow(_food, "");
                SetRow(_stone, "");
                SetRow(_iron, "");
                SetRow(_ammo, "");
                SetRow(_workers, "");
                SetActionHint("Resource patch not found.");
                SetEnabled(_btnUpgrade, false);
                SetEnabled(_btnRepair, false);
                SetEnabled(_btnAssignNpc, false);
                SetEnabled(_btnCancelConstruction, false);
                return;
            }

            if (_info != null) _info.text = "RESOURCE PATCH";
            SetRow(_id, ToDisplayName(patch.Resource.ToString()));
            SetRow(_def, $"Remaining: {patch.RemainingAmount} / {patch.TotalAmount}");
            SetRow(_hp, $"Cells: {patch.Cells?.Count ?? 0}");
            SetRow(_wood, "");
            SetRow(_food, "");
            SetRow(_stone, "");
            SetRow(_iron, "");
            SetRow(_ammo, "");
            SetRow(_workers, "");
            SetActionHint("Click another resource patch or building to inspect it.");
            SetEnabled(_btnUpgrade, false);
            SetEnabled(_btnRepair, false);
            SetEnabled(_btnAssignNpc, false);
            SetEnabled(_btnCancelConstruction, false);
        }

        private void RenderActions(GameServices s, BuildingId bid, BuildingState bs)
        {
            bool runEnded = IsRunEnded();
            int assigned = WorkforceAssignmentRules.CountAssignedToBuilding(s.WorldState, bid);
            int lvl = WorkforceAssignmentRules.NormalizeLevel(bs.Level);
            var def = SafeGetBuildingDef(s, bs.DefId);
            int max = WorkforceAssignmentRules.GetMaxAssignedFor(def, lvl);

            SetRow(_workers, max > 0 ? $"Workers: {assigned}/{max}" : (assigned > 0 ? $"Workers: {assigned}" : ""));

            bool isUpgrading = IsUpgradeInProgress(s, bid, out var upSite);

            string edgeHint = "";

            bool canUpgrade = !runEnded && bs.IsConstructed && !isUpgrading && HasUpgradeEdgeAvailable(s, bs.DefId, out edgeHint);
            SetEnabled(_btnUpgrade, canUpgrade);

            bool canRepair = !runEnded && bs.IsConstructed && bs.MaxHP > 0 && bs.HP < bs.MaxHP;
            SetEnabled(_btnRepair, canRepair);

            bool canAssign = !runEnded && bs.IsConstructed && def != null && max > 0;
            SetEnabled(_btnAssignNpc, canAssign);

            bool canCancelConstruction = !runEnded && !bs.IsConstructed && HasActiveConstructionOrder(s, bid);
            SetEnabled(_btnCancelConstruction, canCancelConstruction);

            string hint = "";
            if (runEnded) hint = "Run has ended.";
            else if (!bs.IsConstructed) hint = canCancelConstruction ? "Under construction. You can cancel this construction." : "Under construction.";
            else if (isUpgrading) hint = $"Upgrading -> {upSite.BuildingDefId}";
            else if (!canUpgrade && !string.IsNullOrEmpty(edgeHint)) hint = edgeHint;
            else if (!canAssign && def != null && max <= 0) hint = "Building này không nhận worker.";
            else if (canRepair) hint = "Can repair: HP missing.";

            SetActionHint(hint);
        }

        private void OnUpgrade()
        {
            if (IsRunEnded()) return;
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;
            if (id < 0) return;
            _s?.EventBus?.Publish(new UiInspectActionRequestedEvent("Upgrade", id));
            Refresh();
        }

        private void OnRepair()
        {
            if (IsRunEnded()) return;
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;
            if (id < 0) return;
            _s?.EventBus?.Publish(new UiInspectActionRequestedEvent("Repair", id));
            Refresh();
        }

        private void OnAssignNpc()
        {
            if (IsRunEnded()) return;
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;
            if (id < 0) return;
            _s?.EventBus?.Publish(new UiInspectActionRequestedEvent("AssignNpc", id));
        }

        private void OnCancelConstruction()
        {
            if (IsRunEnded()) return;
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;
            if (id < 0) return;
            _s?.EventBus?.Publish(new UiInspectActionRequestedEvent("CancelConstruction", id));
            Refresh();
        }

        private static void Set(Label l, string t) { if (l != null) l.text = t ?? ""; }

        private static void SetRow(Label l, string t)
        {
            if (l == null) return;
            l.text = t ?? "";
            bool visible = !string.IsNullOrWhiteSpace(t);
            l.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string ToDisplayName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            string text = raw;
            if (text.StartsWith("bld_")) text = text.Substring(4);
            else if (text.StartsWith("npc_")) text = text.Substring(4);
            else if (text.StartsWith("res_")) text = text.Substring(4);

            text = text.Replace("_t1", "")
                       .Replace("_t2", "")
                       .Replace("_t3", "")
                       .Replace('_', ' ')
                       .Trim();

            if (text.Length == 0) return raw;

            var parts = text.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return string.Join(" ", parts);
        }

        private void SetActionHint(string t)
        {
            if (_actionHint == null) return;
            if (string.IsNullOrEmpty(t))
            {
                _actionHint.text = "";
                _actionHint.AddToClassList("hidden");
            }
            else
            {
                _actionHint.text = t;
                _actionHint.RemoveFromClassList("hidden");
            }
        }

        private static void SetEnabled(Button b, bool on)
        {
            if (b == null) return;
            b.SetEnabled(on);
        }

        private bool IsRunEnded()
        {
            return _s?.RunOutcomeService != null && _s.RunOutcomeService.Outcome != RunOutcome.Ongoing;
        }

        private static int NormalizeLevel(int level)
        {
            if (level < 1) return 1;
            if (level > 3) return 3;
            return level;
        }

        private static BuildingDef SafeGetBuildingDef(GameServices s, string defId)
        {
            if (s?.DataRegistry == null || string.IsNullOrEmpty(defId)) return null;
            try { return s.DataRegistry.GetBuilding(defId); }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[InspectPanelPresenter] Failed to resolve BuildingDef '{defId}' while rendering inspect panel. {ex}");
                return null;
            }
        }

        private static bool IsUpgradeInProgress(GameServices s, BuildingId bid, out BuildSiteState site)
        {
            site = default;
            if (s?.WorldState?.Sites == null) return false;

            foreach (var sid in s.WorldState.Sites.Ids)
            {
                if (!s.WorldState.Sites.Exists(sid)) continue;
                var st = s.WorldState.Sites.Get(sid);
                if (!st.IsActive) continue;
                if (!st.IsUpgrade) continue;
                if (st.TargetBuilding.Value != bid.Value) continue;
                site = st;
                return true;
            }
            return false;
        }

        private static bool HasActiveConstructionOrder(GameServices s, BuildingId bid)
        {
            if (s?.WorldState?.Sites == null) return false;

            foreach (var sid in s.WorldState.Sites.Ids)
            {
                if (!s.WorldState.Sites.Exists(sid)) continue;
                var st = s.WorldState.Sites.Get(sid);
                if (!st.IsActive) continue;
                if (st.TargetBuilding.Value != bid.Value) continue;
                return true;
            }

            return false;
        }

        private static bool HasUpgradeEdgeAvailable(GameServices s, string fromDefId, out string hint)
        {
            hint = "";
            if (s?.DataRegistry == null || string.IsNullOrEmpty(fromDefId))
            {
                hint = "Data missing.";
                return false;
            }

            IReadOnlyList<UpgradeEdgeDef> edges = null;
            try { edges = s.DataRegistry.GetUpgradeEdgesFrom(fromDefId); }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[InspectPanelPresenter] Failed to get upgrade edges from '{fromDefId}'. Treating as no upgrade available. {ex}");
                edges = null;
            }

            if (edges == null || edges.Count == 0)
            {
                hint = "No upgrade available.";
                return false;
            }

            var e = edges[0];
            if (!string.IsNullOrWhiteSpace(e.RequiresUnlocked) && s.UnlockService != null && !s.UnlockService.IsUnlocked(e.RequiresUnlocked))
            {
                hint = $"Locked: {e.RequiresUnlocked}";
                return false;
            }

            return true;
        }

        private void OnSelectionChanged(SelectionRef _) => Refresh();

        private void OnClear()
        {
            _s?.EventBus?.Publish(new UiClearInspectRequestedEvent());
        }
    }
}