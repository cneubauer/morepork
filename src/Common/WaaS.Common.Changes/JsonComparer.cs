namespace WaaS.Common.Changes;

/// <summary>
/// The recursive comparison over two JSON trees. Operates purely on <see cref="JsonNode"/> — all type
/// information has already been reduced to the resolved identity keys it is handed.
/// </summary>
internal sealed class JsonComparer
{
    /// <summary>
    /// Matches the serializer's own nesting limit, so a tree handed to us directly cannot recurse
    /// further than one we produced ourselves.
    /// </summary>
    private const int MaxDepth = 64;

    private readonly ComparisonOptions options;
    private readonly IReadOnlyDictionary<string, string> arrayKeys;
    private readonly Dictionary<string, Change> changes = [];
    private readonly List<string> order = [];

    private JsonComparer(ComparisonOptions options, IReadOnlyDictionary<string, string> arrayKeys)
    {
        this.options = options;
        this.arrayKeys = arrayKeys;
    }

    internal static ChangeSet Compare(
        JsonNode? current,
        JsonNode? proposed,
        ComparisonOptions options,
        IReadOnlyDictionary<string, string> arrayKeys)
    {
        var engine = new JsonComparer(options, arrayKeys);
        engine.DiffNode(current, proposed, ChangePath.Root, 0);

        return engine.changes.Count == 0 ? ChangeSet.Empty : new ChangeSet(engine.changes, engine.order);
    }

    private void DiffNode(JsonNode? current, JsonNode? proposed, ChangePath path, int depth)
    {
        if (JsonNode.DeepEquals(current, proposed))
        {
            return;
        }

        if (depth > MaxDepth)
        {
            throw new InvalidOperationException(
                $"Comparison exceeded the maximum depth of {MaxDepth} at '{path.Dotted}'.");
        }

        var currentKind = Kind(current);
        var proposedKind = Kind(proposed);

        // A change of shape — including to or from null, and between object and array — is reported at
        // this path rather than descended into, because pairing the members of two different shapes
        // produces meaningless comparisons.
        if (currentKind != proposedKind)
        {
            Record(path, ChangeType.Modified, Render(current), Render(proposed));
            return;
        }

        switch (currentKind)
        {
            case JsonValueKind.Object:
                DiffObject(current!.AsObject(), proposed!.AsObject(), path, depth);
                break;

            case JsonValueKind.Array:
                DiffArray(current!.AsArray(), proposed!.AsArray(), path, depth);
                break;

            default:
                Record(path, ChangeType.Modified, Render(current), Render(proposed));
                break;
        }
    }

    private void DiffObject(JsonObject current, JsonObject proposed, ChangePath path, int depth)
    {
        // Current's order first, then properties only present in proposed, so the output order is
        // deterministic and stable against reordered serialization.
        foreach (var (name, currentValue) in current)
        {
            var itemPath = path.Property(name);

            if (proposed.TryGetPropertyValue(name, out var proposedValue))
            {
                DiffNode(currentValue, proposedValue, itemPath, depth + 1);
            }
            else
            {
                EmitSubtree(ChangeType.Removed, currentValue, itemPath, depth);
            }
        }

        foreach (var (name, proposedValue) in proposed)
        {
            if (!current.ContainsKey(name))
            {
                EmitSubtree(ChangeType.Added, proposedValue, path.Property(name), depth);
            }
        }
    }

    private void DiffArray(JsonArray current, JsonArray proposed, ChangePath path, int depth)
    {
        var keyName = ResolveKey(path.Pointer);

        if (keyName is not null && CanMatchByKey(current, proposed, keyName))
        {
            DiffKeyed(current, proposed, keyName, path, depth);
        }
        else if (options.ScalarArraysAsSets && AllScalars(current) && AllScalars(proposed))
        {
            DiffScalarSet(current, proposed, path);
        }
        else
        {
            DiffByIndex(current, proposed, path, depth);
        }
    }

