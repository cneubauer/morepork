using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SpaceMiddleware;

/// <summary>
/// Note: This model is also used as the actual state property, therefore:
/// - all properties in this model should be null-able
/// - the attribute "[Required]" should not be set
/// - no default values should be set
/// </summary>
public class SpaceState : ResourceBase
{
    [JsonPropertyName("tech_webspace_id")]
    [ReadOnly(true)]
    public ulong? TechId { get; set; }

    [JsonPropertyName("tenant")]
    [ReadOnly(true)]
    public string? Tenant { get; set; }

    [JsonPropertyName("tech_mode")]
    [ReadOnly(true)]
    public string? Mode { get; set; }

    [JsonPropertyName("slot_id")]
    [ReadOnly(true)]
    public ulong? SlotId { get; set; }

    [JsonPropertyName("webspace_ipv4")]
    [ReadOnly(true)]
    public string? IPv4 { get; set; }

    [JsonPropertyName("webspace_ipv6")]
    [ReadOnly(true)]
    public string? IPv6 { get; set; }

    [JsonPropertyName("owner")]
    [ReadOnly(true)]
    public SpaceOwner? Owner { get; set; }

    /// <summary>
    /// READONLY IMPORTABLE
    /// </summary>
    [JsonPropertyName("host")]
    public string? Hostname { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// WRITABLE enum Region "europe|america|..."
    /// </summary>
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("limits")]
    public SpaceLimits? Limits { get; set; }

    [JsonPropertyName("mailconfig")]
    public MailConfig? MailConfiguration { get; set; }

    [JsonPropertyName("accounts")]
    public IEnumerable<Account>? Accounts { get; set; }

    [JsonPropertyName("domains")]
    public IEnumerable<DomainBinding>? Domains { get; set; }

    [JsonPropertyName("web_analytics")]
    public WebAnalytics? WebAnalytics { get; set; }

    [JsonPropertyName("crontab")]
    public List<Crontab>? Crontab { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("placement_tags")]
    public List<string>? PlacementTags { get; set; }

    [JsonPropertyName("biofilter_enabled")]
    public bool? BiofilterEnabled { get; set; }
}
