using System;
using System.Collections.Generic;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartFacade
    {
        public static bool TryApply(GameServices s, string jsonOrMarkdown, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }

            if (!RunStartInputParser.TryParse(jsonOrMarkdown, out var cfg, out error))
                return false;

            if (!RunStartConfigValidator.Validate(s, cfg, out error))
                return false;

            RunStartRuntimeCacheBuilder.Apply(s, cfg);

            var ctx = new RunStartBuildContext();
            if (!RunStartWorldBuilder.Apply(s, cfg, ctx, out error))
                return false;

            RunStartZoneInitializer.Apply(s, cfg);
            RunStartHqResolver.BuildLaneRuntime(s, cfg);
            RunStartStorageInitializer.Apply(s);
            RunStartNpcSpawner.Apply(s, cfg, ctx);

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
