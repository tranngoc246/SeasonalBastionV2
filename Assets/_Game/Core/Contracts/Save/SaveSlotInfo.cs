using System;

namespace SeasonalBastion.Contracts
{
    [Serializable]
    public sealed class SaveSlotInfo
    {
        public int Slot;
        public string FileName;
        public bool IsAutosave;
        public bool IsLegacy;
        public bool IsBackup;
        public bool IsValid;
        public string DisplayName;
        public string Season;
        public int DayIndex;
        public int YearIndex;
        public int WaveIndex;
        public string TimestampUtc;
        public string Error;
    }
}