    /// <summary>
    /// Pairs items by the value of their identity key, so that inserting, removing or reordering items
    /// does not disturb the paths of unrelated items.
    /// </summary>
    private void DiffKeyed(JsonArray current, JsonArray proposed, string keyName, ChangePath path, int depth)
    {
        var proposedByKey = IndexByKey(proposed, keyName);

        for (var ordinal = 0; ordinal < current.Count; ordinal++)
        {
            var item = current[ordinal]!;
            var keyValue = KeyValueOf(item, keyName)!;

            if (proposedByKey.TryGetValue(keyValue, out var match))
            {
                DiffNode(item, match.Node, path.Key(keyName, keyValue, match.Ordinal), depth + 1);
            }
            else
            {
                EmitSubtree(ChangeType.Removed, item, path.Key(keyName, keyValue, ordinal), depth);
            }
        }

        var currentKeys = IndexByKey(current, keyName);

        for (var ordinal = 0; ordinal < proposed.Count; ordinal++)
        {
            var item = proposed[ordinal]!;
            var keyValue = KeyValueOf(item, keyName)!;

            if (!currentKeys.ContainsKey(keyValue))
            {
                EmitSubtree(ChangeType.Added, item, path.Key(keyName, keyValue, ordinal), depth);
            }
        }
    }

    /// <summary>
    /// Compares scalar collections as unordered multisets, so a reorder reports nothing and a
    /// duplicated value is accounted for once per occurrence.
    /// </summary>
    private void DiffScalarSet(JsonArray current, JsonArray proposed, ChangePath path)
    {
        var remaining = new Dictionary<string, int>();

        foreach (var item in proposed)
        {
            var value = Render(item) ?? NullPlaceholder;
            remaining[value] = remaining.GetValueOrDefault(value) + 1;
        }

        for (var ordinal = 0; ordinal < current.Count; ordinal++)
        {
            var value = Render(current[ordinal]) ?? NullPlaceholder;

            if (remaining.TryGetValue(value, out var count) && count > 0)
            {
                remaining[value] = count - 1;
            }
            else
            {
                Record(path.ScalarValue(value, ordinal), ChangeType.Removed, value, null);
            }
        }

        for (var ordinal = 0; ordinal < proposed.Count; ordinal++)
        {
            var value = Render(proposed[ordinal]) ?? NullPlaceholder;

            if (remaining.TryGetValue(value, out var count) && count > 0)
            {
                remaining[value] = count - 1;
                Record(path.ScalarValue(value, ordinal), ChangeType.Added, null, value);
            }
        }
    }

    /// <summary>
    /// Positional comparison, used when no identity key applies. Inserting an item shifts every
    /// following item, so each is reported as modified.
    /// </summary>
    private void DiffByIndex(JsonArray current, JsonArray proposed, ChangePath path, int depth)
    {
        var shared = Math.Min(current.Count, proposed.Count);

        for (var index = 0; index < shared; index++)
        {
            DiffNode(current[index], proposed[index], path.Index(index), depth + 1);
        }

        for (var index = shared; index < current.Count; index++)
        {
            EmitSubtree(ChangeType.Removed, current[index], path.Index(index), depth);
        }

        for (var index = shared; index < proposed.Count; index++)
        {
            EmitSubtree(ChangeType.Added, proposed[index], path.Index(index), depth);
        }
    }

