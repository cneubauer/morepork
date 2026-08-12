using System.Text.Json.Nodes;

namespace WaaS.Common.Changes.UnitTests;

/// <summary>
/// Covers the two properties the path forms must hold: a dotted path uniquely identifies a leaf, and a
/// pointer resolves back to the node it describes.
/// </summary>
public class ChangePathTests
{
    [Theory]
    [InlineData("plain", "plain")]                          // nothing to escape
    [InlineData("with.dot", "'with.dot'")]                  // a dot would read as a separator
    [InlineData("with[bracket", "'with[bracket'")]          // an opening bracket would read as an index
    [InlineData("with]bracket", "'with]bracket'")]          // a closing bracket would end a segment
    [InlineData("with'quote", "'with''quote'")]             // a quote is doubled inside quotes
    public void Compare_PropertyNameWithSpecialCharacters_IsQuotedInPath(string name, string expectedSegment)
    {
        var current = new JsonObject { [name] = 1 };
        var proposed = new JsonObject { [name] = 2 };

        var changes = ChangeSet.Between(current, proposed);

        Assert.Equal(expectedSegment, Assert.Single(changes.Keys));
    }

    [Theory]
    [InlineData("a.com", "a.com")]                          // a dot inside brackets is unambiguous
    [InlineData("a]b", "'a]b'")]                            // a bracket must be quoted
    [InlineData("a'b", "'a''b'")]                           // a quote is doubled
    [InlineData(" padded ", "' padded '")]                  // edge whitespace would be lost visually
    [InlineData("", "''")]                                  // an empty key still needs a segment
    public void Compare_KeyValueWithSpecialCharacters_IsQuotedWhenAmbiguous(string keyValue, string expectedValue)
    {
        var current = Keyed(keyValue, enabled: true);
        var proposed = Keyed(keyValue, enabled: false);

        var changes = ChangeSet.Between(current, proposed, KeyedById);

        Assert.Equal($"Items[Id={expectedValue}].IsEnabled", Assert.Single(changes.Keys));
    }

    [Theory]
    [InlineData("slash/name")]                              // must become ~1
    [InlineData("tilde~name")]                              // must become ~0
    [InlineData("both~/name")]
    public void Compare_PropertyNameNeedingPointerEscape_ProducesResolvablePointer(string name)
    {
        var current = new JsonObject { [name] = 1 };
        var proposed = new JsonObject { [name] = 2 };

        var changes = ChangeSet.Between(current, proposed);
        var pointer = Assert.Single(changes.Values).Pointer;

        Assert.Equal("2", Resolve(proposed, pointer)?.ToJsonString());
    }

    [Fact]
    public void Compare_ManyAwkwardNames_ProducesUniquePaths()
    {
        // Uniqueness is what makes a dotted path usable as a persisted key, so it is asserted across a
        // set of names chosen to collide under naive escaping.
        var names = new[] { "a.b", "a", "b", "a'b", "a]b", "a[b", "a.b.c", "'a.b'", "" };

        var current = new JsonObject();
        var proposed = new JsonObject();

        foreach (var name in names)
        {
            current[name] = 1;
            proposed[name] = 2;
        }

        var changes = ChangeSet.Between(current, proposed);

        Assert.Equal(names.Length, changes.Count);
        Assert.Equal(names.Length, changes.Keys.Distinct().Count());
    }

    private static readonly ComparisonOptions KeyedById = ComparisonOptions.Default with
    {
        ArrayKeys = new Dictionary<string, string> { ["/Items"] = "Id" },
    };

    private static JsonObject Keyed(string id, bool enabled) =>
        new() { ["Items"] = new JsonArray(new JsonObject { ["Id"] = id, ["IsEnabled"] = enabled }) };

    /// <summary>Minimal RFC 6901 resolver, used to prove the emitted pointers are well formed.</summary>
    private static JsonNode? Resolve(JsonNode root, string pointer)
    {
        if (pointer.Length == 0)
        {
            return root;
        }

        var node = root;

        foreach (var rawToken in pointer.Split('/').Skip(1))
        {
            var token = rawToken.Replace("~1", "/").Replace("~0", "~");

            node = node switch
            {
                JsonObject obj => obj[token],
                JsonArray array => array[int.Parse(token)],
                _ => null,
            };
        }

        return node;
    }
}
