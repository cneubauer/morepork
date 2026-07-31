namespace WaaS.Common.DesiredState;

/// <summary>
/// A base class for all WaaS resources containing common indentifiers.
/// </summary>
public class WaasResource
{
    /// <summary>
    /// An unique identifier for the resource
    /// </summary>
    public string ReferenceId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// A correlation ID to identify the resource in specific processes or transactions
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    public override string ToString() => ReferenceId;
}
