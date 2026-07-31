namespace WaaS.Persistence;

/// <summary>
/// Marker interface for the root data object of a Desired State.
/// Every domain-specific desired state data class must implement this interface
/// and must produce a valid default state when instantiated with <c>new()</c>.
/// </summary>
public interface IDesiredStateData
{
    /// <summary>
    /// Returns the timestamp at which the next scheduled consistency check should run,
    /// or <c>null</c> if no periodic check is required.
    /// The framework uses this value to schedule re-evaluation of the desired state
    /// against the actual state reported by the backend system.
    /// </summary>
    DateTime? GetNextCheck();
}
