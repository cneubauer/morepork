using System.Text.Json.Nodes;

namespace WaaS.Common.Comparison.UnitTests;

/// <summary>
/// Covers the fluent entry point, which must behave identically to <see cref="Changes.Between{T}"/>.
/// </summary>
public class ComparisonExtensionsTests
{
    private sealed class Binding
    {
        [ListItemKey]
        public string DomainName { get; set; } = "";

        public bool IsEnabled { get; set; }
    }

    private sealed class Host
    {
        public string State { get; set; } = "";

        public List<Binding> Bindings { get; set; } = [];
    }

    [Fact]
    public void Compare_ReadsCurrentToProposedInReceiverOrder()
    {
        var current = new Host { State = "unknown" };
        var proposed = new Host { State = "active" };

        var changes = current.Compare(proposed);

        Assert.Equal("unknown", changes["State"].Current);
        Assert.Equal("active", changes["State"].New);
    }

    [Fact]
    public void Compare_MatchesChangesBetween()
    {
        var current = new Host { Bindings = [new() { DomainName = "a.com", IsEnabled = true }] };
        var proposed = new Host
        {
            Bindings =
            [
                new() { DomainName = "z.com", IsEnabled = true },
                new() { DomainName = "a.com", IsEnabled = false },
            ],
        };

        Assert.Equal(
            Changes.Between(current, proposed).ToJson(),
            current.Compare(proposed).ToJson());
    }

    [Fact]
    public void Compare_HonoursOptions()
    {
        var current = new Host { Bindings = [new() { DomainName = "a", IsEnabled = true }] };
        var proposed = new Host { Bindings = [new() { DomainName = "b", IsEnabled = true }] };

        var changes = current.Compare(proposed, ComparisonOptions.Default with
        {
            ArrayKeys = new Dictionary<string, string> { ["/Bindings"] = "IsEnabled" },
        });

        Assert.Equal("Bindings[IsEnabled=true].DomainName", Assert.Single(changes.Keys));
    }

    [Fact]
    public void Compare_NullReceiver_ReportsModifiedAtRoot()
    {
        var changes = ((Host?)null).Compare(new Host { State = "active" });

        Assert.Equal(ChangeType.Modified, changes[""].ChangeType);
    }

    [Fact]
    public void Compare_DoesNotModifyEitherObject()
    {
        var current = new Host { State = "unknown" };
        var proposed = new Host { State = "active" };

        current.Compare(proposed);

        Assert.Equal("unknown", current.State);
        Assert.Equal("active", proposed.State);
    }

    [Fact]
    public void Compare_OnJsonNode_ResolvesToTheJsonOverload()
    {
        // The extension is generic and would bind for JsonNode too. Both paths must agree, since
        // JsonNode serializes to itself.
        var current = JsonNode.Parse("""{"State":"unknown"}""");
        var proposed = JsonNode.Parse("""{"State":"active"}""");

        Assert.Equal(
            Changes.Between(current, proposed).ToJson(),
            current.Compare(proposed).ToJson());
    }
}
