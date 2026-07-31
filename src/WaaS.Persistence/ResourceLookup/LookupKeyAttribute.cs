namespace WaaS.Persistence;

/// <summary>
/// Marks a property as a lookup index entry. The extractor in <c>WaaS.Persistence</c> walks
/// the object graph at save time and inserts a row into <c>lookup_resource</c> for each
/// annotated property that has a non-default, non-empty value.
/// </summary>
/// <param name="resourceKey">The <c>resource_key</c> value identifying the lookup type.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class LookupKeyAttribute(LookupResourceKeyType resourceKey) : Attribute
{
    public LookupResourceKeyType ResourceKey { get; } = resourceKey;
}
