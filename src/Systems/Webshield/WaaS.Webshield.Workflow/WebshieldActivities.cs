using Microsoft.Extensions.DependencyInjection;
using Temporalio.Activities;
using WaaS.Common.Workflow;
using WaaS.Persistence;
using WaaS.Webshield.DesiredState;

public class WebshieldActivities(
    ISslProxyRepository sslProxyRepository,
    IRabbitMqPublisher statePublisher,
    IWebshieldMappingService webshieldMappingService,
    ILogger<WebshieldActivities> logger
)
{
    [Activity]
    public async Task<IReadOnlyList<string>> SendToWebshieldNodes(WaasContext<WebshieldData> waasContext)
    {
        var nodes = await sslProxyRepository.GetWebshieldNodes(waasContext.StackInstance.Zone);
        if (nodes.Count == 0)
        {
            logger.LogWarning("No Webshield nodes found for zone {Zone} on stack {StackInstanceId}", waasContext.StackInstance.Zone, waasContext.StackInstance.Id);
            return [];
        }

        var protobuf = waasContext.DesiredState.Data.ToProtobuf(
            waasContext.StackInstance.Id,
            (StateHeader.Zone)waasContext.StackInstance.Zone,
            waasContext.DesiredState.Version,
            waasContext.DesiredState.Tombstoned,
            waasContext.Tenant.Name,
            waasContext.TransactionId
        );

        var routingKey = protobuf.GetRoutingKey();
        var body = protobuf.ToProtoBuf();

        logger.LogInformation("Publishing desired state to {Count} Webshield nodes with routing key {RoutingKey}", nodes.Count, routingKey);
        await statePublisher.Publish(routingKey, body, waasContext.TransactionId);

        return nodes;
    }

    [Activity]
    public async Task PatchWebshieldMappings(
        StackInstance stackInstance,
        List<WebshieldMapping> mappings
    )
    {
        await webshieldMappingService.PatchWebshieldMappings(
            stackInstance,
            mappings
        );
    }
}