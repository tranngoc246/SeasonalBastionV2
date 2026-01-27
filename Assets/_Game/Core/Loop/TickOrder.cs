namespace SeasonalBastion
{
    public static class TickOrder
    {
        public static void TickAll(GameServices s, float dtUnscaled)
        { 
            if (s == null) return;
            if (dtUnscaled <= 0f) return;

            // 1) Clock luôn tick bằng unscaled dtUnscaled (clock tự scale bằng TimeScale)
            if (s.RunClock is ITickable clockTick)
                clockTick.Tick(dtUnscaled);

            // 2) Simulation dtUnscaled: unscaled * run timescale (pause => 0)
            float ts = s.RunClock != null ? s.RunClock.TimeScale : 1f;
            if (ts <= 0f) return;

            float simDt = dtUnscaled * ts;
            if (simDt <= 0f) return;

            // 3) Các hệ còn lại tick theo simDt
            if (s.NotificationService is ITickable notiTick) notiTick.Tick(simDt);
            if (s.BuildOrderService is ITickable buildTick) buildTick.Tick(simDt);
            if (s.JobScheduler is ITickable jobTick) jobTick.Tick(simDt);
            if (s.ResourceFlowService is ITickable flowTick) flowTick.Tick(simDt);
            if (s.AmmoService is ITickable ammoTick) ammoTick.Tick(simDt);
            if (s.CombatService is ITickable combatTick) combatTick.Tick(simDt);
            if (s.RunOutcomeService is ITickable outcomeTick) outcomeTick.Tick(simDt);
        }
    }
}
