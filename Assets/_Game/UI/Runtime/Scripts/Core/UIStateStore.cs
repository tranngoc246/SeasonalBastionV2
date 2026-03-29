using System;
using System.Collections.Generic;

namespace SeasonalBastion.UI
{
    /// <summary>
    /// Store tối giản: selection + active panel + modal stack.
    /// Không MVVM nặng, chỉ cần event theo thay đổi state.
    /// </summary>
    public sealed class UIStateStore
    {
        public event Action<int> SelectionChanged;
        public event Action<SelectionRef> SelectionRefChanged;
        public event Action<string> ActivePanelChanged;
        public event Action<int> ModalStackChanged;

        private int _selectedId = -1;
        private SelectionRef _selected = SelectionRef.None;
        private string _activePanelKey = "";
        private readonly List<string> _modalStack = new(4);

        public int SelectedId => _selectedId;
        public SelectionRef Selected => _selected;
        public string ActivePanelKey => _activePanelKey;
        public int ModalCount => _modalStack.Count;

        public bool HasModal => _modalStack.Count > 0;

        public void Select(int id)
        {
            Select(SelectionRef.Building(id));
        }

        public void Select(SelectionRef selection)
        {
            if (_selected.Kind == selection.Kind && _selected.Id == selection.Id)
                return;

            _selected = selection;
            _selectedId = selection.Kind == SelectionKind.Building ? selection.Id : -1;
            SelectionChanged?.Invoke(_selectedId);
            SelectionRefChanged?.Invoke(_selected);
        }

        public void SelectBuilding(int id) => Select(SelectionRef.Building(id));
        public void SelectResourcePatch(int id) => Select(SelectionRef.ResourcePatch(id));
        public void ClearSelection() => Select(SelectionRef.None);

        public void SetActivePanel(string key)
        {
            key ??= "";
            if (string.Equals(_activePanelKey, key, StringComparison.Ordinal)) return;
            _activePanelKey = key;
            ActivePanelChanged?.Invoke(_activePanelKey);
        }

        public void PushModal(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _modalStack.Add(key);
            ModalStackChanged?.Invoke(_modalStack.Count);
        }

        public bool PopModal(out string poppedKey)
        {
            if (_modalStack.Count <= 0)
            {
                poppedKey = "";
                return false;
            }

            int last = _modalStack.Count - 1;
            poppedKey = _modalStack[last];
            _modalStack.RemoveAt(last);
            ModalStackChanged?.Invoke(_modalStack.Count);
            return true;
        }

        public void ClearModals()
        {
            if (_modalStack.Count == 0) return;
            _modalStack.Clear();
            ModalStackChanged?.Invoke(0);
        }

        public string PeekModalKey()
        {
            if (_modalStack.Count <= 0) return "";
            return _modalStack[_modalStack.Count - 1];
        }
    }
}