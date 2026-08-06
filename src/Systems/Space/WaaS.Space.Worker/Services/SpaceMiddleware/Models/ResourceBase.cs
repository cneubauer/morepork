using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public abstract class ResourceBase
{
    [JsonPropertyName("ext_reference")]
    public string? ExternalReference { get; set; }

    [JsonPropertyName("ext_correlation")]
    public string? ExternalCorrelation { get; set; }
}
