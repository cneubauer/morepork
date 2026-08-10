namespace WaaS.Webshield.Workflow;

using WaaS.Persistence;
using WaaS.Webshield.DesiredState;

public class WebshieldMappingService(IDesiredStateStore<WebshieldData> webshieldDesiredStateStore) : IWebshieldMappingService
{
    public async Task PatchWebshieldMappings(
        IStackInstance stackInstance,
        IEnumerable<WebshieldMapping> mappings
    )
    {
        var webshieldDesiredState = await webshieldDesiredStateStore.Read(stackInstance.Id, 0);

        if (webshieldDesiredState is null)
        {
            webshieldDesiredState = new DesiredState<WebshieldData>
            {
                StackInstanceId = stackInstance.Id,
                Tenant = stackInstance.TenantId,
                Zone = stackInstance.Zone,
                SystemInstanceId = 0,
                Data = new WebshieldData(),
            };
        }

        var existingMappings = webshieldDesiredState.Data.Mappings;

        foreach (var mapping in mappings)
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

        await webshieldDesiredStateStore.Save(webshieldDesiredState);
    }
}
