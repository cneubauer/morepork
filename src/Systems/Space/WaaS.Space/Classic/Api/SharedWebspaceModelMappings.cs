using WaaS.Space.Classic.DesiredState;
using WaaS.Space.DesiredState;
using WaaS.Space.Api;

namespace WaaS.Space.Classic;

public static class DesiredStateExtensions
{
    public static void Apply(this IDesiredState<SharedWebspaceData> desiredState, ViewModel.SharedWebspace viewModel)
    {
        var webspace = desiredState.Data.Webspace;

        if (viewModel.Data.Platform is { } platform)
            webspace.Platform = (Platform)platform;

        webspace.MailConfiguration = viewModel.MailConfiguration is null
            ? null
            : new MailConfiguration
            {
                Host = viewModel.MailConfiguration.Host,
                Hostport = viewModel.MailConfiguration.HostPort ?? 0,
                Username = viewModel.MailConfiguration.Username ?? "",
                SecurePasswordToken = viewModel.MailConfiguration.PasswordToken ?? "",
                DefaultEnvelopeFromPolicy = viewModel.MailConfiguration.DefaultEnvelopeFromPolicy,
                DefaultSender = viewModel.MailConfiguration.DefaultSender,
            };

        foreach (var domainViewModel in viewModel.Domains ?? [])
        {
            var existingDomain = webspace.Domains.FirstOrDefault(x => x.DomainName == domainViewModel.Domain);

            if (existingDomain is not null)
                existingDomain.Apply(domainViewModel);
            else
                webspace.Domains.Add(new()
                {
                    DomainName = domainViewModel.Domain,
                    Environment = domainViewModel.Environment,
                });
        }

        foreach (var account in viewModel.Accounts ?? [])
        {
            var existingAccount = webspace.Accounts.FirstOrDefault(x => x.Username == account.Username);

            if (existingAccount is not null)
                existingAccount.Apply(account);
            else
                webspace.Accounts.Add(new()
                {
                    ReferenceId = Guid.NewGuid().ToString(),
                    ExtReference = account.ExtReference,
                    SecurePasswordToken = account.PasswordToken ?? "",
                });
        }

        var domains = viewModel.Domains?.Select(x => x.Domain) ?? [];
        webspace.Domains.RemoveAll(x => !domains.Contains(x.DomainName));
    }

    private static void Apply(this DomainBinding<string> desiredState, ViewModel.DomainBinding viewModel)
    {
        desiredState.Environment = viewModel.Environment;
        // TODO: Apply other properties from the view model to the desired state as needed.
    }

    private static void Apply(this Account desiredState, Space.ViewModel.Account viewModel)
    {
        desiredState.ExtReference = viewModel.ExtReference;
        desiredState.SecurePasswordToken = viewModel.PasswordToken ?? "";
        // TODO: Apply other properties from the view model to the desired state as needed.
    }
    public static ViewModel.SharedWebspace ToViewModel(this SharedWebspace entity, ulong systemInstanceId)
    {
        var viewModel = entity.ToViewModel<ViewModel.SharedWebspace>(systemInstanceId);

        viewModel.Data.WebspaceId = entity.WebspaceId;
        viewModel.Data.Hostname = entity.Hostname;
        viewModel.Data.Ipv4 = entity.IpSet?.IPv4;
        viewModel.Data.Ipv6 = entity.IpSet?.IPv6;
        viewModel.Data.RegionName = entity.Region;
        viewModel.Data.PlacementTags = [.. entity.PlacementTagsActual];
        viewModel.Data.Limits = new()
        {
            DiskQuotaInBytes = entity.Limits.DiskQuotaActual
        };
        viewModel.Data.Platform = (Space.ViewModel.PlatformType)entity.Platform;

        viewModel.Accounts = entity.Accounts.Count == 0 ? null
            : [.. entity.Accounts.Select(x => x.ToViewModel<Space.ViewModel.Account>())];

        viewModel.AdminAccounts = entity.AdminAccounts.Count == 0 ? null
            : [.. entity.AdminAccounts.Select(x => x.ToViewModel<Space.ViewModel.AdminAccount>())];

        viewModel.Domains = entity.Domains.Count == 0 ? null
            : [.. entity.Domains.Select(x => x.ToViewModel())];

        viewModel.ManagedDomainBindings = entity.HttpAccessDomains.Count == 0 ? null
            : [.. entity.HttpAccessDomains.Select(x => x.ToViewModel())];

        viewModel.CronTabs = entity.CronTabs.Count == 0 ? null
            : [.. entity.CronTabs.Select(x => new Space.ViewModel.CronTab
            {
                Command = x.Command,
                Schedule = x.Schedule,
                MailTo = x.MailTo,
                Comment = x.Comment,
            })];

        return viewModel;
    }

    private static ViewModel.DomainBinding ToViewModel(this DomainBinding<string> desiredState)
        => new()
        {
            Domain = desiredState.DomainName,
            Environment = desiredState.Environment,
            TargetPath = new()
            {
                Path = desiredState.TargetPath.DirPath,
                Type = (ViewModel.DirectoryType)desiredState.TargetPath.DirType,
            }
        };
}
