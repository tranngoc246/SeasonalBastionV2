namespace SeasonalBastion
{
    public static class TickOrder
    {
        public static void TickAll(GameServices s, float dt)
        {
            if (s.RunClock is ITickable clockTick) clockTick.Tick(dt);
            if (s.NotificationService is ITickable notiTick) notiTick.Tick(dt);
            if (s.BuildOrderService is ITickable buildTick) buildTick.Tick(dt);
            if (s.JobScheduler is ITickable jobTick) jobTick.Tick(dt);
            if (s.ResourceFlowService is ITickable flowTick) flowTick.Tick(dt);
            if (s.AmmoService is ITickable ammoTick) ammoTick.Tick(dt);
            if (s.CombatService is ITickable combatTick) combatTick.Tick(dt);
            if (s.RunOutcomeService is ITickable outcomeTick) outcomeTick.Tick(dt);
        }
    }
}
