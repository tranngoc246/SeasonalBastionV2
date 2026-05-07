using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderRepairService
    {
        private readonly IWorldState _worldState;
        private readonly IDataRegistry _dataRegistry;
        private readonly INotificationService _notificationService;
        private readonly IJobBoard _jobBoard;
        private readonly Dictionary<int, JobId> _repairJobByOrder;
        private readonly Action<int> _cancelRepairJob;

        public BuildOrderRepairService(
            IWorldState worldState,
            IDataRegistry dataRegistry,
            INotificationService notificationService,
            IJobBoard jobBoard,
            Dictionary<int, JobId> repairJobByOrder,
            Action<int> cancelRepairJob)
        {
            _worldState = worldState;
            _dataRegistry = dataRegistry;
            _notificationService = notificationService;
            _jobBoard = jobBoard;
            _repairJobByOrder = repairJobByOrder;
            _cancelRepairJob = cancelRepairJob;
        }

        public void TickRepairOrder(int orderId, ref BuildOrder order, BuildingId workplace)
        {
            if (_jobBoard == null) return;

            if (!_worldState.Buildings.Exists(order.TargetBuilding))
            {
                _cancelRepairJob(orderId);
                order.Completed = true;
                return;
            }

            var bs = _worldState.Buildings.Get(order.TargetBuilding);
            if (!bs.IsConstructed)
            {
                _cancelRepairJob(orderId);
                order.Completed = true;
                return;
            }

            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                if (_dataRegistry.TryGetBuilding(bs.DefId, out var repairDef) && repairDef != null)
                    mhp = Math.Max(1, repairDef.MaxHp);
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                _worldState.Buildings.Set(order.TargetBuilding, bs);
            }

            if (bs.HP >= bs.MaxHP)
            {
                _cancelRepairJob(orderId);
                order.Completed = true;
                _notificationService?.Push(
                    key: $"RepairDone_{order.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Repair completed: {bs.DefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(order.TargetBuilding, default, bs.DefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true);
                return;
            }

            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                if (!_jobBoard.TryGet(jid, out var j) || IsTerminal(j.Status))
                {
                    _repairJobByOrder.Remove(orderId);
                }
                else
                {
                    if (j.Status == JobStatus.Created && j.Workplace.Value != workplace.Value)
                    {
                        j.Workplace = workplace;
                        _jobBoard.Update(j);
                    }
                    return;
                }
            }

            var job = new Job
            {
                Archetype = JobArchetype.RepairWork,
                Status = JobStatus.Created,
                Workplace = workplace,
                SourceBuilding = default,
                DestBuilding = order.TargetBuilding,
                Site = default,
                Tower = default,
                ResourceType = 0,
                Amount = 0,
                TargetCell = bs.Anchor,
                CreatedAt = 0
            };

            var newId = _jobBoard.Enqueue(job);
            _repairJobByOrder[orderId] = newId;
        }

        private static bool IsTerminal(JobStatus s)
            => s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
    }
}
