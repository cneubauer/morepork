using System.Text.Json.Serialization;
using WaaS.Common.DesiredState;

namespace WaaS.Space.DesiredState;

public class Space : WaasResource
{
    public Platform Platform { get; set; }

    public string Region { get; set; } = "";

    /// <summary>
    /// State of the webspace, reported by the Webspace Middleware.
    /// </summary>
    public string State { get; set; } = "unknown";

    /// <summary>
    /// Host of the webspace, reported by the Webspace Middleware.
    /// </summary>
    public string Hostname { get; set; } = "";

    /// <summary>
    /// IPs of the web server the webspace is located on.
    /// </summary>
    public IpSet IpSet { get; set; } = new();

    public Limits Limits { get; set; } = new();

    public MailConfiguration? MailConfiguration { get; set; }

    public Owner Owner { get; set; } = new();

    /// <summary>
    /// A list of product subdomains pointing to the web server directly for SSH/SFTP purposes.
    /// </summary>
    public List<DataAccessDomainBinding> DataAccessDomains { get; set; } = [];

    public List<CompatLink> CompatLinks { get; set; } = [];

    /// <summary>
    /// GPHWAAS-7264: a list of locks
    /// </summary>
    public List<LockItem> LockItems { get; set; } = [];

    public ExpirationInfo? Temporary { get; set; }

    [JsonPropertyName("wa")]
    public WebAnalytics? WebAnalytics { get; set; }

    /// <summary>
    /// A list of placement tags requested (desired state). The target hosting server is selected depending on placement tags.
    /// </summary>
    public List<string> PlacementTags { get; set; } = [];

    /// <summary>
    /// A list of placement tags currently set (actual state).
    /// </summary>
    public List<string> PlacementTagsActual { get; set; } = [];

    /// <summary>
    /// A list of placement tags set by a platform admin. Admin tags always overwrite any tenant tags. READONLY for the tenant.
    /// </summary>
    public List<string> PlacementTagsAdmin { get; set; } = [];

    public bool? BiofilterEnabled { get; set; }

    public virtual DateTime? CalculateNextCheckTimestamp() => Temporary?.Expires;
}