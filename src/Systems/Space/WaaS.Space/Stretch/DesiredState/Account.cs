
using WaaS.Persistence;

namespace WaaS.Space.Stretch.DesiredState;

public class Account : Space.DesiredState.Account
{
    [LookupKey(LookupResourceKeyType.StretchSpaceAccountId)]
    public new ulong? AccountId { get; set; }

    [LookupKey(LookupResourceKeyType.StretchSpaceAccountUsername)]
    public new string Username { get; set; } = "";

    [LookupKey(LookupResourceKeyType.AccountExtReference)]
    public new string? ExtReference { get; set; }

    public Environment? Environment { get; set; }
    public string? SshView { get; set; }
    public string? SftpView { get; set; }
}
