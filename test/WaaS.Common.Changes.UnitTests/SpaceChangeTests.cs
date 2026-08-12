using WaaS.Space.DesiredState;

// WaaS.Space is also a namespace prefix, so the type needs an unambiguous name here.
using SpaceResource = WaaS.Space.DesiredState.Space;

namespace WaaS.Common.Changes.UnitTests;

/// <summary>
/// End-to-end coverage against the real desired-state models, which is where the component will be used.
/// </summary>
/// <remarks>
/// Identity keys are supplied through <see cref="ComparisonOptions.ArrayKeys"/> so that the production models
/// stay unchanged. Annotating <c>DataAccessDomainBinding.DomainName</c> with
/// <see cref="ListItemKeyAttribute"/> would make the keying automatic for every caller.
/// </remarks>
public class SpaceChangeTests
{
    private static readonly ComparisonOptions SpaceKeys = ComparisonOptions.Default with
    {
        ArrayKeys = new Dictionary<string, string> { ["/DataAccessDomains"] = "DomainName" },
    };

    [Fact]
    public void Compare_Space_ReportsDomainAdditionTagSwapAndScalarEdit()
    {
        var current = NewSpace();
        var proposed = NewSpace();

        // A domain added at the head of the list, which must not disturb the existing entry.
        proposed.DataAccessDomains.Insert(0, new DataAccessDomainBinding { DomainName = "new.example.com" });

        // One tag swapped in an unordered list.
        proposed.PlacementTags = ["shared", "canary"];

        // A nested scalar and a top-level scalar.
        proposed.Owner.Username = "web2";
        proposed.State = "active";

        var changes = ChangeSet.Between(current, proposed, SpaceKeys);

        Assert.Equal(
            [
                "DataAccessDomains[DomainName=new.example.com]",
                "Owner.Username",
                "PlacementTags[value=beta]",
                "PlacementTags[value=canary]",
                "State",
            ],
            changes.Keys.Order());

        Assert.Equal(ChangeType.Added, changes["DataAccessDomains[DomainName=new.example.com]"].ChangeType);
        Assert.Equal(ChangeType.Removed, changes["PlacementTags[value=beta]"].ChangeType);
        Assert.Equal(ChangeType.Added, changes["PlacementTags[value=canary]"].ChangeType);
        Assert.Equal("unknown", changes["State"].Current);
        Assert.Equal("active", changes["State"].New);
        Assert.Equal("web1", changes["Owner.Username"].Current);
    }

    [Fact]
    public void Compare_UnchangedSpace_ReportsNoChanges()
    {
        var changes = ChangeSet.Between(NewSpace(), NewSpace(), SpaceKeys);

        Assert.False(changes.HasChanges);
    }

    [Fact]
    public void Compare_Space_ReportsGeneratedIdentifiersThatWereNotPinned()
    {
        // WaasResource seeds ReferenceId and CorrelationId with fresh GUIDs, so two independently
        // constructed instances differ. Real callers compare a stored state against a proposed one, where
        // these are carried over; this documents that the diff sees whatever the serializer sees.
        var changes = ChangeSet.Between(new SpaceResource(), new SpaceResource(), SpaceKeys);

        Assert.Equal(["CorrelationId", "ReferenceId"], changes.Keys.Order());
    }

    [Fact]
    public void Compare_NestedCollectionInsideKeyedItem_StaysStable()
    {
        var current = NewSpace();
        var proposed = NewSpace();

        proposed.CompatLinks.Add(new CompatLink());

        var changes = ChangeSet.Between(current, proposed, SpaceKeys);

        Assert.Equal("CompatLinks[0]", Assert.Single(changes.Keys));
        Assert.Equal(ChangeType.Added, changes["CompatLinks[0]"].ChangeType);
    }

    /// <summary>
    /// A space with the generated identifiers pinned, so that only deliberate edits show up as changes.
    /// </summary>
    private static SpaceResource NewSpace() => new()
    {
        ReferenceId = "space-1",
        CorrelationId = "correlation-1",
        State = "unknown",
        Region = "eu-central",
        Owner = new Owner { Uid = 1000, Gid = 1000, Username = "web1", Groupname = "web" },
        PlacementTags = ["shared", "beta"],
        DataAccessDomains =
        [
            new DataAccessDomainBinding { DomainName = "ssh.example.com" },
        ],
    };
}
