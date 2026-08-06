namespace WaaS.Space.DesiredState;

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

    public string ResourceLevel { get; set; } = "";

    public AutoQuotaInfo? AutoQuota { get; set; }
}