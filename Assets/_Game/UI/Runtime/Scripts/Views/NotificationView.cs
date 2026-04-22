using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Views
{
    public sealed class NotificationView
    {
        private const int MaxVisibleNotifications = 3;
        private const string BaseItemClass = "notification-panel__item";
        private const string TextClass = "notification-panel__text";
        private const string EnterClass = "notification-panel__item--enter";
        private const string InfoClass = "notification-panel__item--info";
        private const string WarningClass = "notification-panel__item--warning";
        private const string ErrorClass = "notification-panel__item--error";

        private readonly List<VisualElement> _notificationItems = new(MaxVisibleNotifications);
        private VisualElement _root;
        private VisualElement _list;

        public void Initialize(VisualElement root)
        {
            _root = root;
            _list = root?.Q<VisualElement>("NotificationList") ?? root;
            Clear();
        }

        public void Show(string message)
        {
            Show(message, NotificationKind.Info);
        }

        public void Show(string message, NotificationKind kind)
        {
            if (_list == null || string.IsNullOrWhiteSpace(message))
                return;

            var itemElement = CreateItem(message, kind);
            _list.Insert(0, itemElement);
            _notificationItems.Insert(0, itemElement);

            itemElement.schedule.Execute(() => itemElement.RemoveFromClassList(EnterClass)).ExecuteLater(0);

            TrimExcess();
        }

        public void Clear()
        {
            for (var index = 0; index < _notificationItems.Count; index++)
                _notificationItems[index]?.RemoveFromHierarchy();

            _notificationItems.Clear();

            _list?.Clear();
        }

        public void SetNotifications(IReadOnlyList<NotificationItemViewModel> items)
        {
            Clear();
            if (items == null)
                return;

            for (var index = items.Count - 1; index >= 0; index--)
            {
                var item = items[index];
                if (item == null)
                    continue;

                Show(item.Message, item.Kind);
            }
        }

        private VisualElement CreateItem(string message, NotificationKind kind)
        {
            var itemElement = new VisualElement();
            itemElement.pickingMode = PickingMode.Ignore;
            itemElement.AddToClassList(BaseItemClass);
            itemElement.AddToClassList(EnterClass);
            itemElement.AddToClassList(GetKindClass(kind));

            var textLabel = new Label(message);
            textLabel.pickingMode = PickingMode.Ignore;
            textLabel.AddToClassList(TextClass);
            itemElement.Add(textLabel);

            return itemElement;
        }

        private void TrimExcess()
        {
            while (_notificationItems.Count > MaxVisibleNotifications)
            {
                var lastIndex = _notificationItems.Count - 1;
                var lastItem = _notificationItems[lastIndex];
                lastItem?.RemoveFromHierarchy();
                _notificationItems.RemoveAt(lastIndex);
            }
        }

        private static string GetKindClass(NotificationKind kind)
        {
            return kind switch
            {
                NotificationKind.Warning => WarningClass,
                NotificationKind.Error => ErrorClass,
                _ => InfoClass,
            };
        }
    }

    public sealed class NotificationItemViewModel
    {
        public string Message { get; set; }
        public NotificationKind Kind { get; set; } = NotificationKind.Info;
    }

    public enum NotificationKind
    {
        Info,
        Warning,
        Error,
    }
}
