using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public readonly struct SeasonMetricsSnapshot
    {
        public readonly Season Season;
        public readonly int YearIndex;
        public readonly int DayIndex;

        public readonly int[] GainedByRes; // index = (int)ResourceType
        public readonly int[] SpentByRes;

        public readonly int EnemiesKilled;
        public readonly int BuildingsBuilt;
        public readonly int AmmoUsed;

        public SeasonMetricsSnapshot(
            Season s, int year, int day,
            int[] gained, int[] spent,
            int killed, int built, int ammoUsed)
        {
            Season = s; YearIndex = year; DayIndex = day;
            GainedByRes = gained; SpentByRes = spent;
            EnemiesKilled = killed; BuildingsBuilt = built; AmmoUsed = ammoUsed;
        }
    }

    public sealed class SeasonMetricsService : IResettable
    {
        private readonly IEventBus _bus;

        private Season _season;
        private int _year;
        private int _day;

        private readonly int[] _gained = new int[5];
        private readonly int[] _spent = new int[5];

        private int _enemiesKilled;
        private int _buildingsBuilt;
        private int _ammoUsed;

        // reset guard theo season (year+season) dựa trên DayStartedEvent(day==1)
        private int _activeSeasonKey;

        public SeasonMetricsService(IEventBus bus)
        {
            _bus = bus;

            _bus.Subscribe<DayStartedEvent>(OnDayStarted);
            _bus.Subscribe<ResourceDeliveredEvent>(OnDelivered);
            _bus.Subscribe<ResourceSpentEvent>(OnSpent);
            _bus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            _bus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            _bus.Subscribe<AmmoUsedEvent>(OnAmmoUsed);
        }

        public void Reset()
        {
            _season = Season.Spring;
            _year = 1;
            _day = 1;
            _activeSeasonKey = 0;

            Array.Clear(_gained, 0, _gained.Length);
            Array.Clear(_spent, 0, _spent.Length);

            _enemiesKilled = 0;
            _buildingsBuilt = 0;
            _ammoUsed = 0;
        }

        public SeasonMetricsSnapshot GetSnapshot()
        {
            // clone arrays để UI đọc an toàn (tránh bị mutate lúc render)
            var g = new int[_gained.Length];
            var s = new int[_spent.Length];
            Array.Copy(_gained, g, g.Length);
            Array.Copy(_spent, s, s.Length);

            return new SeasonMetricsSnapshot(
                _season, _year, _day,
                g, s,
                _enemiesKilled, _buildingsBuilt, _ammoUsed
            );
        }

        private void OnDayStarted(DayStartedEvent ev)
        {
            _season = ev.Season;
            _year = ev.YearIndex;
            _day = ev.DayIndex;

            // Reset metrics khi vào ngày 1 của season mới
            if (ev.DayIndex == 1)
            {
                int key = (ev.YearIndex * 10) + (int)ev.Season;
                if (key != _activeSeasonKey)
                {
                    _activeSeasonKey = key;

                    Array.Clear(_gained, 0, _gained.Length);
                    Array.Clear(_spent, 0, _spent.Length);
                    _enemiesKilled = 0;
                    _buildingsBuilt = 0;
                    _ammoUsed = 0;
                }
            }
        }

        private void OnDelivered(ResourceDeliveredEvent ev)
        {
            int idx = (int)ev.Type;
            if ((uint)idx >= (uint)_gained.Length) return;
            if (ev.Amount <= 0) return;
            _gained[idx] += ev.Amount;
        }

        private void OnSpent(ResourceSpentEvent ev)
        {
            int idx = (int)ev.Type;
            if ((uint)idx >= (uint)_spent.Length) return;
            if (ev.Amount <= 0) return;
            _spent[idx] += ev.Amount;
        }

        private void OnBuildingPlaced(BuildingPlacedEvent ev)
        {
            _buildingsBuilt++;
        }

        private void OnEnemyKilled(EnemyKilledEvent ev)
        {
            int c = ev.Count <= 0 ? 1 : ev.Count;
            _enemiesKilled += c;
        }

        private void OnAmmoUsed(AmmoUsedEvent ev)
        {
            if (ev.Amount <= 0) return;
            _ammoUsed += ev.Amount;
        }
    }
}
