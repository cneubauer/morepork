namespace WaaS.WebApi;

public static class DesiredStateExtensions
{
    public static void Apply(this SharedWebspace desiredState, Space.Classic.ViewModel.SharedWebspace viewModel)
    {
        if (viewModel.MailConfiguration is null)
            return;

        desiredState.MailConfiguration = new Space.DesiredState.MailConfiguration
        {
            Host = viewModel.MailConfiguration.Host,
            Hostport = viewModel.MailConfiguration.HostPort ?? 0,
            Username = viewModel.MailConfiguration.Username ?? "",
        };
    }
}