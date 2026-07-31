namespace WaaS.Space.DesiredState;

public class Metadata
{
    /// <summary>
    /// Under which admin scope is the account assigned.
    /// </summary>
    public string? AdminScope { get; set; }

    /// <summary>
    /// Free text field to save additional information about the account
    /// </summary>
    public string? Description { get; set; }
}