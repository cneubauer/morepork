using WaaS.Persistence;
using WaaS.Space.DesiredState;


namespace WaaS.Space.Stretch.DesiredState;

public class Stretchspace : Space.DesiredState.Space, IWebspace
{
    [LookupKey(LookupResourceKeyType.StretchSpaceId)]
    public ulong StretchspaceId { get; set; }
    
    public string? StretchGlobalId { get; set; }

    public string? StretchSpaceApiEndpoint { get; set; }

    /// <summary>
    /// Environment profiles e.g. for homedir aliases
    /// </summary>
    public List<EnvironmentProfile> EnvironmentProfiles { get; set; } = [];

    public List<PlatformAccessToken>? PlatformAccessTokens { get; set; } = [];

    [LookupKey(LookupResourceKeyType.StretchSpaceHostname)]
    public string WebspacePublicHostname { get; set; } = "";

    public List<Account> Accounts { get; set; } = [];

    /// <summary>
    /// Accounts created and operated using admin-scope.
    /// </summary>
    public List<Account> AdminAccounts { get; set; } = [];

    /// <summary>
    /// A list of customer domains.
    /// We don't manage dns settings for customer domains, the tenant is responsible for it.
    /// </summary>
    public List<DomainBinding<Environment>> Domains { get; set; } = [];

    /// <summary>
    /// A list of http product subdomains mostly pointing to a webshield ip.
    /// </summary>
    public List<DomainBinding<Environment>> HttpAccessDomains { get; set; } = [];

    public List<CronTab> CronTabs { get; set; } = [];

    IEnumerable<Space.DesiredState.Account> IWebspace.Accounts => Accounts;

    public override DateTime? CalculateNextCheckTimestamp()
        => Accounts
            .Select(x => x.CalculateNextCheckTimestamp())
            .Concat(AdminAccounts.Select(x => x.CalculateNextCheckTimestamp()))
            .Append(base.CalculateNextCheckTimestamp())
            .Where(x => x.HasValue)
            .Min();
}