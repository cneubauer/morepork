using System.ComponentModel.DataAnnotations;
using WaaS.SharedWebspaceManager.ViewModel;
using WaaS.Space.ViewModel;

namespace WaaS.Space.Stretch.ViewModel;

public class Stretchspace : Webspace
{
    /// <summary>
    /// Platform-provided metadata for this stretchspace.
    /// </summary>
    public StretchspaceData Data { get; set; } = new StretchspaceData();

    /// <summary>
    /// Standard user accounts for SFTP/SSH access.
    /// </summary>
    public List<Account>? Accounts { get; set; }

    /// <summary>
    /// Admin-scoped accounts with elevated filesystem access.
    /// </summary>
    public List<AdminAccount>? AdminAccounts { get; set; }

    /// <summary>
    /// Customer domains bound to this stretchspace.
    /// </summary>
    public List<DomainBinding>? Domains { get; set; }

    /// <summary>
    /// Currently a single http access domain is supported only.
    /// </summary>
    [MaxLength(1)]
    public List<DomainBinding>? ManagedDomainBindings { get; set; }

    /// <summary>
    /// Scheduled cron jobs running in this stretchspace.
    /// </summary>
    [MaxLength(100)]
    public List<CronTab>? CronTabs { get; set; }

    /// <summary>
    /// Encoded tokens which allow direct interaction with stretch
    /// </summary>
    [MaxLength(5)]
    public IEnumerable<PlatformAccessToken>? PlatformAccessTokens { get; set; }

    /// <summary>
    /// Environment profiles e.g. for homedir aliases. The name of the profiles must be unique. The environment of Cronjob, Account, AdminAccount and Domain can reference to the environment profile names definde here.
    /// </summary>
    [MinLength(0)]
    [MaxLength(2)]
    public IEnumerable<EnvironmentProfile>? EnvironmentProfiles { get; set; }

    public void Tombstone()
    {
        Accounts = [];
        AdminAccounts = [];
        Domains = [];
        ManagedDomainBindings = [];
        CronTabs = [];
        PlatformAccessTokens = null;
        EnvironmentProfiles = null;
        MailConfiguration = null;
        WebAnalytics = null;
        PlacementTags = [];
        TenantLocks = [];
        Temporary = null;
        BiofilterEnabled = null;
    }
}
