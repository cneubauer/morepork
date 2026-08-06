using System.Net.Http.Json;

namespace WaaS.Space.Worker;

public abstract class SpaceMiddlewareService<TDesiredState, TTechModel>(HttpClient httpClient)
    where TDesiredState : IDesiredStateData
{
    protected abstract string ResourcePath { get; }

    protected abstract ulong BackendId(IDesiredState<TDesiredState> desiredState);

    protected abstract TTechModel BuildTechModel(IDesiredState<TDesiredState> desiredState, string extCorrelationId, string[]? tags);

    public async Task<IDesiredState<TDesiredState>> Publish(string tenant, IStackInstance stackInstance, IDesiredState<TDesiredState> desiredState, string extCorrelationId)
    {
        var id = BackendId(desiredState);

        if (desiredState.Tombstoned)
        {
            var delelteResponse = await httpClient.DeleteAsync($"{tenant}/{ResourcePath}/{id}");
            delelteResponse.EnsureSuccessStatusCode();
            return desiredState;
        }

        var techModel = BuildTechModel(desiredState, extCorrelationId, stackInstance.Tags);

        var response = id == 0
            ? await httpClient.PostAsJsonAsync($"{tenant}/{ResourcePath}", techModel)
            : await httpClient.PutAsJsonAsync($"{tenant}/{ResourcePath}/{id}", techModel);

        response.EnsureSuccessStatusCode();

        return desiredState;
    }
}
