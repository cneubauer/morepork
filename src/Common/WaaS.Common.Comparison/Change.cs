namespace WaaS.Common.Comparison;

/// <summary>
/// A single change at one path.
/// </summary>
/// <param name="ChangeType">What kind of change occurred.</param>
/// <param name="Current">
/// The value in the current state, or <c>null</c> when the path was absent or its value was JSON null.
/// Scalars are rendered without JSON quoting, so the string <c>"foo"</c> becomes <c>foo</c> and the
/// number <c>42</c> becomes <c>42</c>. Objects and arrays are rendered as compact JSON. This makes
/// the value directly displayable and storable, at the cost of not distinguishing the string
/// <c>"42"</c> from the number <c>42</c> — a change between those two is still reported, because the
/// JSON value kinds differ.
/// </param>
/// <param name="New">The value in the proposed state, rendered as described for <paramref name="Current"/>.</param>
public sealed record Change(ChangeType ChangeType, string? Current, string? New)
{
    /// <summary>
    /// RFC 6901 JSON Pointer to this node, for navigating back into the compared documents.
    /// </summary>
    /// <remarks>
    /// This is deliberately <em>not</em> a stable identifier: a collection item's pointer reflects its
    /// position, so the same logical item is <c>/1</c> before an insert and <c>/2</c> after. Use the
    /// <see cref="ChangeSet"/> key — which encodes identity rather than position — as the stable key
    /// when persisting changes.
    /// </remarks>
    public required string Pointer { get; init; }
}
