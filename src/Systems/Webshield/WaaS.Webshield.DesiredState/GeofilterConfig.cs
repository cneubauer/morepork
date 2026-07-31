namespace WaaS.Webshield.DesiredState;

public class GeofilterConfig
{
    public GeofilterType Type { get; set; }
    public List<string> Countries { get; set; } = [];
}
