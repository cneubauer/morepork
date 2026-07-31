using System.ComponentModel;

namespace WaaS.Common.ViewModel;

public class PagingParameters
{
    /// <summary>
    /// The number of items to skip before returning results.
    /// </summary>
    /// <example>0</example>
    [DefaultValue(0)]
    public int Offset { get; set; } = 0;

    /// <summary>
    /// The maximum number of items to return.
    /// </summary>
    /// <example>100</example>
    [DefaultValue(100)]
    public int Limit { get; set; } = 100;
}
