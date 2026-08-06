using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class SpaceOwner
{
    [JsonPropertyName("uid")]
    [ReadOnly(true)]
    public int? Uid { get; set; }

    [JsonPropertyName("gid")]
    [ReadOnly(true)]
    public int? Gid { get; set; }

    [JsonPropertyName("username")]
    [ReadOnly(true)]
    public string? Username { get; set; }

    [JsonPropertyName("groupname")]
    [ReadOnly(true)]
    public string? Groupname { get; set; }
}
