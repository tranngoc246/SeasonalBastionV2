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

        private static bool IsInsideVisible(VisualElement ve, Vector2 panelPos)
        {
            if (ve == null) return false;
            if (ve.resolvedStyle.display == DisplayStyle.None) return false;
            // worldBound dùng tọa độ panel-space
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

            // Screen -> Panel space
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

            // ---- Strict container-based blocking (NO Pick) ----
            // HUD blockers
            if (IsInsideVisible(root.Q<VisualElement>("TopBar"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("BottomToolbar"), panelPos)) return true;
            // NotiStack: chỉ block khi cursor nằm trên toast card thật (child), không block theo container
            var noti = root.Q<VisualElement>("NotiStack");
            if (IsInsideAnyVisibleChild(noti, panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("SpeedGroup"), panelPos)) return true;

            // Panels blockers
            if (IsInsideVisible(root.Q<VisualElement>("BuildPanel"), panelPos)) return true;
            var inspect = root.Q<VisualElement>("InspectPanel");
            if (inspect != null && inspect.resolvedStyle.display != DisplayStyle.None)
            {
                // chỉ block trên phần header/body thực sự
                var header = inspect.Q<VisualElement>(className: "panel-header");
                var body = inspect.Q<VisualElement>(className: "panel-body");
                if (IsInsideVisible(header, panelPos)) return true;
                if (IsInsideVisible(body, panelPos)) return true;
            }

            // Modals blockers
            if (IsInsideVisible(root.Q<VisualElement>("ModalsRoot"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("Scrim"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("ModalHost"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("SettingsModal"), panelPos)) return true;
            if (IsInsideVisible(root.Q<VisualElement>("RunEndModal"), panelPos)) return true;

            // Optional: nếu bạn có thêm container nào muốn block bằng class "ui-block"
            // thì KHÔNG dùng Query().ToList() để tránh alloc; thay vào đó add name cụ thể ở trên.

            return false;
        }
    }
}
