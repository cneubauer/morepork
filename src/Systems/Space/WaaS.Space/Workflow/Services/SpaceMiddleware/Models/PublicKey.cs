using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class PublicKey
{
    [JsonPropertyName("pubkey")]
    public string? PubKey { get; set; }

    [JsonPropertyName("key_type")]
    public string? KeyType { get; set; }

    [JsonPropertyName("options")]
    public List<PublicKeyOption>? Options { get; set; } = [];
}
