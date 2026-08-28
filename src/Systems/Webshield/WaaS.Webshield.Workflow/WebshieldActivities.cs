using Temporalio.Activities;
using WaaS.Persistence;

namespace WaaS.Webshield.Workflow;

public class WebshieldActivities(
    ISslProxyRepository sslProxyRepository,
    IRabbitMqPublisher statePublisher,
    IDesiredStateStore<WebshieldData> webshieldDesiredStateStore,
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
    public async Task<WaasContext<WebshieldData>> PatchWebshieldMappings(
        WaasContext waasContext,
        List<WebshieldMapping> mappingsToAdd,
        List<WebshieldMapping> mappingsToRemove
    )
    {
        await using var transaction = await webshieldDesiredStateStore.BeginTransaction();
        await using var connection = transaction.Connection;

        await webshieldDesiredStateStore.Lock(transaction, waasContext.StackInstance.Id, 0);

        var webshieldDesiredState = await webshieldDesiredStateStore.Read(transaction, waasContext.StackInstance.Id, 0);

        webshieldDesiredState ??= new DesiredState<WebshieldData>
            {
                StackInstanceId = waasContext.StackInstance.Id,
                Tenant = waasContext.StackInstance.TenantId,
                Zone = waasContext.StackInstance.Zone,
                SystemInstanceId = 0,
                Data = new WebshieldData(),
                TransactionId = waasContext.TransactionId,
            };

        var existingMappings = webshieldDesiredState.Data.Mappings;

        foreach (var mapping in mappingsToRemove)
        {
            if (string.IsNullOrWhiteSpace(mapping.Domain))
                continue;
        }

        foreach (var mapping in mappingsToAdd)
        {
            if (string.IsNullOrWhiteSpace(mapping.Domain))
                continue;

            var existingMapping = existingMappings
                .FirstOrDefault(m => string.Equals(m.Domain, mapping.Domain, StringComparison.OrdinalIgnoreCase));

            if (existingMapping is not null)
            {
                existingMapping.Destination = mapping.Destination;
                existingMapping.IsEnabled = mapping.IsEnabled;
            }
            else
            {
                existingMappings.Add(new ProxyMapping
                {
                    Domain = mapping.Domain,
                    Destination = mapping.Destination,
                    Mode = ModeType.Proxy,
                    WebshieldType = WebshieldType.Default,
                    IsEnabled = mapping.IsEnabled,
                });
            }
        }

        await webshieldDesiredStateStore.Save(transaction, webshieldDesiredState, webshieldDesiredState.TransactionId);

        await transaction.CommitAsync();

        return new WaasContext<WebshieldData>
        {
            Tenant = waasContext.Tenant,
            StackInstance = waasContext.StackInstance,
            TransactionId = waasContext.TransactionId,
            DesiredState = (DesiredState<WebshieldData>)webshieldDesiredState,
        };
    }
}