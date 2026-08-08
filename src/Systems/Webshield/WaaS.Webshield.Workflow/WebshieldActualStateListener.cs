using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace WaaS.Webshield.Workflow;

public class WebshieldActualStateListener(
    ITemporalClient temporalClient,
    IRabbitMqConsumer consumer
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        => await consumer.StartConsuming(
            handler: HandleActualState,
            cancellationToken);

    private async Task<bool> HandleActualState(byte[] data, string transactionId, string node)
    {
        var protobuf = data.FromProtoBuf<ActualStateProxy>();

        var resourceId = $"webshield-{protobuf.header.stackInstanceId}";

        var workflowHandle = temporalClient.GetWorkflowHandle<PublishWebshieldWorkflow>(resourceId);

        await workflowHandle.SignalAsync(
            workflow => workflow.ReceiveBackendNotification(transactionId, node)
        );

        return true;
    }
}
