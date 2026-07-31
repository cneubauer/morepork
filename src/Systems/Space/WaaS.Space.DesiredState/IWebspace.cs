namespace WaaS.Space.DesiredState;

public interface IWebspace
{
    // IEnumerable is covariant, allowing us to treat a list of a derived type 
    // (StretchWebspaceAccount) as a list of the base type (WebspaceAccount).
    IEnumerable<Account> Accounts { get; }

    IpSet IpSet { get; }
}