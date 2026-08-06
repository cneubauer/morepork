using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class Environment
{
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("env_profile")]
    public string? EnvironmentProfile { get; set; }
}
