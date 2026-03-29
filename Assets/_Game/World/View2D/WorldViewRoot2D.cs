using System;
using System.Collections.Generic;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SeasonalBastion.View2D
{
    /// <summary>
    /// View2D root (Option A):
    /// - Road: Tilemap
    /// - Building/NPC/Enemy: SpriteRenderer
    ///
    /// IMPORTANT:
    /// - No compile-time dependency to GameBootstrap/GameServices (avoid asmdef cycles).
    /// - Services resolved via reflection from a MonoBehaviour that exposes `Services` (GameBootstrap) or `GetServices()`.
    /// </summary>
    public sealed class WorldViewRoot2D : MonoBehaviour
    {
        [Header("Services Source (drag GameBootstrap or any component that has Services/GetServices)")]
        [SerializeField] private MonoBehaviour _servicesSource;
        [SerializeField] private bool _autoFindIfNull = true;

        [Header("Grid / Tilemap (Road)")]
        [SerializeField] private Grid _grid;
        [SerializeField] private Tilemap _roadTilemap;
        [SerializeField] private TileBase _roadTile;

        [Header("Grid / Tilemap (Resource Overlay)")]
        [SerializeField] private Tilemap _resourceZoneTilemap;
        [SerializeField] private TileBase _resourceZoneTile;
        [SerializeField] private bool _showResourceZones = true;
        [SerializeField] private Color _foodZoneColor = new(0.80f, 0.92f, 0.38f, 0.55f);
        [SerializeField] private Color _woodZoneColor = new(0.22f, 0.72f, 0.30f, 0.50f);
        [SerializeField] private Color _stoneZoneColor = new(0.65f, 0.65f, 0.67f, 0.45f);
        [SerializeField] private Color _ironZoneColor = new(0.45f, 0.52f, 0.78f, 0.52f);

        [Header("Fallback Sprites (optional but recommended)")]
        [SerializeField] private Sprite _fallbackBuilding;
        [SerializeField] private Sprite _fallbackNpc;
        [SerializeField] private Sprite _fallbackEnemy;

        [Header("Sync")]
        [SerializeField, Range(0.05f, 2f)] private float _pollInterval = 0.25f;
        [SerializeField] private bool _rebuildRoadOnStart = true;
        [SerializeField] private bool _rebuildEntitiesOnStart = true;

        [SerializeField, Range(0.5f, 1f)] private float _buildingFill = 0.92f; // 0.9–0.95 nhìn đẹp
        [SerializeField, Range(0.3f, 1f)] private float _agentFill = 0.70f;    // NPC/enemy nhỏ hơn 1 ô

        // Resolved contracts services
        private IEventBus _bus;
        private IGridMap _gridMap;
        private IWorldState _world;
        private IDataRegistry _data;

        private bool _warnedMissing;

        // View roots
        private Transform _buildingsRoot;
        private Transform _npcsRoot;
        private Transform _enemiesRoot;

        private readonly Dictionary<int, SpriteRenderer> _buildingViews = new();
        private readonly Dictionary<int, SpriteRenderer> _npcViews = new();
        private readonly Dictionary<int, SpriteRenderer> _enemyViews = new();

        private float _pollAcc;
        private bool _bound;

        private void Awake()
        {
            EnsureRoots();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!_bound)
            {
                // bootstrap order safe: keep trying
                TryBind();
                return;
            }

            _pollAcc += Time.unscaledDeltaTime;
            if (_pollAcc >= _pollInterval)
            {
                _pollAcc = 0f;
                SyncEntities();
            }
        }

        // ---------------- Bind / Resolve ----------------

        private void TryBind()
        {
            if (_bound) return;

            object services = ResolveServicesObject();
            if (services == null) return;

            _bus = ReadMember<IEventBus>(services, "EventBus", "_eventBus", "Bus");
            _gridMap = ReadMember<IGridMap>(services, "GridMap", "_gridMap", "Grid");
            _world = ReadMember<IWorldState>(services, "WorldState", "_world", "World");
            _data = ReadMember<IDataRegistry>(services, "DataRegistry", "_dataRegistry", "Registry", "Data");

            if (_gridMap == null || _world == null)
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning($"[View2D] Services resolved but missing GridMap/WorldState. " +
                                     $"servicesType={services.GetType().FullName} " +
                                     $"GridMapNull={_gridMap == null} WorldStateNull={_world == null}");
                }
                return;
            }

            Subscribe();

            _bound = true;

            if (_rebuildRoadOnStart) RebuildRoad();
            RebuildResourceZones();
            if (_rebuildEntitiesOnStart) SyncEntities();
        }

        private object ResolveServicesObject()
        {
            // 1) try assigned
            if (_servicesSource != null)
            {
                var s = TryExtractServicesFromMono(_servicesSource);
                if (s != null) return s;
            }

            if (!_autoFindIfNull) return null;

            // 2) auto-find any MonoBehaviour that has Services/GetServices
            var all = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == null) continue;

                var s = TryExtractServicesFromMono(mb);
                if (s != null)
                {
                    _servicesSource = mb;
                    return s;
                }
            }

            return null;
        }

        private static object TryExtractServicesFromMono(MonoBehaviour mb)
        {
            if (mb == null) return null;

            var t = mb.GetType();

            // (A) public property Services
            var prop = t.GetProperty("Services", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                try
                {
                    var v = prop.GetValue(mb);
                    if (v != null) return v;
                }
                catch { /* ignore */ }
            }

            // (B) method GetServices()
            var m = t.GetMethod("GetServices", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null && m.GetParameters().Length == 0)
            {
                try
                {
                    var v = m.Invoke(mb, null);
                    if (v != null) return v;
                }
                catch { /* ignore */ }
            }

            return null;
        }

        private static T ReadMember<T>(object obj, params string[] names) where T : class
        {
            if (obj == null || names == null || names.Length == 0) return null;

            var t = obj.GetType();

            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                if (string.IsNullOrEmpty(n)) continue;

                // 1) Property
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    try
                    {
                        var v = p.GetValue(obj) as T;
                        if (v != null) return v;
                    }
                    catch { }
                }

                // 2) Field
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    try
                    {
                        var v = f.GetValue(obj) as T;
                        if (v != null) return v;
                    }
                    catch { }
                }

                // 3) Method (optional): GetGridMap(), GetWorldState()...
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null && m.GetParameters().Length == 0)
                {
                    try
                    {
                        var v = m.Invoke(obj, null) as T;
                        if (v != null) return v;
                    }
                    catch { }
                }
            }

            return null;
        }

        // ---------------- Events ----------------

        private void Subscribe()
        {
            if (_bus == null) return;

            _bus.Subscribe<RoadPlacedEvent>(OnRoadPlaced);
            _bus.Subscribe<RoadsDirtyEvent>(OnRoadsDirty);

            _bus.Subscribe<BuildingPlacedEvent>(OnBuildingsChanged);
            _bus.Subscribe<BuildingUpgradedEvent>(OnBuildingsChanged);
        }

        private void Unsubscribe()
        {
            if (_bus == null) return;

            _bus.Unsubscribe<RoadPlacedEvent>(OnRoadPlaced);
            _bus.Unsubscribe<RoadsDirtyEvent>(OnRoadsDirty);

            _bus.Unsubscribe<BuildingPlacedEvent>(OnBuildingsChanged);
            _bus.Unsubscribe<BuildingUpgradedEvent>(OnBuildingsChanged);
        }

        private void OnRoadPlaced(RoadPlacedEvent ev)
        {
            if (_roadTilemap == null || _roadTile == null || _gridMap == null) return;

            var c = ev.Cell;
            var v = new Vector3Int(c.X, c.Y, 0);
            _roadTilemap.SetTile(v, _gridMap.IsRoad(c) ? _roadTile : null);
        }

        private void OnRoadsDirty(RoadsDirtyEvent _)
        {
            // throttle-safe: rebuild all
            RebuildRoad();
        }

        private void OnBuildingsChanged(BuildingPlacedEvent _)
        {
            SyncBuildings();
        }

        private void OnBuildingsChanged(BuildingUpgradedEvent _)
        {
            SyncBuildings();
        }

        // ---------------- Road ----------------

        private void RebuildRoad()
        {
            if (_roadTilemap == null || _roadTile == null || _gridMap == null) return;

            _roadTilemap.ClearAllTiles();

            int w = _gridMap.Width;
            int h = _gridMap.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new CellPos(x, y);
                    if (_gridMap.IsRoad(c))
                        _roadTilemap.SetTile(new Vector3Int(x, y, 0), _roadTile);
                }
            }
        }

        private void RebuildResourceZones()
        {
            if (_resourceZoneTilemap == null)
                return;

            _resourceZoneTilemap.ClearAllTiles();
            _resourceZoneTilemap.color = Color.white;

            if (!_showResourceZones || _resourceZoneTile == null || _world?.Zones?.Zones == null)
                return;

            var zones = _world.Zones.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null || z.Cells == null)
                    continue;

                Color tint = GetZoneColor(z.Resource);
                for (int c = 0; c < z.Cells.Count; c++)
                {
                    var cell = z.Cells[c];
                    var v = new Vector3Int(cell.X, cell.Y, 0);
                    _resourceZoneTilemap.SetTile(v, _resourceZoneTile);
                    _resourceZoneTilemap.SetColor(v, tint);
                }
            }
        }

        private Color GetZoneColor(ResourceType rt)
        {
            return rt switch
            {
                ResourceType.Food => _foodZoneColor,
                ResourceType.Wood => _woodZoneColor,
                ResourceType.Stone => _stoneZoneColor,
                ResourceType.Iron => _ironZoneColor,
                _ => new Color(1f, 1f, 1f, 0.35f)
            };
        }

        // ---------------- Entities ----------------

        private void SyncEntities()
        {
            SyncBuildings();
            SyncNpcs();
            SyncEnemies();
        }

        private void SyncBuildings()
        {
            if (_world?.Buildings == null) return;

            // alive set
            var alive = new HashSet<int>();

            foreach (var id in _world.Buildings.Ids)
            {
                int key = id.Value;
                alive.Add(key);

                var bs = _world.Buildings.Get(id);

                if (!_buildingViews.TryGetValue(key, out var sr) || sr == null)
                {
                    sr = CreateSpriteRenderer(_buildingsRoot, $"B_{key}", _fallbackBuilding);
                    sr.sortingLayerName = "Entities";
                    _buildingViews[key] = sr;
                }

                // position: center of footprint if data exists
                Vector3 pos = GetBuildingWorldPos(bs);
                sr.transform.position = pos;
                sr.sortingOrder = ComputeOrder(pos);

                BuildingDef def = null;
                if (_data != null)
                    _data.TryGetBuilding(bs.DefId, out def);

                // Scale theo footprint
                if (def != null)
                    ApplyScaleToFootprint(sr, def.SizeX, def.SizeY, _buildingFill);
                else
                    ApplyScaleToFootprint(sr, 1, 1, _buildingFill);
            }

            // remove stale
            RemoveStale(_buildingViews, alive);
        }

        private void SyncNpcs()
        {
            if (_world?.Npcs == null) return;

            var alive = new HashSet<int>();

            foreach (var id in _world.Npcs.Ids)
            {
                int key = id.Value;
                alive.Add(key);

                var ns = _world.Npcs.Get(id);

                if (!_npcViews.TryGetValue(key, out var sr) || sr == null)
                {
                    sr = CreateSpriteRenderer(_npcsRoot, $"N_{key}", _fallbackNpc);
                    sr.sortingLayerName = "Entities";
                    _npcViews[key] = sr;
                    ApplyScaleToFootprint(sr, 1, 1, _agentFill);
                }

                Vector3 pos = CellToWorld(ns.Cell);
                sr.transform.position = pos;
                sr.sortingOrder = ComputeOrder(pos);
            }

            RemoveStale(_npcViews, alive);
        }

        private void SyncEnemies()
        {
            if (_world?.Enemies == null) return;

            var alive = new HashSet<int>();

            foreach (var id in _world.Enemies.Ids)
            {
                int key = id.Value;
                alive.Add(key);

                var es = _world.Enemies.Get(id);

                if (!_enemyViews.TryGetValue(key, out var sr) || sr == null)
                {
                    sr = CreateSpriteRenderer(_enemiesRoot, $"E_{key}", _fallbackEnemy);
                    sr.sortingLayerName = "Entities";
                    _enemyViews[key] = sr;
                    ApplyScaleToFootprint(sr, 1, 1, _agentFill);
                }

                Vector3 pos = CellToWorld(es.Cell);
                sr.transform.position = pos;
                sr.sortingOrder = ComputeOrder(pos);
            }

            RemoveStale(_enemyViews, alive);
        }

        private Vector3 GetBuildingWorldPos(BuildingState bs)
        {
            // fallback: anchor cell center
            Vector3 basePos = CellToWorld(bs.Anchor);

            // if DataRegistry exists, try compute center of footprint
            BuildingDef def = null;
            if (_data != null)
                _data.TryGetBuilding(bs.DefId, out def);
            if (def == null) return basePos;

            // center offset in world units
            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;
            float ox = (def.SizeX * 0.5f - 0.5f) * cellSize.x;
            float oy = (def.SizeY * 0.5f - 0.5f) * cellSize.y;

            return basePos + new Vector3(ox, oy, 0f);
        }

        private Vector3 CellToWorld(CellPos c)
        {
            var v = new Vector3Int(c.X, c.Y, 0);

            if (_grid != null)
                return _grid.GetCellCenterWorld(v);

            // fallback grid mapping
            return new Vector3(c.X + 0.5f, c.Y + 0.5f, 0f);
        }

        private static int ComputeOrder(Vector3 worldPos)
        {
            // higher Y => render behind (order smaller)
            return -Mathf.RoundToInt(worldPos.y * 100f);
        }

        private static SpriteRenderer CreateSpriteRenderer(Transform parent, string name, Sprite fallbackSprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fallbackSprite;
            sr.color = sr.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            sr.sortingLayerName = "Entities";
            sr.sortingOrder = 0;

            return sr;
        }

        private static void RemoveStale(Dictionary<int, SpriteRenderer> map, HashSet<int> alive)
        {
            if (map.Count == 0) return;

            // collect stale
            var stale = ListPool<int>.Get();
            foreach (var kv in map)
            {
                if (!alive.Contains(kv.Key))
                    stale.Add(kv.Key);
            }

            for (int i = 0; i < stale.Count; i++)
            {
                int key = stale[i];
                if (map.TryGetValue(key, out var sr) && sr != null)
                    Destroy(sr.gameObject);
                map.Remove(key);
            }

            ListPool<int>.Release(stale);
        }

        private void EnsureRoots()
        {
            _buildingsRoot = GetOrCreateChild("BuildingsRoot");
            _npcsRoot = GetOrCreateChild("NpcsRoot");
            _enemiesRoot = GetOrCreateChild("EnemiesRoot");
        }

        private Transform GetOrCreateChild(string name)
        {
            var t = transform.Find(name);
            if (t != null) return t;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void ApplyScaleToFootprint(SpriteRenderer sr, int sizeX, int sizeY, float fill)
        {
            if (sr == null || sr.sprite == null) return;

            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;

            float targetW = Mathf.Max(0.01f, sizeX * cellSize.x * fill);
            float targetH = Mathf.Max(0.01f, sizeY * cellSize.y * fill);

            Vector3 native = sr.sprite.bounds.size; // world units (phụ thuộc PPU)
            float nativeW = Mathf.Max(0.0001f, native.x);
            float nativeH = Mathf.Max(0.0001f, native.y);

            sr.transform.localScale = new Vector3(targetW / nativeW, targetH / nativeH, 1f);
        }
    }
}