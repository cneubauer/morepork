using System.Text.Json.Serialization;
using ObjectCompare;
using WaaS.Space.DesiredState;

namespace ObjectCompare.UnitTests;

public class ObjectComparerTests
{
    private enum Status
    {
        Inactive,
        Active,
        Suspended
    }

    private class SimpleModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Status Status { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid TenantId { get; set; }
    }

    private class NestedModel
    {
        public string Title { get; set; } = "";
        public SimpleModel Details { get; set; } = new();
    }

    private class ItemWithKey
    {
        [ItemKey]
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public int Priority { get; set; }
    }

    private class KeyedCollectionModel
    {
        public List<ItemWithKey> Items { get; set; } = [];
    }

    private class NonKeyedCollectionModel
    {
        public List<string> Tags { get; set; } = [];
        public List<SimpleModel> Elements { get; set; } = [];
    }

    private class NodeModel
    {
        public string Name { get; set; } = "";
        public NodeModel? Next { get; set; }
    }

    private class IgnoredPropertyModel
    {
        public string Normal { get; set; } = "";

        [JsonIgnore]
        public string Secret { get; set; } = "";
    }

    [Fact]
    public void Compare_SameReferences_ReturnsNoChanges()
    {
        var model = new SimpleModel { Id = 1, Name = "Test" };
        var changes = model.CompareTo(model);
        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_BothNull_ReturnsNoChanges()
    {
        SimpleModel? model1 = null;
        SimpleModel? model2 = null;
        var changes = model1.CompareTo(model2);
        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_NullVersusInstance_ReturnsRootPropertyChange()
    {
        var model = new SimpleModel { Id = 1, Name = "Test" };
        var changes = ((SimpleModel?)null).CompareTo(model);

        Assert.Single(changes);
        var change = Assert.IsAssignableFrom<IPropertyChange>(changes[0]);
        Assert.Equal("", change.Path);
        Assert.Null(change.OldValue);
        Assert.Equal(model, change.NewValue);
    }

    [Fact]
    public void Compare_InstanceVersusNull_ReturnsRootPropertyChange()
    {
        var model = new SimpleModel { Id = 1, Name = "Test" };
        var changes = model.CompareTo(null);

        Assert.Single(changes);
        var change = Assert.IsAssignableFrom<IPropertyChange>(changes[0]);
        Assert.Equal("", change.Path);
        Assert.Equal(model, change.OldValue);
        Assert.Null(change.NewValue);
    }

    [Fact]
    public void Compare_ScalarModifications_ReportsPropertyChanges()
    {
        var tenantId = Guid.NewGuid();
        var dt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var oldObj = new SimpleModel
        {
            Id = 1,
            Name = "Alice",
            Status = Status.Active,
            IsEnabled = true,
            CreatedAt = dt,
            TenantId = tenantId
        };

        var newObj = new SimpleModel
        {
            Id = 2,
            Name = "Bob",
            Status = Status.Suspended,
            IsEnabled = false,
            CreatedAt = dt.AddDays(1),
            TenantId = tenantId
        };

        var changes = oldObj.CompareTo(newObj);

        Assert.Equal(5, changes.Count);

        var idChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == nameof(SimpleModel.Id));
        Assert.NotNull(idChange);
        Assert.Equal("Id", idChange.Path);
        Assert.Equal(1, idChange.OldValue);
        Assert.Equal(2, idChange.NewValue);

        var nameChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == nameof(SimpleModel.Name));
        Assert.NotNull(nameChange);
        Assert.Equal("Name", nameChange.Path);
        Assert.Equal("Alice", nameChange.OldValue);
        Assert.Equal("Bob", nameChange.NewValue);

        var statusChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == nameof(SimpleModel.Status));
        Assert.NotNull(statusChange);
        Assert.Equal("Status", statusChange.Path);
        Assert.Equal(Status.Active, statusChange.OldValue);
        Assert.Equal(Status.Suspended, statusChange.NewValue);

