using SeasonalBastion.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Input
{
    /// <summary>
    /// Hit-test theo USS class ui-block-world.
    /// Kh�ng hardcode element name.
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

                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
                var picked = panel.Pick(panelPos) as VisualElement;
                if (picked == null) continue;

                var blocking = FindBlockingAncestor(picked, panelPos);
                if (blocking != null)
                    return true;
            }

            return false;
        }

        private static VisualElement FindBlockingAncestor(VisualElement picked, Vector2 panelPos)
        {
            var current = picked;
            while (current != null)
            {
                if (current.ClassListContains(UiKeys.Class_BlockWorld) && current.worldBound.Contains(panelPos))
                    return current;
                current = current.parent;
            }

            return null;
        }
    }
}