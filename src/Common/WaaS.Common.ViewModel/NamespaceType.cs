namespace WaaS.Common.ViewModel;

/// <summary>
/// Identifies the system namespace (resource type) within a Stack Instance.
/// Each value corresponds to one managed system domain.
/// </summary>
public enum NamespaceType
{
    /// <summary>Reverse proxy namespace.</summary>
    Proxy = 1,
    /// <summary>Product DNS namespace.</summary>
    ProductDns = 2,
    /// <summary>Shared webspace namespace.</summary>
    SharedWebspace = 3,
    /// <summary>Database namespace.</summary>
    Database = 4,
    /// <summary>Redirect namespace.</summary>
    Redirect = 5,
    /// <summary>Redirect configuration namespace.</summary>
    RedirectConfig = 6,
    /// <summary>Proxy configuration namespace.</summary>
    ProxyConfig = 7,
    /// <summary>Product domains (database) namespace.</summary>
    ProductDomainsDb = 8,
    /// <summary>Product domains (webspace) namespace.</summary>
    ProductDomainsWs = 9,
    /// <summary>Stretchspace namespace.</summary>
    StretchSpace = 10,
}
