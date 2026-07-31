using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>Per-URI configuration for a mapping.</summary>
public class UriConfig : IValidatableObject
{
    /// <summary>The URI to which this configuration applies.</summary>
    [Required]
    public required UriMatch Match { get; set; }

    /// <summary>The destination of the proxy or redirect.</summary>
    [Required]
    public required Destination Destination { get; set; }

    /// <summary>WAF configuration. Not allowed when destination type is <c>Redirect</c>.</summary>
    public WafConfig? Waf { get; set; }

    /// <summary>Cache configuration. Not allowed when destination type is <c>Redirect</c>.</summary>
    public CacheConfig? Cache { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Destination.Type == DestinationType.Redirect)
        {
            if (Waf is not null)
                yield return new ValidationResult("WAF configuration is not allowed for redirect destinations.", [nameof(Waf)]);
            if (Cache is not null)
                yield return new ValidationResult("Cache configuration is not allowed for redirect destinations.", [nameof(Cache)]);
        }
    }
}
