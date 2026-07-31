namespace WaaS.Webshield.DesiredState;

public class WafConfig
{
    public WafRuleset Ruleset { get; set; }
    public GeofilterConfig? Geofilter { get; set; }
    public uint? Ratelimit { get; set; }
}
