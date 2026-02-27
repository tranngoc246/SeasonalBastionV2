using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Overlay
{
    /// <summary>
    /// Toast cực tối giản: add label vào ToastHost, tự timeout.
    /// </summary>
    public sealed class ToastController
    {
        private VisualElement _host;

        private struct ToastItem
        {
            public Label Label;
            public float T;
        }

        private readonly List<ToastItem> _items = new(8);

        public void Bind(VisualElement host)
        {
            _host = host;
            if (_host != null)
                _host.pickingMode = PickingMode.Ignore;
        }

        public void Show(string text, float seconds = 2.0f)
        {
            if (_host == null) return;

            var lbl = new Label(text ?? "");
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            lbl.style.paddingLeft = 8;
            lbl.style.paddingRight = 8;
            lbl.style.paddingTop = 6;
            lbl.style.paddingBottom = 6;

            // Không block world
            lbl.pickingMode = PickingMode.Ignore;

            _host.Add(lbl);
            _items.Add(new ToastItem { Label = lbl, T = seconds });
        }

        public void Tick(float dt)
        {
            if (_items.Count == 0) return;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                it.T -= dt;

                if (it.T <= 0f)
                {
                    it.Label?.RemoveFromHierarchy();
                    _items.RemoveAt(i);
                    continue;
                }

                _items[i] = it;
            }
        }
    }
}