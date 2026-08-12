using System.Collections;
using System.Text.Encodings.Web;

namespace WaaS.Common.Comparison;

/// <summary>
/// The result of comparing two states: a flat map from path to <see cref="Change"/>, in document order.
/// </summary>
/// <remarks>
/// <para>
/// Keys are dotted paths such as <c>Owner.ContactId</c>, <c>Domains[DomainName=a.com].IsEnabled</c> or
/// <c>PlacementTags[value=beta]</c>. Collection items are addressed by identity rather than position
/// wherever a key is known, which is what makes a key usable for persisted change records: inserting an
/// item at the head of a list does not change the paths of the items that follow it.
/// </para>
/// <para>Create one with <see cref="Changes.Between{T}(T, T, ComparisonOptions?)"/>.</para>
/// </remarks>
public sealed class ChangeSet : IReadOnlyDictionary<string, Change>
{
    private readonly Dictionary<string, Change> changes;
    private readonly List<string> order;

    internal ChangeSet(Dictionary<string, Change> changes, List<string> order)
    {
        this.changes = changes;
        this.order = order;
    }

    /// <summary>An empty change set, meaning the two states are equivalent.</summary>
    public static ChangeSet Empty { get; } = new([], []);

    /// <summary>Whether any change was recorded.</summary>
    public bool HasChanges => changes.Count > 0;

    public Change this[string path] => changes[path];

    public IEnumerable<string> Keys => order;

    public IEnumerable<Change> Values => order.Select(path => changes[path]);

    public int Count => changes.Count;

    public bool ContainsKey(string path) => changes.ContainsKey(path);

    public bool TryGetValue(string path, out Change value) => changes.TryGetValue(path, out value!);

    public IEnumerator<KeyValuePair<string, Change>> GetEnumerator() =>
        order.Select(path => new KeyValuePair<string, Change>(path, changes[path])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Renders the change set as a JSON object keyed by path, preserving document order.
    /// </summary>
    public string ToJson(JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(this.ToDictionary(x => x.Key, x => x.Value), options ?? DefaultJson);

    private static readonly JsonSerializerOptions DefaultJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
        // Values are JSON fragments rather than markup, so the stricter HTML-safe escaping only makes
        // them harder to read.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };
}
