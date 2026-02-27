using SeasonalBastion.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Input
{
    /// <summary>
    /// Hit-test theo USS class ui-block-world.
    /// Không hardcode element name.
    /// </summary>
    public sealed class UiHitTest
    {
        private readonly IInputGate _gate;

        private UIDocument[] _docs = new UIDocument[0];

        public UiHitTest(IInputGate gate)
        {
            _gate = gate;
        }

        public void SetDocuments(params UIDocument[] docs)
        {
            _docs = docs ?? new UIDocument[0];
        }

        public void UpdatePointerBlocking()
        {
            if (_gate == null)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
            {
                _gate.SetPointerOverBlockingUi(false);
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            bool blocked = IsScreenPointOverBlockingUi(screen);
            _gate.SetPointerOverBlockingUi(blocked);
        }

        private bool IsScreenPointOverBlockingUi(Vector2 screenPos)
        {
            for (int i = 0; i < _docs.Length; i++)
            {
                var doc = _docs[i];
                if (doc == null) continue;

                var root = doc.rootVisualElement;
                if (root == null) continue;

                var panel = root.panel;
                if (panel == null) continue;

                // Convert screen -> panel space
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);

                // Pick element at position
                var picked = panel.Pick(panelPos) as VisualElement;
                if (picked == null) continue;

                if (UiElementUtil.HasClassInHierarchy(picked, UiKeys.Class_BlockWorld))
                    return true;
            }

            return false;
        }
    }
}