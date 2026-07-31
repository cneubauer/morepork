namespace WaaS.Space.DesiredState;

public class CronTab
{
    public string? Command { get; set; }
    public string? MailTo { get; set; }
    public string Schedule { get; set; } = "";
    public string? Comment { get; set; }
}