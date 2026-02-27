using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Inspect panel:
    /// - show selected building from UIStateStore.SelectedId
    /// - read BuildingState from WorldState.Buildings
    /// </summary>
    public sealed class InspectPanelPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Label _info;
        private Button _btnClear;
        private Button _btnClose;

        private Label _id, _def, _hp;
        private Label _wood, _food, _stone, _iron, _ammo;

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

            if (_btnClear != null) _btnClear.clicked += OnClear;
            if (_btnClose != null) _btnClose.clicked += OnClear;

            if (Ctx?.Store != null)
                Ctx.Store.SelectionChanged += OnSelectionChanged;
        }

        protected override void OnUnbind()
        {
            if (_btnClear != null) _btnClear.clicked -= OnClear;
            if (_btnClose != null) _btnClose.clicked -= OnClear;

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
        }

        private static void Set(Label l, string t) { if (l != null) l.text = t ?? ""; }

        private void OnSelectionChanged(int _) => Refresh();

        private void OnClear()
        {
            Ctx?.Store?.ClearSelection();
            Ctx?.Panels?.HideCurrent();
        }
    }
}
