using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class PlacementUiGate
    {
        private readonly UIDocument _hudDoc;
        private readonly UIDocument _panelsDoc;
        private readonly UIDocument _modalsDoc;
        private readonly string _blockClass;

        public PlacementUiGate(UIDocument hudDoc, UIDocument panelsDoc, UIDocument modalsDoc, string blockClass)
        {
            _hudDoc = hudDoc;
            _panelsDoc = panelsDoc;
            _modalsDoc = modalsDoc;
            _blockClass = blockClass;
        }

        public bool IsPointerOverBlockingUi()
        {
            if (_hudDoc == null && _panelsDoc == null && _modalsDoc == null)
                return false;

            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            Vector2 screen = mouse.position.ReadValue();
            return IsOverBlocking(_modalsDoc, screen)
                || IsOverBlocking(_panelsDoc, screen)
                || IsOverBlocking(_hudDoc, screen);
        }

        private bool IsOverBlocking(UIDocument document, Vector2 screen)
        {
            if (document == null)
                return false;

            var root = document.rootVisualElement;
            if (root == null)
                return false;

            var panel = root.panel;
            if (panel == null)
                return false;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screen);
            var picked = panel.Pick(panelPos) as VisualElement;
            if (picked == null)
                return false;

            var current = picked;
            while (current != null)
            {
                if (current.ClassListContains(_blockClass))
                    return true;
                current = current.parent;
            }

            return false;
        }
    }
}
