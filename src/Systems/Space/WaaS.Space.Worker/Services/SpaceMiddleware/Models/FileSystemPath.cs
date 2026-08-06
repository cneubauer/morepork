using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class FileSystemPath
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
}
