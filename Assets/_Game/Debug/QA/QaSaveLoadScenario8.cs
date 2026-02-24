using System;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// One-click Save/Load matrix (8 checkpoints) to catch softlocks and save/apply regressions.
    /// Runs synchronously (safe to call from Debug GUI button).
    /// </summary>
    public static class QaSaveLoadScenario8
    {
        private struct WorldCounts
        {
            public int Buildings, Sites, Npcs, Towers, Enemies, Roads;
        }

        public static bool Run(GameServices s, out string summary)
        {
            summary = "";
            if (s == null) { summary = "FAIL: GameServices null"; return false; }
            if (s.SaveService == null) { summary = "FAIL: SaveService null"; return false; }
            if (s.WorldState == null) { summary = "FAIL: WorldState null"; return false; }
            if (s.GridMap == null) { summary = "FAIL: GridMap null"; return false; }
            if (s.RunClock == null) { summary = "FAIL: RunClock null"; return false; }

            int pass = 0, fail = 0;

            // 0) Force a clean run start (deterministic)
            if (!TryStartFreshRun(out var bootErr))
            {
                summary = "FAIL: StartFreshRun: " + bootErr;
                return false;
            }

            // Re-acquire services after starting new run (Bootstrap keeps same Services instance, but state resets)
            // NOTE: We still use 's' passed in, because Debug HUD holds the same reference.

            // Step runner
            if (RunCheckpoint(s, "C1: Fresh run baseline",
                prepare: () => { /* no-op */ },
                validateBeforeSave: () => EnsureHQExists(s, out _),
                extraValidateAfterLoad: () => EnsureHQExists(s, out _),
                out var msg1))
                pass++;
            else { fail++; Debug.LogError(msg1); }

            if (RunCheckpoint(s, "C2: Roads placed",
                prepare: () => PlaceRoadChain(s, 3),
                validateBeforeSave: () => true,
                extraValidateAfterLoad: () => true,
                out var msg2))
                pass++;
            else { fail++; Debug.LogError(msg2); }

            // C3..C6 need a build site. Create once, then mutate site state for each checkpoint.
            var buildCtx = new BuildCtx();
            if (!EnsureOneBuildSiteCreated(s, ref buildCtx, out var createErr))
            {
                // Don't stop silently — report and continue to C7/C8
                Debug.LogError("[QA] FAIL: cannot create build site for C3..C6: " + createErr);
                fail += 4; // mark C3..C6 as failed
            }
            else
            {
                if (RunCheckpoint(s, "C3: Site created (no delivery)",
                    prepare: () => { /* site already created */ },
                    validateBeforeSave: () => ValidateSiteState(s, buildCtx, expReadyToWork: false, expWorkDoneMin: 0f, expWorkDoneMax: 0.001f, out _),
                    extraValidateAfterLoad: () => ValidateSiteState(s, buildCtx, expReadyToWork: false, expWorkDoneMin: 0f, expWorkDoneMax: 0.001f, out _),
                    out var msg3))
                    pass++;
                else { fail++; Debug.LogError(msg3); }

                if (RunCheckpoint(s, "C4: Mid-delivery (DeliveredSoFar > 0, RemainingCosts > 0)",
                    prepare: () => MutateSite_MidDelivery(s, buildCtx),
                    validateBeforeSave: () => ValidateMidDelivery(s, buildCtx, out _),
                    extraValidateAfterLoad: () => ValidateMidDelivery(s, buildCtx, out _),
                    out var msg4))
                    pass++;
                else { fail++; Debug.LogError(msg4); }

                if (RunCheckpoint(s, "C5: Mid-work (ReadyToWork=true, WorkSecondsDone in (0,total))",
                    prepare: () => MutateSite_MidWork(s, buildCtx),
                    validateBeforeSave: () => ValidateMidWork(s, buildCtx, out _),
                    extraValidateAfterLoad: () => ValidateMidWork(s, buildCtx, out _),
                    out var msg5))
                    pass++;
                else { fail++; Debug.LogError(msg5); }

                if (RunCheckpoint(s, "C6: Completed build (site destroyed, building constructed)",
                    prepare: () => MutateSite_CompleteAndFinalize(s, buildCtx),
                    validateBeforeSave: () => ValidateCompleted(s, buildCtx, out _),
                    extraValidateAfterLoad: () => ValidateCompleted(s, buildCtx, out _),
                    out var msg6))
                    pass++;
                else { fail++; Debug.LogError(msg6); }
            }

            // ---- C7
            if (RunCheckpoint(s, "C7: Defend start (Autumn Y1 D1)",
                prepare: () =>
                {
                    ForceClock(s, year: 1, season: Season.Autumn, day: 1, timer: 0f, scale: 1f);

                    if (s.RunClock is RunClockService rc)
                        rc.Tick(0.01f);
                },
                validateBeforeSave: () => ValidateClockPhase(s, Season.Autumn, Phase.Defend, out _),
                extraValidateAfterLoad: () => ValidateClockPhase(s, Season.Autumn, Phase.Defend, out _),
                out var msg7))
                pass++;
            else { fail++; Debug.LogError(msg7); }

            // ---- C8
            if (RunCheckpoint(s, "C8: Mid-wave (spawn enemies, persist on save/load)",
                prepare: () => PrepareMidWave(s),
                validateBeforeSave: () => ValidateHasEnemies(s, out _),
                extraValidateAfterLoad: () => ValidateHasEnemies(s, out _),
                out var msg8))
                pass++;
            else { fail++; Debug.LogError(msg8); }

            summary = $"QA Save/Load Matrix done. PASS={pass} FAIL={fail}. Check Console for details.";
            return fail == 0;
        }

        // -------------------------
        // Core: Save -> Load -> Apply + invariants
        // -------------------------

        private static bool RunCheckpoint(
            GameServices s,
            string name,
            Action prepare,
            Func<bool> validateBeforeSave,
            Func<bool> extraValidateAfterLoad,
            out string msg)
        {
            msg = $"[QA] {name} :: ";

            Debug.Log("[QA] START " + name);

            try
            {
                prepare?.Invoke();

                if (!validateBeforeSave())
                {
                    msg += "FAIL pre-save validation.";
                    return false;
                }

                var expClock = CaptureClock(s);
                var expCounts = CaptureCounts(s);

                // SAVE
                var sr = s.SaveService.SaveRun(s.WorldState, s.RunClock);
                if (sr.Code != SaveResultCode.Ok)
                {
                    msg += $"FAIL Save: {sr.Code} {sr.Message}";
                    return false;
                }

                // LOAD
                var lr = s.SaveService.LoadRun(out var dto);
                if (lr.Code != SaveResultCode.Ok || dto == null)
                {
                    msg += $"FAIL Load: {lr.Code} {lr.Message}";
                    return false;
                }

                // APPLY
                if (!SaveLoadApplier.TryApply(s, dto, out var err))
                {
                    msg += $"FAIL Apply: {err}";
                    return false;
                }

                // CLOCK CHECK
                if (!ClockEquals(expClock, CaptureClock(s), out var clockErr))
                {
                    msg += "FAIL Clock mismatch: " + clockErr;
                    return false;
                }

                // COUNTS CHECK
                var nowCounts = CaptureCounts(s);
                if (!CountsEquals(expCounts, nowCounts, out var countsErr))
                {
                    msg += "FAIL Counts mismatch: " + countsErr;
                    return false;
                }

                // EXTRA CHECK
                if (!extraValidateAfterLoad())
                {
                    msg += "FAIL post-load extra validation.";
                    return false;
                }

                msg += "PASS";
                Debug.Log(msg);
                return true;
            }
            catch (Exception e)
            {
                msg += "EXCEPTION: " + e.Message;
                return false;
            }
        }

        // -------------------------
        // Build context + mutations (C3..C6)
        // -------------------------

        private struct BuildCtx
        {
            public int OrderId;
            public BuildingId Building;
            public SiteId Site;
            public string DefId;
            public CellPos Anchor;
            public Dir4 Rotation;
        }

        private static bool EnsureOneBuildSiteCreated(GameServices s, ref BuildCtx ctx, out string err)
        {
            err = null;

            if (ctx.OrderId > 0 && ctx.Site.Value != 0)
                return true;

            // Prefer cheap + likely-unlocked first
            string[] candidates =
            {
        "bld_woodcutter_t1",
        "bld_farm_t1",
        "bld_stonecutter_t1",
        "bld_house_t1",
        "bld_ironhut_t1",
        "bld_warehouse_t1",
        "bld_forge_t1",
    };

            string lastReason = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                var defId = candidates[i];

                // must exist in data
                try { s.DataRegistry.GetBuilding(defId); }
                catch { lastReason = $"Def not found: {defId}"; continue; }

                // prefer unlocked, but if UnlockService is absent just proceed
                if (s.UnlockService != null && !s.UnlockService.IsUnlocked(defId))
                {
                    lastReason = $"Locked: {defId}";
                    continue;
                }

                if (!TryFindValidPlacement(s, defId, Dir4.N, out var anchor, out var suggestedRoad))
                {
                    lastReason = $"No valid placement for {defId}";
                    continue;
                }

                int orderId = s.BuildOrderService.CreatePlaceOrder(defId, anchor, Dir4.N);
                if (orderId <= 0)
                {
                    lastReason = $"CreatePlaceOrder failed for {defId} (locked or insufficient storage?)";
                    continue;
                }

                if (!s.BuildOrderService.TryGet(orderId, out var order) || order.TargetBuilding.Value == 0 || order.Site.Value == 0)
                {
                    lastReason = $"Order invalid after creation. orderId={orderId} def={defId}";
                    continue;
                }

                // Make sure driveway/entry road exists if suggestedRoad is not road
                if (s.PlacementService != null && s.GridMap != null)
                {
                    if (s.GridMap.IsInside(suggestedRoad) && !s.GridMap.IsRoad(suggestedRoad))
                        s.PlacementService.PlaceRoad(suggestedRoad);
                }

                ctx = new BuildCtx
                {
                    OrderId = orderId,
                    Building = order.TargetBuilding,
                    Site = order.Site,
                    DefId = defId,
                    Anchor = anchor,
                    Rotation = Dir4.N
                };

                Debug.Log($"[QA] Created build site using {defId} at {anchor.X},{anchor.Y}");
                return true;
            }

            err = "No QA build candidate succeeded. LastReason=" + lastReason;
            return false;
        }

        private static void MutateSite_MidDelivery(GameServices s, BuildCtx ctx)
        {
            var sites = s.WorldState.Sites;
            if (sites == null || !sites.Exists(ctx.Site)) return;

            var st = sites.Get(ctx.Site);
            if (st.RemainingCosts == null || st.RemainingCosts.Count == 0) return;
            if (st.DeliveredSoFar == null || st.DeliveredSoFar.Count == 0) return;

            // Deliver 1 unit of first remaining line
            var rem = st.RemainingCosts[0];
            if (rem == null || rem.Amount <= 0) return;

            int deliver = 1;
            if (deliver > rem.Amount) deliver = rem.Amount;
            rem.Amount -= deliver;

            if (rem.Amount <= 0)
                st.RemainingCosts.RemoveAt(0);
            else
                st.RemainingCosts[0] = rem;

            // Mirror into DeliveredSoFar (match by resource)
            for (int i = 0; i < st.DeliveredSoFar.Count; i++)
            {
                var d = st.DeliveredSoFar[i];
                if (d == null) continue;
                if (d.Resource != rem.Resource) continue;
                d.Amount += deliver;
                st.DeliveredSoFar[i] = d;
                break;
            }

            sites.Set(ctx.Site, st);
        }

        private static void MutateSite_MidWork(GameServices s, BuildCtx ctx)
        {
            var sites = s.WorldState.Sites;
            if (sites == null || !sites.Exists(ctx.Site)) return;

            var st = sites.Get(ctx.Site);

            // Mark ready-to-work
            st.RemainingCosts?.Clear();
            st.RemainingCosts = null;

            if (st.WorkSecondsTotal <= 0f) st.WorkSecondsTotal = 0.1f;

            // Mid work (not complete)
            float half = st.WorkSecondsTotal * 0.5f;
            if (half <= 0f) half = 0.05f;

            st.WorkSecondsDone = Mathf.Clamp(half, 0.01f, st.WorkSecondsTotal - 0.01f);

            sites.Set(ctx.Site, st);
        }

        private static void MutateSite_CompleteAndFinalize(GameServices s, BuildCtx ctx)
        {
            var sites = s.WorldState.Sites;
            if (sites == null || !sites.Exists(ctx.Site)) return;

            var st = sites.Get(ctx.Site);

            st.RemainingCosts?.Clear();
            st.RemainingCosts = null;

            if (st.WorkSecondsTotal <= 0f) st.WorkSecondsTotal = 0.1f;
            st.WorkSecondsDone = st.WorkSecondsTotal;

            sites.Set(ctx.Site, st);

            // Force finalize immediately (BuildOrderService.Tick ignores dt<=0)
            for (int i = 0; i < 3; i++)
                s.BuildOrderService?.Tick(0.05f);
        }

        // -------------------------
        // Mid-wave (C8)
        // -------------------------

        private static void PrepareMidWave(GameServices s)
        {
            // Ensure defend phase
            ForceClock(s, year: 1, season: Season.Autumn, day: 1, timer: 2f, scale: 1f);

            if (s.CombatService is CombatService cs)
            {
                cs.ResetAfterLoad(new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 });

                // Tick a bit to allow spawn
                for (int k = 0; k < 25; k++)
                    cs.Tick(0.2f);
            }
        }

        // -------------------------
        // Validations
        // -------------------------

        private static bool EnsureHQExists(GameServices s, out string err)
        {
            err = null;

            var buildings = s.WorldState?.Buildings;
            if (buildings == null)
            {
                err = "Buildings store null.";
                return false;
            }

            foreach (var bid in buildings.Ids)
            {
                if (!buildings.Exists(bid)) continue;

                var b = buildings.Get(bid);
                if (!b.IsConstructed) continue;

                if (string.Equals(b.DefId, "bld_hq_t1", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            err = "HQ not found (expected a constructed building with defId = bld_hq_t1).";
            return false;
        }

        private static bool ValidateSiteState(GameServices s, BuildCtx ctx, bool expReadyToWork, float expWorkDoneMin, float expWorkDoneMax, out string err)
        {
            err = null;
            if (s.WorldState.Sites == null) { err = "Sites store null"; return false; }
            if (!s.WorldState.Sites.Exists(ctx.Site)) { err = "Site not found"; return false; }

            var st = s.WorldState.Sites.Get(ctx.Site);
            bool ready = st.IsReadyToWork;
            if (ready != expReadyToWork) { err = $"ReadyToWork mismatch exp={expReadyToWork} got={ready}"; return false; }

            float wd = st.WorkSecondsDone;
            if (wd < expWorkDoneMin || wd > expWorkDoneMax)
            {
                err = $"WorkSecondsDone out of range exp=[{expWorkDoneMin},{expWorkDoneMax}] got={wd}";
                return false;
            }

            if (s.WorldState.Buildings == null || !s.WorldState.Buildings.Exists(ctx.Building))
            {
                err = "Placeholder building missing";
                return false;
            }

            var b = s.WorldState.Buildings.Get(ctx.Building);
            if (b.IsConstructed)
            {
                err = "Building already constructed (expected placeholder).";
                return false;
            }

            return true;
        }

        private static bool ValidateMidDelivery(GameServices s, BuildCtx ctx, out string err)
        {
            err = null;
            if (!ValidateSiteState(s, ctx, expReadyToWork: false, expWorkDoneMin: 0f, expWorkDoneMax: 99999f, out err))
                return false;

            var st = s.WorldState.Sites.Get(ctx.Site);
            int deliveredSum = SumList(st.DeliveredSoFar);
            int remainingSum = SumList(st.RemainingCosts);

            if (deliveredSum <= 0) { err = "DeliveredSoFar sum <= 0"; return false; }
            if (remainingSum <= 0) { err = "RemainingCosts sum <= 0 (expected still remaining)"; return false; }

            return true;
        }

        private static bool ValidateMidWork(GameServices s, BuildCtx ctx, out string err)
        {
            err = null;
            if (s.WorldState.Sites == null || !s.WorldState.Sites.Exists(ctx.Site)) { err = "Site missing"; return false; }
            var st = s.WorldState.Sites.Get(ctx.Site);

            if (!st.IsReadyToWork) { err = "Expected ReadyToWork=true"; return false; }
            if (st.WorkSecondsTotal <= 0f) { err = "WorkSecondsTotal <= 0"; return false; }
            if (st.WorkSecondsDone <= 0f || st.WorkSecondsDone >= st.WorkSecondsTotal)
            {
                err = $"WorkSecondsDone not mid-range: {st.WorkSecondsDone}/{st.WorkSecondsTotal}";
                return false;
            }

            return true;
        }

        private static bool ValidateCompleted(GameServices s, BuildCtx ctx, out string err)
        {
            err = null;

            // Site should be destroyed
            if (s.WorldState.Sites != null && s.WorldState.Sites.Exists(ctx.Site))
            {
                err = "Site still exists (expected destroyed).";
                return false;
            }

            // Building should be constructed
            if (s.WorldState.Buildings == null || !s.WorldState.Buildings.Exists(ctx.Building))
            {
                err = "Building missing after completion.";
                return false;
            }

            var b = s.WorldState.Buildings.Get(ctx.Building);
            if (!b.IsConstructed)
            {
                err = "Building not constructed after finalize.";
                return false;
            }

            return true;
        }

        private static bool ValidateClockPhase(GameServices s, Season season, Phase phase, out string err)
        {
            err = null;
            if (s.RunClock.CurrentSeason != season) { err = $"Season mismatch exp={season} got={s.RunClock.CurrentSeason}"; return false; }
            if (s.RunClock.CurrentPhase != phase) { err = $"Phase mismatch exp={phase} got={s.RunClock.CurrentPhase}"; return false; }
            return true;
        }

        private static bool ValidateHasEnemies(GameServices s, out string err)
        {
            err = null;
            int e = s.WorldState?.Enemies?.Count ?? 0;
            if (e <= 0)
            {
                err = "No enemies found (mid-wave spawn failed or lanes missing).";
                return false;
            }
            return true;
        }

        // -------------------------
        // Placement/road helpers
        // -------------------------

        private static void PlaceRoadChain(GameServices s, int length)
        {
            if (s.PlacementService == null) return;
            if (length <= 0) return;

            // Find first road
            if (!TryFindFirstRoad(s.GridMap, out var r0)) return;

            var cur = r0;
            for (int i = 0; i < length; i++)
            {
                if (TryFindRoadExtensionCell(s, cur, out var next))
                {
                    s.PlacementService.PlaceRoad(next);
                    cur = next;
                }
                else
                {
                    // Can't extend further - stop
                    break;
                }
            }
        }

        private static bool TryFindRoadExtensionCell(GameServices s, CellPos from, out CellPos next)
        {
            next = default;

            // Try 4-neighbors in deterministic order
            var cands = new[]
            {
                new CellPos(from.X, from.Y + 1),
                new CellPos(from.X + 1, from.Y),
                new CellPos(from.X, from.Y - 1),
                new CellPos(from.X - 1, from.Y),
            };

            for (int i = 0; i < cands.Length; i++)
            {
                var c = cands[i];
                if (s.PlacementService.CanPlaceRoad(c))
                {
                    next = c;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindFirstRoad(IGridMap grid, out CellPos road)
        {
            road = default;
            if (grid == null) return false;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    if (grid.IsRoad(c)) { road = c; return true; }
                }
            }
            return false;
        }

        private static string ResolveBuildableDefId(GameServices s)
        {
            // Keep list short & stable
            string[] candidates =
            {
                "bld_farm_t1",
                "bld_woodcutter_t1",
                "bld_stonecutter_t1",
                "bld_ironhut_t1",
                "bld_warehouse_t1",
                "bld_forge_t1",
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var id = candidates[i];
                try { s.DataRegistry.GetBuilding(id); }
                catch { continue; }

                // optional unlock check
                if (s.UnlockService != null && !s.UnlockService.IsUnlocked(id))
                    continue;

                return id;
            }

            return null;
        }

        private static bool TryFindValidPlacement(GameServices s, string defId, Dir4 rot, out CellPos anchor, out CellPos suggestedRoad)
        {
            anchor = default;
            suggestedRoad = default;

            if (s.PlacementService == null) return false;

            for (int y = 0; y < s.GridMap.Height; y++)
            {
                for (int x = 0; x < s.GridMap.Width; x++)
                {
                    var a = new CellPos(x, y);
                    var vr = s.PlacementService.ValidateBuilding(defId, a, rot);
                    if (!vr.Ok) continue;

                    anchor = a;
                    suggestedRoad = vr.SuggestedRoadCell;
                    return true;
                }
            }

            return false;
        }

        // -------------------------
        // Clock / counts
        // -------------------------

        private struct ClockSnap
        {
            public int Year;
            public Season Season;
            public int Day;
            public float DayTimer;
            public float Scale;
            public Phase Phase;
        }

        private static ClockSnap CaptureClock(GameServices s)
        {
            var rc = s.RunClock as RunClockService;
            return new ClockSnap
            {
                Year = rc != null ? rc.YearIndex : 1,
                Season = s.RunClock.CurrentSeason,
                Day = s.RunClock.DayIndex,
                DayTimer = rc != null ? rc.DayTimerSeconds : 0f,
                Scale = s.RunClock.TimeScale,
                Phase = s.RunClock.CurrentPhase
            };
        }

        private static bool ClockEquals(ClockSnap a, ClockSnap b, out string err)
        {
            err = null;

            if (a.Year != b.Year || a.Season != b.Season || a.Day != b.Day || a.Phase != b.Phase)
            {
                err = $"exp Y{a.Year} {a.Season} D{a.Day} {a.Phase} got Y{b.Year} {b.Season} D{b.Day} {b.Phase}";
                return false;
            }

            if (Mathf.Abs(a.DayTimer - b.DayTimer) > 0.05f)
            {
                err = $"DayTimer exp {a.DayTimer:0.00} got {b.DayTimer:0.00}";
                return false;
            }

            if (Mathf.Abs(a.Scale - b.Scale) > 0.01f)
            {
                err = $"Scale exp {a.Scale:0.00} got {b.Scale:0.00}";
                return false;
            }

            return true;
        }

        private static WorldCounts CaptureCounts(GameServices s)
        {
            return new WorldCounts
            {
                Buildings = s.WorldState?.Buildings?.Count ?? 0,
                Sites = s.WorldState?.Sites?.Count ?? 0,
                Npcs = s.WorldState?.Npcs?.Count ?? 0,
                Towers = s.WorldState?.Towers?.Count ?? 0,
                Enemies = s.WorldState?.Enemies?.Count ?? 0,
                Roads = CountRoads(s.GridMap)
            };
        }

        private static bool CountsEquals(WorldCounts a, WorldCounts b, out string err)
        {
            err = null;
            if (a.Buildings != b.Buildings || a.Sites != b.Sites || a.Npcs != b.Npcs || a.Towers != b.Towers || a.Enemies != b.Enemies || a.Roads != b.Roads)
            {
                err = $"B {a.Buildings}->{b.Buildings} | S {a.Sites}->{b.Sites} | N {a.Npcs}->{b.Npcs} | T {a.Towers}->{b.Towers} | E {a.Enemies}->{b.Enemies} | R {a.Roads}->{b.Roads}";
                return false;
            }
            return true;
        }

        private static int CountRoads(IGridMap grid)
        {
            if (grid == null) return 0;
            int count = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsRoad(new CellPos(x, y))) count++;
            return count;
        }

        private static int SumList(System.Collections.Generic.List<CostDef> list)
        {
            if (list == null) return 0;
            int sum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null) continue;
                if (c.Amount > 0) sum += c.Amount;
            }
            return sum;
        }

        private static void ForceClock(GameServices s, int year, Season season, int day, float timer, float scale)
        {
            if (s.RunClock is RunClockService rc)
            {
                rc.LoadSnapshot(year, season.ToString(), day, Mathf.Max(0f, timer), scale);
            }
        }

        // -------------------------
        // Fresh run (deterministic)
        // -------------------------

        private static bool TryStartFreshRun(out string error)
        {
            error = null;

            var boot = UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (boot == null)
            {
                error = "GameBootstrap not found in scene.";
                return false;
            }

            // wipe existing save to avoid contamination
            if (!boot.TryStartNewRun(seed: 12345, startMapConfigOverride: null, wipeExistingSave: true, out var err))
            {
                error = err;
                return false;
            }

            return true;
        }
    }
}