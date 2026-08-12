using System.Text.Json;
using System.Text.Json.Nodes;

namespace WaaS.Common.Comparison.UnitTests;

public class ChangeSetTests
{
    private static readonly ComparisonOptions KeyedDomains = ComparisonOptions.Default with
    {
        ArrayKeys = new Dictionary<string, string> { ["/Domains"] = "DomainName" },
    };

    [Theory]
    [InlineData("""{"A":{"B":1}}""", """{"A":{"B":2}}""",
        """"{"A.B":{"ChangeType":"Modified","Current":"1","New":"2","Pointer":"/A/B"}}"""")]                 // nested scalar
    [InlineData("""{"A":1}""", """{"A":1}""", "{}")]                                                        // identical
    [InlineData("""{"A":1}""", """{"A":1.0}""", "{}")]                                                      // numbers compare by value
    [InlineData("""{"A":1}""", """{"A":1.5}""",
        """"{"A":{"ChangeType":"Modified","Current":"1","New":"1.5","Pointer":"/A"}}"""")]                   // number changed
    [InlineData("""{"A":1}""", """{"A":"1"}""",
        """"{"A":{"ChangeType":"Modified","Current":"1","New":"1","Pointer":"/A"}}"""")]                     // type change, identical rendering
    [InlineData("{}", """{"A":{"B":1,"C":2}}""",
        """"{"A":{"ChangeType":"Added","Current":null,"New":"{\"B\":1,\"C\":2}","Pointer":"/A"}}"""")]       // subtree added as one row
    [InlineData("""{"A":1}""", "{}",
        """"{"A":{"ChangeType":"Removed","Current":"1","New":null,"Pointer":"/A"}}"""")]                     // property removed
    [InlineData("""{"A":null}""", """{"A":1}""",
        """"{"A":{"ChangeType":"Modified","Current":null,"New":"1","Pointer":"/A"}}"""")]                    // null to value is Modified
    [InlineData("{}", """{"A":null}""",
        """"{"A":{"ChangeType":"Added","Current":null,"New":null,"Pointer":"/A"}}"""")]                      // absent to null is Added
    [InlineData("""{"A":{"B":1}}""", """{"A":[1]}""",
        """"{"A":{"ChangeType":"Modified","Current":"{\"B\":1}","New":"[1]","Pointer":"/A"}}"""")]           // object to array, no descent
    [InlineData("""{"A":"x"}""", """{"A":"y"}""",
        """"{"A":{"ChangeType":"Modified","Current":"x","New":"y","Pointer":"/A"}}"""")]                     // strings render unquoted
    public void Compare_Json_ProducesExpectedChangeSet(string current, string proposed, string expected)
    {
        var changes = Changes.Between(JsonNode.Parse(current), JsonNode.Parse(proposed));

        Assert.Equal(Canonical(expected), Canonical(changes.ToJson()));
    }

    [Theory]
    [InlineData("""{"Domains":[{"DomainName":"a"}]}""",
        """{"Domains":[{"DomainName":"z"},{"DomainName":"a"}]}""",
        """"{"Domains[DomainName=z]":{"ChangeType":"Added","Current":null,"New":"{\"DomainName\":\"z\"}","Pointer":"/Domains/0"}}"""")]  // head insert does not cascade
    [InlineData("""{"Domains":[{"DomainName":"a"},{"DomainName":"b"}]}""",
        """{"Domains":[{"DomainName":"b"},{"DomainName":"a"}]}""",
        "{}")]                                                                                              // reorder is not a change
    [InlineData("""{"Domains":[{"DomainName":"a","IsEnabled":true}]}""",
        """{"Domains":[{"DomainName":"a","IsEnabled":false}]}""",
        """"{"Domains[DomainName=a].IsEnabled":{"ChangeType":"Modified","Current":"true","New":"false","Pointer":"/Domains/0/IsEnabled"}}"""")]  // keyed item modified
    [InlineData("""{"Domains":[{"DomainName":"a"},{"DomainName":"b"}]}""",
        """{"Domains":[{"DomainName":"a"}]}""",
        """"{"Domains[DomainName=b]":{"ChangeType":"Removed","Current":"{\"DomainName\":\"b\"}","New":null,"Pointer":"/Domains/1"}}"""")]  // keyed removal
    [InlineData("""{"Domains":[{"DomainName":"a.com","IsEnabled":true}]}""",
        """{"Domains":[{"DomainName":"a.com","IsEnabled":false}]}""",
        """"{"Domains[DomainName=a.com].IsEnabled":{"ChangeType":"Modified","Current":"true","New":"false","Pointer":"/Domains/0/IsEnabled"}}"""")]  // a dot in a key needs no quoting
    [InlineData("""{"Domains":[{"DomainName":"a]b","IsEnabled":true}]}""",
        """{"Domains":[{"DomainName":"a]b","IsEnabled":false}]}""",
        """"{"Domains[DomainName='a]b'].IsEnabled":{"ChangeType":"Modified","Current":"true","New":"false","Pointer":"/Domains/0/IsEnabled"}}"""")]  // a bracket is quoted
    public void Compare_KeyedCollection_MatchesByIdentity(string current, string proposed, string expected)
    {
        var changes = Changes.Between(JsonNode.Parse(current), JsonNode.Parse(proposed), KeyedDomains);

        Assert.Equal(Canonical(expected), Canonical(changes.ToJson()));
    }

    [Theory]
    [InlineData("""{"Tags":["a","b"]}""", """{"Tags":["b","a"]}""", "{}")]                                   // reorder is not a change
    [InlineData("""{"Tags":["a","b"]}""", """{"Tags":["a","c"]}""",
        """"{"Tags[value=b]":{"ChangeType":"Removed","Current":"b","New":null,"Pointer":"/Tags/1"},"Tags[value=c]":{"ChangeType":"Added","Current":null,"New":"c","Pointer":"/Tags/1"}}"""")]  // swap
    [InlineData("""{"Tags":["a","a","b"]}""", """{"Tags":["a","b"]}""",
        """"{"Tags[value=a]":{"ChangeType":"Removed","Current":"a","New":null,"Pointer":"/Tags/1"}}"""")]     // multiset: one duplicate removed
    [InlineData("""{"Tags":[]}""", """{"Tags":[]}""", "{}")]                                                 // both empty
    [InlineData("""{"Tags":[]}""", """{"Tags":["a"]}""",
        """"{"Tags[value=a]":{"ChangeType":"Added","Current":null,"New":"a","Pointer":"/Tags/0"}}"""")]       // first entry
    public void Compare_ScalarCollection_ComparesAsMultiset(string current, string proposed, string expected)
    {
        var changes = Changes.Between(JsonNode.Parse(current), JsonNode.Parse(proposed));

        Assert.Equal(Canonical(expected), Canonical(changes.ToJson()));
    }

    [Fact]
    public void Compare_NullVersusEmptyCollection_ReportsModified()
    {
        var changes = Changes.Between(JsonNode.Parse("""{"Tags":null}"""), JsonNode.Parse("""{"Tags":[]}"""));

        Assert.Equal(ChangeType.Modified, changes["Tags"].ChangeType);
        Assert.Null(changes["Tags"].Current);
        Assert.Equal("[]", changes["Tags"].New);
    }

    [Fact]
    public void Compare_BothNull_ReportsNoChanges()
    {
        var changes = Changes.Between((JsonNode?)null, null);

        Assert.False(changes.HasChanges);
        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_RootReplaced_ReportsModifiedAtEmptyPath()
    {
        // Added and Removed describe a property or item appearing within a container. The root has no
        // container, so a root that becomes an object is a change of shape: Modified from null.
        var changes = Changes.Between(null, JsonNode.Parse("""{"A":1}"""));

        Assert.Equal(ChangeType.Modified, changes[""].ChangeType);
        Assert.Null(changes[""].Current);
        Assert.Equal("""{"A":1}""", changes[""].New);
        Assert.Equal("", changes[""].Pointer);
    }

    [Fact]
    public void Compare_ScalarArraysAsSetsDisabled_ComparesPositionally()
    {
        var changes = Changes.Between(
            JsonNode.Parse("""{"Tags":["a","b"]}"""),
            JsonNode.Parse("""{"Tags":["b","a"]}"""),
            ComparisonOptions.Default with { ScalarArraysAsSets = false });

        Assert.Equal(["Tags[0]", "Tags[1]"], changes.Keys);
    }

    [Fact]
    public void Compare_WithoutKey_FallsBackToIndexAndCascades()
    {
        // The behaviour keyed matching exists to avoid, asserted so the contrast stays visible.
        var changes = Changes.Between(
            JsonNode.Parse("""{"Domains":[{"DomainName":"a"},{"DomainName":"b"}]}"""),
            JsonNode.Parse("""{"Domains":[{"DomainName":"z"},{"DomainName":"a"},{"DomainName":"b"}]}"""));

        Assert.Equal(3, changes.Count);
        Assert.Contains("Domains[0].DomainName", changes.Keys);
    }

    [Fact]
    public void Compare_DuplicateKeyValues_FallsBackToIndex()
    {
        var changes = Changes.Between(
            JsonNode.Parse("""{"Domains":[{"DomainName":"a"},{"DomainName":"a"}]}"""),
            JsonNode.Parse("""{"Domains":[{"DomainName":"a"},{"DomainName":"b"}]}"""),
            KeyedDomains);

        Assert.Equal("Domains[1].DomainName", Assert.Single(changes.Keys));
    }

    [Fact]
    public void Compare_KeyMissingOnOneItem_FallsBackToIndex()
    {
        var changes = Changes.Between(
            JsonNode.Parse("""{"Domains":[{"DomainName":"a"},{"State":"orphan"}]}"""),
            JsonNode.Parse("""{"Domains":[{"DomainName":"a"},{"State":"changed"}]}"""),
            KeyedDomains);

        Assert.Equal("Domains[1].State", Assert.Single(changes.Keys));
    }

    [Fact]
    public void Compare_ExpandAddedSubtrees_ReportsEachLeaf()
    {
        var changes = Changes.Between(
            JsonNode.Parse("{}"),
            JsonNode.Parse("""{"A":{"B":1,"C":{"D":2}}}"""),
            ComparisonOptions.Default with { ExpandAddedSubtrees = true });

        Assert.Equal(["A.B", "A.C.D"], changes.Keys);
        Assert.All(changes.Values, x => Assert.Equal(ChangeType.Added, x.ChangeType));
    }

    [Fact]
    public void Compare_ExpandAddedSubtrees_EmptyContainerReportsItself()
    {
        var changes = Changes.Between(
            JsonNode.Parse("{}"),
            JsonNode.Parse("""{"A":{}}"""),
            ComparisonOptions.Default with { ExpandAddedSubtrees = true });

        Assert.Equal("A", Assert.Single(changes.Keys));
        Assert.Equal("{}", changes["A"].New);
    }

    [Fact]
    public void Compare_PreservesDocumentOrder()
    {
        var changes = Changes.Between(
            JsonNode.Parse("""{"Z":1,"A":2,"M":3}"""),
            JsonNode.Parse("""{"Z":9,"A":9,"M":9}"""));

        Assert.Equal(["Z", "A", "M"], changes.Keys);
    }

    /// <summary>
    /// Compares change sets by content rather than by serialized property order.
    /// </summary>
    private static string Canonical(string json) =>
        JsonSerializer.Serialize(
            JsonSerializer.Deserialize<SortedDictionary<string, JsonElement>>(json),
            new JsonSerializerOptions { WriteIndented = true });
}
