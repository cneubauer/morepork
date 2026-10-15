namespace WaaS.Space.ViewModel;

/// <summary>
/// A webspace account with elevated admin-scope privileges.
/// </summary>
public class AdminAccount : Account, IAdminAccount
{
    /// <summary>
    /// Additional metadata for admin-scoped accounts, including admin scope and description.
    /// </summary>
    public Metadata? Metadata { get; set; }
}
