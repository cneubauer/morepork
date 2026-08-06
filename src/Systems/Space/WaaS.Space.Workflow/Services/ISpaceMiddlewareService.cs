namespace WaaS.Space.Workflow;

public interface ISpaceMiddlewareService<TDesiredState, TBackendModel>
{
    IDesiredState<TDesiredState> ApplyBackendResponse(IDesiredState<TDesiredState> desiredState, TBackendModel backendModel);
    Task<IDesiredState<TDesiredState>> Publish(string tenant, IStackInstance stackInstance, IDesiredState<TDesiredState> desiredState, string extCorrelationId);
}
