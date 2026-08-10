namespace WaaS.Webshield.Workflow;

using WaaS.Persistence;

public record WebshieldMapping(string Domain, string Destination, bool IsEnabled = true);

public interface IWebshieldMappingService
{
    Task PatchWebshieldMappings(
        IStackInstance stackInstance,
        IEnumerable<WebshieldMapping> mappings
    );
}
