using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>
/// A Webshield domain mapping.
/// </summary>
public class WebshieldMapping : IValidatableObject
{
    /// <summary>
    /// The domain name of the mapping (the identifier). Converted to lower-case before processing.
    /// </summary>
    /// <example>example.com</example>
    [Required]
    public required string Id { get; set; }

    /// <summary>
    /// The hostname to which the proxy forwards. Must be null or empty when <c>UriConfigs</c> is set.
    /// </summary>
    /// <example>some-infong.server.lan</example>
    [Required]
    public required string Destination { get; set; }

    /// <summary>
    /// The mode the proxy operates in for this domain.
    /// </summary>
    [DefaultValue(WebshieldMode.Proxy)]
    public WebshieldMode Mode { get; set; } = WebshieldMode.Proxy;

    /// <summary>
    /// The Webshield type (node pool) on which the mapping is provisioned.
    /// </summary>
    public WebshieldType WebshieldType { get; set; } = WebshieldType.Default;

    /// <summary>
    /// The SSL certificate for this domain.
    /// </summary>
    public CertificateInfo? Certificate { get; set; }

    /// <summary>
    /// IPv4 address assigned for this domain on the SSL proxies. Set by the server.
    /// </summary>
    /// <example>123.123.123.123</example>
    public string? IpV4 { get; set; }

    /// <summary>
    /// IPv6 address assigned for this domain on the SSL proxies. Set by the server.
    /// </summary>
    /// <example>2001:db8::1</example>
    public string? IpV6 { get; set; }

    /// <summary>
    /// Whether the domain mapping is active on the backend system.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Per-URI configurations. Required for CDN mappings. Forbidden for Default mappings.
    /// </summary>
    public List<UriConfig>? UriConfigs { get; set; }

    /// <summary>
    /// Web Analytics assignment for this mapping.
    /// </summary>
    public WebshieldAnalytics? WebAnalytics { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UriConfigs is { Count: > 0 })
        {
            if (!string.IsNullOrEmpty(Destination))
                yield return new ValidationResult("Destination must not be set when UriConfigs are set.", [nameof(Destination)]);

            if (Mode != WebshieldMode.Proxy)
                yield return new ValidationResult("Mode must be Proxy for CDN mappings.", [nameof(Mode)]);

            if (UriConfigs.Count > 25)
                yield return new ValidationResult("A mapping may have at most 25 UriConfigs.", [nameof(UriConfigs)]);
        }
        else
        {
            if (string.IsNullOrEmpty(Destination))
                yield return new ValidationResult("Destination is required when UriConfigs are not set.", [nameof(Destination)]);
        }

        if (WebshieldType == WebshieldType.Default && UriConfigs is { Count: > 0 })
            yield return new ValidationResult("UriConfigs are not allowed for Default Webshield mappings.", [nameof(UriConfigs)]);

        if (WebshieldType == WebshieldType.Cdn && (UriConfigs is null || UriConfigs.Count == 0))
            yield return new ValidationResult("UriConfigs are required for CDN Webshield mappings.", [nameof(WebshieldType), nameof(UriConfigs)]);
    }
}