        var enabledChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == nameof(SimpleModel.IsEnabled));
        Assert.NotNull(enabledChange);
        Assert.Equal("IsEnabled", enabledChange.Path);
        Assert.Equal(true, enabledChange.OldValue);
        Assert.Equal(false, enabledChange.NewValue);

        var createdChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == nameof(SimpleModel.CreatedAt));
        Assert.NotNull(createdChange);
        Assert.Equal("CreatedAt", createdChange.Path);
        Assert.Equal(dt, createdChange.OldValue);
        Assert.Equal(dt.AddDays(1), createdChange.NewValue);
    }

    [Fact]
    public void Compare_NestedObjects_ReportsDottedPath()
    {
        var oldObj = new NestedModel
        {
            Title = "Original",
            Details = new SimpleModel { Id = 1, Name = "Inner1" }
        };

        var newObj = new NestedModel
        {
            Title = "Original",
            Details = new SimpleModel { Id = 1, Name = "InnerUpdated" }
        };

        var changes = oldObj.CompareTo(newObj);

        Assert.Single(changes);
        var change = Assert.IsAssignableFrom<IPropertyChange>(changes[0]);
        Assert.Equal("Details.Name", change.Path);
        Assert.Equal("Name", change.PropertyName);
        Assert.Equal("Inner1", change.OldValue);
        Assert.Equal("InnerUpdated", change.NewValue);
    }

    [Fact]
    public void Compare_KeyedCollection_ItemAdded_ReportsListChangeAdded()
    {
        var oldObj = new KeyedCollectionModel
        {
            Items = [new ItemWithKey { Key = "k1", Value = "v1" }]
        };

        var newObj = new KeyedCollectionModel
        {
            Items =
            [
                new ItemWithKey { Key = "k1", Value = "v1" },
                new ItemWithKey { Key = "k2", Value = "v2" }
            ]
        };

        var changes = oldObj.CompareTo(newObj);

        Assert.Single(changes);
        var change = Assert.IsAssignableFrom<IListChange<ItemWithKey>>(changes[0]);
        Assert.Equal(ChangeKind.List, change.Kind);
        Assert.Equal(ListChangeType.Added, change.ChangeType);
        Assert.Equal("Items[Key=k2]", change.Path);
        Assert.Equal("k2", change.ItemKey);
        Assert.NotNull(change.Item);
        Assert.Equal("v2", change.Item.Value);
    }

    [Fact]
    public void Compare_KeyedCollection_ItemRemoved_ReportsListChangeRemoved()
    {
        var oldObj = new KeyedCollectionModel
        {
            Items =
            [
                new ItemWithKey { Key = "k1", Value = "v1" },
                new ItemWithKey { Key = "k2", Value = "v2" }
            ]
        };

        var newObj = new KeyedCollectionModel
        {
            Items = [new ItemWithKey { Key = "k1", Value = "v1" }]
        };

        var changes = oldObj.CompareTo(newObj);

        Assert.Single(changes);
        var change = Assert.IsAssignableFrom<IListChange<ItemWithKey>>(changes[0]);
        Assert.Equal(ChangeKind.List, change.Kind);
        Assert.Equal(ListChangeType.Removed, change.ChangeType);
        Assert.Equal("Items[Key=k2]", change.Path);
        Assert.Equal("k2", change.ItemKey);
        Assert.NotNull(change.Item);
        Assert.Equal("v2", change.Item.Value);
    }

    [Fact]
    public void Compare_KeyedCollection_ItemModified_ReportsNestedPropertyChange()
    {
        var oldObj = new KeyedCollectionModel
        {
            Items = [new ItemWithKey { Key = "k1", Value = "v1", Priority = 10 }]
        };

        var newObj = new KeyedCollectionModel
        {
            Items = [new ItemWithKey { Key = "k1", Value = "v1-updated", Priority = 20 }]
        };

        var changes = oldObj.CompareTo(newObj);

        Assert.Equal(2, changes.Count);

        var valChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == "Value");
        Assert.NotNull(valChange);
        Assert.Equal("Items[Key=k1].Value", valChange.Path);
        Assert.Equal("v1", valChange.OldValue);
        Assert.Equal("v1-updated", valChange.NewValue);

        var prioChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == "Priority");
        Assert.NotNull(prioChange);
        Assert.Equal("Items[Key=k1].Priority", prioChange.Path);
        Assert.Equal(10, prioChange.OldValue);
        Assert.Equal(20, prioChange.NewValue);
    }

    [Fact]
    public void Compare_KeyedCollection_Reordered_ReportsNoChanges()
    {
        var oldObj = new KeyedCollectionModel
        {
            Items =
            [
                new ItemWithKey { Key = "k1", Value = "v1" },
                new ItemWithKey { Key = "k2", Value = "v2" }
            ]
        };

        var newObj = new KeyedCollectionModel
        {
            Items =
            [
                new ItemWithKey { Key = "k2", Value = "v2" },
                new ItemWithKey { Key = "k1", Value = "v1" }
            ]
        };

        var changes = oldObj.CompareTo(newObj);
        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_NonKeyedCollection_IndexFallback()
    {
        var oldObj = new NonKeyedCollectionModel { Tags = ["tag1", "tag2"] };
        var newObj = new NonKeyedCollectionModel { Tags = ["tag1", "tag3", "tag4"] };

        var changes = oldObj.CompareTo(newObj);

        Assert.Equal(2, changes.Count);

        // Tags[1] modified from "tag2" to "tag3"
        var modChange = Assert.IsAssignableFrom<IPropertyChange>(changes[0]);
        Assert.Equal("Tags[1]", modChange.Path);
        Assert.Equal("tag2", modChange.OldValue);
        Assert.Equal("tag3", modChange.NewValue);

        // Tags[2] added "tag4"
        var addChange = Assert.IsAssignableFrom<IListChange>(changes[1]);
        Assert.Equal(ListChangeType.Added, addChange.ChangeType);
        Assert.Equal("Tags[2]", addChange.Path);
        Assert.Equal("tag4", addChange.Item);
    }

    [Fact]
    public void Compare_CyclicalGraph_DoesNotRecurseInfinitely()
    {
        var node1 = new NodeModel { Name = "Node 1" };
        var node2 = new NodeModel { Name = "Node 2", Next = node1 };
        node1.Next = node2;

        var other1 = new NodeModel { Name = "Node 1" };
        var other2 = new NodeModel { Name = "Node 2 - edited", Next = other1 };
        other1.Next = other2;

        var changes = node1.CompareTo(other1);

        Assert.Single(changes);
        var change = Assert.IsAssignableFrom<IPropertyChange>(changes[0]);
        Assert.Equal("Next.Name", change.Path);
        Assert.Equal("Node 2", change.OldValue);
        Assert.Equal("Node 2 - edited", change.NewValue);
    }

    [Fact]
    public void Compare_JsonIgnoreProperty_IsSkipped()
    {
        var oldObj = new IgnoredPropertyModel { Normal = "same", Secret = "secret1" };
        var newObj = new IgnoredPropertyModel { Normal = "same", Secret = "secret2" };

        var changes = oldObj.CompareTo(newObj);

        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_DesiredStateAccount_MatchesByKey()
    {
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var oldAccount = new Account
        {
            ReferenceId = "ref-id",
            CorrelationId = "corr-id",
            Created = created,
            Username = "user1",
            ExtReference = "ref-1",
            State = "unknown",
            SshPublicKeys = [new SshPublicKey { Data = "AAAAKeyData1==", KeyType = "ed25519" }]
        };

        var newAccount = new Account
        {
            ReferenceId = "ref-id",
            CorrelationId = "corr-id",
            Created = created,
            Username = "user1",
            ExtReference = "ref-1",
            State = "active",
            SshPublicKeys =
            [
                new SshPublicKey { Data = "AAAAKeyData1==", KeyType = "ed25519" },
                new SshPublicKey { Data = "BBBBKeyData2==", KeyType = "rsa" }
            ]
        };

        var changes = oldAccount.CompareTo(newAccount);

        Assert.Equal(2, changes.Count);

        var stateChange = changes.OfType<IPropertyChange>().FirstOrDefault(c => c.PropertyName == "State");
        Assert.NotNull(stateChange);
        Assert.Equal("State", stateChange.Path);
        Assert.Equal("unknown", stateChange.OldValue);
        Assert.Equal("active", stateChange.NewValue);

        var keyChange = changes.OfType<IListChange<SshPublicKey>>().FirstOrDefault();
        Assert.NotNull(keyChange);
        Assert.Equal(ListChangeType.Added, keyChange.ChangeType);
        Assert.Equal("SshPublicKeys[Data=BBBBKeyData2==]", keyChange.Path);
        Assert.Equal("BBBBKeyData2==", keyChange.ItemKey);
    }
}
