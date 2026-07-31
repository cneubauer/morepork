namespace WaaS.Webshield.ViewModel;

/// <summary>The full Webshield state for a stack instance.</summary>
public class WebshieldState
{
    /// <summary>All domain mappings configured for this stack instance.</summary>
    public List<WebshieldMapping> Mappings { get; set; } = [];

    /// <summary>Public IP addresses assigned to default Webshield nodes.</summary>
    public List<string> PublicIPs { get; set; } = [];

    /// <summary>Public IP addresses assigned to CDN Webshield nodes.</summary>
    public List<string> CdnIPs { get; set; } = [];

    public void Tombstone() => Mappings = [];
}
