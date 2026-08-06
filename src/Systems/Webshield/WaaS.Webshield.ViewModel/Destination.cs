using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WaaS.Webshield.ViewModel;

/// <summary>The destination of a URI config.</summary>
public class Destination
{
    /// <summary>The type of the destination.</summary>
    /// <example>Proxy</example>
    [DefaultValue(DestinationType.Proxy)]
    public DestinationType Type { get; set; } = DestinationType.Proxy;

    /// <summary>
    /// The target domain (when <c>Type</c> is <c>Proxy</c>) or URI (when <c>Type</c> is <c>Redirect</c>).
    /// </summary>
    /// <example>some-infong.server.lan</example>
    [Required]
    public required string Target { get; set; }

    /// <summary>The SNI mode for the proxy mapping.</summary>
    /// <example>Target</example>
    [DefaultValue(SniMode.Target)]
    public SniMode SniMode { get; set; } = SniMode.Target;
}
