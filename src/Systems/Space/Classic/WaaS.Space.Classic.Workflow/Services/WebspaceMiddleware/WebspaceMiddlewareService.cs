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

        desiredState.Data.Space.State = backendModel.State ?? desiredState.Data.Space.State;

        return desiredState;
    }

}
