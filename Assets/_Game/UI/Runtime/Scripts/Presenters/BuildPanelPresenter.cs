using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Build panel:
    /// - list unlocked + placeable buildable nodes (ids) from runtime DataRegistry
    /// - click item => notify + (TODO) set placement tool selection
    /// </summary>
    public sealed class BuildPanelPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Button _btnClose;
        private ScrollView _list;
        private Label _hint;

        private readonly List<string> _cachedIds = new(256);
        private bool _builtOnce;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _btnClose = Root.Q<Button>("BtnClose");
            _list = Root.Q<ScrollView>("BuildList");
            _hint = Root.Q<Label>("LblBuildHint");

            if (_btnClose != null) _btnClose.clicked += OnClose;

            if (_hint != null && string.IsNullOrEmpty(_hint.text))
                _hint.text = "Chọn công trình để đặt.";

            BuildList();
        }

        protected override void OnUnbind()
        {
            if (_btnClose != null) _btnClose.clicked -= OnClose;
            _s = null;
        }

        protected override void OnRefresh()
        {
            BuildList(force: false);
        }

        private void OnClose()
        {
            Ctx?.Panels?.HideCurrent();
        }

        private void BuildList(bool force = true)
        {
            if (_list == null) return;
            if (!force && _builtOnce) return;

            _list.Clear();
            _cachedIds.Clear();

            var s = _s;
            if (s?.DataRegistry == null)
            {
                _list.Add(new Label("DataRegistry missing"));
                return;
            }

            // Prefer runtime DataRegistry implementation (has GetAllBuildableNodeIds()).
            // If not available, fallback to showing nothing.
            if (s.DataRegistry is SeasonalBastion.DataRegistry dr)
            {
                foreach (var id in dr.GetAllBuildableNodeIds())
                {
                    if (string.IsNullOrEmpty(id)) continue;

                    // Placeable gate (nodeId)
                    if (!s.DataRegistry.IsPlaceableBuildable(id)) continue;

                    // Unlock gate (if service exists)
                    if (s.UnlockService != null && !s.UnlockService.IsUnlocked(id))
                        continue;

                    _cachedIds.Add(id);
                }
            }

            if (_cachedIds.Count == 0)
            {
                _list.Add(new Label("No buildables (maybe BuildablesGraph missing or unlocks closed)"));
                _builtOnce = true;
                return;
            }

            // Deterministic sort
            _cachedIds.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _cachedIds.Count; i++)
            {
                string id = _cachedIds[i];
                _list.Add(MakeItem(id));
            }

            _builtOnce = true;
        }

        private VisualElement MakeItem(string defId)
        {
            var root = new VisualElement();
            root.AddToClassList("build-item");
            root.AddToClassList(UiKeys.Class_BlockWorld);
            root.pickingMode = PickingMode.Position;

            var title = new Label(defId);
            title.AddToClassList("build-item-title");
            root.Add(title);

            string sub = BuildSub(defId);
            if (!string.IsNullOrEmpty(sub))
            {
                var s = new Label(sub);
                s.AddToClassList("build-item-sub");
                root.Add(s);
            }

            root.RegisterCallback<ClickEvent>(_ =>
            {
                // TODO: wire to placement tool state
                _s?.NotificationService?.Push(
                    key: "ui.build.select",
                    title: "Build selected",
                    body: defId,
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(default, default, defId),
                    cooldownSeconds: 0.5f,
                    dedupeByKey: false);
            });

            return root;
        }

        private string BuildSub(string defId)
        {
            var s = _s;
            if (s?.DataRegistry == null) return "";
            var def = s.DataRegistry.GetBuilding(defId);
            if (def == null) return "";

            // size + roles + hp
            return $"{def.SizeX}x{def.SizeY}  Lv{def.BaseLevel}  HP {def.MaxHp}";
        }
    }
}
