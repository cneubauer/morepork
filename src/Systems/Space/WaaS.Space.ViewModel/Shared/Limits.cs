using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

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
    [Required]
    [Options("XS", "S", "M", "L", "XL", "XXL", "Z", AllowNull = true)]
    public string? ResourceLevel { get; set; }

    /// <summary>
    /// Configuration for automatic quota adjustment based on tenant profile rules.
    /// </summary>
    public AutoQuotaInfo? AutoQuota { get; set; }
}
