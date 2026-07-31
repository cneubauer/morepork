namespace WaaS.Workflow;

using Temporalio.Activities;

public class SharedWebspaceActivities(IDesiredStateStore<SharedWebspace> desiredStateStore)
{
    [Activity]
    public async Task<IDesiredState<SharedWebspace>?> Read(ulong stackInstanceId, ulong systemInstanceId)
    {
        return await desiredStateStore.Read(stackInstanceId, systemInstanceId);
    }
}