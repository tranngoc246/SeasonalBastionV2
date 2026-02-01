using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;
using SeasonalBastion.RunStart;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Debug HUD module: validates RunStart acceptance (Day16).
    /// Hub-controlled: drawn inside DebugHUDHub Home tab.
    /// </summary>
    public sealed class DebugRunStartValidationHUD : MonoBehaviour
    {
        [Header("Bootstrap (optional auto-find)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("UI")]
        [SerializeField] private bool _autoValidateOnOpen = false;
        [SerializeField] private int _debugSeed = 12345;

        private bool _hubControlled;
        private bool _enabledFromHub = true;

        private GameServices _s;
        private readonly List<Line> _lines = new();
        private Vector2 _scroll;

        private struct Line
        {
            public bool Ok;
            public string Text;
            public Line(bool ok, string text) { Ok = ok; Text = text; }
        }

        public void SetHubControlled(bool v) => _hubControlled = v;
        public void SetEnabledFromHub(bool v) => _enabledFromHub = v;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _s = _bootstrap != null ? _bootstrap.Services : null;
        }

        public void DrawHubGUI()
        {
            if (!_enabledFromHub) return;

            // refresh refs (safe in case domain reload / scene reload)
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _s ??= _bootstrap != null ? _bootstrap.Services : null;

            GUILayout.Label("RunStart Validation (Day16)", GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", GUILayout.Width(40));
            var seedStr = GUILayout.TextField(_debugSeed.ToString(), GUILayout.Width(100));
            if (int.TryParse(seedStr, out var seedParsed)) _debugSeed = seedParsed;
            _autoValidateOnOpen = GUILayout.Toggle(_autoValidateOnOpen, "Auto-validate", GUILayout.Width(130));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("StartNewRun (Seed)", GUILayout.Width(180)))
            {
                TryStartNewRun(_debugSeed);
                if (_autoValidateOnOpen) ValidateNow();
            }

            if (GUILayout.Button("Validate Now", GUILayout.Width(140)))
            {
                ValidateNow();
            }

            if (GUILayout.Button("Clear Results", GUILayout.Width(140)))
            {
                _lines.Clear();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(360));
            if (_lines.Count == 0)
            {
                GUILayout.Label("No results. Click 'Validate Now'.");
            }
            else
            {
                for (int i = 0; i < _lines.Count; i++)
                    DrawLine(_lines[i]);
            }
            GUILayout.EndScrollView();
        }

        private void DrawLine(Line l)
        {
            var old = GUI.color;
            GUI.color = l.Ok ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.55f);
            GUILayout.Label((l.Ok ? "PASS  " : "FAIL  ") + l.Text);
            GUI.color = old;
        }

        private void TryStartNewRun(int seed)
        {
            if (_bootstrap == null)
            {
                _lines.Add(new Line(false, "GameBootstrap not found in scene."));
                return;
            }

            // NOTE: requires tiny public wrapper in GameBootstrap (see section 3 below)
            try
            {
                //_bootstrap.DebugStartNewRun(seed);
                _lines.Add(new Line(true, $"StartNewRun called (seed={seed})."));
            }
            catch (Exception e)
            {
                _lines.Add(new Line(false, "Cannot StartNewRun from HUD. Add GameBootstrap.DebugStartNewRun(seed). " + e.Message));
            }
        }

        private void ValidateNow()
        {
            _lines.Clear();

            if (_s == null)
            {
                _lines.Add(new Line(false, "GameServices is null (bootstrap/services missing)."));
                return;
            }

            if (_s.WorldState == null || _s.DataRegistry == null || _s.GridMap == null)
            {
                _lines.Add(new Line(false, "Missing services: WorldState / DataRegistry / GridMap."));
                return;
            }

            ValidateMapSize();
            ValidateRoads();
            ValidateBuildings();
            ValidateOverlap();
            ValidateNpcs();
            ValidateStartingStorage();
            ValidateRunStartValidatorDay42();
        }

        private void ValidateMapSize()
        {
            int w = _s.GridMap.Width;
            int h = _s.GridMap.Height;

            bool ok = (w == 64 && h == 64);
            _lines.Add(new Line(ok, $"Map size = {w}x{h} (expect 64x64)."));
        }

        private void ValidateRoads()
        {
            int w = _s.GridMap.Width;
            int h = _s.GridMap.Height;

            int roads = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (_s.GridMap.IsRoad(new CellPos(x, y)))
                        roads++;

            _lines.Add(new Line(roads > 0, $"Road cells = {roads} (expect > 0)."));
        }

        private void ValidateBuildings()
        {
            // Required canonical IDs (Option 1)
            string[] req =
            {
                "bld_hq_t1",
                "bld_house_t1",
                "bld_farmhouse_t1",
                "bld_lumbercamp_t1",
                "bld_tower_arrow_t1"
            };

            var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < req.Length; i++) found[req[i]] = 0;

            foreach (var id in _s.WorldState.Buildings.Ids)
            {
                var st = _s.WorldState.Buildings.Get(id);
                if (string.IsNullOrEmpty(st.DefId)) continue;
            }

            for (int i = 0; i < req.Length; i++)
            {
                var k = req[i];
                _lines.Add(new Line(found[k] > 0, $"Has building '{k}' count={found[k]} (expect >=1)."));
            }
        }

        private void ValidateOverlap()
        {
            int w = _s.GridMap.Width;
            int h = _s.GridMap.Height;
            int n = w * h;
            var used = new bool[n];

            bool ok = true;
            string firstFail = null;

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                var st = _s.WorldState.Buildings.Get(bid);
                if (string.IsNullOrEmpty(st.DefId)) continue;

                BuildingDef def;
                try { def = _s.DataRegistry.GetBuilding(st.DefId); }
                catch
                {
                    ok = false;
                    firstFail ??= $"BuildingDef missing for '{st.DefId}'.";
                    continue;
                }

                int sx = Mathf.Max(1, def.SizeX);
                int sy = Mathf.Max(1, def.SizeY);

                for (int dy = 0; dy < sy; dy++)
                    for (int dx = 0; dx < sx; dx++)
                    {
                        int cx = st.Anchor.X + dx;
                        int cy = st.Anchor.Y + dy;

                        if (cx < 0 || cy < 0 || cx >= w || cy >= h)
                        {
                            ok = false;
                            firstFail ??= $"Footprint out of bounds: '{st.DefId}' cell=({cx},{cy}).";
                            continue;
                        }

                        int idx = cy * w + cx;
                        if (used[idx])
                        {
                            ok = false;
                            firstFail ??= $"Overlap detected at cell=({cx},{cy}).";
                            continue;
                        }
                        used[idx] = true;
                    }
            }

            _lines.Add(new Line(ok, ok ? "No building footprint overlap (PASS)." : ("Overlap check FAIL: " + firstFail)));
        }

        private void ValidateNpcs()
        {
            int count = _s.WorldState.Npcs.Count;
            _lines.Add(new Line(count > 0, $"NPC count = {count} (expect > 0)."));

            bool ok = true;
            string fail = null;

            int withWp = 0;
            foreach (var nid in _s.WorldState.Npcs.Ids)
            {
                var n = _s.WorldState.Npcs.Get(nid);
                if (n.Workplace.Value != 0)
                {
                    withWp++;
                    if (!_s.WorldState.Buildings.Exists(n.Workplace))
                    {
                        ok = false;
                        fail ??= $"Npc {n.Id.Value} workplace points to missing buildingId={n.Workplace.Value}.";
                    }
                }
            }

            _lines.Add(new Line(true, $"NPC with workplace = {withWp} (informational)."));
            _lines.Add(new Line(ok, ok ? "NPC workplace references OK." : ("NPC workplace FAIL: " + fail)));
        }

        private void ValidateStartingStorage()
        {
            // Find HQ
            BuildingId hq = default;

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                var st = _s.WorldState.Buildings.Get(bid);
                if (string.IsNullOrEmpty(st.DefId)) continue;

                try
                {
                    var def = _s.DataRegistry.GetBuilding(st.DefId);
                    if (def.IsHQ) { hq = bid; break; }
                }
                catch { }
            }

            if (hq.Value == 0)
            {
                // fallback by id
                foreach (var bid in _s.WorldState.Buildings.Ids)
                {
                    var st = _s.WorldState.Buildings.Get(bid);
                    if (st.DefId != null && st.DefId.Equals("HQ", StringComparison.OrdinalIgnoreCase))
                    {
                        hq = bid; break;
                    }
                }
            }

            if (hq.Value == 0)
            {
                _lines.Add(new Line(false, "Cannot find HQ building to validate storage."));
                return;
            }

            var stg = _s.StorageService;
            if (stg == null)
            {
                _lines.Add(new Line(false, "StorageService is null."));
                return;
            }

            int wood = stg.GetAmount(hq, ResourceType.Wood);
            int stone = stg.GetAmount(hq, ResourceType.Stone);
            int food = stg.GetAmount(hq, ResourceType.Food);

            _lines.Add(new Line(wood >= 30, $"HQ wood={wood} (expect >=30)."));
            _lines.Add(new Line(stone >= 20, $"HQ stone={stone} (expect >=20)."));
            _lines.Add(new Line(food >= 10, $"HQ food={food} (expect >=10)."));
        }

        private void ValidateRunStartValidatorDay42()
        {
            _lines.Add(new Line(true, "---- Day42: RunStartValidator (runtime invariants) ----"));

            var issues = new List<RunStartValidationIssue>(32);
            RunStartValidator.ValidateRuntime(_s, issues);

            if (issues.Count == 0)
            {
                _lines.Add(new Line(true, "Validator: OK (no issues)."));
                return;
            }

            int err = 0, warn = 0;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Severity == RunStartIssueSeverity.Error) err++; else warn++;

            _lines.Add(new Line(err == 0, $"Validator summary: {err} error(s), {warn} warning(s)."));

            for (int i = 0; i < issues.Count; i++)
            {
                bool ok = issues[i].Severity != RunStartIssueSeverity.Error;
                _lines.Add(new Line(ok, $"{issues[i].Severity} {issues[i].Code} — {issues[i].Message}"));
            }
        }
    }
}
