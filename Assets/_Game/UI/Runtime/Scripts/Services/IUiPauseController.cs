namespace SeasonalBastion.UI.Services
{
    /// <summary>
    /// Optional: nếu bạn muốn modal pause gameplay (timescale/runclock),
    /// hãy tạo adapter implements interface này.
    /// </summary>
    public interface IUiPauseController
    {
        void PauseUI();
        void ResumeUI();
    }
}