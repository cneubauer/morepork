using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.ViewModel;

public class CronTab
{
    /// <summary>
    /// The command to execute on the cron schedule.
    /// </summary>
    /// <example>php /var/www/html/cron.php</example>
    [Required]
    [StringLength(maximumLength: 1024, MinimumLength = 1)]
    public string? Command { get; set; }

    /// <summary>
    /// The email address to send cron output to.
    /// </summary>
    /// <example>admin@example.com</example>
    [StringLength(maximumLength: 512)]
    [EmailAddress]
    public string? MailTo { get; set; }

    /// <summary>
    /// An optional human-readable description of this cron job.
    /// </summary>
    /// <example>Nightly cache cleanup</example>
    [StringLength(maximumLength: 512)]
    public string? Comment { get; set; }

    /// <summary>
    /// Cron schedule expression. Supports standard 5-field syntax and nicknames: @yearly, @annually, @monthly, @weekly, @daily, @hourly.
    /// </summary>
    /// <example>0 2 * * *</example>
    [Required]
    [StringLength(maximumLength: 128, MinimumLength = 1)]
    [CronExpression]
    public string? Schedule { get; set; }
}