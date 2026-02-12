using SeasonalBastion.Contracts;
using System;
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

            // ---- Assign limit (Max NPC per building by level) ----
            try
            {
                var bs = _world.Buildings.Get(buildingId);
                var def = _data.GetBuilding(bs.DefId);

                int max = GetMaxAssignedFor(def, bs.Level);
                if (max <= 0)
                {
                    _noti?.Push($"NpcAssign_NoSlots_{_selectedNpc.Value}", "NPC",
                        $"Building {bs.DefId} không nhận worker.",
                        NotificationSeverity.Warning, default, 0.35f, true);
                    return;
                }

                int assigned = CountAssignedToBuilding(buildingId, excludeNpc: _selectedNpc);

                if (assigned >= max)
                {
                    _noti?.Push($"NpcAssign_Full_{_selectedNpc.Value}", "NPC",
                        $"Building {bs.DefId} đã đủ worker ({assigned}/{max}).",
                        NotificationSeverity.Warning, default, 0.6f, true);
                    return;
                }
            }
            catch
            {
                _noti?.Push($"NpcAssign_LimitCheckFail_{_selectedNpc.Value}", "NPC",
                    "Assign limit check failed (exception). Check Console.",
                    NotificationSeverity.Warning, default, 0.5f, true);
                // không return để tool vẫn assign nếu fail check (giữ behaviour debug)
            }
            // ---- end assign limit ----

            if (npc.CurrentJob.Value != 0)
            {
                ForceReleaseSelectedNpc("Assign", keepWorkplace: false);
                npc = _world.Npcs.Get(_selectedNpc); // re-fetch sau force release
            }

            npc.Workplace = buildingId;
            npc.IsIdle = true;
            npc.CurrentJob = default;

            _world.Npcs.Set(_selectedNpc, npc);

            _bus?.Publish(new NPCAssignedEvent(_selectedNpc, buildingId));

            _noti?.Push($"NpcAssigned_{_selectedNpc.Value}", "NPC",
                $"NPC #{_selectedNpc.Value} assigned to Building #{buildingId.Value}",
                NotificationSeverity.Info, default, 0.15f, true);

            // Extra diagnostics: explain why NPC may idle + optionally enqueue a starter job for Harvest buildings.
            try
            {
                var bs = _world.Buildings.Get(buildingId);
                var def = _data.GetBuilding(bs.DefId);

                // 1) No roles => expected idle
                if (def.WorkRoles == WorkRoleFlags.None)
                {
                    _noti?.Push($"NpcAssigned_NoRoles_{_selectedNpc.Value}", "NPC",
                        $"Building {bs.DefId} has no WorkRoles => NPC will idle (expected).",
                        NotificationSeverity.Info, default, 0.3f, true);
                    return;
                }

                // 2) Check workplace queue right after assign
                int q = (_jobs != null) ? _jobs.CountForWorkplace(buildingId) : -1;

                // 3) Harvest buildings: ensure zone exists; if queue empty => auto-enqueue 1 Harvest job
                if ((def.WorkRoles & WorkRoleFlags.Harvest) != 0)
                {
                    var rt = GuessHarvestResourceType(bs.DefId);
                    if (rt != ResourceType.None)
                    {
                        var zc = _world.Zones.PickCell(rt, bs.Anchor);
                        if (zc.X == 0 && zc.Y == 0)
                        {
                            _noti?.Push($"NpcAssigned_NoZone_{_selectedNpc.Value}", "NPC",
                                $"No zone cell for {rt} near {bs.DefId} => Harvest cannot start. Check zones seeding.",
                                NotificationSeverity.Warning, default, 0.6f, true);
                            return;
                        }
                    }

                    if (_jobs != null && q == 0)
                    {
                        // enqueue starter harvest job so assigned NPC works immediately
                        _jobs.Enqueue(new Job
                        {
                            Archetype = JobArchetype.Harvest,
                            Status = JobStatus.Created,
                            Workplace = buildingId
                        });

                        _noti?.Push($"NpcAssigned_EnqueueHarvest_{_selectedNpc.Value}", "NPC",
                            $"Enqueued 1 Harvest job for {bs.DefId} => NPC should start moving.",
                            NotificationSeverity.Info, default, 0.3f, true);
                    }
                    else if (q == 0)
                    {
                        _noti?.Push($"NpcAssigned_NoJobBoard_{_selectedNpc.Value}", "NPC",
                            $"JobBoard is null => cannot enqueue jobs; NPC will idle.",
                            NotificationSeverity.Warning, default, 0.6f, true);
                    }

                    return;
                }

                // 4) Non-harvest roles: if queue empty, tell user why likely idle
                if (_jobs != null && q == 0)
                {
                    _noti?.Push($"NpcAssigned_NoJobs_{_selectedNpc.Value}", "NPC",
                        $"No jobs queued for {bs.DefId}. If this is HQ/Warehouse => needs producer local goods; if Build => needs Site; otherwise wait for scheduler.",
                        NotificationSeverity.Info, default, 0.5f, true);
                }
            }
            catch
            {
                _noti?.Push($"NpcAssigned_DiagFail_{_selectedNpc.Value}", "NPC",
                    "Assign diagnostics failed (exception). Check Console for details.",
                    NotificationSeverity.Warning, default, 0.5f, true);
            }
        }

        private void OnReleaseAllClaims(InputAction.CallbackContext _)
        {
            if (!_enabled) return;

            var kb = Keyboard.current;
            bool keepWorkplace = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);

            ForceReleaseSelectedNpc(keepWorkplace ? "Ctrl+R" : "R", keepWorkplace);
        }

        private void ForceReleaseSelectedNpc(string source, bool keepWorkplace)
        {
            if (!_hasSelectedNpc) return;
            if (_world == null || _claims == null) return;
            if (!_world.Npcs.Exists(_selectedNpc)) { _hasSelectedNpc = false; return; }

            var npc = _world.Npcs.Get(_selectedNpc);

            var curJobId = npc.CurrentJob;
            if (curJobId.Value != 0 && _jobs != null && _jobs.TryGet(curJobId, out var job))
            {
                job.Status = JobStatus.Cancelled;
                job.ClaimedBy = default;
                _jobs.Update(job);
            }

            // Release claims
            _claims.ReleaseAll(_selectedNpc);

            // Reset npc state
            npc.IsIdle = true;
            npc.CurrentJob = default;

            if (!keepWorkplace)
                npc.Workplace = default;

            _world.Npcs.Set(_selectedNpc, npc);

            _noti?.Push("ClaimsReleaseAll", "Claims",
                $"ForceRelease NPC #{_selectedNpc.Value} job={(curJobId.Value != 0 ? curJobId.Value : 0)} ({source})",
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

        /// <summary>
        /// Minimal quick UI for Hub: spawn + select + show current job + unassign/release.
        /// </summary>
        public void DrawQuickGUI()
        {
            if (_s == null || _world == null)
            {
                GUILayout.Label("NPC Tool: not bound (GameServices null)");
                return;
            }

            // --- Spawn UI (works even when tool is OFF) ---
            GUILayout.Label("Spawn NPC (near HQ):");
            GUILayout.BeginHorizontal();
            GUILayout.Label("DefId", GUILayout.Width(45));
            _uiNpcDefId = GUILayout.TextField(_uiNpcDefId ?? _npcDefId, GUILayout.Width(140));
            GUILayout.Label("Count", GUILayout.Width(50));
            _uiSpawnCount = GUILayout.TextField(_uiSpawnCount ?? _spawnBurstCount.ToString(), GUILayout.Width(50));
            if (GUILayout.Button("Spawn", GUILayout.Width(80)))
            {
                if (string.IsNullOrWhiteSpace(_uiNpcDefId)) _uiNpcDefId = _npcDefId;
                if (!int.TryParse(_uiSpawnCount, out var n)) n = _spawnBurstCount;
                DebugSpawn(_uiNpcDefId, n);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // --- Select/Info (re-use logic similar to DrawHubGUI, but compact) ---
            GUILayout.Label($"Tool Assign Mode: {(_enabled ? "ON (LMB assign)" : "OFF")} | Selected: {(_hasSelectedNpc ? $"#{_selectedNpc.Value}" : "none")}");

            if (_hasSelectedNpc && _world.Npcs.Exists(_selectedNpc))
            {
                var npc = _world.Npcs.Get(_selectedNpc);
                string wp = npc.Workplace.Value == 0 ? "none" : $"#{npc.Workplace.Value}";
                GUILayout.Label($"WP: {wp}  Idle:{npc.IsIdle}  Cell:({npc.Cell.X},{npc.Cell.Y})");

                if (npc.CurrentJob.Value != 0 && _jobs != null && _jobs.TryGet(npc.CurrentJob, out var job))
                    GUILayout.Label($"Job: #{job.Id.Value} {job.Archetype} {job.Status} Res:{job.ResourceType} Amt:{job.Amount}");
                else
                    GUILayout.Label($"Job: {(npc.CurrentJob.Value == 0 ? "none" : $"#{npc.CurrentJob.Value} (missing in JobBoard)")}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Unassign", GUILayout.Width(110)))
                {
                    npc.Workplace = default;
                    npc.IsIdle = true;
                    npc.CurrentJob = default;
                    _world.Npcs.Set(_selectedNpc, npc);
                    _bus?.Publish(new NPCAssignedEvent(_selectedNpc, default));
                    _noti?.Push($"NpcUnassigned_{_selectedNpc.Value}", "NPC", $"NPC #{_selectedNpc.Value} unassigned", NotificationSeverity.Info, default, 0.2f, true);
                }
                if (GUILayout.Button("Release Claims", GUILayout.Width(140)))
                    ForceReleaseSelectedNpc("Quick", keepWorkplace: false);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);

            // List first 10 NPCs
            var npcIds = _world.Npcs.Ids;
            var tmp = new List<NpcId>(npcIds.Count());
            foreach (var id in npcIds) tmp.Add(id);
            tmp.Sort((a, b) => a.Value.CompareTo(b.Value));

            int show = tmp.Count > 10 ? 10 : tmp.Count;
            for (int i = 0; i < show; i++)
            {
                var id = tmp[i];
                var st = _world.Npcs.Get(id);
                string wp = st.Workplace.Value == 0 ? "none" : $"#{st.Workplace.Value}";
                string job = st.CurrentJob.Value == 0 ? "-" : $"#{st.CurrentJob.Value}";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"#{id.Value} {st.DefId} WP:{wp} Job:{job}", GUILayout.Width(320));
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
                    ForceReleaseSelectedNpc("Button", keepWorkplace: false);
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

            if (defId.Equals("bld_farmhouse_t1", StringComparison.OrdinalIgnoreCase)) return ResourceType.Food;
            if (defId.Equals("bld_lumbercamp_t1", StringComparison.OrdinalIgnoreCase)) return ResourceType.Wood;
            if (defId.Equals("bld_quarry_t1", StringComparison.OrdinalIgnoreCase)) return ResourceType.Stone;
            if (defId.Equals("bld_ironhut_t1", StringComparison.OrdinalIgnoreCase)) return ResourceType.Iron;

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
