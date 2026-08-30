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
) : ListChange(Path, ChangeType, ItemKey, Item, ItemTypeName ?? typeof(T).Name), IListChange<T>
{
    new public T? Item
    {
        get
        {
            if (base.Item is T typed) return typed;
            if (base.Item is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            return (T?)base.Item;
        }
    }

    public override string ToString() => base.ToString();
}

public static class ChangeExtensions
{
    public static IEnumerable<IListChange<T>> OfListType<T>(this IEnumerable<IChange> changes)
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
                    var deserialized = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
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
