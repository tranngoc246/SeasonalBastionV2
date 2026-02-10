using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Build placement tool.
    /// - Mouse hover => preview footprint (green/red)
    /// - LMB => try place
    /// - Q/E => rotate
    /// - RMB/ESC => cancel (ESC handled by ToolModeController)
    ///
    /// Rules (from existing PlacementService):
    /// - must fit inside map
    /// - must not overlap
    /// - must have road near entry cell (driveway length 1)
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        public event Action Placed;

        private GameServices _s;
        private WorldSelectionController _mapper;
        private PlacementPreviewView _preview;
        private UIDocument _hudDoc;
        private UIDocument _panelsDoc;
        private UIDocument _modalsDoc;


        private bool _active;
        private string _defId;
        private Dir4 _rot = Dir4.N;

        private readonly List<CellPos> _footprint = new(32);

        public bool IsActive => _active;
        public string CurrentDefId => _defId;
        public Dir4 CurrentRot => _rot;

        public void Bind(
            GameServices s,
            WorldSelectionController mapper,
            PlacementPreviewView preview,
            UIDocument hudDoc,
            UIDocument panelsDoc,
            UIDocument modalsDoc)
        {
            _s = s;
            _mapper = mapper;
            _preview = preview;

            _hudDoc = hudDoc;
            _panelsDoc = panelsDoc;
            _modalsDoc = modalsDoc;
        }


        public void Begin(string defId)
        {
            _defId = defId;
            _rot = Dir4.N;
            _active = !string.IsNullOrEmpty(defId);

            if (_active && _s?.NotificationService != null)
            {
                _s.NotificationService.Push(
                    key: "tool_build",
                    title: "BUILD",
                    body: "LMB: place • Q/E: rotate • ESC: cancel",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 1.0f,
                    dedupeByKey: true
                );
            }
        }

        public void End()
        {
            _active = false;
            _defId = null;
            _preview?.Clear();
        }

        private void Update()
        {
            if (!_active) return;
            if (_s == null || _s.PlacementService == null || _s.DataRegistry == null) return;
            if (_mapper == null || _preview == null) return;
            if (Mouse.current == null) return;

            // rotate
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame) _rot = RotateLeft(_rot);
                if (Keyboard.current.eKey.wasPressedThisFrame) _rot = RotateRight(_rot);
            }

            // hover -> preview
            if (!_mapper.TryScreenToCell(Mouse.current.position.ReadValue(), out var cell))
                return;

            if (!_s.GridMap.IsInside(cell))
            {
                _preview.Clear();
                return;
            }

            if (!TryGetBuildingDef(_s.DataRegistry, _defId, out var def))
            {
                _preview.Clear();
                return;
            }

            int w = Mathf.Max(1, def.SizeX);
            int h = Mathf.Max(1, def.SizeY);
            ComputeFootprint(cell, w, h, _rot, _footprint);

            var v = _s.PlacementService.ValidateBuilding(_defId, cell, _rot);
            _preview.ShowCells(_mapper, _footprint, v.Ok, v.SuggestedRoadCell);

            // click -> place
            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;

            // ignore if clicking UI (uGUI)
            if (UiBlocker.IsPointerOverBlockingUi(Mouse.current.position.ReadValue(), _hudDoc, _panelsDoc, _modalsDoc))
                return;

            if (!v.Ok)
            {
                // Click invalid placement -> show 1 deduped notification
                _s?.NotificationService?.Push(
                    key: "CantPlace",
                    title: "Can't place",
                    body: DescribeFail(v.Reason),
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, "placement"),
                    cooldownSeconds: 0.35f,
                    dedupeByKey: true
                );
                return;
            }


            var bid = _s.PlacementService.CommitBuilding(_defId, cell, _rot);
            if (bid.Value <= 0)
            {
                return;
            }

            Placed?.Invoke();
        }

        private static bool TryGetBuildingDef(IDataRegistry reg, string defId, out BuildingDef def)
        {
            def = null;
            if (reg == null || string.IsNullOrEmpty(defId)) return false;

            try
            {
                def = reg.GetBuilding(defId);
                return def != null;
            }
            catch
            {
                def = null;
                return false;
            }
        }

        private static string DescribeFail(PlacementFailReason r)
        {
            return r switch
            {
                PlacementFailReason.OutOfBounds => "Out of bounds.",
                PlacementFailReason.Overlap => "Overlaps road/building.",
                PlacementFailReason.NoRoadConnection => "Need road near the entry (driveway length = 1).",
                PlacementFailReason.InvalidRotation => "Invalid rotation.",
                PlacementFailReason.BlockedBySite => "Blocked by construction site.",
                PlacementFailReason.Unknown => "Unknown placement rule failure.",
                _ => "Invalid placement."
            };
        }

        private static void ComputeFootprint(CellPos anchor, int w, int h, Dir4 rot, List<CellPos> outCells)
        {
            outCells.Clear();

            // Simple deterministic footprint preview
            if (rot == Dir4.N)
            {
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        outCells.Add(new CellPos(anchor.X + dx, anchor.Y + dy));
            }
            else if (rot == Dir4.E)
            {
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        outCells.Add(new CellPos(anchor.X + dy, anchor.Y + (w - 1 - dx)));
            }
            else if (rot == Dir4.S)
            {
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        outCells.Add(new CellPos(anchor.X + (w - 1 - dx), anchor.Y + (h - 1 - dy)));
            }
            else // W
            {
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        outCells.Add(new CellPos(anchor.X + (h - 1 - dy), anchor.Y + dx));
            }
        }

        private static Dir4 RotateLeft(Dir4 d)
        {
            return d switch
            {
                Dir4.N => Dir4.W,
                Dir4.W => Dir4.S,
                Dir4.S => Dir4.E,
                _ => Dir4.N,
            };
        }

        private static Dir4 RotateRight(Dir4 d)
        {
            return d switch
            {
                Dir4.N => Dir4.E,
                Dir4.E => Dir4.S,
                Dir4.S => Dir4.W,
                _ => Dir4.N,
            };
        }
    }
}
