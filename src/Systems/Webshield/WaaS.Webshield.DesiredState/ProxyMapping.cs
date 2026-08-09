namespace WaaS.Webshield.DesiredState;

public class ProxyMapping
{
    public string Domain { get; set; } = "";
    public string Destination { get; set; } = "";
    public ModeType Mode { get; set; }
    public WebshieldType WebshieldType { get; set; }
    public uint? SslCertificateId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<UriConfig> UriConfigs { get; set; } = [];
    public AnalyticsRef? WebAnalytics { get; set; }
}
