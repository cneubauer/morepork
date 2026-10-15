using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class PublicKeyOption
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("val")]
    public string? Value { get; set; }
}
