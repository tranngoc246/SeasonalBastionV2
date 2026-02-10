using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class NotificationCenterPresenter
    {
        private readonly GameServices _s;
        private readonly VisualElement _hudRoot;
        private readonly VisualTreeAsset _itemTemplate;
        private readonly NotificationStackPresenter _toastPresenter;

        private WorldSelectionController _selection;
        private WorldCameraController _cameraController;

        private Button _btnIcon;
        private Label _lblBadge;

        private VisualElement _panel;
        private ScrollView _scroll;
        private Button _btnClearAll;

        private bool _open;

        private EventCallback<PointerDownEvent> _outsideClickCb;

        public NotificationCenterPresenter(
            VisualElement hudRoot,
            GameServices s,
            VisualTreeAsset itemTemplate,
            NotificationStackPresenter toastPresenter)
        {
            _hudRoot = hudRoot;
            _s = s;
            _itemTemplate = itemTemplate;
            _toastPresenter = toastPresenter;
        }

        public void Bind()
        {
            var ns = _s?.NotificationService as NotificationService;
            if (ns == null)
            {
                Debug.LogWarning("[UI] NotificationService missing; notification center disabled.");
                return;
            }

            EnsureUi();

            ns.NotificationsChanged += Rebuild;
            Rebuild();
        }

        public void Unbind()
        {
            var ns = _s?.NotificationService as NotificationService;
            if (ns == null) return;

            ns.NotificationsChanged -= Rebuild;
        }

        private void EnsureUi()
        {
            if (_hudRoot == null) return;

            // Icon button (top-left)
            _btnIcon ??= new Button();
            _btnIcon.name = "BtnNotiCenter";
            _btnIcon.text = "!";
            _btnIcon.style.position = Position.Absolute;
            _btnIcon.style.left = StyleKeyword.Auto;
            _btnIcon.style.right = 10; 
            _btnIcon.style.top = 46;
            _btnIcon.style.width = 34;
            _btnIcon.style.height = 34;
            _btnIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            _btnIcon.style.fontSize = 18;
            _btnIcon.style.borderTopLeftRadius = 6;
            _btnIcon.style.borderTopRightRadius = 6;
            _btnIcon.style.borderBottomLeftRadius = 6;
            _btnIcon.style.borderBottomRightRadius = 6;
            _btnIcon.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            _btnIcon.style.color = new StyleColor(Color.white);

            // Badge
            _lblBadge ??= new Label();
            _lblBadge.name = "LblNotiBadge";
            _lblBadge.style.position = Position.Absolute;
            _lblBadge.style.right = 2;
            _lblBadge.style.top = -2;
            _lblBadge.style.minWidth = 16;
            _lblBadge.style.height = 16;
            _lblBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            _lblBadge.style.fontSize = 10;
            _lblBadge.style.backgroundColor = new StyleColor(new Color(0.85f, 0.2f, 0.2f, 1f));
            _lblBadge.style.color = new StyleColor(Color.white);
            _lblBadge.style.borderTopLeftRadius = 8;
            _lblBadge.style.borderTopRightRadius = 8;
            _lblBadge.style.borderBottomLeftRadius = 8;
            _lblBadge.style.borderBottomRightRadius = 8;

            if (_lblBadge.parent != _btnIcon)
                _btnIcon.Add(_lblBadge);

            // Panel
            _panel ??= new VisualElement();
            _panel.name = "NotiCenterPanel";
            _panel.style.position = Position.Absolute;
            _panel.style.left = StyleKeyword.Auto;
            _panel.style.right = 10;
            _panel.style.top = 86;
            _panel.style.width = 380;
            _panel.style.height = 420;
            _panel.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.78f));
            _panel.style.borderTopLeftRadius = 8;
            _panel.style.borderTopRightRadius = 8;
            _panel.style.borderBottomLeftRadius = 8;
            _panel.style.borderBottomRightRadius = 8;
            _panel.style.paddingLeft = 10;
            _panel.style.paddingRight = 10;
            _panel.style.paddingTop = 10;
            _panel.style.paddingBottom = 10;
            _panel.style.display = DisplayStyle.None;

            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            var title = new Label("Notifications");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.color = new StyleColor(Color.white);

            _btnClearAll ??= new Button();
            _btnClearAll.text = "Clear";
            _btnClearAll.style.height = 24;

            header.Add(title);
            header.Add(_btnClearAll);

            // list
            _scroll ??= new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;

            _panel.Clear();
            _panel.Add(header);
            _panel.Add(_scroll);

            // Attach to tree if needed
            if (_btnIcon.parent == null) _hudRoot.Add(_btnIcon);
            if (_panel.parent == null) _hudRoot.Add(_panel);

            // callbacks
            _btnIcon.clicked -= Toggle;
            _btnIcon.clicked += Toggle;

            _btnClearAll.clicked -= ClearAll;
            _btnClearAll.clicked += ClearAll;

            // click outside to close
            if (_outsideClickCb == null)
            {
                _outsideClickCb = OnRootPointerDown;
                _hudRoot.RegisterCallback(_outsideClickCb, TrickleDown.TrickleDown);
            }
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (!_open) return;

            var target = evt.target as VisualElement;
            if (target == null) return;

            // If click inside panel or on icon => ignore
            if ((_panel != null && _panel.Contains(target)) || (_btnIcon != null && _btnIcon.Contains(target)))
                return;

            // Otherwise close
            _open = false;
            if (_panel != null) _panel.style.display = DisplayStyle.None;

            _toastPresenter?.SetSuppressed(false);
            Rebuild();
        }

        private void Toggle()
        {
            _open = !_open;

            if (_panel != null)
                _panel.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;

            // When panel is open: hide toast stack.
            _toastPresenter?.SetSuppressed(_open);

            // Rebuild to ensure list is up-to-date
            Rebuild();
        }

        private void ClearAll()
        {
            var ns = _s?.NotificationService as NotificationService;
            if (ns == null) return;

            ns.ClearInbox();
        }

        private void Rebuild()
        {
            var ns = _s?.NotificationService as NotificationService;
            if (ns == null) return;

            // badge
            int count = ns.GetInboxCount();
            if (_lblBadge != null)
            {
                _lblBadge.text = count.ToString();
                _lblBadge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!_open || _scroll == null)
                return;

            _scroll.Clear();

            var inbox = ns.GetInbox(); // newest-first
            for (int i = 0; i < inbox.Count; i++)
            {
                var vm = inbox[i];
                if (vm == null) continue;

                var item = _itemTemplate != null ? _itemTemplate.CloneTree() : new VisualElement();
                item.AddToClassList("noti-item");

                item.AddToClassList(vm.Severity switch
                {
                    Contracts.NotificationSeverity.Info => "sev-info",
                    Contracts.NotificationSeverity.Warning => "sev-warning",
                    Contracts.NotificationSeverity.Error => "sev-error",
                    _ => "sev-info"
                });

                var lblT = item.Q<Label>("LblTitle");
                if (lblT != null) lblT.text = string.IsNullOrEmpty(vm.Title) ? (vm.Key ?? "NOTICE") : vm.Title;

                var lblB = item.Q<Label>("LblBody");
                if (lblB != null) lblB.text = vm.Body ?? string.Empty;

                // click item => mark read (remove from inbox)
                var id = vm.Id;
                var payload = vm.Payload;

                item.RegisterCallback<ClickEvent>(_ =>
                {
                    if (payload.Building.Value != 0)
                    {
                        var bid = payload.Building;

                        // 1) Focus camera
                        var cam = ResolveCameraController();
                        cam?.TryFocusBuilding(bid, instant: false);

                        // 2) Select building (to open info panel)
                        var sel = ResolveSelection();
                        sel?.SelectBuilding(bid);
                    }

                    ns.MarkRead(id);
                });


                _scroll.Add(item);
            }
        }

        private WorldCameraController ResolveCameraController()
        {
            if (_cameraController != null) return _cameraController;

#if UNITY_2023_1_OR_NEWER
    _cameraController = Object.FindFirstObjectByType<WorldCameraController>(FindObjectsInactive.Include);
#else
            _cameraController = Object.FindObjectOfType<WorldCameraController>(true);
#endif
            return _cameraController;
        }

        private WorldSelectionController ResolveSelection()
        {
            if (_selection != null) return _selection;

#if UNITY_2023_1_OR_NEWER
    _selection = UnityEngine.Object.FindFirstObjectByType<WorldSelectionController>(UnityEngine.FindObjectsInactive.Include);
#else
            _selection = UnityEngine.Object.FindObjectOfType<WorldSelectionController>(true);
#endif
            return _selection;
        }
    }
}
