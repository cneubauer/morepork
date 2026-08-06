using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class WebAnalytics
{
    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }

    [JsonPropertyName("credentials")]
    public Credential? Credentials { get; set; }
}
