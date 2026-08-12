using System.Text.Json.Serialization;

namespace WaaS.Common.Comparison.UnitTests;

/// <summary>
/// Covers the typed entry point: how the serializer contract determines which properties are visible,
/// what they are called in a path, and where identity keys come from.
/// </summary>
public class ObjectProjectionTests
{
    private enum Status { Active, Inactive }

    private sealed class Secretive
    {
        public string Name { get; set; } = "";

        [JsonIgnore]
        public string PasswordHash { get; set; } = "";
    }

    private sealed class Renamed
    {
        [JsonPropertyName("wa")]
        public string? WebAnalytics { get; set; }
    }

    private sealed class Stateful
    {
        public Status Status { get; set; }

        public DateTimeOffset ChangedAt { get; set; }
    }

    private sealed class Binding
    {
        [ListItemKey]
        public string DomainName { get; set; } = "";

        public bool IsEnabled { get; set; }
    }

    private sealed class Host
    {
        public List<Binding> Bindings { get; set; } = [];

        public List<string> Tags { get; set; } = [];
    }

    private sealed class Node
    {
        public string Name { get; set; } = "";

        public List<Node> Children { get; set; } = [];
    }

    [Fact]
    public void Compare_IgnoredProperty_IsNotReported()
    {
        // Hiding a property from serialization is the supported way to keep a secret out of a change record.
        var changes = Changes.Between(
            new Secretive { Name = "same", PasswordHash = "before" },
            new Secretive { Name = "same", PasswordHash = "after" });

        Assert.False(changes.HasChanges);
    }

    [Fact]
    public void Compare_RenamedProperty_UsesJsonName()
    {
        var changes = Changes.Between(
            new Renamed { WebAnalytics = "off" },
            new Renamed { WebAnalytics = "on" });

        Assert.Equal("wa", Assert.Single(changes.Keys));
    }

    [Fact]
    public void Compare_Enum_RendersAsName()
    {
        var changes = Changes.Between(
            new Stateful { Status = Status.Active },
            new Stateful { Status = Status.Inactive });

        Assert.Equal("Active", changes["Status"].Current);
        Assert.Equal("Inactive", changes["Status"].New);
    }

    [Fact]
    public void Compare_EqualTimestamps_ReportNoChange()
    {
        var moment = new DateTimeOffset(2026, 8, 11, 10, 30, 0, TimeSpan.Zero);

        var changes = Changes.Between(
            new Stateful { ChangedAt = moment },
            new Stateful { ChangedAt = moment });

        Assert.False(changes.HasChanges);
    }

    [Fact]
    public void Compare_ListItemKeyAttribute_MatchesCollectionByIdentity()
    {
        // The attribute alone should be enough: no ArrayKeys are supplied here.
        var current = new Host { Bindings = [new() { DomainName = "a.com", IsEnabled = true }] };
        var proposed = new Host
        {
            Bindings =
            [
                new() { DomainName = "z.com", IsEnabled = true },
                new() { DomainName = "a.com", IsEnabled = false },
            ],
        };

        var changes = Changes.Between(current, proposed);

        Assert.Equal(
            ["Bindings[DomainName=a.com].IsEnabled", "Bindings[DomainName=z.com]"],
            changes.Keys.Order());
        Assert.Equal(ChangeType.Added, changes["Bindings[DomainName=z.com]"].ChangeType);
    }

    [Fact]
    public void Compare_ScalarCollectionOnPoco_ComparesAsMultiset()
    {
        var changes = Changes.Between(
            new Host { Tags = ["beta", "canary"] },
            new Host { Tags = ["canary", "beta"] });

        Assert.False(changes.HasChanges);
    }

    [Fact]
    public void Compare_RecursiveModel_DoesNotHangResolvingKeys()
    {
        // The type walk must terminate on a self-referencing model.
        var changes = Changes.Between(
            new Node { Name = "root", Children = [new() { Name = "child" }] },
            new Node { Name = "root", Children = [new() { Name = "changed" }] });

        Assert.Equal("Children[0].Name", Assert.Single(changes.Keys));
    }

    [Fact]
    public void Compare_ExplicitArrayKeyOverridesAttribute()
    {
        var current = new Host { Bindings = [new() { DomainName = "a", IsEnabled = true }] };
        var proposed = new Host { Bindings = [new() { DomainName = "b", IsEnabled = true }] };

        var changes = Changes.Between(current, proposed, ComparisonOptions.Default with
        {
            ArrayKeys = new Dictionary<string, string> { ["/Bindings"] = "IsEnabled" },
        });

        // Keyed on IsEnabled, both items share identity "true", so only DomainName differs.
        Assert.Equal("Bindings[IsEnabled=true].DomainName", Assert.Single(changes.Keys));
    }

    [Fact]
    public void Compare_NullVersusInstance_ReportsModifiedAtRoot()
    {
        var changes = Changes.Between<Secretive?>(null, new Secretive { Name = "new" });

        Assert.Equal(ChangeType.Modified, changes[""].ChangeType);
    }
}
