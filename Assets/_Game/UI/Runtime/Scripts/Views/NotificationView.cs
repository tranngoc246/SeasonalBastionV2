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

        private readonly List<NotificationItemViewModel> _notificationItems = new(MaxVisibleNotifications);
        private ListView _list;

        public void Initialize(VisualElement root)
        {
            _list = root?.Q<ListView>("NotificationList");
            ConfigureList();
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

            _notificationItems.Insert(0, new NotificationItemViewModel { Message = message, Kind = kind });
            TrimExcess();
            _list.itemsSource = _notificationItems;
            _list.Rebuild();
        }

        public void Clear()
        {
            _notificationItems.Clear();

            if (_list != null)
            {
                _list.itemsSource = _notificationItems;
                _list.Rebuild();
            }
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

        private void ConfigureList()
        {
            if (_list == null)
                return;

            _list.selectionType = SelectionType.None;
            _list.makeItem = CreateItem;
            _list.bindItem = BindItem;
            _list.unbindItem = UnbindItem;
            _list.fixedItemHeight = 52f;
            _list.itemsSource = _notificationItems;
        }

        private VisualElement CreateItem()
        {
            var itemElement = new VisualElement();
            itemElement.pickingMode = PickingMode.Ignore;
            itemElement.AddToClassList(BaseItemClass);

            var textLabel = new Label();
            textLabel.pickingMode = PickingMode.Ignore;
            textLabel.AddToClassList(TextClass);
            itemElement.Add(textLabel);
            itemElement.userData = textLabel;
            return itemElement;
        }

        private void BindItem(VisualElement itemElement, int index)
        {
            if (itemElement?.userData is not Label textLabel)
                return;

            if (index < 0 || index >= _notificationItems.Count)
            {
                UnbindItem(itemElement, index);
                return;
            }

            var item = _notificationItems[index];
            textLabel.text = item?.Message ?? string.Empty;
            itemElement.RemoveFromClassList(EnterClass);
            itemElement.RemoveFromClassList(InfoClass);
            itemElement.RemoveFromClassList(WarningClass);
            itemElement.RemoveFromClassList(ErrorClass);
            itemElement.AddToClassList(GetKindClass(item?.Kind ?? NotificationKind.Info));
            itemElement.AddToClassList(EnterClass);
            itemElement.schedule.Execute(() => itemElement.RemoveFromClassList(EnterClass)).ExecuteLater(0);
        }

        private void UnbindItem(VisualElement itemElement, int index)
        {
            if (itemElement?.userData is not Label textLabel)
                return;

            textLabel.text = string.Empty;
            itemElement.RemoveFromClassList(EnterClass);
            itemElement.RemoveFromClassList(InfoClass);
            itemElement.RemoveFromClassList(WarningClass);
            itemElement.RemoveFromClassList(ErrorClass);
        }

        private void TrimExcess()
        {
            while (_notificationItems.Count > MaxVisibleNotifications)
                _notificationItems.RemoveAt(_notificationItems.Count - 1);
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
