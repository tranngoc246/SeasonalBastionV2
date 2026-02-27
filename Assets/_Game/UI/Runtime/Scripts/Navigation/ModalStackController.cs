using System;
using System.Collections.Generic;
using SeasonalBastion.UI.Presenters;
using SeasonalBastion.UI.Services;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Navigation
{
    /// <summary>
    /// Modal stack: push/pop modal, quản lý Scrim + pause/resume (optional).
    /// </summary>
    public sealed class ModalStackController
    {
        private readonly UIStateStore _store;
        private readonly IUiPauseController _pause;

        private VisualElement _modalsRoot;
        private VisualElement _scrim;
        private VisualElement _host;

        private readonly Dictionary<string, (IUiPresenter presenter, VisualElement root)> _map =
            new(StringComparer.Ordinal);

        private readonly List<string> _stack = new(4);

        public ModalStackController(UIStateStore store, IUiPauseController pause)
        {
            _store = store;
            _pause = pause;
        }

        public void Bind(VisualElement modalsRoot, VisualElement scrim, VisualElement host)
        {
            _modalsRoot = modalsRoot;
            _scrim = scrim;
            _host = host;

            if (_scrim != null)
            {
                _scrim.pickingMode = PickingMode.Position;
                _scrim.RegisterCallback<ClickEvent>(_ => Pop());
            }

            CloseRoot();
        }

        public void Register(string key, IUiPresenter presenter, VisualElement root)
        {
            if (string.IsNullOrEmpty(key) || presenter == null || root == null) return;

            _map[key] = (presenter, root);
            root.style.display = DisplayStyle.None;
        }

        public bool Push(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_map.TryGetValue(key, out var entry)) return false;

            if (_stack.Count == 0) OpenRoot();
            else HideTopVisual();

            _stack.Add(key);
            _store?.PushModal(key);

            entry.root.style.display = DisplayStyle.Flex;
            entry.presenter.Refresh();
            return true;
        }

        public bool Pop()
        {
            if (_stack.Count <= 0) return false;

            string topKey = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            _store?.PopModal(out _);

            if (_map.TryGetValue(topKey, out var entry))
                entry.root.style.display = DisplayStyle.None;

            if (_stack.Count == 0)
            {
                CloseRoot();
                return true;
            }

            // Show new top
            string newTopKey = _stack[_stack.Count - 1];
            if (_map.TryGetValue(newTopKey, out var newTop))
            {
                newTop.root.style.display = DisplayStyle.Flex;
                newTop.presenter.Refresh();
            }

            return true;
        }

        public void CloseAll()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                string key = _stack[i];
                if (_map.TryGetValue(key, out var entry))
                    entry.root.style.display = DisplayStyle.None;
            }

            _stack.Clear();
            _store?.ClearModals();

            CloseRoot();
        }

        private void HideTopVisual()
        {
            if (_stack.Count <= 0) return;
            string key = _stack[_stack.Count - 1];
            if (_map.TryGetValue(key, out var entry))
                entry.root.style.display = DisplayStyle.None;
        }

        private void OpenRoot()
        {
            if (_modalsRoot != null)
            {
                _modalsRoot.style.display = DisplayStyle.Flex;
                _modalsRoot.pickingMode = PickingMode.Position;
            }

            _pause?.PauseUI();
        }

        private void CloseRoot()
        {
            if (_modalsRoot != null)
            {
                _modalsRoot.style.display = DisplayStyle.None;
                _modalsRoot.pickingMode = PickingMode.Ignore;
            }

            _pause?.ResumeUI();
        }
    }
}