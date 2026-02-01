using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class NotificationStackPresenter
    {
        private const int MaxVisible = 3;

        private readonly GameServices _s;
        private readonly VisualElement _stack;
        private readonly VisualTreeAsset _itemTemplate;

        public NotificationStackPresenter(VisualElement root, GameServices s, VisualTreeAsset itemTemplate)
        {
            _s = s;
            _stack = root.Q<VisualElement>("NotiStack");
            _itemTemplate = itemTemplate;
        }

        public void Bind()
        {
            var ns = _s?.NotificationService;
            if (ns == null)
            {
                Debug.LogWarning("[UI] NotificationService missing; notifications UI disabled.");
                return;
            }

            ns.NotificationsChanged += Rebuild;
            Rebuild();
        }

        public void Unbind()
        {
            var ns = _s?.NotificationService;
            if (ns == null) return;

            ns.NotificationsChanged -= Rebuild;
        }

        private void Rebuild()
        {
            if (_stack == null) return;

            var ns = _s?.NotificationService;
            if (ns == null) return;

            _stack.Clear();

            // Quan trọng: không giả định kiểu collection.
            // Chỉ cần IEnumerable theo đúng thứ tự service trả về (newest first theo spec).
            int shown = 0;
            foreach (var vm in ns.GetVisible())
            {
                AddItem(ns, vm);
                shown++;
                if (shown >= MaxVisible) break;
            }
        }

        private void AddItem(INotificationService ns, NotificationViewModel vm)
        {
            VisualElement item = _itemTemplate != null ? _itemTemplate.CloneTree() : new VisualElement();

            // đảm bảo class nền
            item.AddToClassList("noti-item");

            // severity class
            item.AddToClassList(vm.Severity switch
            {
                NotificationSeverity.Info => "sev-info",
                NotificationSeverity.Warning => "sev-warning",
                NotificationSeverity.Error => "sev-error",
                _ => "sev-info"
            });

            // fill text
            var title = item.Q<Label>("LblTitle");
            if (title != null) title.text = string.IsNullOrEmpty(vm.Title) ? (vm.Key ?? "NOTICE") : vm.Title;

            var body = item.Q<Label>("LblBody");
            if (body != null) body.text = vm.Body ?? string.Empty;

            // optional: click to dismiss
            item.RegisterCallback<ClickEvent>(_ => ns.Dismiss(vm.Id));

            _stack.Add(item);
        }
    }
}
