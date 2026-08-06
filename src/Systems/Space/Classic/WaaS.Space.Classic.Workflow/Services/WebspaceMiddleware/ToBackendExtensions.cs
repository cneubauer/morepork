using WaaS.Space.Workflow;

namespace WaaS.Space.Classic.Workflow;

public static class ToBackendExtensions
{
    public static WebspaceMiddleware.Webspace ToBackendModel(this IDesiredState<SharedWebspaceData> desiredState, string extCorrelationId, string[]? stackInstanceTags)
    {
        var spaceData = desiredState.Data.Space;

        var techWebspace = new WebspaceMiddleware.Webspace
        {
            ExternalReference = $"{desiredState.StackInstanceId}-{desiredState.SystemInstanceId}-{desiredState.Namespace}-{desiredState.Zone}",
            ExternalCorrelation = extCorrelationId,
            Id = spaceData.WebspaceId,
            Region = spaceData.Region,
            State = spaceData.LockItems.AsStatus(),
            Accounts = [],
            Domains = [],
            BiofilterEnabled = spaceData.BiofilterEnabled,
            Tags = stackInstanceTags,
        };

        var domains = new List<SpaceMiddleware.DomainBinding>();

        var customerDomains = spaceData.Domains?
            .Where(x => x.IsEnabled ?? true)
            .Select(x => x.ToBackendModel());

        if (customerDomains != null)
            domains.AddRange(customerDomains);

        var httpAccess = spaceData.HttpAccessDomains?
            .FirstOrDefault()?
            .ToBackendModel("http-access-domain-correlation-id");

        if (httpAccess != null)
            domains.Add(httpAccess);

        if (domains.Count != 0)
            techWebspace.Domains = domains;

        var allAccounts = new List<SpaceMiddleware.Account>();

        if (spaceData.Accounts != null && spaceData.Accounts.Count != 0)
            allAccounts.AddRange(spaceData.Accounts.Select(account => account.ToBackendModel()));

        if (spaceData.AdminAccounts != null && spaceData.AdminAccounts.Count != 0)
            allAccounts.AddRange(spaceData.AdminAccounts.Select(account => account.ToBackendModel()));

        techWebspace.Accounts = allAccounts;

        if (spaceData.Limits != null)
        {
            _ = Enum.TryParse(spaceData.Limits.ResourceLevel, out SpaceMiddleware.ResourceLevelType resourceLevel);

            techWebspace.Limits = new SpaceMiddleware.SpaceLimits
            {
                DiskQuota = $"{spaceData.Limits.DiskQuota}b",
                ResourceLevel = resourceLevel.ToString(),
            };
        }

        if (spaceData.Owner != null)
            techWebspace.Owner = new SpaceMiddleware.SpaceOwner
            {
                Uid = spaceData.Owner.Uid,
                Gid = spaceData.Owner.Gid,
                Username = spaceData.Owner.Username,
                Groupname = spaceData.Owner.Groupname,
            };

        if (spaceData.MailConfiguration != null)
        {
            techWebspace.MailConfiguration = new SpaceMiddleware.MailConfig
            {
                Hostname = spaceData.MailConfiguration.Host,
                Port = (int)spaceData.MailConfiguration.Hostport,
                DefaultSender = spaceData.MailConfiguration.DefaultSender,
                DefaultEnvelopeFromPolicy = spaceData.MailConfiguration.DefaultEnvelopeFromPolicy,
                Username = spaceData.MailConfiguration.Username,
            };

            if (spaceData.MailConfiguration.SecurePasswordToken != null)
                techWebspace.MailConfiguration.Credentials = new SpaceMiddleware.Credential
                {
                    PasswordToken = spaceData.MailConfiguration.SecurePasswordToken,
                };
        }

        // take admin placement tags first, tenant placement tags last
        if (spaceData.PlacementTagsAdmin != null && spaceData.PlacementTagsAdmin.Any())
            techWebspace.PlacementTags = spaceData.PlacementTagsAdmin.ToList();
        else if (spaceData.PlacementTags != null && spaceData.PlacementTags.Any())
            techWebspace.PlacementTags = spaceData.PlacementTags.ToList();

        if (spaceData.CronTabs != null && spaceData.CronTabs.Any())
        {
            techWebspace.Crontab = spaceData.CronTabs.Select(x => new SpaceMiddleware.Crontab
            {
                Command = x.Command,
                MailTo = string.IsNullOrEmpty(x.MailTo) ? null : x.MailTo,
                Schedule = x.Schedule,
                Comment = x.Comment,
            }).ToList();
        }

        if (spaceData.WebAnalytics != null)
        {
            techWebspace.WebAnalytics = new SpaceMiddleware.WebAnalytics
            {
                WaId = spaceData.WebAnalytics.WebAnalyticsId,
            };
            if (!string.IsNullOrEmpty(spaceData.WebAnalytics.SecurePasswordToken))
            {
                techWebspace.WebAnalytics.Credentials = new SpaceMiddleware.Credential
                {
                    PasswordToken = spaceData.WebAnalytics.SecurePasswordToken,
                };
            }
        }

        return techWebspace;
    }

    public static SpaceMiddleware.DomainBinding ToBackendModel(this Space.DesiredState.DomainBinding<string> domain, string extCorrelationId = "0")
        => new()
        {
            ExternalReference = domain.ReferenceId,
            Id = domain.DomainId > 0 ? domain.DomainId : null, // DomainId = 0 should never happen, but use this check as a safety mechanism
            ExternalCorrelation = domain.CorrelationId ?? extCorrelationId,
            DomainName = domain.DomainName,
            ConnectType = SpaceMiddleware.ConnectionType.Docroot.ToString().ToLower(),
            State = domain.LockItems.AsStatus(),
            DocRoot = new SpaceMiddleware.DocRoot
            {
                Path = domain.TargetPath?.DirPath,
                Type = domain.TargetPath?.DirType.ToString().ToLower()
            }
        };

    public static SpaceMiddleware.Account ToBackendModel(this Space.DesiredState.Account account)
        => new()
        {
            ExternalReference = account.ReferenceId,
            ExternalCorrelation = account.CorrelationId ?? "0", // Should use new GUID instead
            Id = account.AccountId,
            Username = string.IsNullOrEmpty(account.Username) ? null : account.Username,
            State = account.ForceEnabled && account.State == "enabled"
                ? "forcedEnabled"
                : account.LockItems.AsStatus(),
            AccessTypes = account.AccessType
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.ToLower().Trim()),
            Target = new SpaceMiddleware.FileSystemTarget
            {
                Path = account.TargetPath?.DirPath,
                Type = account.TargetPath?.DirType.ToString().ToLower(),
            },
            Credentials = new SpaceMiddleware.Credential
            {
                PasswordToken = account.SecurePasswordToken,
                PublicKeys = account.SshPublicKeys?.Select(x => new SpaceMiddleware.PublicKey
                {
                    PubKey = x.Data,
                    KeyType = x.KeyType,
                    Options = x.Options?.Select(sshOptions => new SpaceMiddleware.PublicKeyOption
                    {
                        Key = sshOptions.Key,
                        Value = sshOptions.Value,
                    }).ToList()
                })
            },
            HomeDirPubKeys = account.HomeDirPubKeys,
            AccountType = account.AccountType,
        };
}
