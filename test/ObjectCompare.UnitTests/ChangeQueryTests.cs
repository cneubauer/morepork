using System.Text.Json;
using ObjectCompare;
using WaaS.Common.DesiredState;
using WaaS.Space.Classic.DesiredState;
using WaaS.Space.DesiredState;
using WaaS.WebApi;
using ClassicVm = WaaS.Space.Classic.ViewModel;
using Vm = WaaS.Space.ViewModel;

namespace ObjectCompare.UnitTests;

/// <summary>
/// Covers <see cref="ChangeExtensions.OfObject{T}"/> and
/// <see cref="ChangeExtensions.OfProperty{T, TProperty}"/> against each shape a credential change
/// can arrive in.
/// </summary>
public class ChangeQueryTests
{
    private static readonly DateTime Pinned = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The query the publish workflow runs to find the tokens a transaction orphaned.</summary>
    private static List<string> StaleTokens(IEnumerable<IChange> changes) =>
        [.. changes
            .OfObject<ICredential>()
            .OfProperty(x => x.SecurePasswordToken)
            .Select(x => x.OldValue)
            .Where(token => !string.IsNullOrEmpty(token))
            .Select(token => token!)
            .Distinct()];

    /// <summary>
    /// A change only ever reaches a consumer as JSON, so every shape is asserted through a round
    /// trip as well as directly.
    /// </summary>
    private static IReadOnlyList<IChange> RoundTrip(IReadOnlyList<IChange> changes, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(changes, options);
        return JsonSerializer.Deserialize<IReadOnlyList<IChange>>(json, options)!;
    }

    private static Account Account(string extReference, string token, string username = "user") => new()
    {
        ReferenceId = $"ref-{extReference}",
        CorrelationId = $"corr-{extReference}",
        Created = Pinned,
        ExtReference = extReference,
        Username = username,
        SecurePasswordToken = token,
    };

    private static SharedWebspace Webspace(Action<SharedWebspace>? configure = null)
    {
        var webspace = new SharedWebspace
        {
            ReferenceId = "webspace-ref",
            CorrelationId = "webspace-corr",
            Created = Pinned,
        };

        configure?.Invoke(webspace);
        return webspace;
    }

    // The changed value is the token itself.
    [Fact]
    public void OfObject_TokenRotatedInPlace_ReturnsOldToken()
    {
        var changes = Account("acc-1", "old-token").CompareTo(Account("acc-1", "new-token"));

        Assert.Equal(["old-token"], StaleTokens(changes));
        Assert.Equal(["old-token"], StaleTokens(RoundTrip(changes)));
    }

    // The subject is the removed list item.
    [Fact]
    public void OfObject_AccountRemovedFromList_ReturnsOldToken()
    {
        List<Account> old = [Account("acc-1", "kept-token"), Account("acc-2", "dropped-token")];
        List<Account> updated = [Account("acc-1", "kept-token")];

        var changes = old.CompareTo(updated);

        Assert.Equal(["dropped-token"], StaleTokens(changes));
        Assert.Equal(["dropped-token"], StaleTokens(RoundTrip(changes)));
    }

    // The subject is the nulled property's old value.
    [Fact]
    public void OfObject_MailConfigurationNulledOut_ReturnsOldToken()
    {
        var old = Webspace(x => x.MailConfiguration = new MailConfiguration
        {
            Host = "smtp.example.com",
            Username = "mailer",
            SecurePasswordToken = "mail-token",
        });

        var changes = old.CompareTo(Webspace());

        Assert.Equal(["mail-token"], StaleTokens(changes));
        Assert.Equal(["mail-token"], StaleTokens(RoundTrip(changes)));
    }

    [Fact]
    public void OfObject_WebAnalyticsNulledOut_ReturnsOldToken()
    {
        var old = Webspace(x => x.WebAnalytics = new WebAnalytics
        {
            WebAnalyticsId = "wa-1",
            SecurePasswordToken = "wa-token",
        });

        var changes = old.CompareTo(Webspace());

        Assert.Equal(["wa-token"], StaleTokens(changes));
        Assert.Equal(["wa-token"], StaleTokens(RoundTrip(changes)));
    }

    [Fact]
    public void OfObject_AdminAccountRemoved_ReturnsOldToken()
    {
        var old = Webspace(x => x.AdminAccounts = [Account("admin-1", "admin-token")]);

        var changes = old.CompareTo(Webspace());

        Assert.Equal(["admin-token"], StaleTokens(changes));
        Assert.Equal(["admin-token"], StaleTokens(RoundTrip(changes)));
    }

    // The token is untouched and still live, so it must not be reported.
    [Fact]
    public void OfObject_UnrelatedPropertyChanged_ReturnsNothing()
    {
        var changes = Account("acc-1", "live-token", "before")
            .CompareTo(Account("acc-1", "live-token", "after"));

        Assert.NotEmpty(changes);
        Assert.Empty(StaleTokens(changes));
        Assert.Empty(StaleTokens(RoundTrip(changes)));
    }

