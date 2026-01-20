using UnityEngine;
using SeasonalBastion.Contracts;
using UnityEngine.InputSystem;
using System;

namespace SeasonalBastion.DebugTools
{
    public sealed class DebugStorageHUD : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Key _toggleKey = Key.S;
        [SerializeField] private Key _lockKey = Key.L;
        [SerializeField] private int _addAmountDefault = 50;
        [SerializeField] private bool _hubControlled;
        [SerializeField] private Key _freezeFromKey = Key.F;

        private bool _hasFrozenFrom;
        private CellPos _frozenFrom;

        private bool _hasLocked;
        private BuildingId _lockedBuilding;

        private string _addAmountStr = "50";

        // --- Day10 Flow Debug ---
        private bool _hasLastDest;
        private StoragePick _lastDest;

        private bool _hasLastSource;
        private StoragePick _lastSource;

        private GameServices _gs;

        private void Awake()
        {
            _gs = FindObjectOfType<GameBootstrap>()?.Services;
        }

        private void Update()
        {
            TryResolveServices();

            var kb = Keyboard.current;
            if (kb == null) return;

            if (!_hubControlled && kb[_toggleKey].wasPressedThisFrame)
                _enabled = !_enabled;

            if (!_enabled) return;

            if (kb[_lockKey].wasPressedThisFrame)
            {
                // nếu đang hover building thì lock vào nó, nếu không thì unlock
                if (TryGetHoverBuilding(out var bid))
                {
                    _hasLocked = true;
                    _lockedBuilding = bid;
                    Debug.Log($"[DebugStorageHUD] Locked BuildingId={bid.Value}");
                }
                else
                {
                    _hasLocked = false;
                    Debug.Log("[DebugStorageHUD] Unlocked");
                }
            }

            if (kb[_freezeFromKey].wasPressedThisFrame)
            {
                if (MouseCellSharedState.HasValue)
                {
                    _hasFrozenFrom = true;
                    _frozenFrom = MouseCellSharedState.Cell;
                    Debug.Log($"[DebugStorageHUD] Frozen FromCell = {_frozenFrom.X},{_frozenFrom.Y}");
                }
                else
                {
                    _hasFrozenFrom = false;
                    Debug.Log("[DebugStorageHUD] Frozen FromCell cleared (no hover)");
                }
            }
        }

        public void SetHubControlled(bool v) => _hubControlled = v;

        public void SetEnabledFromHub(bool enabled)
        {
            _enabled = enabled;
            // reset hover cache nhẹ, giữ locked state
        }

        private bool TryGetHoverBuilding(out BuildingId bid)
        {
            bid = default;

            if (_gs == null || _gs.GridMap == null) return false;
            if (!MouseCellSharedState.HasValue) return false;

            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);
            if (occ.Kind != CellOccupancyKind.Building) return false;

