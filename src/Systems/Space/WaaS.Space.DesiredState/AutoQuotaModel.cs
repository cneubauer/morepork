namespace WaaS.Space.DesiredState;

public class AutoQuotaInfo
{
    /// <summary>
    /// The name should exist in the tenant profile.
    /// </summary>
    public string? DiskQuotaProfile { get; set; }

    /// <summary>
    /// The min allowed quota in bytes which can be set by the tenant on a single resource.
    /// </summary>
    public ulong MinDiskQuota { get; set; }

    /// <summary>
    /// The max allowed quota in bytes which can be set by the tenant on a single resource.
    /// </summary>
    public ulong MaxDiskQuota { get; set; }

    /// <summary>
    /// A timestamp until the next possible automatic quota change.
    /// </summary>
    public DateTime NextEvalNotBefore { get; set; }
}