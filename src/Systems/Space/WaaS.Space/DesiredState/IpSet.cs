using System.Text.Json.Serialization;

namespace WaaS.Space.DesiredState;

public class IpSet
{
    [JsonPropertyName("ipv4")]
    public string? IPv4 { get; set; }

    [JsonPropertyName("ipv6")]
    public string? IPv6 { get; set; }
}