using Temporalio.Client;

namespace WaaS.Webshield.Workflow;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Client;

public class WebshieldActualStateListener(
    ITemporalClient temporalClient,
    IRabbitMqConsumer consumer,
    ILogger<WebshieldActualStateListener> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        => await consumer.StartConsuming(
            handler: HandleActualState,
            cancellationToken);

    private async Task<bool> HandleActualState(byte[] data, string correlationId, string replyTo)
    {
        try
        {
            var workflowId = $"webshield-{correlationId}";

            var workflowHandle = temporalClient.GetWorkflowHandle<PublishWebshieldWorkflow>(workflowId);

            await workflowHandle.SignalAsync(
                workflow => workflow.ReceiveNodeAck(correlationId, replyTo)
            );

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle actual state notification");
            return false;
        }
    }
}
