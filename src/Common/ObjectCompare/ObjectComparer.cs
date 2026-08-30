using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ObjectCompare;

public static class ObjectComparer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> KeyPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    public static IReadOnlyList<IChange> CompareTo<T>(this T? oldObject, T? newObject)
    {
        return Compare(oldObject, newObject);
    }

    public static IReadOnlyList<IChange> Compare<T>(T? oldObject, T? newObject, string rootPath = "")
    {
        var changes = new List<IChange>();
        var visited = new HashSet<(object, object)>(ReferenceTupleEqualityComparer.Instance);
        CompareInternal(oldObject, newObject, rootPath, "", changes, visited, typeof(T));
        return changes;
    }

    private static void CompareInternal(
        object? oldObj,
        object? newObj,
        string currentPath,
        string propName,
        List<IChange> changes,
        HashSet<(object, object)> visited,
        Type targetType)
    {
        if (ReferenceEquals(oldObj, newObj))
        {
            return;
        }

        if (oldObj is null || newObj is null)
        {
            changes.Add(new PropertyChange(
                Path: currentPath,
                PropertyName: propName,
                OldValue: oldObj,
                NewValue: newObj,
                TargetTypeName: targetType.Name
            ));
            return;
        }

        var actualType = oldObj.GetType();

        if (IsScalarType(actualType))
        {
            if (!Equals(oldObj, newObj))
            {
                changes.Add(new PropertyChange(
                    Path: currentPath,
                    PropertyName: propName,
                    OldValue: oldObj,
                    NewValue: newObj,
                    TargetTypeName: targetType.Name
                ));
            }
            return;
        }

        if (typeof(IEnumerable).IsAssignableFrom(actualType) && actualType != typeof(string))
        {
            CompareCollections(
                (IEnumerable)oldObj,
                (IEnumerable)newObj,
                currentPath,
                changes,
                visited,
                actualType);
            return;
        }

        // Prevent infinite recursion in cyclical graphs
        if (!visited.Add((oldObj, newObj)))
        {
            return;
        }

        var properties = PropertyCache.GetOrAdd(actualType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
             .ToArray()
        );

        foreach (var prop in properties)
        {
            var oldVal = prop.GetValue(oldObj);
            var newVal = prop.GetValue(newObj);
            var nextPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";

            CompareInternal(oldVal, newVal, nextPath, prop.Name, changes, visited, actualType);
        }
    }

    private static void CompareCollections(
        IEnumerable oldEnum,
        IEnumerable newEnum,
        string collectionPath,
        List<IChange> changes,
        HashSet<(object, object)> visited,
        Type collectionType)
    {
        var oldList = oldEnum.Cast<object?>().ToList();
        var newList = newEnum.Cast<object?>().ToList();

        var itemType = GetCollectionItemType(collectionType);
        var keyProperty = itemType != null ? GetKeyProperty(itemType) : null;

        if (keyProperty != null)
        {
            // Compare key-based collections
            var oldMap = new Dictionary<object, object>();
            foreach (var item in oldList)
            {
                if (item != null)
                {
                    var key = keyProperty.GetValue(item);
                    if (key != null)
                    {
                        oldMap[key] = item;
                    }
                }
            }

            var newMap = new Dictionary<object, object>();
            foreach (var item in newList)
            {
                if (item != null)
                {
                    var key = keyProperty.GetValue(item);
                    if (key != null)
                    {
                        newMap[key] = item;
                    }
                }
            }

            // Detect Added items (in new but not in old)
            foreach (var (key, newItem) in newMap)
            {
                if (!oldMap.ContainsKey(key))
                {
                    var itemPath = $"{collectionPath}[{keyProperty.Name}={key}]";
                    changes.Add(new ListChange(itemPath, ListChangeType.Added, key, newItem, itemType?.Name));
                }
            }

            // Detect Removed items (in old but not in new)
            foreach (var (key, oldItem) in oldMap)
            {
                if (!newMap.ContainsKey(key))
                {
                    var itemPath = $"{collectionPath}[{keyProperty.Name}={key}]";
                    changes.Add(new ListChange(itemPath, ListChangeType.Removed, key, oldItem, itemType?.Name));
                }
            }

            // Recurse into common items
            foreach (var (key, oldItem) in oldMap)
            {
                if (newMap.TryGetValue(key, out var newItem))
                {
                    var itemPath = $"{collectionPath}[{keyProperty.Name}={key}]";
                    CompareInternal(oldItem, newItem, itemPath, "", changes, visited, itemType!);
                }
            }
        }
        else
        {
            // Fallback for non-keyed collections: index-based comparison
            var maxCount = Math.Max(oldList.Count, newList.Count);
            for (var i = 0; i < maxCount; i++)
            {
                var oldItem = i < oldList.Count ? oldList[i] : null;
                var newItem = i < newList.Count ? newList[i] : null;
                var itemPath = $"{collectionPath}[{i}]";

                if (oldItem is null && newItem is not null)
                {
                    changes.Add(new ListChange(itemPath, ListChangeType.Added, i, newItem, itemType?.Name));
                }
                else if (oldItem is not null && newItem is null)
                {
                    changes.Add(new ListChange(itemPath, ListChangeType.Removed, i, oldItem, itemType?.Name));
                }
                else if (oldItem is not null && newItem is not null)
                {
                    CompareInternal(oldItem, newItem, itemPath, "", changes, visited, itemType ?? oldItem.GetType());
                }
            }
        }
    }

    private static Type? GetCollectionItemType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        var enumGeneric = collectionType
            .GetInterfaces()
            .Concat([collectionType])
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumGeneric?.GetGenericArguments()[0];
    }

    private static PropertyInfo? GetKeyProperty(Type type)
    {
        return KeyPropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .FirstOrDefault(p => p.GetCustomAttribute<ItemKeyAttribute>() != null)
        );
    }

    private static bool IsScalarType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType.IsPrimitive
            || actualType.IsEnum
            || actualType == typeof(string)
            || actualType == typeof(decimal)
            || actualType == typeof(Guid)
            || actualType == typeof(DateTime)
            || actualType == typeof(DateTimeOffset)
            || actualType == typeof(TimeSpan)
            || actualType == typeof(DateOnly)
            || actualType == typeof(TimeOnly);
    }

    private sealed class ReferenceTupleEqualityComparer : IEqualityComparer<(object, object)>
    {
        public static readonly ReferenceTupleEqualityComparer Instance = new();

        public bool Equals((object, object) x, (object, object) y)
        {
            return ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
        }

        public int GetHashCode((object, object) obj)
        {
            return HashCode.Combine(
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1),
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2)
            );
        }
    }
}
