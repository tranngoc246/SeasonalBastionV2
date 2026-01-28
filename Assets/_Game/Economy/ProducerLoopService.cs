using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day36: Producer loop tối thiểu (Farm/Lumber subset)
    /// - Khi local storage chưa full và workplace queue trống => enqueue 1 Harvest job.
    /// - Khi full => cancel job harvest đang chờ (nếu có) + notify (anti-spam).
    /// </summary>
    public sealed class ProducerLoopService : ITickable
    {
        private readonly IWorldState _w;
        private readonly IDataRegistry _data;
        private readonly IStorageService _storage;
        private readonly IJobBoard _jobs;
        private readonly INotificationService _noti;
        private readonly IRunClock _clock;

        // scan interval để nhẹ, nhưng vẫn deterministic (tích luỹ dt)
        private float _scanAcc;
        private const float ScanInterval = 0.5f;

        public ProducerLoopService(
            IWorldState w,
            IDataRegistry data,
            IStorageService storage,
            IJobBoard jobs,
            INotificationService noti,
            IRunClock clock)
        {
            _w = w;
            _data = data;
            _storage = storage;
            _jobs = jobs;
            _noti = noti;
            _clock = clock;
        }

        public void Tick(float dt)
        {
            if (_w == null || _data == null || _storage == null || _jobs == null) return;

            // (tối thiểu) Producer chạy trong Build phase để đơn giản.
            if (_clock != null && _clock.CurrentPhase == Phase.Defend) return;

            _scanAcc += dt;
            if (_scanAcc < ScanInterval) return;
            _scanAcc -= ScanInterval;

            foreach (var bid in _w.Buildings.Ids)
            {
                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                var bdef = _data.GetBuilding(bs.DefId);
                if (bdef == null || !bdef.IsProducer) continue;

                // Day36 subset: Farm/Lumber
                if (!TryGetProducedResource(bs.DefId, out var rt))
                    continue;

                int cap = _storage.GetCap(bid, rt);
                if (cap <= 0) continue;

                int cur = _storage.GetAmount(bid, rt);
                bool full = cur >= cap;

                if (full)
                {
                    // Nếu còn job Harvest đang chờ ở workplace => cancel để không bị fail lặp lại
                    if (_jobs.TryPeekForWorkplace(bid, out var peek) && peek.Archetype == JobArchetype.Harvest)
                        _jobs.Cancel(peek.Id);

                    // Notify anti-spam (dedupe theo key + cooldown)
                    if (_noti != null)
                    {
                        _noti.Push(
                            key: "producer.local.full",
                            title: "Producer full",
                            body: $"{ShortName(bs.DefId)}: {rt} full ({cur}/{cap})",
                            severity: NotificationSeverity.Info, 
                            payload: new NotificationPayload(bid, default, null),
                            cooldownSeconds: 8f,
                            dedupeByKey: true);
                    }
                    continue;
                }

                // Nếu workplace queue trống => tạo 1 job Harvest
                // (Dùng JobBoard.CountForWorkplace nếu implementation là JobBoard; nếu không có thì fallback peek)
                int queued = 0;
                if (_jobs is IJobBoard jb) queued = jb.CountForWorkplace(bid);
                else if (_jobs.TryPeekForWorkplace(bid, out _)) queued = 1;

                if (queued <= 0)
                {
                    _jobs.Enqueue(new Job
                    {
                        Archetype = JobArchetype.Harvest,
                        Status = JobStatus.Created,
                        Workplace = bid
                    });
                }
            }
        }

        private static bool TryGetProducedResource(string defId, out ResourceType rt)
        {
            // subset theo yêu cầu Day36: Farm/Lumber
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1")) { rt = ResourceType.Food; return true; }
            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1")) { rt = ResourceType.Wood; return true; }

            rt = default;
            return false;
        }

        private static bool EqualsIgnoreCase(string a, string b)
            => string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

        private static string ShortName(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return "Producer";
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1")) return "Farmhouse";
            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1")) return "LumberCamp";
            return defId;
        }
    }
}
