using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class FileSystemTarget : FileSystemPath
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
