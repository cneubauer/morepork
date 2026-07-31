namespace WaaS.Persistence;

/// <summary>
/// Optional escape hatch for providing additional lookup entries that cannot be expressed
/// via the <c>[LookupKey]</c> attribute — e.g. computed values or ambiguous key types on
/// shared base classes. Only implement this when attributes are insufficient.
/// </summary>
public interface ILookupEntryProvider
{
    IEnumerable<(LookupResourceKeyType ResourceKey, string Text)> GetAdditionalLookupEntries();
}
