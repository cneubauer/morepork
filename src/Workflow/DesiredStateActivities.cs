namespace MyNamespace;

using Temporalio.Activities;

public class DesiredStateActivities
{
    private readonly List<DesiredState> _desiredStates = [];

    [Activity]
    public async Task<DesiredState> Create(ulong stackInstanceId, Webspace webspace)
    {
        var desiredState = new DesiredState
        {
            StackInstanceId = stackInstanceId,
            SystemInstanceId = (ulong)Random.Shared.NextInt64(1, 1000000) + 50000000,
            Webspace = webspace
        };
        
        _desiredStates.Add(desiredState);

        return desiredState;
    }

    [Activity]
    public async Task<DesiredState?> Read(ulong stackInstanceId, ulong systemInstanceId)
    {
        return _desiredStates.FirstOrDefault(ds => ds.StackInstanceId == stackInstanceId && ds.SystemInstanceId == systemInstanceId);
    }

    [Activity]
    public async Task<WaasResult> Validate(DesiredState desiredState)
    {
        var isValid = true;

        if (!isValid)
        {
            return new WaasResult
            {
                ValidationErrors = ["Invalid Desired State"],
            };
        }

        return new WaasResult
        {
            ValidationErrors = [],
            DesiredState = desiredState
        };
    }

    [Activity]
    public async Task<DesiredState> Populate(DesiredState desiredState)
    {
        return desiredState;
    }

    [Activity]
    public async Task<Webspace> Publish(DesiredState desiredState)
    {
        await Task.Delay(2000);
        
        desiredState.Webspace.WebspaceId = 4321;

        return desiredState.Webspace;
    }

    [Activity]
    public async Task<Webspace> Notify(DesiredState desiredState)
    {
        await Task.Delay(2000);

        return desiredState.Webspace;
    }
}