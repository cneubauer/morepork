using System.Collections;
using System.Text.Encodings.Web;

namespace WaaS.Common.Changes;

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
/// <para>Create one with <see cref="Between{T}(T, T, ComparisonOptions?)"/>.</para>
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

    /// <summary>
    /// Compares two objects of the same type, resolving collection identity keys from
    /// <see cref="ListItemKeyAttribute"/> on <typeparamref name="T"/> and the types it contains.
    /// </summary>
    /// <remarks>
    /// Objects are projected to JSON before comparing, so the result describes the serialized form
    /// rather than the object graph. A property hidden from serialization — with
    /// <see cref="JsonIgnoreAttribute"/>, for instance — is therefore invisible here, which is a
    /// convenient way to keep secrets out of a persisted change record. Object graphs containing cycles
    /// cannot be projected and will throw.
    /// </remarks>
    public static ChangeSet Between<T>(T? current, T? proposed, ComparisonOptions? options = null)
    {
        var effective = options ?? ComparisonOptions.Default;

        var currentNode = JsonSerializer.SerializeToNode(current, effective.Serializer);
        var proposedNode = JsonSerializer.SerializeToNode(proposed, effective.Serializer);

        return JsonComparer.Compare(
            currentNode,
            proposedNode,
            effective,
            ListItemKeyResolver.Resolve(typeof(T), effective));
    }

    /// <summary>
    /// Compares two JSON documents. Collection identity keys come from
    /// <see cref="ComparisonOptions.ArrayKeys"/> only, since there is no type to carry attributes.
    /// </summary>
    public static ChangeSet Between(JsonNode? current, JsonNode? proposed, ComparisonOptions? options = null)
    {
        var effective = options ?? ComparisonOptions.Default;

        return JsonComparer.Compare(current, proposed, effective, effective.ArrayKeys);
    }

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
