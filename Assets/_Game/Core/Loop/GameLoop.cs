using System;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;

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
            if (_s.RunOutcomeService is IResettable resetOutcome) resetOutcome.Reset();

            // Reset runtime state (world/grid/jobs/orders/notifications) to avoid leaks between runs.
            ResetForNewRun();

            // Deterministic initial run clock state
            _s.RunClock.ForceSeasonDay(Season.Spring, dayIndex: 1);
            _s.RunClock.SetTimeScale(1f);

            // Apply StartMapConfig (if provided). If missing, keep empty world but deterministic.
            if (!string.IsNullOrEmpty(startMapConfigJsonOrMarkdown))
            {
                if (!RunStartApplier.TryApply(_s, startMapConfigJsonOrMarkdown, out var err))
                {
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
        }
        private void ResetForNewRun()
        {
            // clear transient UI
            try { (_s.NotificationService as INotificationService)?.ClearAll(); } catch { }

            // jobs / claims / build orders (runtime concrete types)
            try { (_s.JobBoard as IJobBoard)?.ClearAll(); } catch { }
            try { (_s.ClaimService as IClaimService)?.ClearAll(); } catch { }
            try { (_s.BuildOrderService as IBuildOrderService)?.ClearAll(); } catch { }

            // grid occupancy
            try { (_s.GridMap as IGridMap)?.ClearAll(); } catch { }

            // world stores (runtime concrete stores)
            try { (_s.WorldState?.Buildings as IEntityStore<BuildingId, BuildingState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Sites as IEntityStore<SiteId, BuildSiteState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Npcs as IEntityStore<NpcId, NpcState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Enemies as IEntityStore<EnemyId, EnemyState>)?.ClearAll(); } catch { }
            try { (_s.WorldState?.Towers as IEntityStore<TowerId, TowerState>)?.ClearAll(); } catch { }
        }

        public void Tick(float dt) => TickOrder.TickAll(_s, dt);

        public void Dispose()
        {
        }
    }
}
