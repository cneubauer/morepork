using WaaS.Common.Comparison;

namespace WaaS.Persistence;

public sealed record DesiredStateSaveResult<T>(
    IDesiredState<T> Current,
    IDesiredState<T>? Previous,
    ChangeSet Changes
);
