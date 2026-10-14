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

        var techModel = BuildBackendModel(desiredState, extCorrelationId, stackInstance.Tags);

        if (desiredState.Tombstoned)
        {
            // TechMW takes the ext_correlation of a delete from the request body, like for create and update.
            using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"{tenant}/{ResourcePath}/{id}")
            {
                Content = JsonContent.Create(techModel),
            };
            
            var deleteResponse = await httpClient.SendAsync(deleteRequest);

            deleteResponse.EnsureSuccessStatusCode();

            return desiredState;
        }

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
