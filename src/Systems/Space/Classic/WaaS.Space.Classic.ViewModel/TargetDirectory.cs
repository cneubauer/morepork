using System.ComponentModel.DataAnnotations;
using WaaS.Space.ViewModel;

namespace WaaS.SharedWebspaceManager.ViewModel;

public class TargetDirectory
{
    /// <summary>
    /// The type of the target directory.
    /// </summary>
    /// <example>User</example>
    [Required]
    public DirectoryType Type { get; set; } = DirectoryType.User;

    /// <summary>
    /// If `accountType` is `standard` this can be set to any path within the webspace.
    /// Otherwise the path always has to be '/' (default).
    /// </summary>
    /// <example>/var/www/html</example>
    [Required]
    [UnixPath]
    public string Path { get; set; } = "/";
}
