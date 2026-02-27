using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal static class UiBlocker
    {
        /// <summary>
        /// UI Toolkit-safe: returns true if pointer is over UI that should block world clicks.
        /// Works with multiple UIDocuments (HUD/Panels/Modals).
        ///
        /// IMPORTANT:
        /// - New Input System / Input.mousePosition: screen origin is BOTTOM-LEFT.
        /// - UI Toolkit panel coordinates: TOP-LEFT when converting via RuntimePanelUtils.ScreenToPanel.
        /// => Must flip Y: yTopLeft = Screen.height - yBottomLeft
        /// </summary>
        public static bool IsPointerOverBlockingUi(Vector2 screenPosBottomLeft, UIDocument hud, UIDocument panels, UIDocument modals)
        {
            // Priority: Modals (top) -> Panels -> HUD
            if (IsOverBlockingInDocument(screenPosBottomLeft, modals)) return true;
            if (IsOverBlockingInDocument(screenPosBottomLeft, panels)) return true;
            if (IsOverBlockingInDocument(screenPosBottomLeft, hud)) return true;

            return false;
        }

        private static bool IsInsideVisible(VisualElement ve, Vector2 panelPos)
        {
            if (ve == null) return false;
            if (ve.resolvedStyle.display == DisplayStyle.None) return false;
            return ve.worldBound.Contains(panelPos);
        }

        private static bool IsInsideAnyVisibleChild(VisualElement parent, Vector2 panelPos)
        {
            if (parent == null) return false;
            if (parent.resolvedStyle.display == DisplayStyle.None) return false;

            var cc = parent.childCount;
            for (int i = 0; i < cc; i++)
            {
                var ch = parent[i];
                if (ch == null) continue;
                if (ch.resolvedStyle.display == DisplayStyle.None) continue;
                if (ch.worldBound.Contains(panelPos)) return true;
            }
            return false;
        }

        private static bool IsOverBlockingInDocument(Vector2 screenPosBottomLeft, UIDocument doc)
        {
            if (doc == null) return false;

            var root = doc.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            if (root.resolvedStyle.display == DisplayStyle.None)
                return false;

            // ---- FIX: flip Y (bottom-left -> top-left) BEFORE ScreenToPanel ----
            Vector2 screenPosTopLeft = screenPosBottomLeft;
            screenPosTopLeft.y = Screen.height - screenPosTopLeft.y;

            Vector2 panelPos;
            try
            {
                panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPosTopLeft);
            }
            catch
            {
                return false;
            }

            // ---- Blocking areas ----

            // HUD blockers
            if (IsInsideVisible(root.Q<VisualElement>("TopBar"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("BottomToolbar"), panelPos)) return true;

            // NotiStack: block only when cursor is on actual toast card (child)
            var noti = root.Q<VisualElement>("NotiStack");
            if (IsInsideAnyVisibleChild(noti, panelPos)) return true;

            if (IsInsideVisible(root.Q<VisualElement>("SpeedGroup"), panelPos)) return true;

            // Panels blockers
            if (IsInsideVisible(root.Q<VisualElement>("BuildPanel"), panelPos)) return true;

            // IMPORTANT: block on whole InspectPanel, not only header/body
            if (IsInsideVisible(root.Q<VisualElement>("InspectPanel"), panelPos)) return true;

            // Modals blockers
            if (IsInsideVisible(root.Q<VisualElement>("ModalsRoot"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("Scrim"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("ModalHost"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("SettingsModal"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("RunEndModal"), panelPos)) return true;

            return false;
        }
    }
}