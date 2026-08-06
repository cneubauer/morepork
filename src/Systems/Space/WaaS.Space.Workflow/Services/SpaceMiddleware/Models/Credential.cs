using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class Credential
{
    [JsonPropertyName("sps_token")]
    public string? PasswordToken { get; set; }

    [JsonPropertyName("pubkeys")]
    public IEnumerable<PublicKey>? PublicKeys { get; set; }
}
