using System;
using System.Collections.Generic;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartFacade
    {
        internal static bool TryApply(GameServices s, string jsonOrMarkdown, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }

            if (!RunStartInputParser.TryParseConfig(jsonOrMarkdown, out var cfg, out error))
                return false;

            if (!RunStartConfigValidator.ValidateConfig(s, cfg, out error))
                return false;

            RunStartRuntimeCacheBuilder.ApplyRuntimeMetadata(s, cfg);

            var ctx = new RunStartBuildContext();
            if (!RunStartWorldBuilder.ApplyWorld(s, cfg, ctx, out error))
                return false;

            RunStartZoneInitializer.ApplyZones(s, cfg);
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
    }
}
