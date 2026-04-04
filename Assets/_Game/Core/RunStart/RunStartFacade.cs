using System;
using System.Collections.Generic;

namespace SeasonalBastion.RunStart
{
    public static class RunStartFacade
    {
        public static bool TryApply(GameServices s, string jsonOrMarkdown, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }

            if (!TryParseAndValidate(s, jsonOrMarkdown, out var cfg, out error))
                return false;

            RunStartRuntimeCacheBuilder.ApplyRuntimeMetadata(s, cfg);

            var ctx = new RunStartBuildContext();
            if (!RunStartWorldBuilder.ApplyWorld(s, cfg, ctx, out error))
                return false;

            RunStartZoneInitializer.ApplyZones(s, cfg);
            s.ResourcePatchService?.RebuildFromZones(s.WorldState?.Zones?.Zones);
            RunStartRuntimeCacheBuilder.ApplyRuntimeZonesFromWorld(s);
            RunStartHqResolver.BuildLanes(s, cfg);
            if (!RunStartStorageInitializer.ApplyStartingStorage(s, out error))
                return false;
            RunStartNpcSpawner.SpawnInitialNpcs(s, cfg, ctx);

            try
            {
                var issues = new List<RunStartValidationIssue>(32);
                RunStartValidator.CollectRuntimeIssues(s, issues);

                if (RunStartValidator.ContainsErrors(issues))
                {
                    error = RunStartValidator.BuildSummary(issues, maxLines: 10);
                    return false;
                }
            }
            catch (Exception e)
            {
                error = "RunStartValidator exception: " + e.Message;
                return false;
            }

            return true;
        }

        public static bool TryRebuildRuntimeCaches(GameServices s, string jsonOrMarkdown, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }

            if (!TryParseAndValidate(s, jsonOrMarkdown, out var cfg, out error))
                return false;

            RunStartRuntimeCacheBuilder.ApplyRuntimeMetadata(s, cfg);
            RunStartRuntimeCacheBuilder.ApplyRuntimeZonesFromWorld(s);
            RunStartHqResolver.BuildLanes(s, cfg);
            return true;
        }

        private static bool TryParseAndValidate(GameServices s, string jsonOrMarkdown, out StartMapConfigDto cfg, out string error)
        {
            cfg = null;
            error = null;

            if (!RunStartInputParser.TryParseConfig(jsonOrMarkdown, out cfg, out error))
                return false;

            if (!RunStartConfigValidator.ValidateConfig(s, cfg, out error))
                return false;

            return true;
        }
    }
}
