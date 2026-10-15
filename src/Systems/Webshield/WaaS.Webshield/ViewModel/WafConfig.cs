using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>WAF configuration for a URI config. Must not be set when the destination type is <c>Redirect</c>.</summary>
public class WafConfig
{
    /// <summary>The WAF ruleset to apply.</summary>
    /// <example>Owasp</example>
    [DefaultValue(WafRuleset.Off)]
    public WafRuleset? Ruleset { get; set; } = WafRuleset.Off;

    /// <summary>Geofilter configuration.</summary>
    public Geofilter? Geofilter { get; set; }

    /// <summary>Maximum requests per second (1–500). Default is 100.</summary>
    /// <example>100</example>
    [Range(1, 500)]
    [DefaultValue(100)]
    public int Ratelimit { get; set; } = 100;
}
