namespace SeasonalBastion.RunStart
{
    /// <summary>
    /// Backward-compatible entrypoint. The implementation now lives in the split RunStart helpers/facade.
    /// </summary>
    public static class RunStartApplier
    {
        public static bool TryApply(GameServices s, string jsonOrMarkdown, out string error)
        {
            return RunStartFacade.TryApply(s, jsonOrMarkdown, out error);
        }
    }
}
