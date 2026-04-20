using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugNpcTool : MonoBehaviour
    {
        [Header("Bootstrap (required)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("NPC Spawn")]
        [SerializeField] private string _npcDefId = "Worker";
        [SerializeField] private int _spawnBurstCount = 1;

        // IMGUI state for Hub/Quick spawn
        [SerializeField] private string _uiNpcDefId = "Worker";
        [SerializeField] private string _uiSpawnCount = "1";

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
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            if (_cam == null) _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
        }

        private void OnDisable()
        {
            // no-op
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

            // ---- Input polling (Option B: no InputActions in tool) ----
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                if (kb.nKey.wasPressedThisFrame) OnToggle(default);
                if (kb.pKey.wasPressedThisFrame) OnSpawn(default);
                if (kb.cKey.wasPressedThisFrame) OnReleaseAllClaims(default);
            }

            if (_enabled && mouse != null && mouse.leftButton.wasPressedThisFrame)
                OnClick(default);
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
            SpawnInternal(ignoreEnabled: false);
        }

        /// <summary>
        /// Hub/Quick helper: spawn NPCs without requiring the NPC tool to be toggled ON.
        /// </summary>
        public void DebugSpawn(string defId, int burst)
        {
            if (!string.IsNullOrWhiteSpace(defId)) _npcDefId = defId.Trim();
            _spawnBurstCount = Mathf.Max(1, burst);
            SpawnInternal(ignoreEnabled: true);
        }

        private void SpawnInternal(bool ignoreEnabled)
        {
            if (!ignoreEnabled && !_enabled) return;

            if (_world == null || _data == null) return;

            if (!TryFindHQAnchor(out var hqCell))
            {
                _noti?.Push("NpcSpawn_NoHQ", "NPC", "Cannot spawn: HQ not found.", NotificationSeverity.Warning, default, 0.5f, true);
                return;
            }

            var spawnCell = ResolveSpawnCellNearBuilding(hqCell, fallback: hqCell);

            int burst = Mathf.Max(1, _spawnBurstCount);
            for (int i = 0; i < burst; i++)
            {
                var st = new NpcState
                {
                    Id = default,
                    DefId = _npcDefId,
                    Cell = spawnCell,
                    Workplace = default,
                    CurrentJob = default,
                    IsIdle = true
                };

                var id = _world.Npcs.Create(st);
                st.Id = id;
                _world.Npcs.Set(id, st);

                _noti?.Push($"NpcSpawn_{id.Value}", "NPC",
                        $"Spawned NPC #{id.Value} near HQ ({spawnCell.X},{spawnCell.Y})",
                        NotificationSeverity.Info, default, 0.1f, true);
            }
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

                bool isHQ = _data != null && _data.TryGetBuilding(b.DefId, out var def) && def != null && def.IsHQ;

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

        private CellPos ResolveSpawnCellNearBuilding(CellPos buildingAnchor, CellPos fallback)
        {
            if (_grid == null) return fallback;

            // Prefer a ROAD cell around the anchor/perimeter area.
            // Deterministic scan order: +X, -X, +Y, -Y, then small ring.
            var dirs = new[]
            {
                new CellPos(1,0), new CellPos(-1,0), new CellPos(0,1), new CellPos(0,-1),
                new CellPos(2,0), new CellPos(-2,0), new CellPos(0,2), new CellPos(0,-2),
                new CellPos(1,1), new CellPos(-1,1), new CellPos(1,-1), new CellPos(-1,-1),
            };

            // 1) Road first
            for (int i = 0; i < dirs.Length; i++)
            {
                var c = new CellPos(buildingAnchor.X + dirs[i].X, buildingAnchor.Y + dirs[i].Y);
                if (!_grid.IsInside(c)) continue;
                var occ = _grid.Get(c);
                if (occ.Kind == CellOccupancyKind.Road) return c;
            }

            // 2) Empty next
            for (int i = 0; i < dirs.Length; i++)
            {
                var c = new CellPos(buildingAnchor.X + dirs[i].X, buildingAnchor.Y + dirs[i].Y);
                if (!_grid.IsInside(c)) continue;
                var occ = _grid.Get(c);
                if (occ.Kind == CellOccupancyKind.Empty) return c;
            }

            return fallback;
        }

        private ResourceType GuessHarvestResourceType(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return ResourceType.None;

            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse")) return ResourceType.Food;
            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp")) return ResourceType.Wood;
            if (DefIdTierUtil.IsBase(defId, "bld_quarry")) return ResourceType.Stone;
            if (DefIdTierUtil.IsBase(defId, "bld_ironhut")) return ResourceType.Iron;

            return ResourceType.None;
        }

        private int CountAssignedToBuilding(BuildingId buildingId, NpcId excludeNpc)
        {
            int assigned = 0;

            var ids = _world.Npcs.Ids;
            foreach (var nid in ids)
            {
                if (!_world.Npcs.Exists(nid)) continue;
                if (nid.Value == excludeNpc.Value) continue;

                var ns = _world.Npcs.Get(nid);
                if (ns.Workplace.Value == buildingId.Value) assigned++;
            }

            return assigned;
        }

        private int GetMaxAssignedFor(BuildingDef def, int level)
        {
            // clamp level 1..3
            if (level < 1) level = 1;
            else if (level > 3) level = 3;

            if (def == null) return 0;

            // No roles => cannot assign
            if (def.WorkRoles == WorkRoleFlags.None) return 0;

            // HOUSE / TOWER thường không assign worker
            if (def.IsHouse || def.IsTower) return 0;

            // HQ: Build + HaulBasic (hub)
            if (def.IsHQ) return level switch
            {
                1 => 2,
                2 => 3,
                3 => 4,
                _ => 2
            };

            // Warehouse: logistics
            if (def.IsWarehouse) return level switch
            {
                1 => 1,
                2 => 2,
                3 => 3,
                _ => 1
            };

            // Producers (Harvest): Farm/Lumber/Quarry/Ironhut...
            if ((def.WorkRoles & WorkRoleFlags.Harvest) != 0) return level switch
            {
                1 => 1,
                2 => 2,
                3 => 3,
                _ => 1
            };

            // Forge/Craft
            if (def.IsForge || (def.WorkRoles & WorkRoleFlags.Craft) != 0) return level switch
            {
                1 => 1,
                2 => 2,
                3 => 2,
                _ => 1
            };

            // Armory
            if (def.IsArmory || (def.WorkRoles & WorkRoleFlags.Armory) != 0) return level switch
            {
                1 => 1,
                2 => 2,
                3 => 2,
                _ => 1
            };

            // Default conservative
            return 1;
        }
    }
}
