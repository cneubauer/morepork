namespace WaaS.Space.ViewModel;

/// <summary>
/// Flags defining which access protocols are enabled for a webspace account.
/// </summary>
[Flags]
public enum SpaceAccessType : ulong
{
    //Ftp = 1 << 0,
    /// <summary>Secure File Transfer Protocol access.</summary>
    Sftp = 1 << 1,
    /// <summary>Secure Shell (interactive terminal) access.</summary>
    Ssh = 1 << 2,
    //Webdav = 1 << 3,
}
