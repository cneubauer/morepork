namespace WaaS.Space.DesiredState;

public enum ResourceLevel { XS, S, M, L, XL, XXL, Z }

public class Limits
{
    /// <summary>
    /// In bytes, quota requested by tenant (desired state).
    /// </summary>
    public ulong DiskQuota { get; set; }

    /// <summary>
    /// In bytes, quota currently set (actual  state).
    /// </summary>
    public ulong? DiskQuotaActual { get; set; }

    public ResourceLevel ResourceLevel { get; set; } = ResourceLevel.M;

    public AutoQuotaInfo? AutoQuota { get; set; }
}