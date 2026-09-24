using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObjectCompare;

[AttributeUsage(AttributeTargets.Property)]
public class ItemKeyAttribute : Attribute
{
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeKind
{
    Property,
    List
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ListChangeType
{
    Added,
    Removed
}

[JsonConverter(typeof(ChangeJsonConverter))]
public interface IChange
{
    string Path { get; }
    ChangeKind Kind { get; }
}

public interface IPropertyChange : IChange
{
    string PropertyName { get; }
    object? OldValue { get; }
    object? NewValue { get; }
    string? TargetTypeName { get; }
}

public interface IListChange : IChange
{
    ListChangeType ChangeType { get; }
    object? ItemKey { get; }
    object? Item { get; }
    string? ItemTypeName { get; }
}

public interface IListChange<out T> : IListChange
{
    new T Item { get; }
}

public record PropertyChange(
    string Path,
    string PropertyName,
    object? OldValue,
    object? NewValue,
    string? TargetTypeName = null
) : IPropertyChange
{
    public ChangeKind Kind => ChangeKind.Property;

    public override string ToString() =>
        $"[Property] {Path}: '{OldValue ?? "<null>"}' => '{NewValue ?? "<null>"}'";
}

public record ListChange(
    string Path,
    ListChangeType ChangeType,
    object? ItemKey,
    object? Item,
    string? ItemTypeName = null
) : IListChange
{
    public ChangeKind Kind => ChangeKind.List;

    public override string ToString() =>
        $"[List:{ChangeType}] {Path} (Key: {ItemKey ?? "<null>"}, Type: {ItemTypeName ?? Item?.GetType().Name ?? "unknown"})";
}

public record ListChange<T>(
    string Path,
    ListChangeType ChangeType,
    object? ItemKey,
    T? Item,
    string? ItemTypeName = null
) : ListChange(Path, ChangeType, ItemKey, Item, ItemTypeName ?? typeof(T).FullName), IListChange<T>
{
    new public T? Item
    {
        get
        {
            if (base.Item is T typed) return typed;
            if (base.Item is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), ChangeExtensions.DeserializeOptions);
            }
            return (T?)base.Item;
        }
    }

    public override string ToString() => base.ToString();
}

/// <summary>
/// A change concerning an object of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// <see cref="OldValue"/> and <see cref="NewValue"/> hold the subject itself, and are only
/// populated where the underlying change carries it: a list item added or removed, or the subject
/// set to or from <c>null</c>. Both are <c>null</c> when a single property of the subject changed.
/// <see cref="ChangeExtensions.OfProperty{T, TProperty}"/> reads a property across either case.
/// </remarks>
public interface IObjectChange<out T> : IChange
{
    T? OldValue { get; }
    T? NewValue { get; }

    /// <summary>The change this was projected from.</summary>
    IChange Change { get; }
}

/// <summary>How a single property of a subject changed.</summary>
public interface IPropertyValueChange<out TProperty> : IChange
{
    TProperty? OldValue { get; }
    TProperty? NewValue { get; }
}

public record ObjectChange<T>(
    string Path,
    T? OldValue,
    T? NewValue,
    IChange Change
) : IObjectChange<T>
{
    public ChangeKind Kind => Change.Kind;

    public override string ToString() => $"[Object:{typeof(T).Name}] {Path}";
}

public record PropertyValueChange<TProperty>(
    string Path,
    TProperty? OldValue,
    TProperty? NewValue,
    ChangeKind Kind
) : IPropertyValueChange<TProperty>
{
    public override string ToString() =>
        $"[Value] {Path}: '{OldValue?.ToString() ?? "<null>"}' => '{NewValue?.ToString() ?? "<null>"}'";
}

public static class ChangeExtensions
{
    /// <summary>
    /// Changes to list membership whose item is a <typeparamref name="T"/>. Use this when the
    /// <see cref="IListChange.ChangeType"/> or <see cref="IListChange.ItemKey"/> matters; use
    /// <see cref="OfObject{T}"/> when you only care that a <typeparamref name="T"/> changed.
    /// </summary>
    public static IEnumerable<IListChange<T>> OfList<T>(this IEnumerable<IChange> changes)
    {
        foreach (var change in changes)
        {
            if (change is IListChange<T> genericChange)
            {
                yield return genericChange;
            }
            else if (change is IListChange listChange)
            {
                if (listChange.Item is T typedItem)
                {
                    yield return new ListChange<T>(
                        listChange.Path,
                        listChange.ChangeType,
                        listChange.ItemKey,
                        typedItem,
                        listChange.ItemTypeName
                    );
                }
                else if (listChange.Item is JsonElement jsonElement)
                {
                    var deserialized = Deserialize<T>(jsonElement);
                    yield return new ListChange<T>(
                        listChange.Path,
                        listChange.ChangeType,
                        listChange.ItemKey,
                        deserialized,
                        listChange.ItemTypeName
                    );
                }
            }
        }
    }

