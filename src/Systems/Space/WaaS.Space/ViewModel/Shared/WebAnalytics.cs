using WaaS.Common.ViewModel;

namespace WaaS.Space.ViewModel;

/// <summary>
/// Web analytics integration credentials and identifier for this webspace.
/// </summary>
public class WebAnalytics : Credential
{
    /// <summary>
    /// The identifier for the web analytics account.
    /// </summary>
    /// <example>analytics-00123456</example>
    public string? Id { get; set; }
}