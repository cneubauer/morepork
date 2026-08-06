using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class MailConfig
{
    [JsonPropertyName("host")]
    public string? Hostname { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("default_sender")]
    public string? DefaultSender { get; set; }

    [JsonPropertyName("default_envelope_from_policy")]
    public string? DefaultEnvelopeFromPolicy { get; set; }

    [JsonPropertyName("credentials")]
    public Credential? Credentials { get; set; }
}
