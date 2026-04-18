using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobNotificationPolicy
    {
        private readonly INotificationService _noti;

        internal JobNotificationPolicy(INotificationService noti)
        {
            _noti = noti;
        }

        internal void NotifyNoJobs(BuildingId workplace, string workplaceDefId)
        {
            // Intentionally silent for player-facing UX.
            // Temporary lack of jobs at HQ/workplaces is a normal state in the current game loop,
            // and onboarding guidance is handled by TutorialHintsService instead.
        }
    }
}