    /// <summary>
    /// Records an added or removed node, either as one change carrying the whole subtree as JSON or,
    /// when <see cref="ComparisonOptions.ExpandAddedSubtrees"/> is set, as one change per leaf.
    /// </summary>
    private void EmitSubtree(ChangeType changeType, JsonNode? node, ChangePath path, int depth)
    {
        var isContainer = node is JsonObject or JsonArray;

        if (!options.ExpandAddedSubtrees || !isContainer)
        {
            var rendered = Render(node);
            Record(
                path,
                changeType,
                changeType == ChangeType.Removed ? rendered : null,
                changeType == ChangeType.Added ? rendered : null);
            return;
        }

        if (depth > MaxDepth)
        {
            throw new InvalidOperationException(
                $"Comparison exceeded the maximum depth of {MaxDepth} at '{path.Dotted}'.");
        }

        switch (node)
        {
            case JsonObject nested when nested.Count > 0:
                foreach (var (name, value) in nested)
                {
                    EmitSubtree(changeType, value, path.Property(name), depth + 1);
                }
                break;

            case JsonArray nested when nested.Count > 0:
                for (var index = 0; index < nested.Count; index++)
                {
                    EmitSubtree(changeType, nested[index], path.Index(index), depth + 1);
                }
                break;

            // An empty container has no leaves, so report the container itself.
            default:
                var rendered = Render(node);
                Record(
                    path,
                    changeType,
                    changeType == ChangeType.Removed ? rendered : null,
                    changeType == ChangeType.Added ? rendered : null);
                break;
        }
    }

    private void Record(ChangePath path, ChangeType changeType, string? current, string? proposed)
    {
        var change = new Change(changeType, current, proposed) { Pointer = path.Pointer };

        if (changes.TryAdd(path.Dotted, change))
        {
            order.Add(path.Dotted);
        }
    }

    private string? ResolveKey(string arrayPointer) =>
        arrayKeys.TryGetValue(arrayPointer, out var keyName) ? keyName : null;

    /// <summary>
    /// Keyed matching applies only when every item on both sides is an object carrying a usable key,
    /// and no key value repeats. A partial match would pair items by a wrong identity, which is worse
    /// than falling back to positional comparison.
    /// </summary>
    private static bool CanMatchByKey(JsonArray current, JsonArray proposed, string keyName) =>
        HasDistinctKeys(current, keyName) && HasDistinctKeys(proposed, keyName);

    private static bool HasDistinctKeys(JsonArray items, string keyName)
    {
        var seen = new HashSet<string>();

        foreach (var item in items)
        {
            if (item is not JsonObject)
            {
                return false;
            }

            var keyValue = KeyValueOf(item, keyName);

            if (keyValue is null || !seen.Add(keyValue))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, (JsonNode Node, int Ordinal)> IndexByKey(JsonArray items, string keyName)
    {
        var index = new Dictionary<string, (JsonNode, int)>(items.Count);

        for (var ordinal = 0; ordinal < items.Count; ordinal++)
        {
            var item = items[ordinal]!;
            index[KeyValueOf(item, keyName)!] = (item, ordinal);
        }

        return index;
    }

    /// <summary>
    /// Reads an item's identity value. Only scalars can identify an item, and a null or absent key
    /// makes the item unmatchable.
    /// </summary>
    private static string? KeyValueOf(JsonNode? item, string keyName)
    {
        if (item is not JsonObject candidate
            || !candidate.TryGetPropertyValue(keyName, out var keyNode)
            || keyNode is null
            || keyNode is JsonObject or JsonArray)
        {
            return null;
        }

        return Render(keyNode);
    }

    private static bool AllScalars(JsonArray items) =>
        items.All(x => x is null or JsonValue);

    private static JsonValueKind Kind(JsonNode? node) => node?.GetValueKind() ?? JsonValueKind.Null;

    /// <summary>
    /// Renders a node for display and storage: strings unquoted, other scalars as written, containers
    /// as compact JSON. Returns <c>null</c> for a missing or JSON-null node.
    /// </summary>
    private static string? Render(JsonNode? node) => node?.GetValueKind() switch
    {
        null or JsonValueKind.Null => null,
        JsonValueKind.String => node!.GetValue<string>(),
        _ => node!.ToJsonString(),
    };

    /// <summary>Stands in for a JSON null inside a scalar collection, where a key is always needed.</summary>
    private const string NullPlaceholder = "null";
}
