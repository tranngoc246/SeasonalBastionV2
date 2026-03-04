using System.Collections.Generic;
using SeasonalBastion.Contracts;
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

            if (_btnClear != null) _btnClear.clicked += OnClear;
            if (_btnClose != null) _btnClose.clicked += OnClear;

            if (_btnUpgrade != null) _btnUpgrade.clicked += OnUpgrade;
            if (_btnRepair != null) _btnRepair.clicked += OnRepair;
            if (_btnAssignNpc != null) _btnAssignNpc.clicked += OnAssignNpc;

            if (Ctx?.Store != null)
                Ctx.Store.SelectionChanged += OnSelectionChanged;
        }

        protected override void OnUnbind()
        {
            if (_btnClear != null) _btnClear.clicked -= OnClear;
            if (_btnClose != null) _btnClose.clicked -= OnClear;

            if (_btnUpgrade != null) _btnUpgrade.clicked -= OnUpgrade;
            if (_btnRepair != null) _btnRepair.clicked -= OnRepair;
            if (_btnAssignNpc != null) _btnAssignNpc.clicked -= OnAssignNpc;

            if (Ctx?.Store != null)
                Ctx.Store.SelectionChanged -= OnSelectionChanged;

            _s = null;
        }

        protected override void OnRefresh()
        {
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;

            if (id < 0)
            {
                if (_info != null) _info.text = "No selection";
                Set(_id, "ID: -");
                Set(_def, "Def: -");
                Set(_hp, "HP: -");
                Set(_wood, "Wood: -");
                Set(_food, "Food: -");
                Set(_stone, "Stone: -");
                Set(_iron, "Iron: -");
                Set(_ammo, "Ammo: -");

                Set(_workers, "Workers: -");
                SetActionHint("");
                SetEnabled(_btnUpgrade, false);
                SetEnabled(_btnRepair, false);
                SetEnabled(_btnAssignNpc, false);
                return;
            }

            var s = _s;
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
                return;
            }

            var bs = s.WorldState.Buildings.Get(bid);

            if (_info != null) _info.text = $"Selected BuildingId={id}";
            Set(_id, $"ID: {bs.Id.Value}");
            Set(_def, $"Def: {bs.DefId}");
            Set(_hp, $"HP: {bs.HP}/{bs.MaxHP}");

            Set(_wood, $"Wood: {bs.Wood}");
            Set(_food, $"Food: {bs.Food}");
            Set(_stone, $"Stone: {bs.Stone}");
            Set(_iron, $"Iron: {bs.Iron}");
            Set(_ammo, $"Ammo: {bs.Ammo}");

            RenderActions(s, bid, bs);
        }

        private void RenderActions(GameServices s, BuildingId bid, BuildingState bs)
        {
            int assigned = CountAssignedToBuilding(s, bid);
            int lvl = NormalizeLevel(bs.Level);
            var def = SafeGetBuildingDef(s, bs.DefId);
            int max = GetMaxAssignedFor(def, lvl);

            if (_workers != null)
                _workers.text = max > 0 ? $"Workers: {assigned}/{max}" : $"Workers: {assigned}/-";

            bool isUpgrading = IsUpgradeInProgress(s, bid, out var upSite);

            string edgeHint = "";

            bool canUpgrade = bs.IsConstructed && !isUpgrading && HasUpgradeEdgeAvailable(s, bs.DefId, out edgeHint);
            SetEnabled(_btnUpgrade, canUpgrade);

            bool canRepair = bs.IsConstructed && bs.MaxHP > 0 && bs.HP < bs.MaxHP;
            SetEnabled(_btnRepair, canRepair);

            bool canAssign = bs.IsConstructed && def != null && max > 0;
            SetEnabled(_btnAssignNpc, canAssign);

            string hint = "";
            if (!bs.IsConstructed) hint = "Under construction.";
            else if (isUpgrading) hint = $"Upgrading -> {upSite.BuildingDefId}";
            else if (!canUpgrade && !string.IsNullOrEmpty(edgeHint)) hint = edgeHint;
            else if (!canAssign && def != null && max <= 0) hint = "Building này không nhận worker.";
            else if (canRepair) hint = "Can repair: HP missing.";

            SetActionHint(hint);
        }

        private void OnUpgrade()
        {
            var s = _s;
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;
            if (id < 0 || s?.BuildOrderService == null) return;

            s.BuildOrderService.CreateUpgradeOrder(new BuildingId(id));
            Refresh();
        }

        private void OnRepair()
        {
            var s = _s;
            int id = Ctx?.Store != null ? Ctx.Store.SelectedId : -1;
            if (id < 0 || s?.BuildOrderService == null) return;

            s.BuildOrderService.CreateRepairOrder(new BuildingId(id));
            Refresh();
        }

        private void OnAssignNpc()
        {
            Ctx?.Modals?.Push(UiKeys.Modal_AssignNpc);
        }

        private static void Set(Label l, string t) { if (l != null) l.text = t ?? ""; }

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
            catch { return null; }
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
            catch { edges = null; }

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

        private static int CountAssignedToBuilding(GameServices s, BuildingId buildingId)
        {
            if (s?.WorldState?.Npcs == null) return 0;
            int assigned = 0;
            foreach (var nid in s.WorldState.Npcs.Ids)
            {
                if (!s.WorldState.Npcs.Exists(nid)) continue;
                var ns = s.WorldState.Npcs.Get(nid);
                if (ns.Workplace.Value == buildingId.Value) assigned++;
            }
            return assigned;
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

        private void OnSelectionChanged(int _) => Refresh();

        private void OnClear()
        {
            Ctx?.Store?.ClearSelection();
            Ctx?.Panels?.HideCurrent();
        }
    }
}