using System.ComponentModel;

namespace WaaS.Space.ViewModel;

public class DataAccess
{
    /// <summary>
    /// The hostname used to access the webspace via FTP or similar protocols.
    /// </summary>
    /// <example>ftp.example.server.lan</example>
    [ReadOnly(true)]
    public string? Domain { get; set; }
}