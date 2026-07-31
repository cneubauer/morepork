using WaaS.Space.ViewModel;

namespace WaaS.Space.Classic.ViewModel;

public class SharedWebspace : Space.ViewModel.Space
{
    /// <summary>
    /// Platform-provided metadata for this shared webspace.
    /// </summary>
    public SharedWebspaceData Data { get; set; } = new SharedWebspaceData();

    /// <summary>
    /// Tenant user accounts.
    /// </summary>
    public List<Account>? Accounts { get; set; }

    /// <summary>
    /// Admin-scoped accounts.
    /// </summary>
    public List<AdminAccount>? AdminAccounts { get; set; }

    /// <summary>
    /// Customer domain bindings.
    /// </summary>
    public List<DomainBinding>? Domains { get; set; }

    /// <summary>
    /// System-managed HTTP access domains.
    /// </summary>
    public List<DomainBinding>? ManagedDomainBindings { get; set; }

    /// <summary>
    /// Scheduled cron jobs.
    /// </summary>
    public List<CronTab>? CronTabs { get; set; }

    public void Tombstone()
    {
        Accounts = [];
        AdminAccounts = [];
        Domains = [];
        ManagedDomainBindings = [];
        CronTabs = [];
        MailConfiguration = null;
        WebAnalytics = null;
        PlacementTags = [];
        TenantLocks = [];
        Temporary = null;
        BiofilterEnabled = null;
    }
}