using ObjectCompare;

namespace WaaS.Persistence;

public sealed record DesiredStateSaveResult<T>(
    IDesiredState<T> Current,
    IDesiredState<T>? Previous,
    IReadOnlyList<IChange> Changes
);
