namespace WaaS.Common.Changes;

/// <summary>
/// The kind of change recorded for a single path in a <see cref="ChangeSet"/>.
/// </summary>
public enum ChangeType
{
    /// <summary>The property or collection item is absent in the current state and present in the proposed state.</summary>
    Added,

    /// <summary>The property or collection item is present in the current state and absent in the proposed state.</summary>
    Removed,

    /// <summary>The path exists on both sides and its value differs. Includes a value changing to or from null.</summary>
    Modified,
}
