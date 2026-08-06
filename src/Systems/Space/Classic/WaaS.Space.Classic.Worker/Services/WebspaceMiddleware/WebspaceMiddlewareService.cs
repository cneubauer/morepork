using WaaS.Space.Worker;

namespace WaaS.Space.Classic.Worker;

public class WebspaceMiddlewareService(HttpClient httpClient)
    : SpaceMiddlewareService<SharedWebspaceData, WebspaceMiddleware.Webspace>(httpClient)
{
    protected override string ResourcePath => "webspaces";

    protected override ulong BackendId(IDesiredState<SharedWebspaceData> desiredState) => desiredState.Data.Space.WebspaceId;

    protected override WebspaceMiddleware.Webspace BuildTechModel(IDesiredState<SharedWebspaceData> desiredState, string extCorrelationId, string[]? tags)
        => desiredState.ToTechModel(extCorrelationId, tags);
}
