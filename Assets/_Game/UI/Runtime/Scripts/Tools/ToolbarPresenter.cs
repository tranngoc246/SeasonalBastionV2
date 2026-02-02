using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Bottom toolbar presenter.
    /// Buttons:
    /// - BUILD (opens build list panel)
    /// - ROAD (road tool)
    /// - REMOVE (remove tool)
    /// - ESC (cancel current tool)
    /// - SET (toggle settings modal)
    /// </summary>
    internal sealed class ToolbarPresenter
    {
        private readonly ToolModeController _toolMode;
        private readonly ModalsPresenter _modals;

        private readonly Button _btnBuild;
        private readonly Button _btnRoad;
        private readonly Button _btnRemove;
        private readonly Button _btnCancel;
        private readonly Button _btnSettings;

        public ToolbarPresenter(VisualElement hudRoot, ToolModeController toolMode, ModalsPresenter modals)
        {
            _toolMode = toolMode;
            _modals = modals;

            _btnBuild = hudRoot.Q<Button>("BtnToolBuild");
            _btnRoad = hudRoot.Q<Button>("BtnToolRoad");
            _btnRemove = hudRoot.Q<Button>("BtnToolRemove");
            _btnCancel = hudRoot.Q<Button>("BtnToolCancel");
            _btnSettings = hudRoot.Q<Button>("BtnToolSettings");
        }

        public void Bind()
        {
            if (_btnBuild != null) _btnBuild.clicked += OnBuild;
            if (_btnRoad != null) _btnRoad.clicked += OnRoad;
            if (_btnRemove != null) _btnRemove.clicked += OnRemove;
            if (_btnCancel != null) _btnCancel.clicked += OnCancel;
            if (_btnSettings != null) _btnSettings.clicked += OnSettings;

            if (_toolMode != null)
                _toolMode.ModeChanged += OnModeChanged;

            RefreshActiveButtons(_toolMode != null ? _toolMode.Mode : ToolMode.None);
        }

        public void Unbind()
        {
            if (_btnBuild != null) _btnBuild.clicked -= OnBuild;
            if (_btnRoad != null) _btnRoad.clicked -= OnRoad;
            if (_btnRemove != null) _btnRemove.clicked -= OnRemove;
            if (_btnCancel != null) _btnCancel.clicked -= OnCancel;
            if (_btnSettings != null) _btnSettings.clicked -= OnSettings;

            if (_toolMode != null)
                _toolMode.ModeChanged -= OnModeChanged;
        }

        private void OnBuild()
        {
            _toolMode?.ToggleBuildPanel();
        }

        private void OnRoad()
        {
            _toolMode?.ToggleRoadTool();
        }

        private void OnRemove()
        {
            _toolMode?.ToggleRemoveTool();
        }

        private void OnCancel()
        {
            _toolMode?.CancelActiveTool();
        }

        private void OnSettings()
        {
            // Prefer using existing modal presenter for pause/settings.
            _modals?.ToggleSettingsExternal();
        }

        private void OnModeChanged(ToolMode mode)
        {
            RefreshActiveButtons(mode);
        }

        private void RefreshActiveButtons(ToolMode mode)
        {
            SetActiveClass(_btnBuild, mode == ToolMode.Build);
            SetActiveClass(_btnRoad, mode == ToolMode.Road);
            SetActiveClass(_btnRemove, mode == ToolMode.Remove);
        }

        private static void SetActiveClass(Button btn, bool active)
        {
            if (btn == null) return;
            const string cls = "is-active";
            if (active) btn.AddToClassList(cls);
            else btn.RemoveFromClassList(cls);
        }
    }
}
