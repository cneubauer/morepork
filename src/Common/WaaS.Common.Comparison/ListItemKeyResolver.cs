using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace WaaS.Common.Comparison;

/// <summary>
/// Finds the identity key of every collection reachable from a type, so the comparison itself can run
/// without reflection.
/// </summary>
/// <remarks>
/// Types are finite whereas a document may hold any number of items, so the whole type graph is walked
/// once and the result cached. Pointers address a collection's items as <c>/0</c>, matching how
/// <see cref="JsonComparer"/> addresses nested collections.
/// </remarks>
internal static class ListItemKeyResolver
{
    private static readonly ConcurrentDictionary<(Type, JsonSerializerOptions), IReadOnlyDictionary<string, string>>
        ResolvedGraphs = new();

    private static readonly ConcurrentDictionary<(Type, JsonSerializerOptions), string?> KeyNames = new();

    /// <summary>
    /// Maps the pointer of every collection reachable from <paramref name="root"/> to that collection's
    /// identity key, with <see cref="ComparisonOptions.ArrayKeys"/> layered on top as an override.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Resolve(Type root, ComparisonOptions options)
    {
        var fromAttributes = ResolvedGraphs.GetOrAdd((root, options.Serializer), key =>
        {
            var resolved = new Dictionary<string, string>();
            Walk(key.Item1, "", resolved, [], options);

            return resolved;
        });

        if (options.ArrayKeys.Count == 0)
        {
            return fromAttributes;
        }

        // Caller-supplied keys win, so they can override or supplement what the attributes describe.
        var combined = new Dictionary<string, string>(fromAttributes);

        foreach (var (pointer, keyName) in options.ArrayKeys)
        {
            combined[pointer] = keyName;
        }

        return combined;
    }

    private static void Walk(
        Type type,
        string pointer,
        Dictionary<string, string> resolved,
        HashSet<Type> visiting,
        ComparisonOptions options)
    {
        // Scalars cannot contain a collection, so there is nothing below them to resolve.
        if (IsScalar(type))
        {
            return;
        }

        // Recursive models are legitimate; only the type walk needs to stop, not the comparison.
        if (!visiting.Add(type))
        {
            return;
        }

        try
        {
            if (ElementTypeOf(type) is { } elementType)
            {
                if (KeyNameOf(elementType, options) is { } keyName)
                {
                    resolved[pointer] = keyName;
                }

                Walk(elementType, $"{pointer}/0", resolved, visiting, options);
                return;
            }

            if (!TryGetTypeInfo(type, options, out var typeInfo))
            {
                return;
            }

            foreach (var property in typeInfo.Properties)
            {
                Walk(property.PropertyType, $"{pointer}/{EscapePointer(property.Name)}", resolved, visiting, options);
            }
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    /// <summary>
    /// The JSON name of the property marked with <see cref="ListItemKeyAttribute"/>, taken from the
    /// serializer contract so that a renamed property is reported under the name the comparison sees.
    /// </summary>
    private static string? KeyNameOf(Type type, ComparisonOptions options) =>
        KeyNames.GetOrAdd((type, options.Serializer), key =>
        {
            if (!TryGetTypeInfo(key.Item1, options, out var typeInfo))
            {
                return null;
            }

            return typeInfo.Properties
                .FirstOrDefault(x =>
                    (x.AttributeProvider as MemberInfo)?.IsDefined(typeof(ListItemKeyAttribute), inherit: true) == true)
                ?.Name;
        });

    private static bool TryGetTypeInfo(Type type, ComparisonOptions options, out JsonTypeInfo typeInfo)
    {
        try
        {
            typeInfo = options.Serializer.GetTypeInfo(type);
            return true;
        }
        catch (Exception)
        {
            // Types the serializer cannot describe — open generics, ref structs, void, pointers — carry
            // no identity keys and need no walking.
            typeInfo = null!;
            return false;
        }
    }

    /// <summary>
    /// Whether the type serializes as a JSON scalar and so cannot contain a keyed collection.
    /// </summary>
    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(Uri)
            || underlying == typeof(Version);
    }

    /// <summary>
    /// The item type of an enumerable, or <c>null</c> when the type is not one the comparison will see
    /// as a JSON array. Strings enumerate as characters but serialize as scalars, and dictionaries
    /// serialize as JSON objects.
    /// </summary>
    private static Type? ElementTypeOf(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        var interfaces = type.GetInterfaces().Concat([type]).Where(x => x.IsGenericType).ToArray();

        if (interfaces.Any(x => x.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
        {
            return null;
        }

        return interfaces
            .FirstOrDefault(x => x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static string EscapePointer(string name) => name.Replace("~", "~0").Replace("/", "~1");
}
