using SeasonalBastion.Contracts;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.EventSystems.EventTrigger;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Day 8 (PART27) - Debug tool:
    /// - Press P: spawn NPC at HQ cell
    /// - Press N: toggle tool ON/OFF
    /// - When ON: select NPC in HUD -> click a building cell to assign workplace
    /// - Publishes NPCAssignedEvent + pushes notifications
    /// </summary>
    public sealed class DebugNpcTool : MonoBehaviour
    {
        [Header("Bootstrap (required)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("NPC Spawn")]
        [SerializeField] private string _npcDefId = "Worker";
        [SerializeField] private int _spawnBurstCount = 1; // set to 3 if you want quick acceptance test

        [Header("Grid Mapping (match DebugBuildingTool)")]
        [SerializeField] private DebugBuildingTool _mappingSource;
        [SerializeField] private Camera _cameraOverride;
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = false;
        [SerializeField] private float _planeY = 0f;

        private InputAction _toggle; // N
        private InputAction _spawn;  // P
        private InputAction _click;  // LMB

        private Camera _cam;
        private bool _enabled;

        // Services
        private GameServices _s;
        private IWorldState _world;
        private IGridMap _grid;
        private IDataRegistry _data;
        private IEventBus _bus;
        private INotificationService _noti;

        // Selection
        private NpcId _selectedNpc;
        private bool _hasSelectedNpc;

        // Cached hover
        private CellPos _hoverCell;
        private bool _hasHover;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            if (_mappingSource == null) _mappingSource = FindObjectOfType<DebugBuildingTool>();

            // Runtime actions - do not touch InputActions asset
            _toggle = new InputAction("ToggleNpcTool", InputActionType.Button, "<Keyboard>/n");
            _spawn = new InputAction("SpawnNpc", InputActionType.Button, "<Keyboard>/p");
            _click = new InputAction("AssignNpc", InputActionType.Button, "<Mouse>/leftButton");
        }

        private void Start()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _s = _bootstrap.Services;

            _world = _s.WorldState;
            _grid = _s.GridMap;
            _data = _s.DataRegistry;
            _bus = _s.EventBus;
            _noti = _s.NotificationService;

            if (_cam == null) _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            if (_mappingSource == null) _mappingSource = FindObjectOfType<DebugBuildingTool>();

        }

        private void OnEnable()
        {
            _toggle.Enable();
            _spawn.Enable();
            _click.Enable();

            _toggle.performed += OnToggle;
            _spawn.performed += OnSpawn;
            _click.performed += OnClick;
        }

        private void OnDisable()
        {
            _toggle.performed -= OnToggle;
            _spawn.performed -= OnSpawn;
            _click.performed -= OnClick;

            _toggle.Disable();
            _spawn.Disable();
            _click.Disable();

            _enabled = false;
            _hasSelectedNpc = false;
            _hasHover = false;
        }

        private void Update()
        {
            // Hover cell is always updated for HUD feedback (even when tool off)
            if (_mappingSource != null)
            {
                _gridOrigin = _mappingSource.GridOrigin;
                _cellSize = Mathf.Max(0.0001f, _mappingSource.CellSize);
                _useXZ = _mappingSource.UseXZ;
                _planeY = _mappingSource.PlaneY;
            }

            _hasHover = TryGetCellUnderMouse(out _hoverCell);
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            _enabled = !_enabled;

            _noti?.Push(
                key: "DebugNpcTool",
                title: "Debug",
                body: _enabled ? "NPC Tool: ON (P spawn, HUD select, LMB assign)" : "NPC Tool: OFF",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.2f,
                dedupeByKey: true
            );
        }

        private void OnSpawn(InputAction.CallbackContext _)
        {
            if (_world == null || _data == null)
                return;

            if (!TryFindHQAnchor(out var hqCell))
            {
                _noti?.Push(
                    key: "NpcSpawn_NoHQ",
                    title: "NPC",
                    body: "Cannot spawn: HQ not found.",
                    severity: NotificationSeverity.Warning,
                    payload: default,
                    cooldownSeconds: 0.5f,
                    dedupeByKey: true
                );
                return;
            }

            int burst = Mathf.Max(1, _spawnBurstCount);
            for (int i = 0; i < burst; i++)
            {
                var st = new NpcState
                {
                    // Id will be set after Create()
                    Id = default,
                    DefId = _npcDefId,
                    Cell = hqCell,
                    Workplace = default,
                    CurrentJob = default,
                    IsIdle = true
                };

                var id = _world.Npcs.Create(st);
                st.Id = id;
                _world.Npcs.Set(id, st);

                _noti?.Push(
                    key: $"NpcSpawn_{id.Value}",
                    title: "NPC",
                    body: $"Spawned NPC #{id.Value} at HQ ({hqCell.X},{hqCell.Y})",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 0.1f,
                    dedupeByKey: true
                );
            }
        }

        private void OnClick(InputAction.CallbackContext _)
        {
            if (!_enabled) return;
            if (!_hasSelectedNpc) return;
            if (_world == null || _grid == null) return;

            if (!_world.Npcs.Exists(_selectedNpc))
            {
                _hasSelectedNpc = false;
                return;
            }

            if (!TryGetCellUnderMouse(out var clickCell))
            {
                _noti?.Push("NpcAssign_NoHover", "NPC", "Mouse not over grid.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            var occ = _grid.Get(clickCell);
            if (occ.Kind != CellOccupancyKind.Building)
            {
                _noti?.Push(
                key: "NpcAssign_NotBuilding",
                title: "NPC",
                body: $"Not a Building. Cell=({clickCell.X},{clickCell.Y}) Kind={occ.Kind}",
                severity: NotificationSeverity.Warning,
                payload: default,
                cooldownSeconds: 0.2f,
                dedupeByKey: true
                                );
                return;
            }

            var buildingId = occ.Building;
            if (buildingId.Value == 0)
            {
                _noti?.Push("NpcAssign_ZeroId", "NPC", "Resolved BuildingId=0 (unexpected).", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            // Assign workplace
            var npc = _world.Npcs.Get(_selectedNpc);
            npc.Workplace = buildingId;
            npc.IsIdle = true;
            npc.CurrentJob = default;
            _world.Npcs.Set(_selectedNpc, npc);

            _bus?.Publish(new NPCAssignedEvent(_selectedNpc, buildingId));

            _noti?.Push(
                key: $"NpcAssigned_{_selectedNpc.Value}",
                title: "NPC",
                body: $"NPC #{_selectedNpc.Value} assigned to Building #{buildingId.Value}",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.15f,
                dedupeByKey: true
            );
        }

        private bool TryFindHQAnchor(out CellPos hqCell)
        {
            hqCell = default;
            if (_world == null || _data == null) return false;

            // Deterministic: pick smallest building id that is HQ
            var ids = _world.Buildings.Ids;
            int bestId = int.MaxValue;
            CellPos bestCell = default;
            bool found = false;

            foreach (var bid in ids)
            {
                var b = _world.Buildings.Get(bid);

                bool isHQ = false;
                try
                {
                    var def = _data.GetBuilding(b.DefId);
                    isHQ = def != null && def.IsHQ;
                }
                catch
                {
                    // ignore
                }

                if (!isHQ)
                {
                    // fallback by id string
                    if (b.DefId == "HQ") isHQ = true;
                }

                if (!isHQ) continue;

                if (bid.Value < bestId)
                {
                    bestId = bid.Value;
                    bestCell = b.Anchor;
                    found = true;
                }
            }

            if (!found) return false;

            hqCell = bestCell;
            return true;
        }

        // ---------------------------------------------------------
        // HUD
        // ---------------------------------------------------------

        private void OnGUI()
        {
            if (_world == null) return;

            //const float w = 420f;
            //const float h = 300f;
            //var r = new Rect(10f, 10f, w, h);
            //GUI.Box(r, "NPC Debug (Day 8)");

            GUILayout.BeginArea(new Rect(10f, 300f, 520f, 200f), GUI.skin.box);

            GUILayout.Label($"Tool: {(_enabled ? "ON (N toggle)" : "OFF (N toggle)")} | Spawn: P | Hover: {(_hasHover ? $"({_hoverCell.X},{_hoverCell.Y})" : "none")}");
            if (_hasHover && _grid != null)
            {
                var o = _grid.Get(_hoverCell);
                string extra = o.Kind == CellOccupancyKind.Building ? $"B#{o.Building.Value}" :
                o.Kind == CellOccupancyKind.Site ? $"S#{o.Site.Value}" : "-";
                GUILayout.Label($"Hover Occ: {o.Kind} {extra}");
            }
            GUILayout.Space(6);

            // List NPCs sorted by id.Value (deterministic)
            var npcIds = _world.Npcs.Ids;
            var tmp = new List<NpcId>(npcIds.Count());
            foreach (var npcid in npcIds) tmp.Add(npcid);
            tmp.Sort((a, b) => a.Value.CompareTo(b.Value));

            int unassigned = 0;
            for (int i = 0; i < tmp.Count; i++)
            {
                var id = tmp[i];
                var st = _world.Npcs.Get(id);
                if (st.Workplace.Value == 0) unassigned++;
            }

            GUILayout.Label($"NPCs: {tmp.Count} | Unassigned: {unassigned}");
            GUILayout.Space(6);

            GUILayout.Label($"Selected: {(_hasSelectedNpc ? $"#{_selectedNpc.Value}" : "none")}");

            // Show list (limit to 12 rows to avoid spamming)
            int show = tmp.Count > 12 ? 12 : tmp.Count;
            for (int i = 0; i < show; i++)
            {
                var id = tmp[i];
                var st = _world.Npcs.Get(id);

                GUILayout.BeginHorizontal();

                string wp = st.Workplace.Value == 0 ? "none" : $"#{st.Workplace.Value}";
                GUILayout.Label($"#{id.Value} {st.DefId} @({st.Cell.X},{st.Cell.Y})  WP:{wp}", GUILayout.Width(300));

                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    _selectedNpc = id;
                    _hasSelectedNpc = true;

                    _noti?.Push(
                        key: "NpcSelect",
                        title: "NPC",
                        body: $"Selected NPC #{id.Value}",
                        severity: NotificationSeverity.Info,
                        payload: default,
                        cooldownSeconds: 0.1f,
                        dedupeByKey: true
                    );
                }

                GUILayout.EndHorizontal();
            }

            if (tmp.Count > show)
                GUILayout.Label($"... ({tmp.Count - show} more)");

            GUILayout.EndArea();
        }

        // ---------------------------------------------------------
        // Mouse -> CellPos (copied style from DebugBuildingTool)
        // ---------------------------------------------------------

        private bool TryGetCellUnderMouse(out CellPos cell)
        {
            cell = default;
            if (_cam == null) return false;
            if (Mouse.current == null) return false;

            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane p = _useXZ ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f)) : new Plane(Vector3.forward, new Vector3(0f, 0, _mappingSource.PlaneZ));

            if (!p.Raycast(ray, out float enter))
                return false;

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - _gridOrigin;

            int x, y;
            if (_useXZ)
            {
                x = Mathf.FloorToInt(local.x / _cellSize);
                y = Mathf.FloorToInt(local.z / _cellSize);
            }
            else
            {
                x = Mathf.FloorToInt(local.x / _cellSize);
                y = Mathf.FloorToInt(local.y / _cellSize);
            }

            cell = new CellPos(x, y);
            return _grid == null || _grid.IsInside(cell);
        }
    }
}
