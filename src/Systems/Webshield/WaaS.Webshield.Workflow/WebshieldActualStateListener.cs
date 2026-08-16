using Temporalio.Client;

namespace WaaS.Webshield.Workflow;

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

    private async Task<bool> HandleActualState(byte[] data, string transactionId, string node)
    {
        try
        {
            var protobuf = data.FromProtoBuf<ActualStateProxy>();
            var txId = protobuf.header.tags.Count > 0 ? protobuf.header.tags[0] : transactionId;
            var stackInstanceId = protobuf.header.stackInstanceId;
            var reportingNode = !string.IsNullOrEmpty(protobuf.nodeFqdn) ? protobuf.nodeFqdn : node;

            var resourceId = $"webshield-{stackInstanceId}-{txId}";

            try
            {
                var workflowHandle = temporalClient.GetWorkflowHandle<PublishWebshieldWorkflow>(resourceId);
                await workflowHandle.SignalAsync(
                    workflow => workflow.ReceiveBackendNotification(txId, reportingNode)
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not signal workflow with ID {ResourceId}, trying fallback", resourceId);
                var fallbackId = $"webshield-{stackInstanceId}";
                var fallbackHandle = temporalClient.GetWorkflowHandle<PublishWebshieldWorkflow>(fallbackId);
                await fallbackHandle.SignalAsync(
                    workflow => workflow.ReceiveBackendNotification(txId, reportingNode)
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle actual state notification");
            return false;
        }
    }
}
