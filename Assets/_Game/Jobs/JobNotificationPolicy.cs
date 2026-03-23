using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class JobNotificationPolicy
    {
        private readonly INotificationService _noti;
        private readonly Dictionary<int, float> _noJobNextNotifyAt = new();

        internal JobNotificationPolicy(INotificationService noti)
        {
            _noti = noti;
        }

        internal void NotifyNoJobs(BuildingId workplace, string workplaceDefId)
        {
            if (_noti == null) return;

            float now = Time.realtimeSinceStartup;
            if (_noJobNextNotifyAt.TryGetValue(workplace.Value, out var next) && now < next)
                return;

            _noJobNextNotifyAt[workplace.Value] = now + 3f;

            _noti.Push(
                key: $"NoJobs_{workplace.Value}",
                title: "NPC không có việc để làm",
                body: $"Workplace={workplaceDefId} (#{workplace.Value}) không có job hợp lệ.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(workplace, default, "no_jobs"),
                cooldownSeconds: 3f,
                dedupeByKey: true);
        }
    }
}
