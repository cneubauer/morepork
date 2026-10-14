namespace WaaS.Common.ViewModel;

/// <summary>
/// Identifies the geographic/datacenter zone of a resource. The values match the stored zone ids.
/// </summary>
public enum ZoneType
{
    De = 1,
    Us = 2,
    Es = 3,
    Uk = 4,
    /// <summary>A temporary zone for Mail &amp; Media.</summary>
    Mm = 5,
}
