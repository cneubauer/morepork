using Temporalio.Workflows;
using Temporalio.Common;

namespace MyNamespace;

[Workflow]
public class WaitForAckWorkflow
{
    private DesiredState? _desiredState = null;

    [WorkflowSignal]
    public async Task ReceiveCompletionSignalAsync(DesiredState desiredState)
    {
        _desiredState = desiredState;
    }

    [WorkflowRun]
    public async Task RunAsync(Webspace webspace)
    {
        await Workflow.WaitConditionAsync(() => _desiredState is not null);

        var options = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new RetryPolicy { MaximumAttempts = 10 }
        };

        // Safe webspace result to database
        // ...

        await Workflow.ExecuteActivityAsync(
            (DesiredStateActivities act) => act.Notify(_desiredState), 
            options
        );
    }
}