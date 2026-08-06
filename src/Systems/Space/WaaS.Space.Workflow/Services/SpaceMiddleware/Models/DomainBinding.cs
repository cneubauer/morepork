using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SpaceMiddleware;

public class DomainBinding : ResourceBase
{
    /// <summary>
    /// IMMUTABLE READONLY.
    /// </summary>
    [JsonPropertyName("domain_id")]
    [ReadOnly(true)]
    public ulong? Id { get; set; }

    [JsonPropertyName("domainname")]
    public string? DomainName { get; set; }

    /// <summary>
    /// WRITABLE enum "docroot|redirect".
    /// </summary>
    [JsonPropertyName("connect_type")]
    public string? ConnectType { get; set; }

    [JsonPropertyName("docroot")]
    public DocRoot? DocRoot { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}
