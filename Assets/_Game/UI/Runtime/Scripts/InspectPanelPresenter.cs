using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class InspectPanelPresenter
    {
        private readonly GameServices _s;
        private readonly WorldSelectionController _sel;

        private readonly VisualElement _panel;
        private readonly Button _btnClose;

        private readonly Label _lblId;
        private readonly Label _lblDef;
        private readonly Label _lblHp;

        private readonly Label _lblWood;
        private readonly Label _lblStone;
        private readonly Label _lblFood;
        private readonly Label _lblIron;
        private readonly Label _lblAmmo;

        private float _pollTimer;
        private readonly float _pollInterval;

        private BuildingId _lastId;
        private BuildingState _lastState;
        private bool _hasLast;

        public InspectPanelPresenter(VisualElement root, GameServices s, WorldSelectionController sel, float pollInterval = 0.33f)
        {
            _s = s;
            _sel = sel;
            _pollInterval = Mathf.Clamp(pollInterval, 0.15f, 1.0f);

            _panel = root.Q<VisualElement>("InspectPanel");
            _btnClose = root.Q<Button>("BtnInspectClose");

            _lblId = root.Q<Label>("LblInspectId");
            _lblDef = root.Q<Label>("LblInspectDef");
            _lblHp = root.Q<Label>("LblInspectHP");

            _lblWood = root.Q<Label>("LblInspectWood");
            _lblStone = root.Q<Label>("LblInspectStone");
            _lblFood = root.Q<Label>("LblInspectFood");
            _lblIron = root.Q<Label>("LblInspectIron");
            _lblAmmo = root.Q<Label>("LblInspectAmmo");
        }

        public void Bind()
        {
            if (_panel != null) _panel.AddToClassList("hidden");
            if (_btnClose != null) _btnClose.clicked += OnCloseClicked;
        }

        public void Unbind()
        {
            if (_btnClose != null) _btnClose.clicked -= OnCloseClicked;
        }

        public void Tick(float dt)
        {
            if (_s == null || _s.WorldState == null || _s.WorldState.Buildings == null) return;
            if (_sel == null) return;

            _pollTimer += dt;
            if (_pollTimer < _pollInterval) return;
            _pollTimer = 0f;

            if (!_sel.HasSelection)
            {
                Hide();
                _hasLast = false;
                return;
            }

            var id = _sel.SelectedBuilding;
            if (id.Value == 0)
            {
                Hide();
                _hasLast = false;
                return;
            }

            if (!_s.WorldState.Buildings.TryGet(id, out var st))
            {
                // building bị destroy / không còn tồn tại
                Hide();
                _sel.ClearSelection();
                _hasLast = false;
                return;
            }

            Show();

            // chỉ repaint khi có thay đổi để tránh alloc vô ích
            if (!_hasLast || id.Value != _lastId.Value || !IsSame(st, _lastState))
            {
                _lastId = id;
                _lastState = st;
                _hasLast = true;

                _lblId.text = $"ID: {st.Id.Value}";
                _lblDef.text = $"Def: {st.DefId ?? "-"}";

                if (st.MaxHP > 0)
                    _lblHp.text = $"HP: {st.HP}/{st.MaxHP}";
                else
                    _lblHp.text = "HP: -";

                _lblWood.text = $"Wood: {st.Wood}";
                _lblStone.text = $"Stone: {st.Stone}";
                _lblFood.text = $"Food: {st.Food}";
                _lblIron.text = $"Iron: {st.Iron}";
                _lblAmmo.text = $"Ammo: {st.Ammo}";
            }
        }

        private void OnCloseClicked()
        {
            if (_sel != null) _sel.ClearSelection();
            Hide();
            _hasLast = false;
        }

        private void Show()
        {
            if (_panel == null) return;
            _panel.RemoveFromClassList("hidden");
        }

        private void Hide()
        {
            if (_panel == null) return;
            _panel.AddToClassList("hidden");
        }

        private static bool IsSame(in BuildingState a, in BuildingState b)
        {
            // compare only what we render (min set)
            return a.Id.Value == b.Id.Value
                && a.DefId == b.DefId
                && a.HP == b.HP
                && a.MaxHP == b.MaxHP
                && a.Wood == b.Wood
                && a.Stone == b.Stone
                && a.Food == b.Food
                && a.Iron == b.Iron
                && a.Ammo == b.Ammo;
        }
    }
}
