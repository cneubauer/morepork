namespace WaaS.Common.Changes;

/// <summary>
/// Marks the property that identifies an item within its containing collection, so that
/// <see cref="ChangeSet"/> matches collection items by identity instead of by position.
/// </summary>
/// <remarks>
/// <para>
/// Without this attribute, a collection of objects is compared positionally, and inserting an item at
/// the head of a list reports every following item as modified. With it, that insert is reported as a
/// single added item and reordering reports no change at all.
/// </para>
/// <para>
/// Apply it to at most one property per type. For a composite identity, expose a computed property
/// that concatenates the parts and mark that instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ListItemKeyAttribute : Attribute;
