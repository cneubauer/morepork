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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$changeType")]
[JsonDerivedType(typeof(PropertyChange), typeDiscriminator: "property")]
[JsonDerivedType(typeof(ListChange), typeDiscriminator: "list")]
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
    new T? Item { get; }
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
    new public T? Item => (T?)base.Item;

    public override string ToString() => base.ToString();
}
