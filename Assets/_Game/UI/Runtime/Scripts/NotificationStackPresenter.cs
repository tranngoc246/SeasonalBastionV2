using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class NotificationStackPresenter
    {
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
            if (_s?.NotificationService == null)
            {
                Debug.LogWarning("[UI] NotificationService missing; notifications UI disabled.");
                return;
            }

            _s.NotificationService.NotificationsChanged += Rebuild;
            Rebuild();
        }

        public void Unbind()
        {
            if (_s?.NotificationService == null) return;
            _s.NotificationService.NotificationsChanged -= Rebuild;
        }

        private void Rebuild()
        {
            if (_stack == null) return;
            var ns = _s?.NotificationService;
            if (ns == null) return;

            _stack.Clear();

            var list = ns.GetVisible();
            if (list == null || list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                var vm = list[i];

                VisualElement item = _itemTemplate != null ? _itemTemplate.CloneTree() : new VisualElement();
                item.AddToClassList("noti-item");

                // Severity class
                item.AddToClassList(vm.Severity switch
                {
                    NotificationSeverity.Info => "sev-info",
                    NotificationSeverity.Warning => "sev-warning",
                    NotificationSeverity.Error => "sev-error",
                    _ => "sev-info"
                });

                // Fill labels
                var title = item.Q<Label>("LblTitle");
                if (title != null) title.text = vm.Title ?? vm.Key ?? "NOTICE";

                var body = item.Q<Label>("LblBody");
                if (body != null) body.text = vm.Body ?? "";

                // Click to dismiss (optional, useful for solo dev)
                item.RegisterCallback<ClickEvent>(_ => ns.Dismiss(vm.Id));

                _stack.Add(item);
            }
        }
    }
}
