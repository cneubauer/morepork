using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class Crontab
{
    [JsonPropertyName("schedule")]
    public string? Schedule { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("mailto")]
    public string? MailTo { get; set; }

    [JsonPropertyName("environment")]
    public Environment? Environment { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}