            bid = occ.Building;
            return bid.Value != 0;
        }

        private void TryResolveServices()
        {
            if (_gs != null) return;

            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap != null)
                _gs = bootstrap.Services;
        }

        private void AddRes(BuildingId bid, ResourceType type, int amount)
        {
            if (_gs == null || _gs.StorageService == null) return;

            int added = _gs.StorageService.Add(bid, type, amount);
            Debug.Log($"[DebugStorageHUD] Add {type} x{amount} -> added={added} to BuildingId={bid.Value}");
        }

        private CellPos GetFromCellFallback(BuildingId target)
        {
            if (_hasFrozenFrom)
                return _frozenFrom;

            if (MouseCellSharedState.HasValue)
                return MouseCellSharedState.Cell;

            if (_gs != null && _gs.WorldState != null && _gs.WorldState.Buildings.Exists(target))
                return _gs.WorldState.Buildings.Get(target).Anchor;

            return new CellPos(0, 0);
        }

        private void DrawFlowDebugUI(BuildingId target)
        {
            // Require ResourceFlowService
            if (_gs == null || _gs.ResourceFlowService == null)
            {
                GUILayout.Space(8);
                GUILayout.Label("ResourceFlowService = null (Day10 not wired?)");
                return;
            }

            var from = GetFromCellFallback(target);

            GUILayout.Space(10);
            GUILayout.Label("Day10 Flow Debug (Pick/Transfer):");
            GUILayout.Label(_hasFrozenFrom
                            ? $"FromCell: {_frozenFrom.X},{_frozenFrom.Y} (FROZEN - press F to update)"
                            : $"FromCell: {from.X},{from.Y} (hover)");

            // ---- Pick Dest (Warehouses / Armory) ----
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pick Dest (Wood)"))
            {
                _hasLastDest = _gs.ResourceFlowService.TryPickDest(from, ResourceType.Wood, 1, out _lastDest);
                Debug.Log(_hasLastDest
                    ? $"[FlowDebug] PickDest Wood -> id={_lastDest.Building.Value} dist={_lastDest.Distance}"
                    : "[FlowDebug] PickDest Wood -> NONE");
            }

            if (GUILayout.Button("Pick Dest (Food)"))
            {
                _hasLastDest = _gs.ResourceFlowService.TryPickDest(from, ResourceType.Food, 1, out _lastDest);
                Debug.Log(_hasLastDest
                    ? $"[FlowDebug] PickDest Food -> id={_lastDest.Building.Value} dist={_lastDest.Distance}"
                    : "[FlowDebug] PickDest Food -> NONE");
            }

            if (GUILayout.Button("Pick Dest (Ammo)"))
            {
                _hasLastDest = _gs.ResourceFlowService.TryPickDest(from, ResourceType.Ammo, 1, out _lastDest);
                Debug.Log(_hasLastDest
                    ? $"[FlowDebug] PickDest Ammo -> id={_lastDest.Building.Value} dist={_lastDest.Distance}"
                    : "[FlowDebug] PickDest Ammo -> NONE");
            }
            GUILayout.EndHorizontal();

            // ---- Pick Source (Producers / Warehouses / Forge) ----
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pick Src (Wood>=1)"))
            {
                _hasLastSource = _gs.ResourceFlowService.TryPickSource(from, ResourceType.Wood, 1, out _lastSource);
                Debug.Log(_hasLastSource
                    ? $"[FlowDebug] PickSource Wood -> id={_lastSource.Building.Value} dist={_lastSource.Distance}"
                    : "[FlowDebug] PickSource Wood -> NONE");
            }

            if (GUILayout.Button("Pick Src (Food>=1)"))
            {
                _hasLastSource = _gs.ResourceFlowService.TryPickSource(from, ResourceType.Food, 1, out _lastSource);
                Debug.Log(_hasLastSource
                    ? $"[FlowDebug] PickSource Food -> id={_lastSource.Building.Value} dist={_lastSource.Distance}"
                    : "[FlowDebug] PickSource Food -> NONE");
            }

            if (GUILayout.Button("Pick Src (Ammo>=1)"))
            {
                _hasLastSource = _gs.ResourceFlowService.TryPickSource(from, ResourceType.Ammo, 1, out _lastSource);
                Debug.Log(_hasLastSource
                    ? $"[FlowDebug] PickSource Ammo -> id={_lastSource.Building.Value} dist={_lastSource.Distance}"
                    : "[FlowDebug] PickSource Ammo -> NONE");
            }
            GUILayout.EndHorizontal();

            // Show last picks
            if (_hasLastSource) GUILayout.Label($"LastSrc: id={_lastSource.Building.Value} dist={_lastSource.Distance}");
            else GUILayout.Label("LastSrc: (none)");

            if (_hasLastDest) GUILayout.Label($"LastDest: id={_lastDest.Building.Value} dist={_lastDest.Distance}");
            else GUILayout.Label("LastDest: (none)");

            // ---- Transfer tests ----
            int amt = _addAmountDefault;
            if (!int.TryParse(_addAmountStr, out amt) || amt <= 0)
                amt = _addAmountDefault;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Transfer Wood (Target->LastDest)"))
            {
                if (!_hasLastDest)
                {
                    Debug.LogWarning("[FlowDebug] Transfer skipped: no LastDest");
                }
                else
                {
                    int moved = _gs.ResourceFlowService.Transfer(target, _lastDest.Building, ResourceType.Wood, amt);
                    Debug.Log($"[FlowDebug] Transfer Wood {amt} target={target.Value} -> dest={_lastDest.Building.Value} moved={moved}");
                }
            }

            if (GUILayout.Button("Transfer Wood (LastSrc->Target)"))
            {
                if (!_hasLastSource)
                {
                    Debug.LogWarning("[FlowDebug] Transfer skipped: no LastSrc");
                }
                else
                {
                    int moved = _gs.ResourceFlowService.Transfer(_lastSource.Building, target, ResourceType.Wood, amt);
                    Debug.Log($"[FlowDebug] Transfer Wood {amt} src={_lastSource.Building.Value} -> target={target.Value} moved={moved}");
                }
            }
            GUILayout.EndHorizontal();
        }

        public void DrawHubGUI()
        {
            TryResolveServices();
            if (!_enabled) { GUILayout.Label("Storage HUD: OFF (enable via Hub mode F5)"); return; }

            DrawContent();
        }

        private void DrawContent()
        {
            // Header + status 
            GUILayout.Label($"[Storage HUD] enabled={_enabled} toggleKey={_toggleKey}");

            var kb = Keyboard.current;
            GUILayout.Label($"Keyboard.current = {(kb == null ? "null" : "OK")}");

            if (_gs == null)
            {
                GUILayout.Space(6);
                GUILayout.Label("GameServices = null (GameBootstrap not found?)");
                return;
            }

            if (_gs.GridMap == null)
            {
                GUILayout.Space(6);
                GUILayout.Label("GridMap = null");
                return;
            }

            BuildingId target = default;
            bool hasTarget = false;

            if (_hasLocked)
            {
                target = _lockedBuilding;
                hasTarget = (_gs.WorldState != null && _gs.WorldState.Buildings.Exists(target));
                if (!hasTarget) _hasLocked = false; // auto unlock nếu building bị mất
            }

            if (!_hasLocked)
            {
                hasTarget = TryGetHoverBuilding(out target);
            }

            GUILayout.Space(6);
            GUILayout.Label(_hasLocked ? $"Target: LOCKED {target.Value}" : $"Target: HOVER {target.Value}");

            if (!hasTarget)
            {
                GUILayout.Label("No building target.");
                return;
            }

            // --- Hover info (phụ trợ) ---
            if (MouseCellSharedState.HasValue)
            {
                var cell = MouseCellSharedState.Cell;
                GUILayout.Space(6);
                GUILayout.Label($"Hover Cell: {cell.X},{cell.Y}");

                var occHover = _gs.GridMap.Get(cell);
                GUILayout.Label($"Hover Occ: {occHover.Kind}");
            }
            else
            {
                GUILayout.Space(6);
                GUILayout.Label("Hover Cell: (none)");
            }

            // --- Render theo TARGET (LOCKED hoặc HOVER building) ---
            var bid = target;

            if (_gs.WorldState != null && _gs.WorldState.Buildings.Exists(bid))
            {
                var bst = _gs.WorldState.Buildings.Get(bid);
                GUILayout.Space(6);
                GUILayout.Label($"BuildingId: {bid.Value}");
                GUILayout.Label($"DefId: {bst.DefId}  Level: {bst.Level}  Constructed: {bst.IsConstructed}");
            }
            else
            {
                GUILayout.Space(6);
                GUILayout.Label($"BuildingId: {bid.Value} (not found in WorldState)");
            }

            if (_gs.StorageService == null)
            {
                GUILayout.Space(6);
                GUILayout.Label("StorageService = null");
                return;
            }

            // ===== Add Resource Panel =====
            GUILayout.Space(8);
            GUILayout.Label("Add Resource:");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Amount:", GUILayout.Width(60));
            _addAmountStr = GUILayout.TextField(_addAmountStr, GUILayout.Width(80));
            if (GUILayout.Button("+10", GUILayout.Width(50))) _addAmountStr = "10";
            if (GUILayout.Button("+50", GUILayout.Width(50))) _addAmountStr = "50";
            if (GUILayout.Button("+100", GUILayout.Width(60))) _addAmountStr = "100";
            GUILayout.EndHorizontal();

            int addAmount = _addAmountDefault;
            if (!int.TryParse(_addAmountStr, out addAmount) || addAmount <= 0)
                addAmount = _addAmountDefault;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wood")) AddRes(bid, ResourceType.Wood, addAmount);
            if (GUILayout.Button("Food")) AddRes(bid, ResourceType.Food, addAmount);
            if (GUILayout.Button("Stone")) AddRes(bid, ResourceType.Stone, addAmount);
            if (GUILayout.Button("Iron")) AddRes(bid, ResourceType.Iron, addAmount);
            if (GUILayout.Button("Ammo")) AddRes(bid, ResourceType.Ammo, addAmount);
            GUILayout.EndHorizontal();
            // ===== end panel =====

            var snap = _gs.StorageService.GetStorage(bid);

            GUILayout.Space(8);
            GUILayout.Label($"Wood:  {snap.Wood} / {snap.CapWood}");
            GUILayout.Label($"Food:  {snap.Food} / {snap.CapFood}");
            GUILayout.Label($"Stone: {snap.Stone} / {snap.CapStone}");
            GUILayout.Label($"Iron:  {snap.Iron} / {snap.CapIron}");
            GUILayout.Label($"Ammo:  {snap.Ammo} / {snap.CapAmmo}");

            DrawFlowDebugUI(bid);
        }

        private void OnGUI()
        {
            if (DebugHubState.Enabled || _hubControlled) return;

            TryResolveServices();

            const float x = 10f;
            const float y = 540f;
            const float w = 520f;
            const float h = 520f;

            GUILayout.BeginArea(new Rect(x, y, w, h), GUI.skin.box);

            DrawContent();

            GUILayout.EndArea();
        }
    }
}
