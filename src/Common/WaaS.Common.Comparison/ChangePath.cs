using System.Buffers;

namespace WaaS.Common.Comparison;

/// <summary>
/// Carries the two path representations for one node side by side: a dotted path for humans and
/// persisted keys, and an RFC 6901 pointer for navigating the compared documents.
/// </summary>
/// <remarks>
/// Both forms are built up together as the comparison descends, because neither can be derived from
/// the other: the dotted form encodes collection identity and the pointer encodes position.
/// </remarks>
internal readonly record struct ChangePath(string Dotted, string Pointer)
{
    /// <summary>The path of the document root.</summary>
    public static ChangePath Root => new("", "");

    /// <summary>Appends an object property.</summary>
    public ChangePath Property(string name) =>
        new(Dotted.Length == 0 ? EscapeSegment(name) : $"{Dotted}.{EscapeSegment(name)}",
            $"{Pointer}/{EscapePointer(name)}");

    /// <summary>Appends a positional collection item, used when no identity key is available.</summary>
    public ChangePath Index(int index) => new($"{Dotted}[{index}]", $"{Pointer}/{index}");

    /// <summary>
    /// Appends a collection item addressed by the value of its identity key. The pointer still uses
    /// <paramref name="ordinal"/> so that it remains resolvable against the document.
    /// </summary>
    public ChangePath Key(string keyName, string keyValue, int ordinal) =>
        new($"{Dotted}[{EscapeSegment(keyName)}={EscapeKeyValue(keyValue)}]", $"{Pointer}/{ordinal}");

    /// <summary>
    /// Appends a scalar collection item addressed by its own value, using the reserved pseudo-key
    /// <c>value</c>.
    /// </summary>
    public ChangePath ScalarValue(string value, int ordinal) =>
        new($"{Dotted}[value={EscapeKeyValue(value)}]", $"{Pointer}/{ordinal}");

    /// <summary>
    /// Quotes a property name if it contains a character that is significant in the dotted grammar.
    /// </summary>
    private static string EscapeSegment(string name) =>
        NeedsQuoting(name) ? Quote(name) : name;

    /// <summary>
    /// Quotes a key value if it contains a character that would make the surrounding brackets
    /// ambiguous. A <c>.</c> needs no quoting here: inside brackets it is not a separator, which keeps
    /// the common case of a domain name readable as <c>[DomainName=a.com]</c>.
    /// </summary>
    private static string EscapeKeyValue(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        var significant = value.AsSpan().IndexOfAny(KeyValueSpecials) >= 0;
        var padded = char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]);

        return significant || padded ? Quote(value) : value;
    }

    private static bool NeedsQuoting(string name) =>
        name.Length == 0 || name.AsSpan().IndexOfAny(SegmentSpecials) >= 0;

    /// <summary>Characters that would make a bracketed key value ambiguous.</summary>
    private static readonly SearchValues<char> KeyValueSpecials = SearchValues.Create("[]'");

    /// <summary>Characters that are significant in the dotted grammar.</summary>
    private static readonly SearchValues<char> SegmentSpecials = SearchValues.Create(".[]'");

    private static string Quote(string value) => $"'{value.Replace("'", "''")}'";

    /// <summary>Escapes a property name for use in an RFC 6901 pointer.</summary>
    private static string EscapePointer(string name) => name.Replace("~", "~0").Replace("/", "~1");
}
