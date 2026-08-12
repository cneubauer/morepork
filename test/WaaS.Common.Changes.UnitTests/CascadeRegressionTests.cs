using System.Text.Json.Nodes;

namespace WaaS.Common.Changes.UnitTests;

/// <summary>
/// Guards the requirement this component was written for.
/// </summary>
/// <remarks>
/// The maintained JSON diff libraries match collection items positionally, so inserting an item at the
/// head of a list reports every following item as changed. Those cascading changes are useless in a
/// change preview and actively misleading in an audit trail. If these tests start failing, keyed
/// matching has regressed to positional matching somewhere.
/// </remarks>
public class CascadeRegressionTests
{
    private static readonly ComparisonOptions KeyedItems = ComparisonOptions.Default with
    {
        ArrayKeys = new Dictionary<string, string> { ["/Items"] = "Id" },
    };

    [Fact]
    public void Compare_InsertAtHeadOfLongCollection_ReportsExactlyOneChange()
    {
        var current = Collection(Enumerable.Range(0, 50));
        var proposed = Collection(Enumerable.Range(-1, 51));

        var changes = ChangeSet.Between(current, proposed, KeyedItems);

        Assert.Equal("Items[Id=-1]", Assert.Single(changes.Keys));
        Assert.Equal(ChangeType.Added, changes["Items[Id=-1]"].ChangeType);
    }

    [Fact]
    public void Compare_RemoveFromHeadOfLongCollection_ReportsExactlyOneChange()
    {
        var current = Collection(Enumerable.Range(0, 50));
        var proposed = Collection(Enumerable.Range(1, 49));

        var changes = ChangeSet.Between(current, proposed, KeyedItems);

        Assert.Equal("Items[Id=0]", Assert.Single(changes.Keys));
        Assert.Equal(ChangeType.Removed, changes["Items[Id=0]"].ChangeType);
    }

    [Fact]
    public void Compare_FullyReversedCollection_ReportsNoChanges()
    {
        var current = Collection(Enumerable.Range(0, 50));
        var proposed = Collection(Enumerable.Range(0, 50).Reverse());

        var changes = ChangeSet.Between(current, proposed, KeyedItems);

        Assert.False(changes.HasChanges);
    }

    [Fact]
    public void Compare_InsertAtHeadAndEditOneItem_ReportsOnlyThoseTwo()
    {
        var current = Collection(Enumerable.Range(0, 50));

        var proposed = Collection(Enumerable.Range(-1, 51));
        proposed["Items"]!.AsArray()
            .First(x => x!["Id"]!.GetValue<int>() == 25)!["Value"] = "edited";

        var changes = ChangeSet.Between(current, proposed, KeyedItems);

        Assert.Equal(["Items[Id=-1]", "Items[Id=25].Value"], changes.Keys.Order());
    }

    private static JsonObject Collection(IEnumerable<int> ids) =>
        new()
        {
            ["Items"] = new JsonArray(
                ids.Select(id => (JsonNode)new JsonObject { ["Id"] = id, ["Value"] = $"item-{id}" }).ToArray()),
        };
}
