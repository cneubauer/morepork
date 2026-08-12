namespace WaaS.Persistence;

/// <summary>
/// The outcome of saving a Desired State: the persisted version, the version it replaced, and what
/// changed between them.
/// </summary>
/// <typeparam name="T">The type of the domain-specific desired state data.</typeparam>
/// <param name="Current">The newly persisted Desired State.</param>
/// <param name="Previous">
/// The version that was current before the save, or <c>null</c> when nothing was stored yet.
/// </param>
/// <param name="Changes">
/// The changes to <see cref="IDesiredState{T}.Data"/> between <paramref name="Previous"/> and
/// <paramref name="Current"/>. Empty when the save was a no-op; when there is no previous version,
/// the saved data is compared against a default instance, so each field that differs from the empty
/// state is reported.
/// </param>
public sealed record DesiredStateSaveResult<T>(
    IDesiredState<T> Current,
    IDesiredState<T>? Previous,
    ChangeSet Changes
);
