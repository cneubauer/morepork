using WaaS.Space.ViewModel;

namespace WaaS.Space.Stretch.ViewModel;

/// <summary>
/// A Stretch webspace account with elevated admin-scope privileges.
/// </summary>
public class AdminAccount : Account, IAdminAccount
{
    /// <summary>
    /// Additional metadata for admin-scoped accounts.
    /// </summary>
    public Metadata? Metadata { get; set; }
}