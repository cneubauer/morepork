using System.Net.Http.Json;

namespace WaaS.Space.Workflow;

public abstract class SpaceMiddlewareService<TDesiredState, TBackendModel>(HttpClient httpClient)
: ISpaceMiddlewareService<TDesiredState, TBackendModel>
where TDesiredState : IDesiredStateData
{
    protected abstract string ResourcePath { get; }

    protected abstract ulong BackendId(IDesiredState<TDesiredState> desiredState);

    protected abstract TBackendModel BuildBackendModel(IDesiredState<TDesiredState> desiredState, string extCorrelationId, string[]? tags);

    public abstract IDesiredState<TDesiredState> ApplyBackendResponse(IDesiredState<TDesiredState> desiredState, TBackendModel backendModel);

    public async Task<IDesiredState<TDesiredState>> Publish(string tenant, IStackInstance stackInstance, IDesiredState<TDesiredState> desiredState, string extCorrelationId)
    {
        var id = BackendId(desiredState);

        if (desiredState.Tombstoned)
        {
            var delelteResponse = await httpClient.DeleteAsync($"{tenant}/{ResourcePath}/{id}");
            delelteResponse.EnsureSuccessStatusCode();

            return desiredState;
        }

        var techModel = BuildBackendModel(desiredState, extCorrelationId, stackInstance.Tags);

        var response = id == 0
            ? await httpClient.PostAsJsonAsync($"{tenant}/{ResourcePath}", techModel)
            : await httpClient.PutAsJsonAsync($"{tenant}/{ResourcePath}/{id}", techModel);

        response.EnsureSuccessStatusCode();

        var backendModel = await response.Content.ReadFromJsonAsync<TBackendModel>()
            ?? throw new InvalidOperationException($"Backend response for {ResourcePath} was null");

        ApplyBackendResponse(desiredState, backendModel);

        return desiredState;
    }
}
