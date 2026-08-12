namespace WaaS.Common.Comparison;

/// <summary>
/// Compares two states and reports what changed.
/// </summary>
/// <remarks>
/// <para>
/// Objects are projected to JSON before comparing, so the result describes the serialized form rather
/// than the object graph. A property hidden from serialization — with <see cref="JsonIgnoreAttribute"/>,
/// for instance — is therefore invisible here, which is a convenient way to keep secrets out of a
/// persisted change record. Object graphs containing cycles cannot be projected and will throw.
/// </para>
/// <para>
/// Collections of objects are matched by identity wherever <see cref="ListItemKeyAttribute"/> or
/// <see cref="ComparisonOptions.ArrayKeys"/> supplies a key, and positionally otherwise.
/// </para>
/// <para>The result describes changes; it is not a patch and cannot be applied.</para>
/// </remarks>
public static class Changes
{
    /// <summary>
    /// Compares two objects of the same type, resolving collection identity keys from
    /// <see cref="ListItemKeyAttribute"/> on <typeparamref name="T"/> and the types it contains.
    /// </summary>
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
}
