using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Core-side resolver: scan DataRegistry internal maps để lấy waves cho 1 calendar day.
    /// Combat assembly chỉ gọi qua IWaveCalendarResolver.
    /// </summary>
    public sealed class WaveCalendarResolver : IWaveCalendarResolver
    {
        private readonly IDataRegistry _reg;

        // cache theo (year, season, day) để deterministic + tránh scan mỗi tick
        private readonly Dictionary<int, List<WaveDef>> _cache = new();

        public WaveCalendarResolver(IDataRegistry reg)
        {
            _reg = reg;
        }

        public IReadOnlyList<WaveDef> Resolve(int year, Season season, int day)
        {
            int key = MakeKey(year, season, day);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var list = new List<WaveDef>(2);

            // Chỉ DataRegistry concrete mới enumerate được (debug helpers).
            // DataRegistry có GetAllWaveIds/TryGetWave【:contentReference[oaicite:2]{index=2}】
            if (_reg is DataRegistry dr)
            {
                foreach (var id in dr.GetAllWaveIds())
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!dr.TryGetWave(id, out var def) || def == null) continue;

                    if (def.Year != year) continue;
                    if (def.Season != season) continue;
                    if (def.Day != day) continue;

                    list.Add(def);
                }
            }
            else
            {
                // Nếu về sau reg không phải DataRegistry, contract Part25 hiện không cho enumerate【:contentReference[oaicite:3]{index=3}】
                // => return empty để fail-safe.
            }

            // deterministic order
            list.Sort((a, b) =>
            {
                int c = a.WaveIndex.CompareTo(b.WaveIndex);
                if (c != 0) return c;
                return string.CompareOrdinal(a.DefId, b.DefId);
            });

            // Day35: inject boss wave (minimal) if boss enemy is scheduled for this calendar
            TryInjectBossWave(year, season, day, list);

            _cache[key] = list;
            return list;
        }

        private static int MakeKey(int year, Season season, int day)
        {
            // year: 1..n, season: 0..3, day: 1..?
            unchecked { return (year * 100000) + ((int)season * 1000) + day; }
        }

        private void TryInjectBossWave(int year, Season season, int day, List<WaveDef> list)
        {
            // Nếu đã có boss wave trong day này thì thôi
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsBoss)
                    return;
            }

            // Boss data-driven nằm trong EnemyDef (isBoss + year/season/day)
            if (!(_reg is DataRegistry dr)) return;

            string bossEnemyId = null;

            foreach (var eid in dr.GetAllEnemyIds())
            {
                if (string.IsNullOrEmpty(eid)) continue;
                if (!dr.TryGetEnemy(eid, out var ed) || ed == null) continue;
                if (!ed.IsBoss) continue;

                if (ed.BossYear == year && ed.BossSeason == season && ed.BossDay == day)
                {
                    bossEnemyId = ed.DefId;
                    break;
                }
            }

            if (string.IsNullOrEmpty(bossEnemyId))
                return;

            // Tạo wave boss minimal (1 con boss). Index lớn để chắc chắn “cuối ngày”
            var bossWave = new WaveDef
            {
                DefId = $"AUTO_BOSS_{year}_{season}_D{day}_{bossEnemyId}",
                WaveIndex = 9999,
                Year = year,
                Season = season,
                Day = day,
                IsBoss = true,
                Entries = new[]
                {
            new WaveEntryDef{ EnemyId = bossEnemyId, Count = 1 }
        }
            };

            list.Add(bossWave);
        }
    }
}
