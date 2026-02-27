using System;
using System.Collections.Generic;
using SeasonalBastion.UI.Presenters;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Navigation
{
    /// <summary>
    /// Panels: 1 active panel tại 1 thời điểm (simple).
    /// Có thể mở rộng thành Left/Right dock nếu bạn muốn.
    /// </summary>
    public sealed class PanelRegistry
    {
        private readonly UIStateStore _store;

        private readonly Dictionary<string, (IUiPresenter presenter, VisualElement root)> _map =
            new(StringComparer.Ordinal);

        private string _currentKey = "";

        public PanelRegistry(UIStateStore store)
        {
            _store = store;
        }

        public void Register(string key, IUiPresenter presenter, VisualElement root)
        {
            if (string.IsNullOrEmpty(key) || presenter == null || root == null) return;
            _map[key] = (presenter, root);
            root.style.display = DisplayStyle.None;
        }

        public bool Show(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_map.TryGetValue(key, out var entry)) return false;

            HideCurrent();

            _currentKey = key;
            entry.root.style.display = DisplayStyle.Flex;
            entry.presenter.Refresh();

            _store?.SetActivePanel(key);
            return true;
        }

        public void HideCurrent()
        {
            if (string.IsNullOrEmpty(_currentKey)) return;

            if (_map.TryGetValue(_currentKey, out var entry))
                entry.root.style.display = DisplayStyle.None;

            _currentKey = "";
            _store?.SetActivePanel("");
        }

        public bool IsOpen(string key) => string.Equals(_currentKey, key, StringComparison.Ordinal);
    }
}