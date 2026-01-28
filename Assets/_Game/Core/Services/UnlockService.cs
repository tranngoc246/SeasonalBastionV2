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

        public UnlockService(IRunClock clock, TextAsset scheduleJsonOrNull)
        {
            _clock = clock;
            _schedule = LoadScheduleOrFallback(scheduleJsonOrNull);
            Recompute();
        }

        public void Tick(float dt)
        {
            _acc += dt;
            if (_acc < ScanInterval) return;
            _acc -= ScanInterval;

            if (_clock == null) return;
            int y = GetYearIndex(_clock);
            var s = _clock.CurrentSeason;
            int d = _clock.DayIndex;

            if (y != _lastYear || s != _lastSeason || d != _lastDay)
                Recompute();
        }

        public bool IsUnlocked(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return false;
            return _unlocked.Contains(defId);
        }

        private void Recompute()
        {
            _unlocked.Clear();

            if (_clock == null)
            {
                ApplyStartUnlockedOnly();
                return;
            }

            _lastYear = GetYearIndex(_clock);
            _lastSeason = _clock.CurrentSeason;
            _lastDay = _clock.DayIndex;

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
