namespace WaaS.Workflow;

using Temporalio.Activities;

public class SharedWebspaceActivities(IDesiredStateStore<SharedWebspaceData> desiredStateStore, ILogger<SharedWebspaceActivities> logger)
{
    [Activity]
    public async Task<IDesiredState<SharedWebspaceData>?> Read(ulong stackInstanceId, ulong systemInstanceId)
    {
        return await desiredStateStore.Read(stackInstanceId, systemInstanceId);
    }

    [Activity]
    public async Task SendNotification(ulong stackInstanceId, ulong systemInstanceId)
    {
        logger.LogInformation("Sending notification for stackInstanceId: {StackInstanceId}, systemInstanceId: {SystemInstanceId}", stackInstanceId, systemInstanceId);
    }
}