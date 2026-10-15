namespace WaaS.Webshield.DesiredState;

public class UriConfig
{
    public UriMatch Match { get; set; } = new();
    public UriDestination Destination { get; set; } = new();
    public WafConfig? Waf { get; set; }
    public CacheConfig? Cache { get; set; }
}
