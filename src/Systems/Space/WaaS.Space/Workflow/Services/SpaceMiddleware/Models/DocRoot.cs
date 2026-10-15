using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class DocRoot : FileSystemTarget
{
    [JsonPropertyName("environment")]
    public Environment? Environment { get; set; }
}
