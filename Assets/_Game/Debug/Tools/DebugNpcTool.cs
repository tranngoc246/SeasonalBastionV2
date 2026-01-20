using SeasonalBastion.Contracts;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonalBastion.DebugTools
{
    public sealed class DebugNpcTool : MonoBehaviour
    {
        [Header("Bootstrap (required)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("NPC Spawn")]
        [SerializeField] private string _npcDefId = "Worker";
        [SerializeField] private int _spawnBurstCount = 1;

        [Header("Mapping (single source)")]
        [SerializeField] private DebugBuildingTool _mappingSource;
        [SerializeField] private Camera _cameraOverride;

        [Header("Grid Mapping (fallback)")]
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = false;
        [SerializeField] private float _planeY = 0f;
        [SerializeField] private float _planeZ = 0f;

        [SerializeField] private bool _hubControlled;
        public void SetHubControlled(bool v) => _hubControlled = v;
        public void SetEnabledFromHub(bool enabled) { _enabled = enabled; }


        private InputAction _toggle; // N
        private InputAction _spawn;  // P
        private InputAction _click;  // LMB

        private Camera _cam;
        private bool _enabled;

        private GameServices _s;
        private IWorldState _world;
        private IGridMap _grid;
        private IDataRegistry _data;
        private IEventBus _bus;
        private INotificationService _noti;
        private IClaimService _claims;
        private IJobBoard _jobs;

        private NpcId _selectedNpc;
        private bool _hasSelectedNpc;

        private CellPos _hoverCell;
        private bool _hasHover;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            if (_mappingSource == null) _mappingSource = FindObjectOfType<DebugBuildingTool>();

            _toggle = new InputAction("ToggleNpcTool", InputActionType.Button, "<Keyboard>/n");
            _spawn = new InputAction("SpawnNpc", InputActionType.Button, "<Keyboard>/p");
            _click = new InputAction("AssignNpc", InputActionType.Button, "<Mouse>/leftButton");
        }

        private void Start()
        {
            _s = _bootstrap.Services;

            _world = _s.WorldState;
            _grid = _s.GridMap;
            _data = _s.DataRegistry;
            _bus = _s.EventBus;
            _noti = _s.NotificationService;
            _claims = _s.ClaimService;
            _jobs = _s.JobBoard;

            if (_cam == null) _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
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
            // Sync mapping (always, so Hub UI has hover)
            if (_mappingSource == null) _mappingSource = FindObjectOfType<DebugBuildingTool>();
            if (_mappingSource != null)
            {
                _gridOrigin = _mappingSource.GridOrigin;
                _cellSize = Mathf.Max(0.0001f, _mappingSource.CellSize);
                _useXZ = _mappingSource.UseXZ;
                _planeY = _mappingSource.PlaneY;
                _planeZ = _mappingSource.PlaneZ;
            }

            if (!_enabled)
            {
                _hasHover = false;
                return;
            }

            _hasHover = TryGetCellUnderMouse(out _hoverCell);
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (_hubControlled) return;

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
            if (!_enabled) return;

            if (_world == null || _data == null) return;

            if (!TryFindHQAnchor(out var hqCell))
            {
                _noti?.Push("NpcSpawn_NoHQ", "NPC", "Cannot spawn: HQ not found.", NotificationSeverity.Warning, default, 0.5f, true);
                return;
            }

            int burst = Mathf.Max(1, _spawnBurstCount);
            for (int i = 0; i < burst; i++)
            {
                var st = new NpcState
                {
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

                _noti?.Push($"NpcSpawn_{id.Value}", "NPC", $"Spawned NPC #{id.Value} at HQ ({hqCell.X},{hqCell.Y})",
                    NotificationSeverity.Info, default, 0.1f, true);
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

            if (!TryGetCellUnderMouse(out var cell))
                return;

            if (!_grid.IsInside(cell))
            {
                _noti?.Push("NpcAssign_OutOfBounds", "NPC", "Out of bounds.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            var occ = _grid.Get(cell);
            if (occ.Kind != CellOccupancyKind.Building || occ.Building.Value == 0)
            {
                _noti?.Push("NpcAssign_NotBuilding", "NPC", $"Not a building cell ({cell.X},{cell.Y}).", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            var buildingId = occ.Building;

            var npc = _world.Npcs.Get(_selectedNpc);
            npc.Workplace = buildingId;
            npc.IsIdle = true;
            npc.CurrentJob = default;
            _world.Npcs.Set(_selectedNpc, npc);

            _bus?.Publish(new NPCAssignedEvent(_selectedNpc, buildingId));

            _noti?.Push($"NpcAssigned_{_selectedNpc.Value}", "NPC",
                $"NPC #{_selectedNpc.Value} assigned to Building #{buildingId.Value}",
                NotificationSeverity.Info, default, 0.15f, true);
        }

        private bool TryFindHQAnchor(out CellPos hqCell)
        {
            hqCell = default;
            if (_world == null || _data == null) return false;

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
                catch { }

                if (!isHQ && b.DefId == "HQ") isHQ = true;
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

        public void DrawHubGUI()
        {
            GUILayout.Label($"Tool: {(_enabled ? "ON (N)" : "OFF (N)")} | Spawn: P | Hover: {(_hasHover ? $"({_hoverCell.X},{_hoverCell.Y})" : "none")}");

            if (_hasHover && _grid != null && _grid.IsInside(_hoverCell))
            {
                var o = _grid.Get(_hoverCell);
                string extra = o.Kind == CellOccupancyKind.Building ? $"B#{o.Building.Value}" :
                               o.Kind == CellOccupancyKind.Site ? $"S#{o.Site.Value}" : "-";
                GUILayout.Label($"Hover Occ: {o.Kind} {extra}");
            }

            GUILayout.Space(6);

            var npcIds = _world.Npcs.Ids;
            var tmp = new List<NpcId>(npcIds.Count());
            foreach (var npcid in npcIds) tmp.Add(npcid);
            tmp.Sort((a, b) => a.Value.CompareTo(b.Value));

            int unassigned = 0;
            for (int i = 0; i < tmp.Count; i++)
            {
                var st = _world.Npcs.Get(tmp[i]);
                if (st.Workplace.Value == 0) unassigned++;
            }

            GUILayout.Label($"NPCs: {tmp.Count} | Unassigned: {unassigned}");
            GUILayout.Label($"Selected: {(_hasSelectedNpc ? $"#{_selectedNpc.Value}" : "none")}");

            GUILayout.Space(6);
            GUILayout.Label("Claims / Jobs:");

            if (_claims != null)
                GUILayout.Label($"ActiveClaimsCount: {_claims.ActiveClaimsCount}");
            else
                GUILayout.Label("ClaimService = null");

            if (_hasSelectedNpc && _world.Npcs.Exists(_selectedNpc))
            {
                if (GUILayout.Button("Release All Claims (Selected NPC)", GUILayout.Width(260)))
                {
                    _claims?.ReleaseAll(_selectedNpc);
                    _noti?.Push("ClaimsReleaseAll", "Claims",
                        $"Released all claims for NPC #{_selectedNpc.Value}",
                        NotificationSeverity.Info, default, 0.15f, true);
                }

                var npc = _world.Npcs.Get(_selectedNpc);
                var wp = npc.Workplace;

                GUILayout.Label($"Workplace: {(wp.Value == 0 ? "none" : $"#{wp.Value}")}");

                if (_jobs != null && wp.Value != 0)
                {
                    int c = _jobs.CountForWorkplace(wp);
                    GUILayout.Label($"Jobs in workplace queue: {c}");

                    if (_jobs.TryPeekForWorkplace(wp, out var j))
                    {
                        GUILayout.Label($"Peek: #{j.Id.Value} {j.Archetype} {j.Status} Amt:{j.Amount} Res:{j.ResourceType}");
                    }
                    else
                    {
                        GUILayout.Label("Peek: (none)");
                    }
                }
                else if (_jobs == null)
                {
                    GUILayout.Label("JobBoard = null");
                }
            }
            else
            {
                GUILayout.Label("Select an NPC to enable ReleaseAllClaims + Workplace job view.");
            }

            int show = tmp.Count > 12 ? 12 : tmp.Count;
            for (int i = 0; i < show; i++)
            {
                var id = tmp[i];
                var st = _world.Npcs.Get(id);

                GUILayout.BeginHorizontal();

                string wp = st.Workplace.Value == 0 ? "none" : $"#{st.Workplace.Value}";
                GUILayout.Label($"#{id.Value} {st.DefId} @({st.Cell.X},{st.Cell.Y})  WP:{wp}", GUILayout.Width(320));

                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    _selectedNpc = id;
                    _hasSelectedNpc = true;
                    _noti?.Push("NpcSelect", "NPC", $"Selected NPC #{id.Value}", NotificationSeverity.Info, default, 0.1f, true);
                }

                GUILayout.EndHorizontal();
            }

            if (tmp.Count > show)
                GUILayout.Label($"... ({tmp.Count - show} more)");
        }

        private void OnGUI()
        {
            if (DebugHubState.Enabled) return;

            if (_world == null) return;

            GUILayout.BeginArea(new Rect(10f, 300f, 520f, 220f), GUI.skin.box);

            DrawHubGUI();

            GUILayout.EndArea();
        }

        private bool TryGetCellUnderMouse(out CellPos cell)
        {
            cell = default;
            if (_cam == null) return false;
            if (Mouse.current == null) return false;

            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            Plane p = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ));

            if (!p.Raycast(ray, out float enter))
                return false;

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - _gridOrigin;

            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = _useXZ ? Mathf.FloorToInt(local.z / _cellSize) : Mathf.FloorToInt(local.y / _cellSize);

            cell = new CellPos(x, y);
            return true;
        }
    }
}
