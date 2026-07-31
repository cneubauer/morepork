using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace WaaS.Persistence;

internal static class LookupResourceExtractor
{

    private static readonly ConcurrentDictionary<Type, TypeMeta> TypeCache = new();

    internal static IEnumerable<(LookupResourceKeyType ResourceKey, string Text)> Extract(object root)
        => Walk(root, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static IEnumerable<(LookupResourceKeyType ResourceKey, string Text)> Walk(object obj, HashSet<object> visited)
    {
        if (!visited.Add(obj)) yield break;

        var type = obj.GetType();
        var meta = TypeCache.GetOrAdd(type, BuildMeta);

        foreach (var (prop, keys) in meta.LookupProps)
        {
            var value = prop.GetValue(obj);
            if (value is null) continue;
            // Skip default value for value types (e.g. ulong 0 = "not yet assigned")
            if (prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) is not null)
            {
                var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (underlying.IsValueType && value.Equals(Activator.CreateInstance(underlying)))
                    continue;
            }
            var text = value.ToString();
            if (string.IsNullOrEmpty(text)) continue;
            foreach (var key in keys)
                yield return (key, text);
        }

        foreach (var (prop, isCollection) in meta.RecurseProps)
        {
            var value = prop.GetValue(obj);
            if (value is null) continue;

            if (isCollection)
            {
                foreach (var item in (IEnumerable)value)
                    if (item is not null)
                        foreach (var entry in Walk(item, visited))
                            yield return entry;
            }
            else
            {
                foreach (var entry in Walk(value, visited))
                    yield return entry;
            }
        }

        if (obj is ILookupEntryProvider provider)
            foreach (var entry in provider.GetAdditionalLookupEntries())
                yield return entry;
    }

    private static bool IsDesiredStateType(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type.Namespace?.StartsWith("System") == true)
            return false;
        if (type.Assembly.GetName().Name?.Contains("DesiredState") == true)
            return true;
        if (typeof(ILookupEntryProvider).IsAssignableFrom(type))
            return true;
        // Also treat any type that directly declares [LookupKey] properties as a DesiredState type
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   .Any(x => x.GetCustomAttributes<LookupKeyAttribute>().Any());
    }

    private static TypeMeta BuildMeta(Type type)
    {
        var lookupProps = new List<(PropertyInfo, LookupResourceKeyType[])>();
        var recurseProps = new List<(PropertyInfo, bool isCollection)>();

        // Walk from most-derived to base so `new`-shadowed properties take precedence
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = type;
        while (current != null && current != typeof(object))
        {
            foreach (var prop in current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!seen.Add(prop.Name)) continue; // shadowed by a more-derived type
                if (prop.GetIndexParameters().Length > 0) continue;
                if (!prop.CanRead) continue;

                // Collect [LookupKey] attributes
                var keys = prop.GetCustomAttributes<LookupKeyAttribute>()
                    .Select(x => x.ResourceKey)
                    .ToArray();

                if (keys.Length > 0)
                    lookupProps.Add((prop, keys));

                // Determine if this property should be recursed into
                var pt = prop.PropertyType;

                var elemType = GetDesiredStateCollectionElementType(pt);
                if (elemType != null)
                    recurseProps.Add((prop, true));
                else if (IsDesiredStateType(pt))
                    recurseProps.Add((prop, false));
            }

            current = current.BaseType;
        }

        return new TypeMeta(lookupProps.ToArray(), recurseProps.ToArray());
    }

    private static Type? GetDesiredStateCollectionElementType(Type type)
    {
        if (!type.IsGenericType) return null;

        // Check the type itself and its interfaces for IEnumerable<T>
        var candidates = type.GetInterfaces().Prepend(type);
        foreach (var iface in candidates)
        {
            if (!iface.IsGenericType) continue;
            if (iface.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;

            var elemType = iface.GetGenericArguments()[0];
            if (IsDesiredStateType(elemType))
                return elemType;
        }

        return null;
    }

    private sealed record TypeMeta(
        (PropertyInfo Prop, LookupResourceKeyType[] Keys)[] LookupProps,
        (PropertyInfo Prop, bool IsCollection)[] RecurseProps);
}
