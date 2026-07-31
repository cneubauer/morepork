namespace WaaS.Space.Stretch.ViewModel;

public class CronTab : Space.ViewModel.CronTab
{
    /// <summary>
    /// The container environment used when executing this cron job.
    /// </summary>
    public Environment? Environment { get; set; }
}