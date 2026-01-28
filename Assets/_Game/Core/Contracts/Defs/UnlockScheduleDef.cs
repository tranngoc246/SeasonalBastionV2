using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    [Serializable]
    public sealed class UnlockScheduleDef
    {
        public int SchemaVersion = 1;
        public List<string> StartUnlocked = new();
        public List<UnlockEntryDef> Entries = new();
    }

    [Serializable]
    public sealed class UnlockEntryDef
    {
        public string DefId = "";
        public int Year = 1;
        public Season Season = Season.Spring;
        public int Day = 1;
    }
}
