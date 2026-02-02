using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Coordinates tool modes (Build/Road/Remove) + ESC clean exit.
    /// Lives on the same GameObject as UiRoot.
    /// </summary>
    internal sealed class ToolModeController : MonoBehaviour
    {
        public event Action<ToolMode> ModeChanged;

        private GameServices _s;
        private WorldSelectionController _selection;
        private VisualElement _panelsRoot;
        private UIDocument _hudDoc;
        private UIDocument _panelsDoc;
        private UIDocument _modalsDoc;

        private PlacementPreviewView _preview;
        private RoadRuntimeView _roadsView;

        private BuildPlacementController _buildTool;
        private RoadPlacementController _roadTool;
        private RemoveToolController _removeTool;

        private BuildPanelPresenter _buildPanel;

        public ToolMode Mode { get; private set; } = ToolMode.None;

        public void Bind(
            GameServices s,
            WorldSelectionController selection,
            UIDocument hudDoc,
            UIDocument panelsDoc,
            UIDocument modalsDoc,
            VisualElement panelsRoot)

        {
            _s = s;
            _selection = selection;
            _panelsRoot = panelsRoot;
            _hudDoc = hudDoc;
            _panelsDoc = panelsDoc;
            _modalsDoc = modalsDoc;

            EnsureComponents();

            _roadsView.Bind(_s, _selection);
            _buildTool.Bind(_s, _selection, _preview, _hudDoc, _panelsDoc, _modalsDoc);
            _roadTool.Bind(_s, _selection, _roadsView, _hudDoc, _panelsDoc, _modalsDoc);
            _removeTool.Bind(_s, _selection, _roadsView, _hudDoc, _panelsDoc, _modalsDoc);

            _buildTool.Placed += OnBuildPlaced;

            SetMode(ToolMode.None);
        }

        internal void SetBuildPanelPresenter(BuildPanelPresenter buildPanel)
        {
            _buildPanel = buildPanel;
        }

        public void Unbind()
        {
            if (_buildTool != null)
                _buildTool.Placed -= OnBuildPlaced;

            _buildTool?.End();
            _roadTool?.End();
            _removeTool?.End();

            _preview?.Clear();
            _roadsView?.Unbind();

            _s = null;
            _selection = null;
            _panelsRoot = null;
            _buildPanel = null;
            _hudDoc = null;
            _panelsDoc = null;
            _modalsDoc = null;

            Mode = ToolMode.None;
        }

        private void EnsureComponents()
        {
            if (_preview == null)
                _preview = GetComponent<PlacementPreviewView>() ?? gameObject.AddComponent<PlacementPreviewView>();

            if (_roadsView == null)
                _roadsView = GetComponent<RoadRuntimeView>() ?? gameObject.AddComponent<RoadRuntimeView>();

            if (_buildTool == null)
                _buildTool = GetComponent<BuildPlacementController>() ?? gameObject.AddComponent<BuildPlacementController>();

            if (_roadTool == null)
                _roadTool = GetComponent<RoadPlacementController>() ?? gameObject.AddComponent<RoadPlacementController>();

            if (_removeTool == null)
                _removeTool = GetComponent<RemoveToolController>() ?? gameObject.AddComponent<RemoveToolController>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // ESC always cancels active tool (even if build panel is open)
                if (Mode != ToolMode.None)
                {
                    CancelActiveTool();
                }
                else
                {
                    // if no tool, close build panel if open
                    if (_buildPanel != null && _buildPanel.IsVisible)
                        _buildPanel.Hide();
                }
            }
        }

        public void ToggleBuildPanel()
        {
            if (_buildPanel == null) return;

            // If other tool active, cancel first (clean)
            if (Mode != ToolMode.None && Mode != ToolMode.Build)
                CancelActiveTool();

            _buildPanel.Toggle();
        }

        public void BeginBuildWithDef(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return;

            // Close build panel, enter Build mode
            _buildPanel?.Hide();
            SetMode(ToolMode.Build);
            _buildTool.Begin(defId);
        }

        public void ToggleRoadTool()
        {
            if (Mode == ToolMode.Road)
            {
                CancelActiveTool();
                return;
            }

            _buildPanel?.Hide();
            SetMode(ToolMode.Road);
            _roadTool.Begin();
        }

        public void ToggleRemoveTool()
        {
            if (Mode == ToolMode.Remove)
            {
                CancelActiveTool();
                return;
            }

            _buildPanel?.Hide();
            SetMode(ToolMode.Remove);
            _removeTool.Begin();
        }

        public void CancelActiveTool()
        {
            switch (Mode)
            {
                case ToolMode.Build:
                    _buildTool.End();
                    break;
                case ToolMode.Road:
                    _roadTool.End();
                    break;
                case ToolMode.Remove:
                    _removeTool.End();
                    break;
            }

            SetMode(ToolMode.None);
        }

        private void SetMode(ToolMode mode)
        {
            Mode = mode;

            if (_selection != null)
                _selection.BlockSelection = (Mode != ToolMode.None);

            ModeChanged?.Invoke(Mode);
        }

        private void OnBuildPlaced()
        {
            // Keep build tool active (allow multi-place). Player can ESC to exit.
        }
    }
}
