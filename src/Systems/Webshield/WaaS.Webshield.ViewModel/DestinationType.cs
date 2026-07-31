namespace WaaS.Webshield.ViewModel;

/// <summary>
/// The type of the destination.
/// For <c>Proxy</c> the target must be a valid domain.
/// For <c>Redirect</c> the target must be a valid URI with HTTP or HTTPS scheme.
/// When the type is <c>Redirect</c>, WAF and Cache configuration are not allowed.
/// </summary>
public enum DestinationType
{
    Proxy = 1,
    Redirect = 2,
}
