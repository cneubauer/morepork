namespace WaaS.Space.Classic.DesiredState;

public static class ToViewModelExtensions
{
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
            : [.. entity.Domains.Select(x => x.ToViewModel<Space.ViewModel.DomainBinding, string>())];

        viewModel.ManagedDomainBindings = entity.HttpAccessDomains.Count == 0 ? null
            : [.. entity.HttpAccessDomains.Select(x => x.ToViewModel<Space.ViewModel.DomainBinding, string>())];

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
}
