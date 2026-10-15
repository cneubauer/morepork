using WaaS.Common.ViewModel;
using WaaS.Space.ViewModel;

namespace WaaS.Space.Classic.ViewModel;

public class SharedWebspace : Space.ViewModel.Space, IPasswordContainer
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

    public IEnumerable<PasswordInfo> GetPasswordInfos()
    {
        var passwordInfos = Enumerable.Empty<Credential>()
            .Concat(Accounts ?? [])
            .Concat(AdminAccounts ?? [])
            .Where(x => x.Password is not null)
            .Select(x => new PasswordInfo(x, PasswordType.SharedWebspaceLinux));
        
        if (MailConfiguration?.Password is not null)
            passwordInfos = passwordInfos.Append(new PasswordInfo(MailConfiguration, PasswordType.Smtp));

        if (WebAnalytics?.Password is not null)
            passwordInfos = passwordInfos.Append(new PasswordInfo(WebAnalytics, PasswordType.WebAnalytics));

        return [.. passwordInfos];
    }

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