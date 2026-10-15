using WaaS.Webshield.DesiredState;
using WaaS.Webshield.ViewModel;

namespace WaaS.Webshield.Api;

public static class WebshieldModelMappings
{
    public static void Apply(this IDesiredState<WebshieldData> desiredState, IEnumerable<WebshieldMapping> mappings)
    {
        desiredState.Data.Mappings = [.. mappings.Select(x => new ProxyMapping()
        {
            Domain = x.Id,
            Destination = x.Destination,
            IsEnabled = x.IsEnabled
        })];
    }
}
