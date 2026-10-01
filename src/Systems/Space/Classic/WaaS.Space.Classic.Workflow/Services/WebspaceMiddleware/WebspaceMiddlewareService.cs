using WebspaceMiddleware;

namespace WaaS.Space.Classic.Workflow;

public class WebspaceMiddlewareService(HttpClient httpClient)
    : SpaceMiddlewareService<SharedWebspaceData, Webspace>(httpClient)
{
    protected override string ResourcePath => "webspaces";

    protected override ulong BackendId(IDesiredState<SharedWebspaceData> desiredState) => desiredState.Data.Space.WebspaceId;

    protected override Webspace BuildBackendModel(IDesiredState<SharedWebspaceData> desiredState, string extCorrelationId, string[]? tags)
        => desiredState.ToBackendModel(extCorrelationId, tags);

    public override IDesiredState<SharedWebspaceData> ApplyBackendResponse(IDesiredState<SharedWebspaceData> desiredState, Webspace backendModel)
    {
        desiredState.Data.Space.Region = backendModel.Region;
        desiredState.Data.Space.Hostname = backendModel.Hostname;
        desiredState.Data.Space.WebspaceId = backendModel.Id ?? 0;
        desiredState.Data.Space.IpSet = new Space.DesiredState.IpSet
        {
            IPv4 = backendModel.IPv4,
            IPv6 = backendModel.IPv6,
        };

        if (backendModel.Owner is not null)
        {
            desiredState.Data.Space.Owner = new Space.DesiredState.Owner
            {
                Uid = backendModel.Owner.Uid ?? 0,
                Gid = backendModel.Owner.Gid ?? 0,
                Username = backendModel.Owner.Username ?? "",
                Groupname = backendModel.Owner.Groupname ?? "",
            };
        }

        foreach (var backendAccount in backendModel.Accounts ?? [])
        {
            var account = desiredState.Data.Space.Accounts
                .Concat(desiredState.Data.Space.AdminAccounts)
                .FirstOrDefault(account => account.ReferenceId == backendAccount.ExternalReference);

            if (account is null)
                continue;

            account.AccountId = backendAccount.Id;
            account.Username = backendAccount.Username ?? account.Username;
        }

        desiredState.Data.Space.State = backendModel.State ?? desiredState.Data.Space.State;

        return desiredState;
    }

}
