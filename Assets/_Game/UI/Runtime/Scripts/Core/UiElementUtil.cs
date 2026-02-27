using UnityEngine.UIElements;

namespace SeasonalBastion.UI
{
    internal static class UiElementUtil
    {
        public static VisualElement GetOrCreateChild(VisualElement parent, string name)
        {
            if (parent == null) return null;

            var ve = parent.Q<VisualElement>(name);
            if (ve != null) return ve;

            ve = new VisualElement { name = name };
            parent.Add(ve);
            return ve;
        }

        public static T GetOrCreateChild<T>(VisualElement parent, string name) where T : VisualElement, new()
        {
            if (parent == null) return null;

            var ve = parent.Q<T>(name);
            if (ve != null) return ve;

            ve = new T { name = name };
            parent.Add(ve);
            return ve;
        }

        public static void SetVisible(VisualElement ve, bool visible)
        {
            if (ve == null) return;
            ve.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static bool HasClassInHierarchy(VisualElement ve, string className)
        {
            if (ve == null || string.IsNullOrEmpty(className)) return false;
            VisualElement cur = ve;
            while (cur != null)
            {
                if (cur.ClassListContains(className)) return true;
                cur = cur.parent;
            }
            return false;
        }
    }
}