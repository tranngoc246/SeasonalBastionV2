using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// Resolve waves theo lịch (year/season/day) mà KHÔNG cần enumerate từ Combat side.
    /// Core sẽ implement dựa trên DataRegistry concrete.
    /// </summary>
    public interface IWaveCalendarResolver
    {
        IReadOnlyList<WaveDef> Resolve(int year, Season season, int day);
    }
}
