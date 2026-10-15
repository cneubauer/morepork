using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>The URI to which this configuration should match.</summary>
public class UriMatch
{
    /// <summary>
    /// The prefix of the URI to match. Must start with <c>/</c>. Only US-ASCII characters allowed. Max 128 characters.
    /// </summary>
    /// <example>/</example>
    [Required(AllowEmptyStrings = true)]
    [MaxLength(128)]
    [RegularExpression(@"^/[ -~]*$")]
    public required string Prefix { get; set; }

    /// <summary>The type of the URI match.</summary>
    /// <example>Prefix</example>
    [DefaultValue(UriMatchType.Prefix)]
    public UriMatchType Type { get; set; } = UriMatchType.Prefix;

    /// <summary>The protocol of the URI to match.</summary>
    /// <example>All</example>
    [DefaultValue(UriProtocol.All)]
    public UriProtocol Protocol { get; set; } = UriProtocol.All;
}
