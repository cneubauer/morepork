namespace WaaS.Space.Stretch.ViewModel;

public class TargetDirectory
{
    /// <summary>
    /// The type of the target directory.
    /// </summary>
    /// <example>User</example>
    public DirectoryType Type { get; set; } = DirectoryType.User;

    /// <summary>
    /// The webspace-relative path the account has access to.
    /// </summary>
    /// <example>/public</example>
    [StretchHttpPath]
    public string Path { get; set; } = "/public";
}
