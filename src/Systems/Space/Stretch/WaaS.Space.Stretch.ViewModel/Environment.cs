using System.ComponentModel.DataAnnotations;
using WaaS.Common.ViewModel;
using WaaS.Space.ViewModel;

namespace WaaS.Space.Stretch.ViewModel;

public class Environment
{
    /// <summary>
    /// Name of the container image to load.
    /// </summary>
    /// <example>nginx-php8.1</example>
    [Required]
    public string Image { get; set; } = "";

    /// <summary>
    /// Version (e.g. a preview version) of environment. 
    /// 
    /// Default: $channel/default
    /// 
    /// Allowed values: $channel/default, $channel/preview
    /// </summary>
    /// <example>$channel/preview</example>
    /// <default>$channel/default</default>
    [Options("$channel/default", "$channel/preview")]
    public string Version { get; set; } = "$channel/default";

    /// <summary>
    /// Name of the environment profile, which configured for the StretchSpace.
    /// </summary>
    /// <example>my-profile</example>
    [RegularExpression("^[0-9A-Za-z_.-]{1,15}$(?![\r\n])")]
    public string? EnvironmentProfileName { get; set; }
}
