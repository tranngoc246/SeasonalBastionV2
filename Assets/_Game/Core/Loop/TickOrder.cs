namespace SeasonalBastion
{
    public static class TickOrder
    {
        public static void TickAll(GameServices s, float dtUnscaled)
        {
            if (s == null) return;
            if (dtUnscaled <= 0f) return;

            // clamp wall dt to avoid huge hitch
            if (dtUnscaled > 0.25f) dtUnscaled = 0.25f;

            // 1) RunClock tick bằng unscaled dt (RunClock tự nhân TimeScale)
            if (s.RunClock is ITickable clockTick)
                clockTick.Tick(dtUnscaled);

            // 2) Lấy timescale từ run clock (pause => 0)
            float ts = s.RunClock != null ? s.RunClock.TimeScale : 1f;
            if (ts <= 0f) return;

            // 3) Sim dt (scaled) — tick 1 lần/frame để không "bắn" JobScheduler quá nhiều lần
            float simDt = dtUnscaled * ts;
            if (simDt <= 0f) return;

            // UI-ish services tick theo frame
            if (s.NotificationService is ITickable notiTick) notiTick.Tick(simDt);
            if (s.TutorialHints is ITickable hintTick) hintTick.Tick(simDt);

            // simulation services
            if (s.BuildOrderService is ITickable buildTick) buildTick.Tick(simDt);
            if (s.ProducerLoopService is ITickable prodTick) prodTick.Tick(simDt);
            if (s.UnlockService is ITickable u) u.Tick(simDt);
            if (s.JobScheduler is ITickable jobTick) jobTick.Tick(simDt);
            if (s.ResourceFlowService is ITickable flowTick) flowTick.Tick(simDt);
            if (s.AmmoService is ITickable ammoTick) ammoTick.Tick(simDt);
            if (s.CombatService is ITickable combatTick) combatTick.Tick(simDt);
            if (s.RunOutcomeService is ITickable outcomeTick) outcomeTick.Tick(simDt);
        }
    }
}
