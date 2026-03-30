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
            catch { }

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
            try { _s.TutorialHints.Reset(); } catch { }
            // Day40: reset season metrics
            try { _s.SeasonMetrics.Reset(); } catch { }

            // jobs / claims / build orders (runtime concrete types)
            try { _s.JobBoard.ClearAll(); } catch { }
            try { _s.ClaimService.ClearAll(); } catch { }
            try { _s.BuildOrderService.ClearAll(); } catch { }

            // grid occupancy
            try { _s.GridMap.ClearAll(); } catch { }
            try { _s.AgentMover?.ClearAll(); } catch { }

            // run-start runtime caches
            try { ResetRunStartRuntime(_s.RunStartRuntime); } catch { }
            try { _s.PopulationService?.Reset(); } catch { }

            // world stores (runtime concrete stores)
            try { (_s.WorldState?.Buildings as IEntityStore<BuildingId, BuildingState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Sites as IEntityStore<SiteId, BuildSiteState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Npcs as IEntityStore<NpcId, NpcState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Enemies as IEntityStore<EnemyId, EnemyState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Towers as IEntityStore<TowerId, TowerState>)?.ClearAll(); } catch { }
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
