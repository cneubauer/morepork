namespace WaaS.WebApi;

public static class DesiredStateExtensions
{
    public static void Apply(this SharedWebspace desiredState, Space.Classic.ViewModel.SharedWebspace viewModel)
    {
        desiredState.MailConfiguration = viewModel.MailConfiguration is null
            ? null
            : new Space.DesiredState.MailConfiguration
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
            var existingDomain = desiredState.Domains.FirstOrDefault(x => x.DomainName == domainViewModel.Domain);

            if (existingDomain is not null)
                existingDomain.Apply(domainViewModel);
            else
                desiredState.Domains.Add(new()
                {
                    DomainName = domainViewModel.Domain,
                    Environment = domainViewModel.Environment,
                });
        }

        foreach (var account in viewModel.Accounts ?? [])
        {
            var existingAccount = desiredState.Accounts.FirstOrDefault(x => x.Username == account.Username);

            if (existingAccount is not null)
                existingAccount.Apply(account);
            else
                desiredState.Accounts.Add(new()
                {
                    // ExtReference is the [ItemKey] for Account: ObjectComparer skips keyed list
                    // items whose key is null, so it has to be set for an account to be diffable.
                    ExtReference = account.ExtReference,
                    SecurePasswordToken = account.PasswordToken ?? "",
                });
        }

        var domains = viewModel.Domains?.Select(x => x.Domain) ?? [];
        desiredState.Domains.RemoveAll(x => !domains.Contains(x.DomainName));
    }

    private static void Apply(this DomainBinding<string> desiredState, Space.Classic.ViewModel.DomainBinding viewModel)
    {
        desiredState.Environment = viewModel.Environment;
        // TODO: Apply other properties from the view model to the desired state as needed.
    }

    private static void Apply(this Space.DesiredState.Account desiredState, Space.ViewModel.Account viewModel)
    {
        desiredState.ExtReference = viewModel.ExtReference;
        desiredState.SecurePasswordToken = viewModel.PasswordToken ?? "";
        // TODO: Apply other properties from the view model to the desired state as needed.
    }
}