namespace WaaS.Space.ViewModel;

public enum ResourceLevel { XS, S, M, L, XL, XXL, Z }

public class Limits
{
    /// <summary>
    /// The desired disk quota in bytes.
    /// </summary>
    /// <example>10737418240</example>
    public ulong? DiskQuota { get; set; }

    /// <summary>
    /// The resource level tier for this webspace.
    /// </summary>
    /// <example>M</example>
    public ResourceLevel ResourceLevel { get; set; } = ResourceLevel.M;

    /// <summary>
    /// Configuration for automatic quota adjustment based on tenant profile rules.
    /// </summary>
    public AutoQuotaInfo? AutoQuota { get; set; }
}
