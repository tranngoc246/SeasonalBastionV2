using System;
using UnityEngine;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartInputParser
    {
        public static bool TryParse(string jsonOrMarkdown, out StartMapConfigRootDto cfg, out string error)
        {
            cfg = null;
            error = null;

            string json = ExtractJsonIfMarkdown(jsonOrMarkdown);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "StartMapConfig json is empty";
                return false;
            }

            try
            {
                cfg = JsonUtility.FromJson<StartMapConfigRootDto>(json);
            }
            catch (Exception ex)
            {
                error = "Parse StartMapConfig failed (JsonUtility): " + ex.Message;
                return false;
            }

            if (cfg == null || cfg.map == null)
            {
                error = "StartMapConfig missing map";
                return false;
            }

            return true;
        }

        internal static string ExtractJsonIfMarkdown(string jsonOrMd)
        {
            if (string.IsNullOrEmpty(jsonOrMd)) return jsonOrMd;

            for (int i = 0; i < jsonOrMd.Length; i++)
            {
                char ch = jsonOrMd[i];
                if (char.IsWhiteSpace(ch)) continue;
                if (ch == '{') return jsonOrMd;
                break;
            }

            int fence = jsonOrMd.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (fence < 0) return jsonOrMd;

            int start = jsonOrMd.IndexOf('\n', fence);
            if (start < 0) return jsonOrMd;
            start++;

            int end = jsonOrMd.IndexOf("```", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return jsonOrMd;

            return jsonOrMd.Substring(start, end - start);
        }
    }
}
