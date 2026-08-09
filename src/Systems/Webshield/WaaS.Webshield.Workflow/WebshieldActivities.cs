using Temporalio.Activities;
using WaaS.Persistence;

namespace WaaS.Webshield.Workflow;

public class WebshieldActivities(
    ISslProxyRepository sslProxyRepository,
    IRabbitMqPublisher statePublisher,
    IWebshieldMappingService webshieldMappingService
)
{
    [Activity]
    public async Task<IReadOnlyList<string>> SendToBackend(WaasContext<WebshieldData> waasContext)
    {
        var nodes = await sslProxyRepository.GetWebshieldNodes(waasContext.StackInstance.Zone);

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

        await statePublisher.Publish(routingKey, body, waasContext.TransactionId);

        return nodes;
    }

    [Activity]
    public async Task SyncWebshieldMappings(
        StackInstance stackInstance,
        List<WebshieldMapping> mappings
    )
    {
        await webshieldMappingService.SyncWebshieldMappings(
            stackInstance,
            mappings
        );
    }
}