using System.Collections.ObjectModel;

namespace WaaS.Common.Changes;

/// <summary>
/// Controls how <see cref="ChangeSet"/> projects and compares two states.
/// </summary>
public sealed record ComparisonOptions
{
    /// <summary>
    /// The serializer used unless overridden: PascalCase names matching the CLR properties, enums as
    /// strings so they read meaningfully, and nulls written so that an absent property stays
    /// distinguishable from one explicitly set to null.
    /// </summary>
    // Declared before Default, because static initializers run in order and Default captures this.
    public static JsonSerializerOptions DefaultSerializer { get; } = new(JsonSerializerDefaults.General)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNamingPolicy = null,
    };

    /// <summary>The default options: pinned serializer, scalar collections as sets, collapsed subtrees.</summary>
    public static ComparisonOptions Default { get; } = new();

    /// <summary>
    /// The serializer used to project objects to JSON before comparing.
    /// </summary>
    /// <remarks>
    /// These settings are part of the diff contract. Property naming, converters and ignore conditions
    /// all determine what the reported paths look like and which properties are visible at all, so
    /// changing them rewrites every path a caller may have persisted. Note that this differs from the
    /// policies used elsewhere in the solution for persistence and for HTTP; pass an explicit instance
    /// when diff paths must line up with one of those representations.
    /// </remarks>
    public JsonSerializerOptions Serializer
    {
        get => serializer ?? DefaultSerializer;
        init => serializer = value;
    }

    private readonly JsonSerializerOptions? serializer;

    /// <summary>
    /// Identity keys for collections that cannot be resolved from <see cref="ListItemKeyAttribute"/>,
    /// keyed by the RFC 6901 pointer of the collection — for example <c>/Domains</c> to
    /// <c>DomainName</c>. Consulted before the attribute, so it also serves as an override.
    /// </summary>
    /// <remarks>
    /// Needed when comparing raw JSON, where there is no CLR type to carry attributes, and for models
    /// we do not own. Pointers of nested collections use the position of the containing item, so
    /// prefer the attribute wherever the type is available.
    /// </remarks>
    public IReadOnlyDictionary<string, string> ArrayKeys { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// Whether collections of scalars are compared as unordered multisets rather than positionally.
    /// Defaults to <c>true</c>, which suits tag and name lists where order carries no meaning.
    /// </summary>
    public bool ScalarArraysAsSets { get; init; } = true;

    /// <summary>
    /// Whether an added or removed object is reported as one change per leaf property rather than a
    /// single change carrying the whole subtree as JSON. Defaults to <c>false</c>, because adding one
    /// item is usually one event; enable it when changes are queried by exact leaf path.
    /// </summary>
    public bool ExpandAddedSubtrees { get; init; } = false;
}
