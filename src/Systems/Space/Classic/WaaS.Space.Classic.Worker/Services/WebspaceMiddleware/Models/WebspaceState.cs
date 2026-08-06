using System.Text.Json.Serialization;

namespace WebspaceMiddleware;

public class WebspaceState : SpaceMiddleware.SpaceState
{
    [JsonPropertyName("webspace_id")]
    public ulong? Id { get; set; }
}
