using System;
using System.IO;
using UnityEngine;

namespace SeasonalBastion
{
    public static class QaInternalCiRunner
    {
        [Serializable]
        public struct Report
        {
            public string schema;
            public string utc;
            public string unity;
            public string appVersion;
            public string platform;

            public bool passed;
            public string summary;

            public string reportPath;
        }

        /// <summary>
        /// One-click internal CI: run B (Save/Load matrix 8 checkpoints).
        /// Returns report (also writes to disk if writeReport=true).
        /// </summary>
        public static Report RunB(GameServices s, bool writeReport)
        {
            var r = new Report
            {
                schema = "qa_internal_ci_v0.1",
                utc = DateTime.UtcNow.ToString("o"),
                unity = Application.unityVersion,
                appVersion = Application.version,
                platform = Application.platform.ToString(),
                passed = false,
                summary = "",
                reportPath = ""
            };

            if (s == null)
            {
                r.summary = "FAIL: GameServices null";
                return r;
            }

            bool ok = QaSaveLoadScenario8.Run(s, out var sum);
            r.passed = ok;
            r.summary = sum ?? (ok ? "PASS" : "FAIL");

            if (writeReport)
            {
                r.reportPath = TryWriteReportJson(r);
            }

            // Always log a single-liner for quick scanning
            Debug.Log($"[QA-CI] B SaveLoadMatrix => {(r.passed ? "PASS" : "FAIL")} | {r.summary}");
            if (!string.IsNullOrEmpty(r.reportPath))
                Debug.Log($"[QA-CI] Report: {r.reportPath}");

            return r;
        }

        private static string TryWriteReportJson(Report r)
        {
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "qa_reports");
                Directory.CreateDirectory(dir);

                var file = $"qa_ci_B_{DateTime.UtcNow:yyyyMMdd_HHmmss}_UTC.json";
                var path = Path.Combine(dir, file);

                var json = JsonUtility.ToJson(r, prettyPrint: true);
                File.WriteAllText(path, json);

                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[QA-CI] Failed to write report: " + e.Message);
                return "";
            }
        }
    }
}