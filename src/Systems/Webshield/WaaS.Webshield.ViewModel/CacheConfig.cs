using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>Cache configuration for a URI config. Must not be set when the destination type is <c>Redirect</c>.</summary>
public class CacheConfig
{
    /// <summary>The cache policy to apply.</summary>
    [DefaultValue(CachePolicy.Off)]
    public CachePolicy Policy { get; set; } = CachePolicy.Off;

    /// <summary>
    /// Cache key salt. Changing this value invalidates existing cache entries.
    /// Only US-ASCII printable characters allowed. Max 64 characters.
    /// </summary>
    /// <example>nq*w8tb/1l-4</example>
    [MaxLength(64)]
    [RegularExpression(@"^[ -~]+$")]
    public string? Salt { get; set; }
}
