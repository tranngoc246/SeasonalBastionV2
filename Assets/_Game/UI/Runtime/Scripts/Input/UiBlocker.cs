using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal static class UiBlocker
    {
        /// <summary>
        /// UI Toolkit-safe: returns true if pointer is over UI that should block world clicks.
        /// Works with multiple UIDocuments (HUD/Panels/Modals).
        /// </summary>
        public static bool IsPointerOverBlockingUi(Vector2 screenPos, UIDocument hud, UIDocument panels, UIDocument modals)
        {
            // Priority: Modals (top) -> Panels -> HUD
            if (IsOverBlockingInDocument(screenPos, modals)) return true;
            if (IsOverBlockingInDocument(screenPos, panels)) return true;
            if (IsOverBlockingInDocument(screenPos, hud)) return true;

            return false;
        }

        private static bool IsOverBlockingInDocument(Vector2 screenPos, UIDocument doc)
        {
            if (doc == null) return false;

            var root = doc.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            // If doc root is hidden, ignore.
            if (root.resolvedStyle.display == DisplayStyle.None)
                return false;

            // IMPORTANT: Panel.Pick expects panel-space coordinates, not screen-space.
            Vector2 panelPos;
            try
            {
                panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
            }
            catch
            {
                // In rare cases (panel not ready), don't block.
                return false;
            }

            var picked = panel.Pick(panelPos);
            if (picked == null) return false;

            // Anything interactive should block world clicks.
            if (picked is Button) return true;
            if (picked is TextField) return true;
            if (picked is Toggle) return true;
            if (picked is Slider) return true;
            if (picked is SliderInt) return true;
            if (picked is ScrollView) return true;

            // Walk parents: block if inside known UI containers (names) or tagged with class.
            for (var ve = picked; ve != null; ve = ve.parent)
            {
                if (ve.ClassListContains("ui-block")) return true;

                // HUD
                if (ve.name == "TopBar") return true;
                if (ve.name == "BottomToolbar") return true;
                if (ve.name == "SpeedGroup") return true;
                if (ve.name == "NotiStack") return true;

                // Panels
                if (ve.name == "BuildPanel") return true;
                if (ve.name == "InspectPanel") return true;

                // Modals
                if (ve.name == "ModalsRoot") return true;
                if (ve.name == "Scrim") return true;
                if (ve.name == "ModalHost") return true;
                if (ve.name == "SettingsModal") return true;
                if (ve.name == "RunEndModal") return true;
            }

            // Otherwise, don't block (allow world clicks).
            return false;
        }
    }
}