    /// <summary>
    /// Every change concerning an object of type <typeparamref name="T"/>, whichever shape it
    /// arrived in: a property of the object changing, the object itself being added or removed
    /// from a list, or the object being set to or from <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The object is identified by the type name recorded on the change. Pair with
    /// <see cref="OfProperty{T, TProperty}"/> to read a value.
    /// </remarks>
    public static IEnumerable<IObjectChange<T>> OfObject<T>(this IEnumerable<IChange> changes)
    {
        foreach (var change in changes)
        {
            switch (change)
            {
                // A property of the subject changed; the change carries that property's values.
                case IPropertyChange propertyChange when DeclaringTypeIs<T>(propertyChange):
                    yield return new ObjectChange<T>(propertyChange.Path, default, default, change);
                    break;

                // The subject is the property's value, set to or from null.
                case IPropertyChange propertyChange when PropertyTypeIs<T>(propertyChange):
                {
                    var subjectType = DeclaredPropertyType(propertyChange);
                    yield return new ObjectChange<T>(
                        propertyChange.Path,
                        Materialize<T>(propertyChange.OldValue, subjectType),
                        Materialize<T>(propertyChange.NewValue, subjectType),
                        change
                    );
                    break;
                }

                case IListChange listChange when ItemTypeIs<T>(listChange):
                {
                    var item = Materialize<T>(listChange.Item, ChangeTypeResolver.Resolve(listChange.ItemTypeName));
                    yield return new ObjectChange<T>(
                        listChange.Path,
                        listChange.ChangeType == ListChangeType.Removed ? item : default,
                        listChange.ChangeType == ListChangeType.Added ? item : default,
                        change
                    );
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Projects changes about a <typeparamref name="T"/> onto a single property of it, reporting
    /// how that property's value changed. Changes that left the property untouched are dropped.
    /// </summary>
    public static IEnumerable<IPropertyValueChange<TProperty>> OfProperty<T, TProperty>(
        this IEnumerable<IObjectChange<T>> changes,
        Expression<Func<T, TProperty>> selector)
    {
        var property = PropertyOf(selector);

        foreach (var change in changes)
        {
            // The change is this property itself.
            if (change.Change is IPropertyChange propertyChange
                && propertyChange.PropertyName == property.Name)
            {
                yield return new PropertyValueChange<TProperty>(
                    change.Path,
                    Coerce<TProperty>(propertyChange.OldValue),
                    Coerce<TProperty>(propertyChange.NewValue),
                    change.Kind
                );
                continue;
            }

            // Otherwise the property is read off the subject. A change to a different property of
            // the subject materializes neither side, and drops out here.
            if (change.OldValue is null && change.NewValue is null)
                continue;

            yield return new PropertyValueChange<TProperty>(
                change.Path,
                Coerce<TProperty>(ReadProperty(property, change.OldValue)),
                Coerce<TProperty>(ReadProperty(property, change.NewValue)),
                change.Kind
            );
        }
    }

    private static bool DeclaringTypeIs<T>(IPropertyChange change) =>
        ChangeTypeResolver.Resolve(change.TargetTypeName) is { } declaringType
        && declaringType.IsAssignableTo(typeof(T));

    private static bool PropertyTypeIs<T>(IPropertyChange change) =>
        DeclaredPropertyType(change) is { } propertyType
        && propertyType.IsAssignableTo(typeof(T));

    private static bool ItemTypeIs<T>(IListChange change) =>
        ChangeTypeResolver.Resolve(change.ItemTypeName) is { } itemType
        && itemType.IsAssignableTo(typeof(T));

    private static Type? DeclaredPropertyType(IPropertyChange change) =>
        ChangeTypeResolver.Resolve(change.TargetTypeName)
            ?.GetProperty(change.PropertyName)
            ?.PropertyType;

    private static PropertyInfo PropertyOf<T, TProperty>(Expression<Func<T, TProperty>> selector) =>
        selector.Body is MemberExpression { Member: PropertyInfo property }
            ? property
            : throw new ArgumentException(
                $"Expected a property access, but got '{selector.Body}'.",
                nameof(selector));

    private static object? ReadProperty(PropertyInfo property, object? subject) =>
        subject is null ? null : property.GetValue(subject);

    /// <summary>
    /// Rehydrates a subject that arrived as JSON into <paramref name="subjectType"/>, the concrete
    /// type resolved from the change. <typeparamref name="T"/> may be an interface.
    /// </summary>
    private static T? Materialize<T>(object? value, Type? subjectType) => value switch
    {
        T typed => typed,
        JsonElement element when subjectType is not null =>
            (T?)JsonSerializer.Deserialize(element.GetRawText(), subjectType, DeserializeOptions),
        _ => default,
    };

    private static TProperty? Coerce<TProperty>(object? value) => value switch
    {
        TProperty typed => typed,
        JsonElement element => Deserialize<TProperty>(element),
        _ => default,
    };

    private static T? Deserialize<T>(JsonElement element) =>
        typeof(T).IsAbstract || typeof(T).IsInterface
            ? default
            : JsonSerializer.Deserialize<T>(element.GetRawText(), DeserializeOptions);

    /// <summary>
    /// A change travels as camelCase through the outbox and as PascalCase through Temporal, so
    /// property names are matched without regard to casing.
    /// </summary>
    internal static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public class ChangeJsonConverter : JsonConverter<IChange>
{
    public override IChange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var hasDiscriminator = root.TryGetProperty("$changeType", out var changeTypeProp)
            || root.TryGetProperty("kind", out changeTypeProp)
            || root.TryGetProperty("Kind", out changeTypeProp);

        if (!hasDiscriminator)
        {
            throw new JsonException("Missing $changeType discriminator for IChange.");
        }

        var changeType = changeTypeProp.GetString();
        if (string.Equals(changeType, "property", StringComparison.OrdinalIgnoreCase))
        {
            var path = GetString(root, "path", "Path") ?? "";
            var propName = GetString(root, "propertyName", "PropertyName") ?? "";
            var targetTypeName = GetString(root, "targetTypeName", "TargetTypeName");
            var oldValue = GetValue(root, "oldValue", "OldValue", options);
            var newValue = GetValue(root, "newValue", "NewValue", options);
            return new PropertyChange(path, propName, oldValue, newValue, targetTypeName);
        }
        else if (string.Equals(changeType, "list", StringComparison.OrdinalIgnoreCase))
        {
            var path = GetString(root, "path", "Path") ?? "";
            var itemTypeName = GetString(root, "itemTypeName", "ItemTypeName");
            var changeTypeEnum = GetEnum<ListChangeType>(root, "changeType", "ChangeType");
            var itemKey = GetValue(root, "itemKey", "ItemKey", options);
            var item = GetValue(root, "item", "Item", options);
            return new ListChange(path, changeTypeEnum, itemKey, item, itemTypeName);
        }

        throw new JsonException($"Unknown $changeType '{changeType}' for IChange.");
    }

    public override void Write(Utf8JsonWriter writer, IChange value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case IPropertyChange prop:
                writer.WriteStartObject();
                writer.WriteString("$changeType", "property");
                writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IChange.Path)) ?? "path", prop.Path);
                writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IChange.Kind)) ?? "kind", nameof(ChangeKind.Property));
                writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IPropertyChange.PropertyName)) ?? "propertyName", prop.PropertyName);

                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(IPropertyChange.OldValue)) ?? "oldValue");
                JsonSerializer.Serialize(writer, prop.OldValue, options);

                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(IPropertyChange.NewValue)) ?? "newValue");
                JsonSerializer.Serialize(writer, prop.NewValue, options);

                if (prop.TargetTypeName is not null)
                {
                    writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IPropertyChange.TargetTypeName)) ?? "targetTypeName", prop.TargetTypeName);
                }
                writer.WriteEndObject();
                break;

            case IListChange list:
                writer.WriteStartObject();
                writer.WriteString("$changeType", "list");
                writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IChange.Path)) ?? "path", list.Path);
                writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IChange.Kind)) ?? "kind", nameof(ChangeKind.List));
                writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IListChange.ChangeType)) ?? "changeType", list.ChangeType.ToString());

                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(IListChange.ItemKey)) ?? "itemKey");
                JsonSerializer.Serialize(writer, list.ItemKey, options);

                writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(IListChange.Item)) ?? "item");
                JsonSerializer.Serialize(writer, list.Item, options);

                if (list.ItemTypeName is not null)
                {
                    writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(IListChange.ItemTypeName)) ?? "itemTypeName", list.ItemTypeName);
                }
                writer.WriteEndObject();
                break;

            default:
                throw new JsonException($"Unsupported IChange type '{value.GetType().FullName}'.");
        }
    }

    private static string? GetString(JsonElement root, string name1, string name2)
    {
        if (root.TryGetProperty(name1, out var p) || root.TryGetProperty(name2, out p))
        {
            return p.GetString();
        }
        return null;
    }

    private static T GetEnum<T>(JsonElement root, string name1, string name2) where T : struct, Enum
    {
        var str = GetString(root, name1, name2);
        if (str != null && Enum.TryParse<T>(str, true, out var val))
        {
            return val;
        }
        return default;
    }

    private static object? GetValue(JsonElement root, string name1, string name2, JsonSerializerOptions options)
    {
        if (root.TryGetProperty(name1, out var p) || root.TryGetProperty(name2, out p))
        {
            return p.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.TryGetInt64(out var l) ? l : p.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => p.Clone()
            };
        }
        return null;
    }
}
