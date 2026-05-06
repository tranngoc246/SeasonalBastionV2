using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed partial class SaveService
    {
        public int GetLatestValidSlot()
        {
            var saves = ListRunSaves();
            int bestSlot = 0;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < saves.Count; i++)
            {
                var s = saves[i];
                if (s == null || !s.IsValid || s.IsAutosave || s.IsLegacy || s.Slot <= 0)
                    continue;

                if (!DateTime.TryParse(s.TimestampUtc, null, DateTimeStyles.RoundtripKind, out var t))
                    t = DateTime.MinValue;

                if (t >= bestTime)
                {
                    bestTime = t;
                    bestSlot = s.Slot;
                }
            }

            return bestSlot;
        }

        public IReadOnlyList<SaveSlotInfo> ListRunSaves()
        {
            var list = new List<SaveSlotInfo>();
            for (int i = 1; i <= 3; i++)
                list.Add(ReadSlotInfo(GetSlotPath(i), i, isAutosave: false, isLegacy: false, isBackup: false));

            list.Add(ReadSlotInfo(GetAutosavePath(), 0, isAutosave: true, isLegacy: false, isBackup: false));
            if (File.Exists(RunPath))
                list.Add(ReadSlotInfo(RunPath, 0, isAutosave: false, isLegacy: true, isBackup: false));

            return list;
        }

        private SaveSlotInfo ReadSlotInfo(string path, int slot, bool isAutosave, bool isLegacy, bool isBackup)
        {
            var info = new SaveSlotInfo
            {
                Slot = slot,
                FileName = Path.GetFileName(path),
                IsAutosave = isAutosave,
                IsLegacy = isLegacy,
                IsBackup = isBackup,
                DisplayName = isAutosave ? "Autosave" : (isLegacy ? "Legacy Continue" : $"Save Slot {slot}"),
                IsValid = false,
                Season = "-",
                DayIndex = 0,
                YearIndex = 0,
                WaveIndex = 0,
                TimestampUtc = string.Empty,
                Error = string.Empty
            };

            if (!File.Exists(path))
            {
                info.Error = "Empty";
                return info;
            }

            try
            {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<RunSaveFile>(json);
                if (file == null)
                {
                    info.Error = "Invalid json";
                    return info;
                }

                info.IsValid = true;
                info.Season = file.season;
                info.DayIndex = file.dayIndex;
                info.YearIndex = file.yearIndex;
                info.WaveIndex = file.combat != null ? file.combat.currentWaveIndex : 0;
                info.TimestampUtc = file.timestampUtc ?? string.Empty;
                return info;
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
                return info;
            }
        }
    }
}
