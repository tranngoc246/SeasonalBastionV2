using System;
using System.Collections.Generic;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class UnlockService : IUnlockService, ITickable
    {
        private readonly IRunClock _clock;
        private readonly UnlockScheduleDef _schedule;

        private readonly HashSet<string> _unlocked = new(StringComparer.OrdinalIgnoreCase);

        private int _lastYear;
        private Season _lastSeason;
        private int _lastDay;

        private float _acc;
        private const float ScanInterval = 0.25f;

        private readonly IEventBus _bus;
        private int _currentYear = 1;

        public UnlockService(IRunClock clock, TextAsset scheduleJsonOrNull, IEventBus bus)
        {
            _clock = clock;
            _bus = bus;

            _schedule = LoadScheduleOrFallback(scheduleJsonOrNull);

            // Latch year/season/day từ event để tránh cast concrete
            if (_bus != null)
                _bus.Subscribe<DayStartedEvent>(OnDayStarted);

            // Init lần đầu
            if (_clock != null)
            {
                _lastSeason = _clock.CurrentSeason;
                _lastDay = _clock.DayIndex;
            }
            _lastYear = _currentYear;

            Recompute();
        }

        public void Tick(float dt)
        {
            _acc += dt;
            if (_acc < ScanInterval) return;
            _acc -= ScanInterval;

            if (_clock == null) return;
            int y = _currentYear;
            var s = _clock.CurrentSeason;
            int d = _clock.DayIndex;

            if (y != _lastYear || s != _lastSeason || d != _lastDay)
                Recompute();
        }

        private void OnDayStarted(DayStartedEvent ev)
        {
            _currentYear = ev.YearIndex;

            // Nếu không đổi mốc thì không cần recompute
            if (ev.YearIndex == _lastYear && ev.Season == _lastSeason && ev.DayIndex == _lastDay)
                return;

            RecomputeFrom(ev.YearIndex, ev.Season, ev.DayIndex);
        }

        public bool IsUnlocked(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return false;
            return _unlocked.Contains(defId);
        }

        private void Recompute()
        {
            if (_clock == null)
            {
                _unlocked.Clear();
                ApplyStartUnlockedOnly();
                return;
            }

            // Year lấy từ cache (event), không cast
            int y = _currentYear;
            var s = _clock.CurrentSeason;
            int d = _clock.DayIndex;

            RecomputeFrom(y, s, d);
        }

        private void RecomputeFrom(int year, Season season, int day)
        {
            _unlocked.Clear();

            _lastYear = year;
            _lastSeason = season;
            _lastDay = day;

            ApplyStartUnlockedOnly();

            if (_schedule?.Entries == null) return;

            for (int i = 0; i < _schedule.Entries.Count; i++)
            {
                var e = _schedule.Entries[i];
                if (e == null || string.IsNullOrEmpty(e.DefId)) continue;

                if (IsTimeReached(_lastYear, _lastSeason, _lastDay, e.Year, e.Season, e.Day))
                    _unlocked.Add(e.DefId);
            }
        }

        private void ApplyStartUnlockedOnly()
        {
            if (_schedule?.StartUnlocked == null) return;
            for (int i = 0; i < _schedule.StartUnlocked.Count; i++)
            {
                var id = _schedule.StartUnlocked[i];
                if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
            }
        }

        private static bool IsTimeReached(int cy, Season cs, int cd, int y, Season s, int d)
        {
            if (cy != y) return cy > y;
            if (cs != s) return cs > s;
            return cd >= d;
        }

        private static UnlockScheduleDef LoadScheduleOrFallback(TextAsset json)
        {
            if (json == null || string.IsNullOrEmpty(json.text))
                return DefaultFallback();

            try { return JsonUtility.FromJson<UnlockScheduleDef>(json.text) ?? DefaultFallback(); }
            catch { return DefaultFallback(); }
        }

        private static UnlockScheduleDef DefaultFallback()
        {
            return new UnlockScheduleDef
            {
                SchemaVersion = 1,
                StartUnlocked = new List<string>
                {
                    "bld_hq_t1",
                    "bld_farmhouse_t1",
                    "bld_lumbercamp_t1",
                    "bld_warehouse_t1"
                }
            };
        }

        private static int GetYearIndex(IRunClock clock)
        {
            if (clock is RunClockService rc) return rc.YearIndex; // RunClockService có YearIndex
            return 1; // fallback an toàn
        }
    }
}
