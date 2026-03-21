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
            RunStartHqResolver.BuildLanes(s, cfg);
            RunStartStorageInitializer.ApplyStartingStorage(s);
            RunStartNpcSpawner.SpawnInitialNpcs(s, cfg, ctx);

            try
            {
                var issues = new List<RunStartValidationIssue>(32);
                RunStartValidator.ValidateRuntime(s, issues);

                if (RunStartValidator.HasErrors(issues))
                {
                    error = RunStartValidator.FormatSummary(issues, maxLines: 10);
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
