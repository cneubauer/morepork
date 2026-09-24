using System.Collections.Concurrent;
using System.Reflection;

namespace ObjectCompare;

/// <summary>
/// Resolves the type names recorded on a change (<see cref="IPropertyChange.TargetTypeName"/>,
/// <see cref="IListChange.ItemTypeName"/>) back to a <see cref="Type"/> by name, searching the
/// loaded non-framework assemblies. Results are cached.
/// </summary>
public static class ChangeTypeResolver
{
    private static readonly ConcurrentDictionary<string, Type?> Cache = new();

    /// <summary>
    /// Resolves a recorded type name. Returns <c>null</c> for an empty name, or for a type this
    /// process cannot see.
    /// </summary>
    public static Type? Resolve(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        return Cache.GetOrAdd(typeName, Lookup);
    }

    private static Type? Lookup(string typeName)
    {
        var direct = Type.GetType(typeName, throwOnError: false);
        if (direct is not null)
            return direct;

        var assemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !IsFrameworkAssembly(assembly));

        foreach (var assembly in assemblies)
        {
            var match = assembly.GetType(typeName, throwOnError: false);
            if (match is not null)
                return match;
        }

        return ResolveBySimpleName(typeName, assemblies);
    }

    /// <summary>Matches an unqualified name against the simple name of every visible type.</summary>
    private static Type? ResolveBySimpleName(string typeName, IEnumerable<Assembly> assemblies)
    {
        if (typeName.Contains('.'))
            return null;

        foreach (var assembly in assemblies)
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(type => type is not null)!];
            }

            var match = Array.Find(types, type => type.Name == typeName);
            if (match is not null)
                return match;
        }

        return null;
    }

    private static bool IsFrameworkAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;

        return name is null
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal)
            || name is "System" or "netstandard" or "mscorlib";
    }
}
