namespace WaaS.Webshield.ViewModel;

/// <summary>The mode the proxy operates in for this specific domain.</summary>
public enum WebshieldMode
{
    /// <summary>Pass requests through to the mapping destination on HTTP or HTTPS.</summary>
    Proxy = 1,
    /// <summary>Pass requests through to the mapping destination on HTTPS only.</summary>
    ProxyForceSsl = 2,
}
