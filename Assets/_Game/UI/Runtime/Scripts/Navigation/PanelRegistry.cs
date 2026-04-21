using System;
using System.Collections.Generic;
using SeasonalBastion.UI.Presenters;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Navigation
{
    /// <summary>
    /// Panels are tracked independently by key. This allows left/right dock panels
    /// to coexist, while callers can still explicitly hide the panel they own.
    /// </summary>
    public sealed class PanelRegistry
    {
        private readonly UIStateStore _store;

        private readonly Dictionary<string, (IUiPresenter presenter, VisualElement root)> _map =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _openKeys = new(StringComparer.Ordinal);

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

            entry.root.style.display = DisplayStyle.Flex;
            entry.presenter.Refresh();
            _openKeys.Add(key);
            _store?.SetActivePanel(key);
            return true;
        }

        public void Hide(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_map.TryGetValue(key, out var entry)) return;

            entry.root.style.display = DisplayStyle.None;
            _openKeys.Remove(key);

            if (_store != null && string.Equals(_store.ActivePanelKey, key, StringComparison.Ordinal))
                _store.SetActivePanel("");
        }

        public void HideCurrent()
        {
            if (_store == null || string.IsNullOrEmpty(_store.ActivePanelKey)) return;
            Hide(_store.ActivePanelKey);
        }

        public bool IsOpen(string key) => !string.IsNullOrEmpty(key) && _openKeys.Contains(key);
    }
}
