using System.ComponentModel;

namespace WaaS.Space.ViewModel;

public class Metadata
{
    /// <summary>
    /// Under which admin scope is the account assigned.
    /// </summary>
    /// <example>admin-scope-1</example>
    [ReadOnly(true)]
    public string? AdminScope { get; set; }

    /// <summary>
    /// Free text field to save additional information about the account
    /// </summary>
    /// <example>WordPress installation for customer XYZ</example>
    public string? Description { get; set; }
}
