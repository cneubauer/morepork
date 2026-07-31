using System.ComponentModel.DataAnnotations;

namespace WaaS.Space.Stretch.ViewModel;

public class EnvironmentProfile
{
    /// <summary>
    /// Name of the EnvironmentProfile. It should bu unique in the StretchSpace and should match ^[0-9A-Za-z_.-]{1,15}$(?![\r\n])
    /// </summary>
    /// <example>my-profile</example>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// Filesystem aliases defined for this profile. Between 1 and 2 aliases.
    /// </summary>
    [MinLength(1)]
    [MaxLength(2)]
    [Required]
    public required List<HomedirAlias> Aliases { get; set; }
}

/// <summary>
/// define a filesystem alias named link path which refers to target
/// 
/// If the type of the alias is <c>symlink</c>, the path of LinkPath must not match <c>/$(?![\r\n])</c>
/// </summary>
public class HomedirAlias
{
    /// <summary>
    /// Allowed value: <c>symlink</c>
    /// </summary>
    [Required]
    public required string Type { get; set; }

    [Required]
    public required AliasTarget Target { get; set; }

    [Required]
    public required LinkPath LinkPath { get; set; }

}

/// <summary>
/// the target path of the alias
/// </summary>
public class AliasTarget
{
    /// <summary>
    /// Allowed value: <c>absolute</c>, <c>user</c>
    /// </summary>
    [Required]
    public required string Type { get; set; }

    /// <summary>
    /// For type <c>absolute</c>, the path must match <c>^/+[^/\x00-\x1f:]{0,255}(/+[^/\x00-\x1f:]{1,255})*/*$(?!\n)</c> and not match <c>(^|/)\.\.(/|$)</c>
    /// 
    /// For type <c>user</c>, the path must match <c>^[^/\x00-\x1f:]{0,255}(/+[^/\x00-\x1f:]{1,255})*/*$(?!\n)</c> and not match <c>(^|/)\\.\\.(/|$)</c>
    /// </summary>
    [Required]
    public required string Path { get; set; }

}

/// <summary>
/// the name of the alias
/// </summary>
public class LinkPath
{
    /// <summary>
    /// Allowed value: <c>absolute</c>
    /// </summary>
    [Required]
    public required string Type { get; set; }

    /// <summary>
    /// For type <c>absolute</c>, the path must match <c>^/+[^/\x00-\x1f:]{0,255}(/+[^/\x00-\x1f:]{1,255})*/*$(?!\n)</c> and not match <c>(^|/)\.\.(/|$)</c>
    /// </summary>
    [Required]
    public required string Path { get; set; }

}