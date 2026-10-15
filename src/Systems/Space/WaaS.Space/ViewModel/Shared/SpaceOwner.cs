namespace WaaS.Space.ViewModel;

public class SpaceOwner
{
    /// <summary>
    /// The numeric user ID of the webspace owner.
    /// </summary>
    /// <example>10042</example>
    public int? Uid { get; set; }

    /// <summary>
    /// The numeric group ID of the webspace owner.
    /// </summary>
    /// <example>10042</example>
    public int? Gid { get; set; }

    /// <summary>
    /// The username of the webspace owner.
    /// </summary>
    /// <example>w0123456</example>
    public string? Username { get; set; }

    /// <summary>
    /// The group name of the webspace owner.
    /// </summary>
    /// <example>w0123456</example>
    public string? Groupname { get; set; }
}