    [Fact]
    public void OfObject_CredentialAdded_ReturnsNothing()
    {
        var updated = Webspace(x => x.MailConfiguration = new MailConfiguration
        {
            Host = "smtp.example.com",
            SecurePasswordToken = "brand-new-token",
        });

        var changes = Webspace().CompareTo(updated);

        Assert.Empty(StaleTokens(changes));
        Assert.Empty(StaleTokens(RoundTrip(changes)));
    }

    [Fact]
    public void OfObject_UnchangedState_ReturnsNothing()
    {
        var changes = Account("acc-1", "token").CompareTo(Account("acc-1", "token"));

        Assert.Empty(changes);
        Assert.Empty(StaleTokens(changes));
    }

    // Apply writes 'PasswordToken ?? ""', so an absent token is empty rather than null.
    [Fact]
    public void OfObject_EmptyOldToken_ReturnsNothing()
    {
        var changes = Account("acc-1", "").CompareTo(Account("acc-1", "new-token"));

        Assert.Empty(StaleTokens(changes));
        Assert.Empty(StaleTokens(RoundTrip(changes)));
    }

    [Fact]
    public void OfObject_SameTokenOnTwoCredentials_ReturnsItOnce()
    {
        List<Account> old = [Account("acc-1", "shared-token"), Account("acc-2", "shared-token")];

        var changes = old.CompareTo(new List<Account>());

        Assert.Equal(["shared-token"], StaleTokens(changes));
        Assert.Equal(["shared-token"], StaleTokens(RoundTrip(changes)));
    }

    [Fact]
    public void OfObject_RotationAndRemovalTogether_ReturnsBothOldTokens()
    {
        var old = Webspace(x =>
        {
            x.Accounts = [Account("acc-1", "rotated-old"), Account("acc-2", "removed-token")];
            x.MailConfiguration = new MailConfiguration { SecurePasswordToken = "mail-old" };
        });

        var updated = Webspace(x =>
        {
            x.Accounts = [Account("acc-1", "rotated-new")];
            x.MailConfiguration = new MailConfiguration { SecurePasswordToken = "mail-new" };
        });

        var expected = new[] { "rotated-old", "removed-token", "mail-old" };

        Assert.Equal(expected.Order(), StaleTokens(old.CompareTo(updated)).Order());
        Assert.Equal(expected.Order(), StaleTokens(RoundTrip(old.CompareTo(updated))).Order());
    }

    // The outbox writes changes as camelCase, Temporal as PascalCase; a rehydrated subject has to
    // bind either way.
    [Fact]
    public void OfObject_CamelCaseSerializedChanges_ReturnsOldToken()
    {
        List<Account> old = [Account("acc-1", "dropped-token")];

        var camelCase = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };

        var changes = RoundTrip(old.CompareTo(new List<Account>()), camelCase);

        Assert.Equal(["dropped-token"], StaleTokens(changes));
    }

    // Stretch.Account derives from Space.DesiredState.Account and both carry the simple name
    // "Account", so only the recorded FullName tells them apart.
    [Fact]
    public void OfObject_DistinguishesTheTwoAccountTypes()
    {
        var changes = Account("acc-1", "old-token").CompareTo(Account("acc-1", "new-token"));

        Assert.NotEmpty(changes.OfObject<Account>());
        Assert.Empty(changes.OfObject<WaaS.Space.Stretch.DesiredState.Account>());
    }

    [Fact]
    public void OfProperty_ReportsBothOldAndNewValue()
    {
        var changes = Account("acc-1", "old-token").CompareTo(Account("acc-1", "new-token"));

        var change = Assert.Single(changes.OfObject<ICredential>().OfProperty(x => x.SecurePasswordToken));

        Assert.Equal("old-token", change.OldValue);
        Assert.Equal("new-token", change.NewValue);
    }

    // ObjectComparer drops keyed list items whose key is null from both the old and the new map,
    // so an account is only diffable at all once Apply has set its ExtReference.
    [Fact]
    public void Apply_KeysAccounts_SoARotationSurfacesInTheDiff()
    {
        var viewModel = new ClassicVm.SharedWebspace
        {
            Accounts = [new Vm.Account { Username = "u100", ExtReference = "acc-1", PasswordToken = "old-token" }],
        };

        var desiredState = new SharedWebspace();
        desiredState.Apply(viewModel);

        var stored = Assert.Single(desiredState.Accounts);
        Assert.Equal("acc-1", stored.ExtReference);

        // Rotate the token against a snapshot of the stored state.
        var previous = JsonSerializer.Deserialize<SharedWebspace>(JsonSerializer.Serialize(desiredState))!;
        stored.SecurePasswordToken = "new-token";

        Assert.Equal(["old-token"], StaleTokens(previous.CompareTo(desiredState)));
    }

    [Fact]
    public void OfProperty_RejectsAnExpressionThatIsNotAPropertyAccess()
    {
        var changes = Account("acc-1", "old").CompareTo(Account("acc-1", "new")).OfObject<ICredential>();

        Assert.Throws<ArgumentException>(() => changes.OfProperty(x => x.ToString()).ToList());
    }
}
