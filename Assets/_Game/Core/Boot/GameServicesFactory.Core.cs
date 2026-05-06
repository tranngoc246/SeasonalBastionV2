using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static partial class GameServicesFactory
    {
        private static void ComposeCore(GameServices services, DefsCatalog catalog)
        {
            services.EventBus = new EventBus();
            services.DataRegistry = new DataRegistry(catalog);
            services.DataValidator = new DataValidator();
            var dr = services.DataRegistry as DataRegistry;
            services.Balance = new BalanceService(services, dr != null ? dr.GetBalanceOrNull() : null);
            services.RunClock = new RunClockService(services.EventBus);
            var unlockJson = UnityEngine.Resources.Load<UnityEngine.TextAsset>("UnlockSchedule_v0_1");
            services.UnlockService = new UnlockService(services.RunClock, unlockJson, services.EventBus);
            services.NotificationService = new NotificationService(services.EventBus);
            services.TutorialHints = new TutorialHintsService(services);
            services.SeasonMetrics = new SeasonMetricsService(services.EventBus);
        }
    }
}
