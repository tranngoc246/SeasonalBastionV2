using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;
using System;

namespace SeasonalBastion
{
    public sealed class GameLoop : IDisposable
    {
        private readonly GameServices _s;

        public GameLoop(GameServices services)
        {
            _s = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void StartNewRun(int seed, string startMapConfigJsonOrMarkdown = null)
        {
            _s.RunOutcomeService?.ResetOutcome();

            // Reset runtime state (world/grid/jobs/orders/notifications) to avoid leaks between runs.
            ResetForNewRun();

            // Mute notifications during boot/loading (prevents false positives in first frames)
            try
            {
                if (_s.NotificationService is NotificationService ns)
                    ns.MuteForSeconds(4.0f);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[GameLoop] Failed to mute notifications during run reset: {ex}");
            }

            // Deterministic initial run clock state
            if (_s.RunStartRuntime != null)
                _s.RunStartRuntime.Seed = seed;

            if (_s.RunClock is RunClockService rc)
                rc.Start(seed);
            else
            {
                _s.RunClock.ForceSeasonDay(Season.Spring, dayIndex: 1);
                _s.RunClock.SetTimeScale(1f);
            }

            // Apply StartMapConfig (if provided). If missing, keep empty world but deterministic.
            if (!string.IsNullOrEmpty(startMapConfigJsonOrMarkdown))
            {
                if (!RunStartFacade.TryApply(_s, startMapConfigJsonOrMarkdown, out var err))
                {
                    if (_s.RunStartRuntime != null)
                    {
                        _s.RunStartRuntime.ResourceGenerationFailureReason = err;
                        _s.RunStartRuntime.OpeningQualityBand = "RunStartApplyFailed";
                    }

                    // Fail-safe: notify + continue (empty world)
                    _s.NotificationService?.Push(
                        key: "RunStartApplyFailed",
                        title: "Run Start",
                        body: err,
                        severity: NotificationSeverity.Error,
                        payload: default,
                        cooldownSeconds: 0f,
                        dedupeByKey: true
                    );
                }
            }

            // rebuild derived world index lists (safe even if world is empty).
            _s.WorldIndex?.RebuildAll();
            _s.PopulationService?.RebuildDerivedState();
        }

        private void ResetForNewRun()
        {
            // clear transient UI
            if (_s.NotificationService is NotificationService ns) ns.ClearInbox();
            else _s.NotificationService.ClearAll();
            try { _s.TutorialHints.Reset(); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[GameLoop] Failed to reset TutorialHints: {ex}"); }
            // Day40: reset season metrics
            try { _s.SeasonMetrics.Reset(); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[GameLoop] Failed to reset SeasonMetrics: {ex}"); }

            // jobs / claims / build orders (runtime concrete types)
            try { _s.JobBoard.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear JobBoard during run reset: {ex}"); }
            try { _s.ClaimService.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear ClaimService during run reset: {ex}"); }
            try { _s.BuildOrderService.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear BuildOrderService during run reset: {ex}"); }

            // grid occupancy
            try { _s.GridMap.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear GridMap during run reset: {ex}"); }
            try { _s.AgentMover?.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[GameLoop] Failed to clear AgentMover caches during run reset: {ex}"); }

            // run-start runtime caches
            try { ResetRunStartRuntime(_s.RunStartRuntime); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[GameLoop] Failed to reset RunStartRuntime: {ex}"); }
            try { _s.PopulationService?.Reset(); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[GameLoop] Failed to reset PopulationService: {ex}"); }

            // world stores (runtime concrete stores)
            try { (_s.WorldState?.Buildings as IEntityStore<BuildingId, BuildingState>)?.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear building store during run reset: {ex}"); }
            try { (_s.WorldState?.Sites as IEntityStore<SiteId, BuildSiteState>)?.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear site store during run reset: {ex}"); }
            try { (_s.WorldState?.Npcs as IEntityStore<NpcId, NpcState>)?.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear NPC store during run reset: {ex}"); }
            try { (_s.WorldState?.Enemies as IEntityStore<EnemyId, EnemyState>)?.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear enemy store during run reset: {ex}"); }
            try { (_s.WorldState?.Towers as IEntityStore<TowerId, TowerState>)?.ClearAll(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[GameLoop] Failed to clear tower store during run reset: {ex}"); }
        }

        public void Tick(float dt) => TickOrder.TickAll(_s, dt);

        private static void ResetRunStartRuntime(RunStartRuntime rt)
        {
            if (rt == null) return;

            rt.Seed = 0;
            rt.MapWidth = 0;
            rt.MapHeight = 0;
            rt.ResourceGenerationModeRequested = null;
            rt.ResourceGenerationModeApplied = null;
            rt.ResourceGenerationFailureReason = null;
            rt.OpeningQualityBand = null;
            rt.BuildableRect = default;
            rt.SpawnGates.Clear();
            rt.Zones.Clear();
            rt.Lanes.Clear();
            rt.LockedInvariants.Clear();
        }

        public void Dispose()
        {
        }
    }
}
