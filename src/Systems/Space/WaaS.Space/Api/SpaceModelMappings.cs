using WaaS.Common.ViewModel;
using WaaS.Space.DesiredState;
using WaaS.Space.ViewModel;

namespace WaaS.Space.Api;

public static class SpaceModelMappings
{
    public static T ToViewModel<T>(this DesiredState.Space entity, ulong? systemInstanceId)
        where T : ViewModel.Space, new()
        => new()
        {
            SystemInstanceId = systemInstanceId,

            Limits = entity.Limits!.ToViewModel(),
            WebAnalytics = entity.WebAnalytics?.ToViewModel(),
            Owner = entity.Owner.ToViewModel(),
            Temporary = entity.Temporary?.ToViewModel(),
            MailConfiguration = entity.MailConfiguration?.ToViewModel(),
            BiofilterEnabled = entity.BiofilterEnabled,
            TenantLocks = entity.LockItems.ToViewModel(Common.DesiredState.LockItemType.Tenant),
            AdminLocks = entity.LockItems.ToViewModel(Common.DesiredState.LockItemType.Admin),
            TechnicalLocks = entity.LockItems.ToViewModel(Common.DesiredState.LockItemType.Technical),
            PlacementTags = entity.PlacementTags,
            ActualState = new()
            {
                PlacementTags = entity.PlacementTagsActual,
                Limits = new()
                {
                    DiskQuota = entity.Limits.DiskQuotaActual,
                },
            },
            AdminPlacementTags = entity.PlacementTagsAdmin,
            DataAccessDomains = entity.DataAccessDomains?
                .Select(x => new DataAccess
                {
                    Domain = x.DomainName,
                })
                .ToList() ?? [],
        };

    public static T ToViewModel<T>(this DesiredState.Account entity) where T : ViewModel.Account, new()
        => new()
        {
            AccountId = entity.AccountId == 0 ? null : entity.AccountId,
            Username = entity.Username,
            PasswordToken = entity.SecurePasswordToken,
            Id = entity.ReferenceId,
            ExtReference = entity.ExtReference,
            AccessTypes = entity.AccessType
                .Split(",")
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Aggregate((SpaceAccessType)0, (acc, x) =>
                    Enum.TryParse<SpaceAccessType>(x, ignoreCase: true, out var val) ? acc | val : acc),
            Temporary = entity.Temporary?.ToViewModel(),
            SshPublicKeys = entity.SshPublicKeys is null || entity.SshPublicKeys.Count == 0 ? null :
                entity.SshPublicKeys
                    .Select(x => x.ToViewModel())
                    .ToList(),
            TenantLocks = entity.LockItems.ToViewModel(Common.DesiredState.LockItemType.Tenant),
            HomeDirPubKeys = entity.HomeDirPubKeys,
            AccountType = entity.AccountType ?? "",
        };

    public static T ToViewModel<T, U>(this DomainBinding<U> entity)
        where T : DomainBinding, new()
        => new()
        {
            Domain = entity.DomainName,
            DomainId = entity.DomainId,
            TenantLocks = entity.LockItems.ToViewModel(Common.DesiredState.LockItemType.Tenant),
        };

    public static ViewModel.SshPublicKey ToViewModel(this Space.DesiredState.SshPublicKey entity)
        => new()
        {
            Data = entity.Data,
            KeyType = entity.KeyType,
            Options = entity.Options?.Select(sshOption => new ViewModel.SshPublicKeyOption
            {
                KeyType = sshOption.Key,
                Value = sshOption.Value,
            }).ToList(),
        };

    public static TemporaryInfo ToViewModel(this Common.DesiredState.ExpirationInfo entity)
        => new() { ExpireAt = entity.Expires };

    public static ViewModel.Limits ToViewModel(this DesiredState.Limits entity)
        => new()
        {
            DiskQuota = entity.DiskQuota,
            ResourceLevel = (ViewModel.ResourceLevel)entity.ResourceLevel,
            AutoQuota = entity.AutoQuota?.ToViewModel(),
        };

    public static ViewModel.AutoQuotaInfo ToViewModel(this DesiredState.AutoQuotaInfo entity)
        => new()
        {
            DiskQuotaProfile = entity.DiskQuotaProfile ?? "",
            MinDiskQuota = entity.MinDiskQuota,
            MaxDiskQuota = entity.MaxDiskQuota,
            NextEvalNotBefore = entity.NextEvalNotBefore,
        };

    public static ViewModel.WebAnalytics ToViewModel(this DesiredState.WebAnalytics entity)
        => new()
        {
            Id = entity.WebAnalyticsId,
            PasswordToken = entity.SecurePasswordToken,
        };

    public static ViewModel.SpaceOwner ToViewModel(this Owner entity)
        => new()
        {
            Uid = entity.Uid,
            Gid = entity.Gid,
            Username = entity.Username,
            Groupname = entity.Groupname,
        };

    public static ViewModel.MailConfiguration ToViewModel(this DesiredState.MailConfiguration entity)
        => new()
        {
            Host = entity.Host,
            HostPort = entity.Hostport,
            Username = entity.Username,
            PasswordToken = entity.SecurePasswordToken,
            DefaultSender = entity.DefaultSender,
            DefaultEnvelopeFromPolicy = entity.DefaultEnvelopeFromPolicy ?? "auto",
        };

    public static List<LockInfo> ToViewModel(this IEnumerable<Common.DesiredState.LockItem>? locks, Common.DesiredState.LockItemType lockType)
        => locks?
            .Where(x => x.LockType == lockType)
            .Select(ToViewModel)
            .OrderBy(x => x.Created)
            .ToList()
            ?? [];

    public static LockInfo ToViewModel(this Common.DesiredState.LockItem entity)
        => new()
        {
            Id = entity.Id,
            Reason = entity.Reason,
            Responsible = entity.Responsible,
            Created = entity.Created,
            CreatedBy = entity.CreatedBy,
            Modified = entity.Modified,
            ModifiedBy = entity.ModifiedBy,
            Category = (LockCategory)entity.Category,
            CategoryProperties = entity.CategoryProperties,
        };
}
