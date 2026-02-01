using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Minimal settings persisted via PlayerPrefs (shipable, easy to expand).
    /// NOTE: This is app-level, not per-run.
    /// </summary>
    public static class AppSettings
    {
        private const string Key_MasterVolume = "SB_MasterVolume";
        private const string Key_DefaultSpeed = "SB_DefaultSpeed"; // 1..3

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(Key_MasterVolume, 1f));
            set
            {
                PlayerPrefs.SetFloat(Key_MasterVolume, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        public static int DefaultSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Key_DefaultSpeed, 1), 1, 3);
            set
            {
                PlayerPrefs.SetInt(Key_DefaultSpeed, Mathf.Clamp(value, 1, 3));
                PlayerPrefs.Save();
            }
        }
    }
}
